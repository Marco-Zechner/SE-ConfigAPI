Set-StrictMode -Version Latest

function Test-ApiPathInsideRoot {
    param([Parameter(Mandatory = $true)][string]$Path, [Parameter(Mandatory = $true)][string]$Root)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $fullRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)

    if ($fullPath.Equals($fullRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    return $fullPath.StartsWith($fullRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-ApiObjectPropertyValue {
    param([AllowNull()][object]$Object, [Parameter(Mandatory = $true)][string]$Name)

    if ($null -eq $Object) {
        return $null
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function ConvertFrom-ApiCSharpStringLiteral {
    param([Parameter(Mandatory = $true)][string]$Value, [Parameter(Mandatory = $true)][string]$Path)

    try {
        return [System.Text.RegularExpressions.Regex]::Unescape($Value)
    }
    catch {
        throw "API metadata in '$Path' contains an unsupported C# string escape."
    }
}

function Read-ApiVersionDescriptor {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "API version file not found: $Path"
    }

    $text = Get-Content -LiteralPath $Path -Raw
    $versionParts = [ordered]@{}

    foreach ($name in @("Major", "Minor", "Patch")) {
        $match = [regex]::Match($text, ('public\s+const\s+int\s+' + [regex]::Escape($name) + '\s*=\s*(?<value>[0-9]+)\s*;'))

        if (-not $match.Success) {
            throw "API version file '$Path' does not declare numeric $name."
        }

        $versionParts[$name] = $match.Groups["value"].Value
    }

    $version = "$($versionParts["Major"]).$($versionParts["Minor"]).$($versionParts["Patch"])"

    try {
        [void][version]::Parse($version)
    }
    catch {
        throw "API version file '$Path' declares invalid version '$version'."
    }

    $dependencyEntryPattern = 'new\s+LibraryDependency\s*\(\s*"(?<packageId>(?:\\.|[^"\\])*)"\s*,\s*"(?<version>(?:\\.|[^"\\])*)"\s*\)'
    $dependencyPropertyPattern = '(?s)public\s+static\s+LibraryDependency\s*\[\s*\]\s+Dependencies\s*\{\s*get;\s*\}\s*=\s*(?:new\s+LibraryDependency\s*\[\s*0\s*\]|(?:new\s*\[\s*\]\s*)?\{(?<entries>.*?)\})\s*;'
    $dependencyPropertyMatches = @([regex]::Matches($text, $dependencyPropertyPattern))

    if ($dependencyPropertyMatches.Count -ne 1) {
        throw "API version file '$Path' must declare exactly one supported Dependencies property."
    }

    $dependencyEntriesText = [string]$dependencyPropertyMatches[0].Groups["entries"].Value
    $dependencies = [ordered]@{}
    $dependencyKeys = @{}

    if (-not [string]::IsNullOrWhiteSpace($dependencyEntriesText)) {
        $dependencyListPattern = '(?s)^\s*(?:' + $dependencyEntryPattern + '\s*(?:,\s*' + $dependencyEntryPattern + '\s*)*(?:,\s*)?)?$'

        if ($dependencyEntriesText -notmatch $dependencyListPattern) {
            throw "Dependencies property in '$Path' contains unsupported syntax. Use only LibraryDependency(packageId, version) entries."
        }

        foreach ($dependencyMatch in @([regex]::Matches($dependencyEntriesText, $dependencyEntryPattern))) {
            $dependencyPackageId = ConvertFrom-ApiCSharpStringLiteral -Value $dependencyMatch.Groups["packageId"].Value -Path $Path
            $dependencyVersion = ConvertFrom-ApiCSharpStringLiteral -Value $dependencyMatch.Groups["version"].Value -Path $Path

            if ($dependencyPackageId -notmatch '^[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*$') {
                throw "API dependency '$dependencyPackageId' in '$Path' has an invalid package ID."
            }

            if ($dependencyVersion -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') {
                throw "API dependency '$dependencyPackageId' in '$Path' has invalid version '$dependencyVersion'."
            }

            try {
                [void][version]::Parse($dependencyVersion)
            }
            catch {
                throw "API dependency '$dependencyPackageId' in '$Path' has invalid version '$dependencyVersion'."
            }

            $key = $dependencyPackageId.ToLowerInvariant()
            if ($dependencyKeys.ContainsKey($key)) {
                throw "API dependency '$dependencyPackageId' is declared more than once in '$Path'."
            }

            $dependencyKeys[$key] = $dependencyPackageId
            $dependencies[$dependencyPackageId] = $dependencyVersion
        }
    }

    $entryPattern = '(?s)new\s+ChangelogEntry\s*\(\s*"(?<version>(?:\\.|[^"\\])*)"\s*,\s*new\s*\[\]\s*\{(?<changes>.*?)\}\s*\)'
    $entryMatches = @([regex]::Matches($text, $entryPattern))

    if ($entryMatches.Count -eq 0) {
        throw "API version file '$Path' does not declare any ChangelogEntry values."
    }

    $changelog = New-Object System.Collections.ArrayList
    $seenVersions = @{}
    $previousVersion = $null

    foreach ($entryMatch in $entryMatches) {
        $entryVersion = ConvertFrom-ApiCSharpStringLiteral -Value $entryMatch.Groups["version"].Value -Path $Path

        if ($entryVersion -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') {
            throw "API changelog in '$Path' contains invalid version '$entryVersion'."
        }

        $versionKey = $entryVersion.ToLowerInvariant()
        if ($seenVersions.ContainsKey($versionKey)) {
            throw "API changelog in '$Path' declares version '$entryVersion' more than once."
        }

        $parsedVersion = [version]::Parse($entryVersion)
        if ($null -ne $previousVersion -and $parsedVersion.CompareTo($previousVersion) -ge 0) {
            throw "API changelog in '$Path' must be ordered from newest to oldest."
        }

        $changesText = $entryMatch.Groups["changes"].Value
        $changeMatches = @([regex]::Matches($changesText, '"(?<value>(?:\\.|[^"\\])*)"'))

        if ($changeMatches.Count -eq 0) {
            throw "API changelog version '$entryVersion' in '$Path' does not contain any changes."
        }

        $residue = [regex]::Replace($changesText, '"(?:\\.|[^"\\])*"', "")
        $residue = [regex]::Replace($residue, '[\s,]', "")

        if (-not [string]::IsNullOrEmpty($residue)) {
            throw "API changelog version '$entryVersion' in '$Path' uses unsupported change-list syntax."
        }

        $changes = New-Object System.Collections.ArrayList
        foreach ($changeMatch in $changeMatches) {
            $change = ConvertFrom-ApiCSharpStringLiteral -Value $changeMatch.Groups["value"].Value -Path $Path

            if ([string]::IsNullOrWhiteSpace($change)) {
                throw "API changelog version '$entryVersion' in '$Path' contains an empty change."
            }

            [void]$changes.Add($change)
        }

        [void]$changelog.Add([pscustomobject]@{ Version = $entryVersion; Changes = @($changes) })
        $seenVersions[$versionKey] = $true
        $previousVersion = $parsedVersion
    }

    if ([string]$changelog[0].Version -ne $version) {
        throw "API changelog in '$Path' must begin with current package version '$version'."
    }

    return [pscustomobject]@{
        Version = $version
        Dependencies = $dependencies
        Changelog = @($changelog)
        VersionFilePath = [System.IO.Path]::GetFullPath($Path)
    }
}

function Get-ApiProjectTargetFramework {
    param([Parameter(Mandatory = $true)][xml]$Project, [Parameter(Mandatory = $true)][string]$ProjectPath)

    $frameworkNodes = @($Project.SelectNodes("/*[local-name()='Project']/*[local-name()='PropertyGroup']/*[local-name()='TargetFramework']") | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.InnerText) })

    if ($frameworkNodes.Count -eq 0) {
        throw "Project '$ProjectPath' does not declare TargetFramework."
    }

    return [string]$frameworkNodes[0].InnerText
}

function ConvertFrom-ApiMsBuildJson {
    param([Parameter(Mandatory = $true)][object[]]$Output, [Parameter(Mandatory = $true)][string]$ProjectPath)

    $text = $Output -join [Environment]::NewLine
    $start = $text.IndexOf("{")
    $end = $text.LastIndexOf("}")

    if ($start -lt 0 -or $end -lt $start) {
        throw "MSBuild returned no JSON for '$ProjectPath'."
    }

    try {
        return $text.Substring($start, $end - $start + 1) | ConvertFrom-Json
    }
    catch {
        throw "MSBuild returned invalid JSON for '$ProjectPath': $($_.Exception.Message)"
    }
}

function Get-ApiEvaluatedCompileItems {
    param([Parameter(Mandatory = $true)][string]$ProjectPath, [Parameter(Mandatory = $true)][string]$TargetFramework)

    $output = @(& dotnet msbuild $ProjectPath -nologo -verbosity:quiet "-property:TargetFramework=$TargetFramework" -getItem:Compile)

    if ($LASTEXITCODE -ne 0) {
        $output | Select-Object -First 80
        throw "MSBuild item query failed for '$ProjectPath'."
    }

    $result = ConvertFrom-ApiMsBuildJson -Output $output -ProjectPath $ProjectPath
    $items = Get-ApiObjectPropertyValue -Object $result -Name "Items"

    if ($null -eq $items) {
        throw "MSBuild item JSON for '$ProjectPath' does not define Items."
    }

    $compile = Get-ApiObjectPropertyValue -Object $items -Name "Compile"
    if ($null -eq $compile) {
        return @()
    }

    return @($compile)
}

function Get-ApiSELibsLockContext {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $manifestPath = Join-Path $RepoRoot "selibs.json"
    $lockPath = Join-Path $RepoRoot "selibs.lock.json"

    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -or -not (Test-Path -LiteralPath $lockPath -PathType Leaf)) {
        throw "API package dependency validation requires both selibs.json and selibs.lock.json."
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json

    if ($manifest.schemaVersion -ne 1 -or [string]::IsNullOrWhiteSpace([string]$manifest.librariesPath)) {
        throw "selibs.json does not use the supported schema."
    }

    if ($lock.schemaVersion -ne 1 -or $null -eq $lock.packages) {
        throw "selibs.lock.json does not use the supported schema."
    }

    $librariesRoot = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot ([string]$manifest.librariesPath)))
    $folderOwners = @{}

    foreach ($packageProperty in $lock.packages.PSObject.Properties) {
        $packageId = [string]$packageProperty.Name
        $entry = $packageProperty.Value
        $version = [string]$entry.version

        if ($version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') {
            throw "Locked package '$packageId' does not use an exact numeric version."
        }

        foreach ($folderValue in @($entry.folders)) {
            $folder = [string]$folderValue
            $key = $folder.ToLowerInvariant()

            if ($folderOwners.ContainsKey($key)) {
                throw "SELibs folder '$folder' is owned by more than one locked package."
            }

            $folderOwners[$key] = [pscustomobject]@{ PackageId = $packageId; Version = $version }
        }
    }

    return [pscustomobject]@{ LibrariesRoot = $librariesRoot; FolderOwners = $folderOwners }
}

