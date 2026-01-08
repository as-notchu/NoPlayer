#!/bin/bash

# Build Windows package for Music Player (from macOS/Linux)
# Creates a distributable ZIP package for Windows
# Usage: ./publish-windows.sh <version>
# Example: ./publish-windows.sh 1.0.0

set -e

# Check if version argument is provided
if [ -z "$1" ]; then
  echo "❌ Error: Version number required"
  echo ""
  echo "Usage: ./publish-windows.sh <version>"
  echo "Example: ./publish-windows.sh 1.0.0"
  echo "         ./publish-windows.sh 0.0.2"
  exit 1
fi

APP_NAME="NoPlayer"
VERSION="$1"
BUILD_ROOT="builds/windows"
OUTPUT_DIR="${BUILD_ROOT}/publish"
ARCHIVE_NAME="${APP_NAME}-${VERSION}-Windows-x64"
ARCHIVE_PATH="${BUILD_ROOT}/${ARCHIVE_NAME}.zip"

echo "🎵 Building NoPlayer v${VERSION} for Windows..."
echo ""

# Clean previous builds
echo "🧹 Cleaning previous builds..."
rm -rf "$OUTPUT_DIR"
rm -rf "$ARCHIVE_PATH"
rm -rf bin/Release
rm -rf obj/Release

# Ensure output directory exists
mkdir -p "$BUILD_ROOT"

# Build for Windows x64
echo "🔨 Building .NET application for Windows x64..."
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:UseAppHost=true \
  -p:PublishTrimmed=false \
  -p:DebugType=None \
  -p:DebugSymbols=false \
  -p:Version="${VERSION}" \
  -o "$OUTPUT_DIR"

# Create README
echo "📝 Creating README..."
cat > "$OUTPUT_DIR/README.txt" << EOF
NoPlayer v${VERSION}
========================

Installation
------------
1. Extract all files from the archive
2. Run NoPlayer.exe
3. (Optional) Create a desktop shortcut

First Launch
------------
Windows may show a security warning because the app isn't signed.
Click "More info" → "Run anyway" to launch the app.

System Requirements
-------------------
- Windows 10/11 (64-bit)
- No additional software required (self-contained)

Features
--------
- Play music files (MP3, FLAC, WAV, etc.)
- Create and manage playlists
- Search your music library
- Support for multiple music directories

Support
-------
For issues or questions, please visit:
https://github.com/yourusername/musicplayer

Copyright © 2025. All rights reserved.
EOF

# Create a simple batch launcher
echo "📝 Creating launcher script..."
cat > "$OUTPUT_DIR/Launch NoPlayer.bat" << 'EOF'
@echo off
start "" "%~dp0NoPlayer.exe"
EOF

# Create ZIP archive
echo "📦 Creating ZIP archive..."
cd "$OUTPUT_DIR"
zip -r "../${ARCHIVE_NAME}.zip" ./*
cd ..

echo ""
echo "✅ Build complete!"
echo ""
echo "📦 Windows package created: $ARCHIVE_PATH"
echo "📂 Standalone folder: $OUTPUT_DIR"
echo ""
echo "📤 To distribute:"
echo "   Share $ARCHIVE_PATH with Windows users"
echo ""
echo "💡 Tip: For a proper installer, consider using tools like:"
echo "   - Inno Setup (free)"
echo "   - WiX Toolset (free)"
echo "   - Advanced Installer"
echo ""


