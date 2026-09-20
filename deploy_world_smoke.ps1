param(
    [switch]$Deploy,
    [switch]$ConfigureWorld
)

$ErrorActionPreference = "Stop"

$configApiRoot = $PSScriptRoot
$modsRoot = Split-Path -Parent $configApiRoot
$commandApiRoot = Join-Path $modsRoot "CommandAPI"

$steamWorkshopRoot = "E:\SteamLibrary\steamapps\workshop\content\244850"
$instanceRoot = "C:\ProgramData\SpaceEngineersDedicated\MzNetworkingSmoke"
$serverWorkshopRoot = Join-Path $instanceRoot "content\244850"
$serverConfigPath = Join-Path $instanceRoot "SpaceEngineers-Dedicated.cfg"

$commandApiWorkshopId = "3768390977"
$configApiWorkshopId = "2891367014"

function Assert-Directory([string]$Path, [string]$Label)
{
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "$Label directory does not exist: $Path"
    }
}

function Assert-File([string]$Path, [string]$Label)
{
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Label file does not exist: $Path"
    }
}

function Assert-CleanGitRepository([string]$Path, [string]$Label)
{
    $status = @(& git -C $Path status --porcelain --untracked-files=all)
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to inspect $Label Git state."
    }

    if ($status.Count -ne 0) {
        throw "$Label has uncommitted changes. Commit or restore them before deployment."
    }
}

function Get-WorldFiles()
{
    Assert-File $serverConfigPath "Dedicated-server config"

    [xml]$serverConfig = [System.IO.File]::ReadAllText($serverConfigPath)
    $loadWorldNode = $serverConfig.SelectSingleNode("//*[local-name()='LoadWorld']")
    if ($null -eq $loadWorldNode -or [string]::IsNullOrWhiteSpace($loadWorldNode.InnerText)) {
        throw "Dedicated-server config does not contain a usable LoadWorld path."
    }

    $sandboxPath = [string]$loadWorldNode.InnerText
    Assert-File $sandboxPath "Active Sandbox"

    $worldRoot = Split-Path -Parent $sandboxPath
    $sandboxConfigPath = Join-Path $worldRoot "Sandbox_config.sbc"
    Assert-File $sandboxConfigPath "Active Sandbox_config"

    return @($sandboxPath, $sandboxConfigPath)
}

function Test-ModEntry([string]$Path, [string]$WorkshopId)
{
    $text = [System.IO.File]::ReadAllText($Path)
    return $text.IndexOf("<PublishedFileId>$WorkshopId</PublishedFileId>", [System.StringComparison]::Ordinal) -ge 0
}