function Get-ApiSELibsLockedOwnerForPath {
    param([Parameter(Mandatory = $true)][string]$Path, [Parameter(Mandatory = $true)][object]$LockContext)

    if (-not (Test-ApiPathInsideRoot -Path $Path -Root $LockContext.LibrariesRoot)) {
        return $null
    }

    $root = $LockContext.LibrariesRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $relative = [System.IO.Path]::GetFullPath($Path).Substring($root.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $segments = @($relative.Split([char[]]@([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar), [System.StringSplitOptions]::RemoveEmptyEntries))

    if ($segments.Count -eq 0) {
        throw "Could not identify SELibs folder for '$Path'."
    }

    $folder = [string]$segments[0]
    $key = $folder.ToLowerInvariant()

    if (-not $LockContext.FolderOwners.ContainsKey($key)) {
        throw "Compiled SELibs folder '$folder' is not owned by any package in selibs.lock.json."
    }

    return $LockContext.FolderOwners[$key]
}

function Add-ApiDependency {
    param([Parameter(Mandatory = $true)][System.Collections.IDictionary]$Dependencies, [Parameter(Mandatory = $true)][hashtable]$DependencyKeys, [Parameter(Mandatory = $true)][string]$PackageId, [Parameter(Mandatory = $true)][string]$Version)

    $key = $PackageId.ToLowerInvariant()

    if ($DependencyKeys.ContainsKey($key)) {
        $existingId = [string]$DependencyKeys[$key]
        $existingVersion = [string]$Dependencies[$existingId]

        if ($existingVersion -ne $Version) {
            throw "Dependency conflict for '$PackageId': '$existingVersion' and '$Version'."
        }

        return
    }

    $DependencyKeys[$key] = $PackageId
    $Dependencies[$PackageId] = $Version
}

function Get-ApiReleasePackages {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $consumerRoot = Join-Path $RepoRoot "Consumer"
    if (-not (Test-Path -LiteralPath $consumerRoot -PathType Container)) {
        throw "Consumer root not found: $consumerRoot"
    }

    $packages = New-Object System.Collections.ArrayList
    $packageKeys = @{}

    foreach ($directory in @(Get-ChildItem -LiteralPath $consumerRoot -Directory | Sort-Object Name)) {
        $versionFile = Join-Path $directory.FullName "ApiVersionFile.cs"
        if (-not (Test-Path -LiteralPath $versionFile -PathType Leaf)) {
            continue
        }

        $packageId = [string]$directory.Name
        if ($packageId -notmatch '^[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*$') {
            throw "Consumer folder '$packageId' is not a valid SELibs package ID."
        }

        $key = $packageId.ToLowerInvariant()
        if ($packageKeys.ContainsKey($key)) {
            throw "API package '$packageId' is declared more than once."
        }

        $descriptor = Read-ApiVersionDescriptor -Path $versionFile
        $testProject = Join-Path $RepoRoot ("test\" + $packageId + ".Tests\" + $packageId + ".Tests.csproj")

        if (-not (Test-Path -LiteralPath $testProject -PathType Leaf)) {
            throw "API package '$packageId' requires dedicated test project '$testProject'."
        }

        [xml]$project = Get-Content -LiteralPath $testProject -Raw
        $targetFramework = Get-ApiProjectTargetFramework -Project $project -ProjectPath $testProject
        $compile = Get-ApiEvaluatedCompileItems -ProjectPath $testProject -TargetFramework $targetFramework

        [void]$packages.Add([pscustomobject]@{
            PackageId = $packageId
            Version = $descriptor.Version
            Dependencies = $descriptor.Dependencies
            Changelog = @($descriptor.Changelog)
            VersionFilePath = $descriptor.VersionFilePath
            SourceDirectory = [System.IO.Path]::GetFullPath($directory.FullName)
            TestProjectPath = [System.IO.Path]::GetFullPath($testProject)
            Compile = @($compile)
        })

        $packageKeys[$key] = $true
    }

    if ($packages.Count -eq 0) {
        throw "No Consumer/*/ApiVersionFile.cs packages were found."
    }

    return @($packages)
}

function Get-ApiDiscoveredDependencies {
    param([Parameter(Mandatory = $true)][object]$Package, [Parameter(Mandatory = $true)][string]$RepoRoot)

    $lockContext = Get-ApiSELibsLockContext -RepoRoot $RepoRoot
    $testRoot = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot "test"))
    $dependencies = [ordered]@{}
    $dependencyKeys = @{}
    $compiledOwnedFiles = @{}

    foreach ($compileItem in @($Package.Compile)) {
        $sourcePathValue = [string](Get-ApiObjectPropertyValue -Object $compileItem -Name "FullPath")
        if ([string]::IsNullOrWhiteSpace($sourcePathValue)) {
            throw "API test project '$($Package.TestProjectPath)' contains a Compile item without FullPath metadata."
        }

        $sourcePath = [System.IO.Path]::GetFullPath($sourcePathValue)

        if ($sourcePath -match '[\\/](bin|obj)[\\/]') {
            continue
        }

        if (Test-ApiPathInsideRoot -Path $sourcePath -Root $Package.SourceDirectory) {
            $compiledOwnedFiles[$sourcePath.ToLowerInvariant()] = $true
            continue
        }

        if (Test-ApiPathInsideRoot -Path $sourcePath -Root $testRoot) {
            continue
        }

        $dependency = Get-ApiSELibsLockedOwnerForPath -Path $sourcePath -LockContext $lockContext

        if ($null -eq $dependency) {
            throw "Compiled source '$sourcePath' is outside API package '$($Package.PackageId)', test sources, and SELibs-managed dependencies."
        }

        Add-ApiDependency -Dependencies $dependencies -DependencyKeys $dependencyKeys -PackageId ([string]$dependency.PackageId) -Version ([string]$dependency.Version)
    }

    foreach ($ownedSource in @(Get-ChildItem -LiteralPath $Package.SourceDirectory -Recurse -File -Filter "*.cs")) {
        $key = [System.IO.Path]::GetFullPath($ownedSource.FullName).ToLowerInvariant()

        if (-not $compiledOwnedFiles.ContainsKey($key)) {
            throw "API test project '$($Package.TestProjectPath)' does not compile owned consumer source '$($ownedSource.FullName)'."
        }
    }

    $ordered = [ordered]@{}
    foreach ($packageId in @($dependencies.Keys | Sort-Object)) {
        $ordered[[string]$packageId] = [string]$dependencies[$packageId]
    }

    return $ordered
}

function Resolve-ApiPackageDependencies {
    param([Parameter(Mandatory = $true)][object]$Package, [Parameter(Mandatory = $true)][string]$RepoRoot)

    $discovered = Get-ApiDiscoveredDependencies -Package $Package -RepoRoot $RepoRoot
    $declared = $Package.Dependencies
    $declaredKeys = @{}
    $discoveredKeys = @{}

    foreach ($packageIdValue in @($declared.Keys)) {
        $packageId = [string]$packageIdValue
        $declaredKeys[$packageId.ToLowerInvariant()] = $packageId
    }

    foreach ($packageIdValue in @($discovered.Keys)) {
        $packageId = [string]$packageIdValue
        $key = $packageId.ToLowerInvariant()
        $discoveredKeys[$key] = $packageId

        if (-not $declaredKeys.ContainsKey($key)) {
            throw "API package '$($Package.PackageId)' uses dependency '$packageId' version '$($discovered[$packageId])' but ApiVersionFile does not declare dependency '$packageId'."
        }

        $declaredId = [string]$declaredKeys[$key]
        $declaredVersion = [string]$declared[$declaredId]
        $discoveredVersion = [string]$discovered[$packageId]

        if ($declaredVersion -ne $discoveredVersion) {
            throw "API package '$($Package.PackageId)' declares dependency '$declaredId' version '$declaredVersion', but compiled source usage requires version '$discoveredVersion'."
        }
    }

    foreach ($packageIdValue in @($declared.Keys)) {
        $packageId = [string]$packageIdValue

        if (-not $discoveredKeys.ContainsKey($packageId.ToLowerInvariant())) {
            throw "API package '$($Package.PackageId)' declares dependency '$packageId' version '$($declared[$packageId])', but no matching compiled SELibs source dependency was discovered."
        }
    }

    $ordered = [ordered]@{}
    foreach ($packageId in @($declared.Keys | Sort-Object)) {
        $ordered[[string]$packageId] = [string]$declared[$packageId]
    }

    return $ordered
}
