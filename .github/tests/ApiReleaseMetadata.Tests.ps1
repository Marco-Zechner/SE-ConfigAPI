$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
. (Join-Path $repoRoot ".github\scripts\ApiReleaseMetadata.ps1")

$script:Passed = 0

function Assert-Equal {
    param([Parameter(Mandatory = $true)]$Expected, [Parameter(Mandatory = $true)]$Actual, [Parameter(Mandatory = $true)][string]$Message)

    if ($Expected -ne $Actual) {
        throw "$Message`nExpected: '$Expected'`nActual:   '$Actual'"
    }

    $script:Passed++
}

function Assert-True {
    param([Parameter(Mandatory = $true)][bool]$Condition, [Parameter(Mandatory = $true)][string]$Message)

    if (-not $Condition) {
        throw $Message
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

$packages = @(Get-ApiReleasePackages -RepoRoot $repoRoot)
$configPackage = @($packages | Where-Object { $_.PackageId -eq "Mz.ConfigAPI.Consumer" })

Assert-Equal -Expected 1 -Actual $configPackage.Count -Message "Mz.ConfigAPI.Consumer was not discovered exactly once."
$package = $configPackage[0]

Assert-Equal -Expected "Mz.ConfigAPI.Consumer" -Actual ([string]$package.PackageId) -Message "Package ID was not derived from the Consumer folder."
Assert-Equal -Expected "2.3.0" -Actual ([string]$package.Version) -Message "API package version was parsed incorrectly."
Assert-Equal -Expected "Mz.ConfigAPI.Consumer.Tests" -Actual ([System.IO.Path]::GetFileNameWithoutExtension($package.TestProjectPath)) -Message "Dedicated consumer test project was not selected."
Assert-Equal -Expected "0.3.0" -Actual ([string]$package.Dependencies["Mz.ApiProtocol"]) -Message "ApiProtocol declaration was parsed incorrectly."
Assert-Equal -Expected "0.2.0" -Actual ([string]$package.Dependencies["Mz.SemanticVersioning"]) -Message "SemanticVersioning declaration was parsed incorrectly."
Assert-True -Condition (-not $package.Dependencies.Contains("Mz.Logging")) -Message "Consumer package unexpectedly declares Mz.Logging."

$resolved = Resolve-ApiPackageDependencies -Package $package -RepoRoot $repoRoot

Assert-Equal -Expected 2 -Actual $resolved.Count -Message "Resolved dependency count is incorrect."
Assert-Equal -Expected "0.3.0" -Actual ([string]$resolved["Mz.ApiProtocol"]) -Message "ApiProtocol compiled-source dependency was not resolved."
Assert-Equal -Expected "0.2.0" -Actual ([string]$resolved["Mz.SemanticVersioning"]) -Message "SemanticVersioning compiled-source dependency was not resolved."

$originalDependencies = $package.Dependencies

try {
    $package.Dependencies = [ordered]@{ "Mz.ApiProtocol" = "0.3.0" }

    Assert-Throws -Action {
        Resolve-ApiPackageDependencies -Package $package -RepoRoot $repoRoot | Out-Null
    } -ExpectedMessagePart "does not declare dependency 'Mz.SemanticVersioning'"

    $package.Dependencies = [ordered]@{
        "Mz.ApiProtocol" = "0.2.5"
        "Mz.SemanticVersioning" = "0.2.0"
    }

    Assert-Throws -Action {
        Resolve-ApiPackageDependencies -Package $package -RepoRoot $repoRoot | Out-Null
    } -ExpectedMessagePart "declares dependency 'Mz.ApiProtocol' version '0.2.5'"

    $package.Dependencies = [ordered]@{
        "Mz.ApiProtocol" = "0.3.0"
        "Mz.SemanticVersioning" = "0.2.0"
        "Mz.Logging" = "0.1.2"
    }

    Assert-Throws -Action {
        Resolve-ApiPackageDependencies -Package $package -RepoRoot $repoRoot | Out-Null
    } -ExpectedMessagePart "declares dependency 'Mz.Logging'"
}
finally {
    $package.Dependencies = $originalDependencies
}

Write-Output "OK API release metadata tests passed: $script:Passed assertions"
