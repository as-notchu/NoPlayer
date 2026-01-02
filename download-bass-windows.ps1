# Download BASS libraries for Windows
# This script downloads the required BASS audio libraries for Windows builds

$ErrorActionPreference = "Stop"

Write-Host "🎵 Downloading BASS libraries for Windows..." -ForegroundColor Cyan
Write-Host ""

# Create directory structure
$targetDir = "runtimes/win-x64/native"
if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    Write-Host "✅ Created directory: $targetDir" -ForegroundColor Green
}

Write-Host ""
Write-Host "📥 Required downloads from https://www.un4seen.com/:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. BASS Library (bass24.zip)" -ForegroundColor White
Write-Host "   URL: https://www.un4seen.com/bass.html" -ForegroundColor Gray
Write-Host "   File needed: x64/bass.dll" -ForegroundColor Gray
Write-Host ""
Write-Host "2. BASSFLAC Plugin (bassflac24.zip)" -ForegroundColor White
Write-Host "   URL: https://www.un4seen.com/bass.html#addons" -ForegroundColor Gray
Write-Host "   File needed: x64/bassflac.dll" -ForegroundColor Gray
Write-Host ""
Write-Host "3. BASSWEBM Plugin (basswebm.zip)" -ForegroundColor White
Write-Host "   URL: https://www.un4seen.com/bass.html#addons" -ForegroundColor Gray
Write-Host "   File needed: x64/basswebm.dll" -ForegroundColor Gray
Write-Host ""
Write-Host "4. BASSMIX Plugin (bassmix24.zip)" -ForegroundColor White
Write-Host "   URL: https://www.un4seen.com/bass.html#addons" -ForegroundColor Gray
Write-Host "   File needed: x64/bassmix.dll" -ForegroundColor Gray
Write-Host ""
Write-Host "⚠️  Manual download required due to license terms" -ForegroundColor Yellow
Write-Host ""
Write-Host "After downloading, extract the x64 DLL files to:" -ForegroundColor Cyan
Write-Host "  $targetDir" -ForegroundColor White
Write-Host ""
Write-Host "Then run: .\publish-windows.ps1 <version>" -ForegroundColor Green
Write-Host ""

# Check if files already exist
$requiredFiles = @("bass.dll", "bassflac.dll", "basswebm.dll", "bassmix.dll")
$existingFiles = @()
$missingFiles = @()

foreach ($file in $requiredFiles) {
    $filePath = Join-Path $targetDir $file
    if (Test-Path $filePath) {
        $existingFiles += $file
    } else {
        $missingFiles += $file
    }
}

if ($existingFiles.Count -gt 0) {
    Write-Host "✅ Found existing files:" -ForegroundColor Green
    foreach ($file in $existingFiles) {
        Write-Host "   - $file" -ForegroundColor White
    }
    Write-Host ""
}

if ($missingFiles.Count -gt 0) {
    Write-Host "❌ Missing files:" -ForegroundColor Red
    foreach ($file in $missingFiles) {
        Write-Host "   - $file" -ForegroundColor White
    }
    Write-Host ""
    Write-Host "Please download and add these files to: $targetDir" -ForegroundColor Yellow
    exit 1
} else {
    Write-Host "🎉 All required BASS libraries are present!" -ForegroundColor Green
    Write-Host ""
    Write-Host "You can now build for Windows:" -ForegroundColor Cyan
    Write-Host "  .\publish-windows.ps1 <version>" -ForegroundColor White
    Write-Host ""
}
