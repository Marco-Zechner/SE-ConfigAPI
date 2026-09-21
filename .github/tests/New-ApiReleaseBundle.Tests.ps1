$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.IO.Compression.FileSystem

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$bundleScript = Join-Path $repoRoot ".github\scripts\New-ApiReleaseBundle.ps1"
$metadataScript = Join-Path $repoRoot ".github\scripts\ApiReleaseMetadata.ps1"
. $metadataScript

$script:Passed = 0

function Assert-True {
    param([Parameter(Mandatory = $true)][bool]$Condition, [Parameter(Mandatory = $true)][string]$Message)

    if (-not $Condition) {
        throw $Message
    }

    $script:Passed++
}

function Assert-Equal {
    param([Parameter(Mandatory = $true)]$Expected, [Parameter(Mandatory = $true)]$Actual, [Parameter(Mandatory = $true)][string]$Message)

    if ($Expected -ne $Actual) {
        throw "$Message`nExpected: '$Expected'`nActual:   '$Actual'"
    }

    $script:Passed++
}

function Assert-Throws {
    param([Parameter(Mandatory = $true)][scriptblock]$Action, [Parameter(Mandatory = $true)][string]$ExpectedMessagePart)

    try {
        & $Action
    }
    catch {
        if ($_.Exception.Message -notlike "*$ExpectedMessagePart*") {
            throw "Expected error containing '$ExpectedMessagePart', but received '$($_.Exception.Message)'."
        }

        $script:Passed++
        return
    }

    throw "Expected an exception containing '$ExpectedMessagePart'."
}

