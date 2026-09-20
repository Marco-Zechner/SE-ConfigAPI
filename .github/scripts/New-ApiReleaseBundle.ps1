[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Tag,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Write-Utf8WithoutBom {
    param([Parameter(Mandatory = $true)][string]$Path, [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Text)

    $encoding = New-Object System.Text.UTF8Encoding($false)
    $normalized = $Text.Replace("`r`n", "`n").Replace("`r", "`n")
    [System.IO.File]::WriteAllText($Path, $normalized, $encoding)
}

function Write-GitHubOutput {
    param([Parameter(Mandatory = $true)][string]$Name, [Parameter(Mandatory = $true)][string]$Value)

    if ([string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT)) {
        return
    }

    [System.IO.File]::AppendAllText($env:GITHUB_OUTPUT, $Name + "=" + $Value + [Environment]::NewLine)
}

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptDirectory "..\.."))
. (Join-Path $scriptDirectory "ApiReleaseMetadata.ps1")

$tagPattern = '^release/(?<packageId>[A-Za-z_][A-Za-z0-9_.]*)/(?<version>[0-9]+\.[0-9]+\.[0-9]+)$'
if ($Tag -notmatch $tagPattern) {
    throw "Invalid release tag '$Tag'. Expected release/<packageId>/<major.minor.patch>, for example release/Mz.ConfigAPI.Consumer/2.0.0."
}

$tagPackageId = [string]$Matches["packageId"]
$version = [string]$Matches["version"]
$packages = @(Get-ApiReleasePackages -RepoRoot $repoRoot)
$matches = @($packages | Where-Object { ([string]$_.PackageId).Equals($tagPackageId, [System.StringComparison]::Ordinal) })

if ($matches.Count -ne 1) {
    throw "API package '$tagPackageId' was not discovered exactly once."
}

$package = $matches[0]
$packageId = [string]$package.PackageId
$changelog = @($package.Changelog)

if ($version -ne [string]$package.Version) {
    throw "Release tag version '$version' does not match ApiVersionFile version '$($package.Version)' for '$packageId'."
}

$dependencies = Resolve-ApiPackageDependencies -Package $package -RepoRoot $repoRoot

Write-Output "Package: $packageId"
Write-Output "Version: $version"
Write-Output "Tag: $Tag"
Write-Output "Portable test project: $([System.IO.Path]::GetFileNameWithoutExtension($package.TestProjectPath))"

if (-not $SkipTests) {
    Write-Output ""
    Write-Output "Running portable consumer tests"

    & dotnet test $package.TestProjectPath --configuration Release --nologo --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Tests failed: $($package.TestProjectPath)"
    }
}
else {
    Write-Output ""
    Write-Output "Portable consumer tests were skipped by request."
}

$outputFull = [System.IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $outputFull) {
    Remove-Item -LiteralPath $outputFull -Recurse -Force
}

New-Item -ItemType Directory -Path $outputFull | Out-Null

$stagingRoot = Join-Path $outputFull ".staging"
$librariesRoot = Join-Path $stagingRoot "Libraries"
$destinationDirectory = Join-Path $librariesRoot $packageId

New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null

