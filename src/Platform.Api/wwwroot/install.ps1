# SpawnWeaver Godot SDK installer (Windows / PowerShell).
#
# Run this from the ROOT of your Godot project:
#   iwr __BASE_URL__/install.ps1 -UseBasicParsing | iex
#
# It downloads the SpawnWeaver addon and extracts it into ./addons/multiplayer_service.
$ErrorActionPreference = 'Stop'

$base   = '__BASE_URL__'
$zipUrl = "$base/sdk/multiplayer_service.zip"
$dest   = (Get-Location).Path

Write-Host "Installing the SpawnWeaver Godot SDK into $dest\addons ..." -ForegroundColor Cyan

$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("sw_sdk_" + [guid]::NewGuid().ToString('N') + ".zip")
try {
    Invoke-WebRequest -Uri $zipUrl -OutFile $tmp -UseBasicParsing
    Expand-Archive -Path $tmp -DestinationPath $dest -Force
} finally {
    if (Test-Path $tmp) { Remove-Item $tmp -Force }
}

Write-Host "Done." -ForegroundColor Green
Write-Host "Next: open your project in Godot and enable 'SpawnWeaver Multiplayer Service'"
Write-Host "      in Project -> Project Settings -> Plugins."