function Get-ZipEntries {
    param([Parameter(Mandatory = $true)][string]$Path)

    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        return @($archive.Entries | ForEach-Object { $_.FullName.Replace("\", "/") } | Sort-Object)
    }
    finally {
        $archive.Dispose()
    }
}

$packages = @(Get-ApiReleasePackages -RepoRoot $repoRoot)
$matches = @($packages | Where-Object { $_.PackageId -eq "Mz.ConfigAPI.Consumer" })

Assert-Equal -Expected 1 -Actual $matches.Count -Message "Expected exactly one Mz.ConfigAPI.Consumer package."
$package = $matches[0]

$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("configapi-api-package-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $testRoot | Out-Null

try {
    $tag = "release/$($package.PackageId)/$($package.Version)"
    $output = Join-Path $testRoot "package"

    & $bundleScript -Tag $tag -OutputDirectory $output -SkipTests | Out-Null

    $manifestPath = Join-Path $output "$($package.PackageId)-$($package.Version)-package.json"
    $componentPath = Join-Path $output "$($package.PackageId)-$($package.Version)-component.zip"
    $notesPath = Join-Path $output "release-notes.md"

    Assert-True -Condition (Test-Path -LiteralPath $manifestPath -PathType Leaf) -Message "Package manifest is missing."
    Assert-True -Condition (Test-Path -LiteralPath $componentPath -PathType Leaf) -Message "Component archive is missing."
    Assert-True -Condition (Test-Path -LiteralPath $notesPath -PathType Leaf) -Message "Release notes are missing."

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json

    Assert-Equal -Expected 1 -Actual ([int]$manifest.schemaVersion) -Message "Manifest schema version is incorrect."
    Assert-Equal -Expected "Mz.ConfigAPI.Consumer" -Actual ([string]$manifest.id) -Message "Manifest package ID is incorrect."
    Assert-Equal -Expected "2.3.0" -Actual ([string]$manifest.version) -Message "Manifest package version is incorrect."
    Assert-Equal -Expected "Mz.ConfigAPI.Consumer" -Actual (@($manifest.folders) -join ",") -Message "Manifest owned folder is incorrect."
    Assert-Equal -Expected 2 -Actual @($manifest.dependencies.PSObject.Properties).Count -Message "Manifest dependency count is incorrect."
    Assert-Equal -Expected "0.3.0" -Actual ([string]$manifest.dependencies."Mz.ApiProtocol") -Message "ApiProtocol dependency is incorrect."
    Assert-Equal -Expected "0.2.0" -Actual ([string]$manifest.dependencies."Mz.SemanticVersioning") -Message "SemanticVersioning dependency is incorrect."

    $hash = (Get-FileHash -LiteralPath $componentPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Assert-Equal -Expected $hash -Actual ([string]$manifest.component.sha256) -Message "Manifest component checksum is incorrect."

    $entries = @(Get-ZipEntries -Path $componentPath)

    Assert-True -Condition ($entries.Count -gt 0) -Message "Component archive is empty."
    Assert-True -Condition (@($entries | Where-Object { -not $_.StartsWith("Libraries/Mz.ConfigAPI.Consumer/", [System.StringComparison]::Ordinal) }).Count -eq 0) -Message "Component archive contains files outside the consumer package folder."
    Assert-True -Condition ($entries -contains "Libraries/Mz.ConfigAPI.Consumer/ApiVersionFile.cs") -Message "ApiVersionFile.cs is missing from the component."
    Assert-True -Condition ($entries -contains "Libraries/Mz.ConfigAPI.Consumer/ConfigApiClient.cs") -Message "ConfigApiClient.cs is missing from the component."
    Assert-True -Condition ($entries -contains "Libraries/Mz.ConfigAPI.Consumer/README.md") -Message "README.md is missing from the component."
    Assert-True -Condition (@($entries | Where-Object { $_.StartsWith("Libraries/Mz.ApiProtocol", [System.StringComparison]::Ordinal) }).Count -eq 0) -Message "Component incorrectly embeds ApiProtocol."
    Assert-True -Condition (@($entries | Where-Object { $_.StartsWith("Libraries/Mz.SemanticVersioning", [System.StringComparison]::Ordinal) }).Count -eq 0) -Message "Component incorrectly embeds SemanticVersioning."
    Assert-True -Condition (@($entries | Where-Object { $_ -match '/test/' -or $_ -match 'SpaceEngineersApiStubs\.cs$' -or $_ -match '\.csproj$' }).Count -eq 0) -Message "Component contains test or project artifacts."

    $notes = Get-Content -LiteralPath $notesPath -Raw
    Assert-True -Condition ($notes.Contains("## Changes")) -Message "Release notes do not contain Changes."
    Assert-True -Condition ($notes.Contains("## Exact dependencies")) -Message "Release notes do not contain Exact dependencies."
    Assert-True -Condition ($notes.Contains("Mz.ApiProtocol")) -Message "Release notes do not mention ApiProtocol."
    Assert-True -Condition ($notes.Contains("Mz.SemanticVersioning")) -Message "Release notes do not mention SemanticVersioning."

    Assert-Throws -Action {
        & $bundleScript -Tag "configapi/v2" -OutputDirectory (Join-Path $testRoot "bad-tag") -SkipTests | Out-Null
    } -ExpectedMessagePart "Invalid release tag"

    Assert-Throws -Action {
        & $bundleScript -Tag "release/mz.ConfigAPI.Consumer/2.3.0" -OutputDirectory (Join-Path $testRoot "wrong-case") -SkipTests | Out-Null
    } -ExpectedMessagePart "was not discovered exactly once"

    Assert-Throws -Action {
        & $bundleScript -Tag "release/Mz.Unknown.Consumer/1.0.0" -OutputDirectory (Join-Path $testRoot "unknown") -SkipTests | Out-Null
    } -ExpectedMessagePart "was not discovered exactly once"

    Assert-Throws -Action {
        & $bundleScript -Tag "release/Mz.ConfigAPI.Consumer/1.9.9" -OutputDirectory (Join-Path $testRoot "wrong-version") -SkipTests | Out-Null
    } -ExpectedMessagePart "does not match ApiVersionFile version"

    Write-Output "OK API package-format tests passed: $script:Passed assertions"
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
