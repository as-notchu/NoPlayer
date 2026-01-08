# Build Windows package for Music Player
# Creates a distributable ZIP package for Windows
# Usage: .\publish-windows.ps1 <version>
# Example: .\publish-windows.ps1 1.0.0

param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

$AppName = "MusicPlayer"
$OutputDir = "publish-windows"
$ArchiveName = "${AppName}-${Version}-Windows-x64"

Write-Host "Building Music Player v${Version} for Windows..." -ForegroundColor Cyan
Write-Host ""

# Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
}
if (Test-Path "${ArchiveName}.zip") {
    Remove-Item -Force "${ArchiveName}.zip"
}
if (Test-Path "bin\Release") {
    Remove-Item -Recurse -Force "bin\Release"
}
if (Test-Path "obj\Release") {
    Remove-Item -Recurse -Force "obj\Release"
}

# Build for Windows x64 (single-file with native libraries extracted)
Write-Host "Building .NET application for Windows x64..." -ForegroundColor Yellow
dotnet publish -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:UseAppHost=true `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:Version="$Version" `
    -o "$OutputDir"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Create README
Write-Host "Creating README..." -ForegroundColor Yellow
$readmeContent = @"
Music Player v${Version}
========================

Installation
------------
1. Extract all files from the archive
2. Run NoPlayer.exe (or MusicPlayer.exe)
3. (Optional) Create a desktop shortcut

First Launch
------------
Windows may show a security warning because the app isn't signed.
Click "More info" -> "Run anyway" to launch the app.

System Requirements
-------------------
- Windows 10/11 (64-bit)
- No additional software required (self-contained)

Features
--------
- Play music files (MP3, FLAC, WAV, OGG, WEBM, etc.)
- Create and manage playlists
- Search your music library
- Support for multiple music directories
- Download YouTube playlists

Support
-------
For issues or questions, please visit the GitHub repository.

Copyright (C) 2025. All rights reserved.
"@

Set-Content -Path "$OutputDir\README.txt" -Value $readmeContent

# Create a simple batch launcher
Write-Host "Creating launcher script..." -ForegroundColor Yellow
$launcherContent = '@echo off
start "" "%~dp0NoPlayer.exe"
'

$launcherPath = Join-Path $OutputDir "Launch-MusicPlayer.bat"
Set-Content -Path $launcherPath -Value $launcherContent

# Create ZIP archive
Write-Host "Creating ZIP archive..." -ForegroundColor Yellow
Compress-Archive -Path "$OutputDir\*" -DestinationPath "${ArchiveName}.zip" -Force

Write-Host ""
Write-Host "Build complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Windows package created: ${ArchiveName}.zip" -ForegroundColor Green
Write-Host "Standalone folder: $OutputDir" -ForegroundColor Green
Write-Host ""
Write-Host "To distribute:" -ForegroundColor Cyan
Write-Host "   Share ${ArchiveName}.zip with Windows users"
Write-Host ""
Write-Host "Tip: For a proper installer, consider using tools like:" -ForegroundColor Yellow
Write-Host "   - Inno Setup (free)"
Write-Host "   - WiX Toolset (free)"
Write-Host "   - Advanced Installer"
Write-Host ""
