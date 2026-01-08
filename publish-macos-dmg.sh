#!/bin/bash

# Build macOS DMG installer for Music Player
# Creates a distributable .dmg file with drag-to-install interface
# Usage: ./publish-macos-dmg.sh <version>
# Example: ./publish-macos-dmg.sh 1.0.0

set -e

# Check if version argument is provided
if [ -z "$1" ]; then
  echo "❌ Error: Version number required"
  echo ""
  echo "Usage: ./publish-macos-dmg.sh <version>"
  echo "Example: ./publish-macos-dmg.sh 1.0.0"
  echo "         ./publish-macos-dmg.sh 0.0.2"
  exit 1
fi

APP_NAME="NoPlayer"
BUILD_ROOT="builds/macos"
APP_BUNDLE="${BUILD_ROOT}/${APP_NAME}.app"
VERSION="$1"
DMG_NAME="${APP_NAME}-${VERSION}-macOS"
DMG_PATH="${BUILD_ROOT}/${DMG_NAME}.dmg"
BUILD_DIR="bin/Release/net10.0/osx-arm64/publish"
IDENTIFIER="com.noplayer.app"
TEMP_DMG="temp.dmg"
ICON_FILE="Assets/ic.icns"

echo "🎵 Building NoPlayer v${VERSION} DMG for macOS..."
echo ""

# Clean previous builds
echo "🧹 Cleaning previous builds..."
rm -rf "$APP_BUNDLE"
rm -rf "$DMG_PATH"
rm -rf bin/Release
rm -rf obj/Release

# Ensure output directory exists
mkdir -p "$BUILD_ROOT"

# Build the app in Release mode for macOS ARM64 (Apple Silicon)
echo "🔨 Building .NET application for Apple Silicon..."
dotnet publish -c Release -r osx-arm64 --self-contained true \
  -p:PublishSingleFile=false \
  -p:UseAppHost=true \
  -p:PublishTrimmed=false \
  -p:DebugType=None \
  -p:DebugSymbols=false \
  -p:Version="${VERSION}"

# Create app bundle structure
echo "📦 Creating app bundle structure..."
mkdir -p "$APP_BUNDLE/Contents/MacOS"
mkdir -p "$APP_BUNDLE/Contents/Resources"

# Copy all files from publish directory
echo "📋 Copying application files..."
cp -r "$BUILD_DIR"/* "$APP_BUNDLE/Contents/MacOS/"

# Copy icon if it exists
if [ -f "$ICON_FILE" ]; then
  echo "🎨 Adding app icon..."
  cp "$ICON_FILE" "$APP_BUNDLE/Contents/Resources/AppIcon.icns"
fi

# Create Info.plist
echo "📝 Creating Info.plist..."
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
    <string>NoPlayer</string>
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
chmod +x "$APP_BUNDLE/Contents/MacOS/NoPlayer"

echo "💿 Creating DMG installer..."

# Create a temporary directory for DMG contents
DMG_TEMP_DIR="dmg_temp"
rm -rf "$DMG_TEMP_DIR"
mkdir -p "$DMG_TEMP_DIR"

# Copy app bundle to temp directory
cp -r "$APP_BUNDLE" "$DMG_TEMP_DIR/"

# Create a symbolic link to /Applications
ln -s /Applications "$DMG_TEMP_DIR/Applications"

# Create the DMG
hdiutil create -volname "$APP_NAME" \
  -srcfolder "$DMG_TEMP_DIR" \
  -ov -format UDZO \
  "$DMG_PATH"

# Clean up
rm -rf "$DMG_TEMP_DIR"

echo ""
echo "✅ Build complete!"
echo ""
echo "📦 DMG installer created: $DMG_PATH"
echo "📱 App bundle available: $APP_BUNDLE"
echo ""
echo "📤 To distribute:"
echo "   Share $DMG_PATH with users"
echo ""
echo "🚀 To install locally:"
echo "   1. Double-click $DMG_PATH"
echo "   2. Drag ${APP_NAME} to Applications folder"
echo "   3. Launch from Applications or Spotlight"
echo ""
echo "⚠️  First launch notes:"
echo "   - Right-click → Open (to bypass Gatekeeper)"
echo "   - Or run: xattr -cr /Applications/${APP_NAME}.app"
echo ""


