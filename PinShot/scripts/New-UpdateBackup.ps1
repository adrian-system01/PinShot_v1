param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("major", "minor", "patch")]
    [string]$Bump
)

$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$workspaceRoot = Split-Path $projectRoot -Parent
$projectFile = Join-Path (Join-Path $workspaceRoot "PinShot.Desktop") "PinShot.Desktop.csproj"
$backupRoot = Join-Path (Split-Path $workspaceRoot -Parent) "PinShot_Respaldos"

[xml]$project = Get-Content -LiteralPath $projectFile
$versionNode = $project.Project.PropertyGroup.Version | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($versionNode)) {
    throw "No se encontró la versión del proyecto en PinShot.Desktop.csproj."
}

$version = [version]$versionNode
$backupNumber = 1
if (Test-Path -LiteralPath $backupRoot) {
    $existing = Get-ChildItem -LiteralPath $backupRoot -Directory | ForEach-Object {
        if ($_.Name -match '^respaldo-(\d+)') { [int]$matches[1] }
    }
    if ($existing) { $backupNumber = ($existing | Measure-Object -Maximum).Maximum + 1 }
}

New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
$formattedNumber = ([string]$backupNumber).PadLeft(3, '0')
$formattedVersion = "$($version.Major).$($version.Minor).$($version.Build)"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupName = "respaldo-$formattedNumber-v$formattedVersion-$timestamp"
$backupPath = Join-Path $backupRoot $backupName
New-Item -ItemType Directory -Path $backupPath | Out-Null

$files = Get-ChildItem -LiteralPath $workspaceRoot -Recurse -File | Where-Object {
    $relative = $_.FullName.Substring($workspaceRoot.Length).TrimStart('\')
    $segments = $relative -split '[\\/]'
    -not ($segments | Where-Object { $_ -in @("bin", "obj", ".git", ".vs", "publish", "Releases", "Releases-x64-check") })
}

foreach ($file in $files) {
    $relative = $file.FullName.Substring($workspaceRoot.Length).TrimStart('\')
    $destination = Join-Path $backupPath $relative
    New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination
}

switch ($Bump) {
    "major" { $nextVersion = [version]::new(($version.Major + 1), 0, 0) }
    "minor" { $nextVersion = [version]::new($version.Major, ($version.Minor + 1), 0) }
    "patch" { $nextVersion = [version]::new($version.Major, $version.Minor, ($version.Build + 1)) }
}

$project.Project.PropertyGroup | ForEach-Object {
    if ($_.Version) { $_.Version = "$($nextVersion.Major).$($nextVersion.Minor).$($nextVersion.Build)" }
}
$project.Save($projectFile)

Write-Output "Respaldo creado: $backupPath"
Write-Output "Versión actualizada: $formattedVersion -> $($nextVersion.Major).$($nextVersion.Minor).$($nextVersion.Build)"
