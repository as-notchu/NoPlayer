#!/bin/bash

# Build macOS App Bundle for Music Player
# This script creates a standalone .app that you can move to /Applications

set -e

APP_NAME="MusicPlayer"
APP_BUNDLE="${APP_NAME}.app"
BUILD_DIR="bin/Release/net10.0/osx-arm64/publish"
IDENTIFIER="com.musicplayer.app"
VERSION="1.0.0"

echo "Building Music Player for macOS..."

# Clean previous builds
echo "Cleaning previous builds..."
rm -rf "$APP_BUNDLE"
rm -rf bin/Release

# Build the app in Release mode for macOS ARM64 (Apple Silicon)
echo "Building .NET application..."
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=false -p:UseAppHost=true

# Create app bundle structure
echo "Creating app bundle structure..."
mkdir -p "$APP_BUNDLE/Contents/MacOS"
mkdir -p "$APP_BUNDLE/Contents/Resources"
mkdir -p "$APP_BUNDLE/Contents/Frameworks"

# Copy all files from publish directory
echo "Copying application files..."
cp -r "$BUILD_DIR"/* "$APP_BUNDLE/Contents/MacOS/"

# Create Info.plist
echo "Creating Info.plist..."
cat > "$APP_BUNDLE/Contents/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleDisplayName</key>
    <string>${APP_NAME}</string>
    <key>CFBundleExecutable</key>
    <string>MusicPlayer</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>CFBundleIdentifier</key>
    <string>${IDENTIFIER}</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>${APP_NAME}</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>${VERSION}</string>
    <key>CFBundleVersion</key>
    <string>1</string>
    <key>LSMinimumSystemVersion</key>
    <string>11.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSHumanReadableCopyright</key>
    <string>Copyright © 2025. All rights reserved.</string>
</dict>
</plist>
EOF

# Make the executable... executable
chmod +x "$APP_BUNDLE/Contents/MacOS/MusicPlayer"

echo ""
echo "✅ Build complete!"
echo ""
echo "Your app bundle is ready: $APP_BUNDLE"
echo ""
echo "To install:"
echo "1. Move to Applications: mv $APP_BUNDLE /Applications/"
echo "2. Double-click to launch from Finder or Spotlight"
echo ""
echo "Note: First launch may require:"
echo "- Right-click → Open (to bypass Gatekeeper)"
echo "- Grant Accessibility permissions in System Settings"
echo ""