function Add-ModEntry([string]$Path, [string]$WorkshopId, [string]$FriendlyName)
{
    $text = [System.IO.File]::ReadAllText($Path)
    if ($text.IndexOf("<PublishedFileId>$WorkshopId</PublishedFileId>", [System.StringComparison]::Ordinal) -ge 0) {
        return
    }

    $closeTag = "</Mods>"
    $first = $text.IndexOf($closeTag, [System.StringComparison]::Ordinal)
    if ($first -lt 0) {
        throw "Mods closing tag not found in $Path"
    }

    $second = $text.IndexOf($closeTag, $first + $closeTag.Length, [System.StringComparison]::Ordinal)
    if ($second -ge 0) {
        throw "Mods closing tag is not unique in $Path"
    }

    $newline = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $entry = "    <ModItem FriendlyName=`"$FriendlyName`">$newline" +
        "      <Name>$WorkshopId.sbm</Name>$newline" +
        "      <PublishedFileId>$WorkshopId</PublishedFileId>$newline" +
        "      <PublishedServiceName>Steam</PublishedServiceName>$newline" +
        "    </ModItem>$newline  "

    $updated = $text.Substring(0, $first) + $entry + $text.Substring($first)
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $updated, $utf8)
}

function Write-WorkshopMetadata([string]$TargetRoot)
{
    $metadataLines = @(
        '<?xml version="1.0" encoding="utf-8"?>',
        '<ModMetadata xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">',
        '  <ModVersion>1.0</ModVersion>',
        '</ModMetadata>'
    )

    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText((Join-Path $TargetRoot "metadata.mod"), ($metadataLines -join [Environment]::NewLine) + [Environment]::NewLine, $utf8)
}

function Copy-Mod([string]$SourceRoot, [string]$WorkshopId, [string]$Label)
{
    $sourceData = Join-Path $SourceRoot "Data"
    Assert-Directory $sourceData "$Label Data"

    foreach ($workshopRoot in @($serverWorkshopRoot, $steamWorkshopRoot)) {
        Assert-Directory $workshopRoot "Workshop root"
        $targetRoot = Join-Path $workshopRoot $WorkshopId

        if (Test-Path -LiteralPath $targetRoot) {
            Remove-Item -LiteralPath $targetRoot -Recurse -Force
        }

        New-Item -ItemType Directory -Path $targetRoot -Force | Out-Null
        $targetData = Join-Path $targetRoot "Data"

        & robocopy.exe $sourceData $targetData "*.*" /S /XD bin obj .vs ignored /XF *.csproj *.csproj.user *.sln *.slnx *.log | Out-Null
        $robocopyExitCode = $LASTEXITCODE
        if ($robocopyExitCode -gt 7) {
            throw "Robocopy failed for $Label -> $targetRoot with exit code $robocopyExitCode."
        }

        Write-WorkshopMetadata $targetRoot
        Write-Output "Deployed $Label to $targetRoot"
    }
}

$serverProcesses = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -match '^SpaceEngineersDedicated' })
if ($serverProcesses.Count -ne 0) {
    throw "A Space Engineers dedicated-server process is running. Stop it before using this smoke helper."
}

Assert-Directory $configApiRoot "ConfigAPI"
Assert-Directory $commandApiRoot "CommandAPI"
Assert-Directory $steamWorkshopRoot "Steam workshop"
Assert-Directory $serverWorkshopRoot "Smoke-server workshop"

$worldFiles = @(Get-WorldFiles)
foreach ($worldFile in $worldFiles) {
    if (-not (Test-ModEntry $worldFile $commandApiWorkshopId)) {
        throw "Smoke world does not contain the existing CommandAPI placeholder ID $commandApiWorkshopId in $worldFile"
    }
}

$missingConfigApiEntry = @($worldFiles | Where-Object { -not (Test-ModEntry $_ $configApiWorkshopId) })

Write-Output "World smoke deployment plan:"
Write-Output "  Instance:   $instanceRoot"
Write-Output "  CommandAPI: $commandApiRoot -> workshop slot $commandApiWorkshopId"
Write-Output "  ConfigAPI:  $configApiRoot -> workshop slot $configApiWorkshopId"
Write-Output "  World files:"
$worldFiles | ForEach-Object { Write-Output "    $_" }

if ($missingConfigApiEntry.Count -eq 0) {
    Write-Output "  ConfigAPI world entry: already present"
}
else {
    Write-Output "  ConfigAPI world entry: missing from $($missingConfigApiEntry.Count) active world file(s)"
}

if (-not $Deploy) {
    Write-Output "Plan only. Re-run with -Deploy to copy mod files."
    if ($missingConfigApiEntry.Count -ne 0) {
        Write-Output "Use -Deploy -ConfigureWorld for the first deployment so the ConfigAPI placeholder is added to both active world files."
    }

    return
}

Assert-CleanGitRepository $configApiRoot "ConfigAPI"
Assert-CleanGitRepository $commandApiRoot "CommandAPI"

if ($missingConfigApiEntry.Count -ne 0 -and -not $ConfigureWorld) {
    throw "ConfigAPI placeholder ID $configApiWorkshopId is missing. First deployment requires -Deploy -ConfigureWorld."
}

if ($ConfigureWorld) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"

    foreach ($worldFile in $worldFiles) {
        $backupPath = "$worldFile.before-configapi-world-smoke-$timestamp.bak"
        Copy-Item -LiteralPath $worldFile -Destination $backupPath
        Write-Output "Backed up $worldFile -> $backupPath"
    }

    foreach ($worldFile in $worldFiles) {
        Add-ModEntry $worldFile $configApiWorkshopId "ConfigAPI"
    }
}

Copy-Mod $commandApiRoot $commandApiWorkshopId "CommandAPI"
Copy-Mod $configApiRoot $configApiWorkshopId "ConfigAPI"

Write-Output "World smoke deployment complete."