try {
    $sourceFiles = @(
        Get-ChildItem -LiteralPath $package.SourceDirectory -Recurse -File |
            Where-Object {
                $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and (
                    $_.Extension.Equals(".cs", [System.StringComparison]::OrdinalIgnoreCase) -or
                    $_.Name.Equals("README.md", [System.StringComparison]::OrdinalIgnoreCase) -or
                    $_.Name.Equals("Guide.md", [System.StringComparison]::OrdinalIgnoreCase)
                )
            } |
            Sort-Object FullName
    )

    $csharpCount = @($sourceFiles | Where-Object { $_.Extension.Equals(".cs", [System.StringComparison]::OrdinalIgnoreCase) }).Count
    $readmeCount = @($sourceFiles | Where-Object { $_.Name.Equals("README.md", [System.StringComparison]::OrdinalIgnoreCase) }).Count

    if ($csharpCount -eq 0) {
        throw "API package '$packageId' contains no C# source files."
    }

    if ($readmeCount -eq 0) {
        throw "API package '$packageId' contains no README.md."
    }

    foreach ($sourceFile in $sourceFiles) {
        $relativePath = $sourceFile.FullName.Substring($package.SourceDirectory.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
        $destinationPath = Join-Path $destinationDirectory $relativePath
        $destinationParent = Split-Path -Parent $destinationPath
        New-Item -ItemType Directory -Path $destinationParent -Force | Out-Null
        Copy-Item -LiteralPath $sourceFile.FullName -Destination $destinationPath
    }

    $assetBaseName = "$packageId-$version"
    $componentName = "$assetBaseName-component.zip"
    $manifestName = "$assetBaseName-package.json"
    $componentPath = Join-Path $outputFull $componentName
    $manifestPath = Join-Path $outputFull $manifestName

    Compress-Archive -LiteralPath $librariesRoot -DestinationPath $componentPath -CompressionLevel Optimal

    $componentHash = (Get-FileHash -LiteralPath $componentPath -Algorithm SHA256).Hash.ToLowerInvariant()

    $manifest = [ordered]@{
        schemaVersion = 1
        id = $packageId
        version = $version
        changelog = @(
            $changelog | ForEach-Object {
                [ordered]@{
                    version = [string]$_.Version
                    changes = @($_.Changes)
                }
            }
        )
        dependencies = $dependencies
        folders = @($packageId)
        component = [ordered]@{
            asset = $componentName
            sha256 = $componentHash
        }
    }

    Write-Utf8WithoutBom -Path $manifestPath -Text (($manifest | ConvertTo-Json -Depth 10) + "`n")

    $commit = (& git -C $repoRoot rev-parse HEAD)
    if ($LASTEXITCODE -ne 0) {
        throw "Could not determine the release commit."
    }

    $notesPath = Join-Path $outputFull "release-notes.md"
    $notes = @(
        "# $packageId $version",
        "",
        "SELibs API consumer source package generated from commit ``$commit``.",
        "",
        "## Changes",
        ""
    )

    foreach ($change in @($changelog[0].Changes)) {
        $notes += "- $change"
    }

    $notes += @(
        "",
        "## Package",
        "",
        "- ID: ``$packageId``",
        "- Version: ``$version``",
        "- Component: ``$componentName``",
        "- SHA-256: ``$componentHash``",
        "- Included folder: ``Libraries/$packageId``",
        "",
        "## Exact dependencies"
    )

    foreach ($dependencyId in @($dependencies.Keys | Sort-Object)) {
        $notes += "- ``$dependencyId`` ``$($dependencies[$dependencyId])``"
    }

    if ($dependencies.Count -eq 0) {
        $notes += "- None"
    }

    $notes += @(
        "",
        "## Validation",
        "",
        "- Portable consumer project: ``$([System.IO.Path]::GetFileNameWithoutExtension($package.TestProjectPath))``",
        "- Canonical consumer source coverage validated through evaluated MSBuild Compile items.",
        "- SELibs dependency declarations validated against selibs.lock.json ownership.",
        "",
        "The component contains only the consumer package source. Provider implementation and dependency source are resolved separately by SELibs."
    )

    Write-Utf8WithoutBom -Path $notesPath -Text (($notes -join "`n") + "`n")

    Write-GitHubOutput -Name "component_path" -Value $componentPath
    Write-GitHubOutput -Name "manifest_path" -Value $manifestPath
    Write-GitHubOutput -Name "release_notes_path" -Value $notesPath
    Write-GitHubOutput -Name "release_title" -Value "$packageId $version"
    Write-GitHubOutput -Name "is_prerelease" -Value "false"

    Write-Output ""
    Write-Output "SELibs API release assets created:"
    Write-Output "  Manifest: $manifestPath"
    Write-Output "  Component: $componentPath"
    Write-Output "  SHA256: $componentHash"
    Write-Output "  Package files: $($sourceFiles.Count)"
}
finally {
    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }
}
