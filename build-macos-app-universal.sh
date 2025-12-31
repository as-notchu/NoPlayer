#!/bin/bash

# Build Universal macOS App Bundle for Music Player
# This creates an app that works on both Intel and Apple Silicon Macs

set -e

APP_NAME="MusicPlayer"
APP_BUNDLE="${APP_NAME}.app"
IDENTIFIER="com.musicplayer.app"
VERSION="1.0.0"

echo "Building Universal Music Player for macOS (Intel + Apple Silicon)..."

# Clean previous builds
echo "Cleaning previous builds..."
rm -rf "$APP_BUNDLE"
rm -rf bin/Release

# Build for both architectures
echo "Building for Apple Silicon (ARM64)..."
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=false -p:UseAppHost=true

echo "Building for Intel (x64)..."
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=false -p:UseAppHost=true

# Create app bundle structure
echo "Creating app bundle structure..."
mkdir -p "$APP_BUNDLE/Contents/MacOS"
mkdir -p "$APP_BUNDLE/Contents/Resources"

# Copy ARM64 build as base
echo "Copying ARM64 build..."
cp -r "bin/Release/net10.0/osx-arm64/publish"/* "$APP_BUNDLE/Contents/MacOS/"

# Create universal binaries using lipo
echo "Creating universal binaries..."
ARM64_DIR="bin/Release/net10.0/osx-arm64/publish"
X64_DIR="bin/Release/net10.0/osx-x64/publish"

# Create universal binary for main executable
if [ -f "$ARM64_DIR/MusicPlayer" ] && [ -f "$X64_DIR/MusicPlayer" ]; then
    lipo -create "$ARM64_DIR/MusicPlayer" "$X64_DIR/MusicPlayer" \
         -output "$APP_BUNDLE/Contents/MacOS/MusicPlayer"
    echo "✓ Created universal MusicPlayer executable"
fi

# Find and create universal binaries for native libraries
for arm_lib in "$ARM64_DIR"/*.dylib; do
    if [ -f "$arm_lib" ]; then
        lib_name=$(basename "$arm_lib")
        x64_lib="$X64_DIR/$lib_name"

        if [ -f "$x64_lib" ]; then
            # Both architectures exist, create universal binary
            lipo -create "$arm_lib" "$x64_lib" \
                 -output "$APP_BUNDLE/Contents/MacOS/$lib_name"
            echo "✓ Created universal $lib_name"
        else
            # Only ARM64 version exists, copy it
            cp "$arm_lib" "$APP_BUNDLE/Contents/MacOS/"
            echo "⚠ Copied ARM64-only $lib_name"
        fi
    fi
done

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

# Verify universal binaries were created
echo ""
echo "Verifying universal binaries..."
file "$APP_BUNDLE/Contents/MacOS/MusicPlayer"

echo ""
echo "✅ Universal build complete!"
echo ""
echo "Your app bundle is ready: $APP_BUNDLE"
echo "This app will run on both Intel and Apple Silicon Macs"
echo ""
echo "To install:"
echo "1. Move to Applications: mv $APP_BUNDLE /Applications/"
echo "2. Double-click to launch from Finder or Spotlight"
echo ""
echo "Note: First launch may require:"
echo "- Right-click → Open (to bypass Gatekeeper)"
echo "- Grant Accessibility permissions in System Settings"
echo ""
