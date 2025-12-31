# Building a macOS App Bundle

This guide shows you how to package your Music Player as a native macOS application (.app) that you can install in `/Applications` and launch like any other Mac app.

## Prerequisites

- macOS 11.0 or later
- .NET 10 SDK installed
- Your project compiles successfully with `dotnet build`

## Option 1: Apple Silicon Only (Faster Build)

If you're only using this on Apple Silicon Macs (M1, M2, M3, etc.):

```bash
./build-macos-app.sh
```

This creates `MusicPlayer.app` optimized for Apple Silicon.

## Option 2: Universal Binary (Intel + Apple Silicon)

To create an app that works on both Intel and Apple Silicon Macs:

```bash
./build-macos-app-universal.sh
```

This takes longer but creates a universal binary that runs on all modern Macs.

## Installing the App

After building, you'll have a `MusicPlayer.app` in your project directory.

### Install to Applications folder:

```bash
mv MusicPlayer.app /Applications/
```

### Launch the app:

1. **From Finder**: Go to Applications folder and double-click MusicPlayer
2. **From Spotlight**: Press Cmd+Space, type "MusicPlayer", press Enter
3. **From Dock**: Drag the app to your Dock for quick access

## First Launch

On first launch, macOS may show security warnings because the app isn't signed. Here's how to open it:

1. **Right-click** (or Control+click) on MusicPlayer.app
2. Select **"Open"** from the menu
3. Click **"Open"** in the dialog that appears
4. The app will launch and remember this choice

### Grant Accessibility Permissions

For media keys to work:

1. Go to **System Settings → Privacy & Security → Accessibility**
2. Find **MusicPlayer** in the list
3. Toggle it **ON**
4. Restart the app

## What Gets Packaged

The app bundle includes:
- All .NET runtime files (self-contained)
- All application DLLs and dependencies
- BASS audio libraries for macOS
- Settings and configuration

The app is completely standalone - no need to install .NET separately!

## Troubleshooting

### "App is damaged and can't be opened"

This happens with unsigned apps. Fix it:

```bash
xattr -cr /Applications/MusicPlayer.app
```

Then try opening again.

### Media keys don't work

Make sure you've granted Accessibility permissions (see above).

### App won't launch

Check the console for errors:

```bash
/Applications/MusicPlayer.app/Contents/MacOS/MusicPlayer
```

## App Structure

```
MusicPlayer.app/
├── Contents/
│   ├── Info.plist          # App metadata
│   ├── MacOS/              # Executables and libraries
│   │   ├── MusicPlayer     # Main executable
│   │   ├── *.dll           # .NET libraries
│   │   └── *.dylib         # Native libraries (BASS, etc.)
│   └── Resources/          # App resources (future: icons, etc.)
```

## Customization

### Change App Name

Edit either build script and change:
```bash
APP_NAME="YourAppName"
```

### Add an Icon

1. Create an icon file: `AppIcon.icns`
2. Copy it to: `MusicPlayer.app/Contents/Resources/`
3. The app will use it automatically

### Change Bundle Identifier

Edit the build script:
```bash
IDENTIFIER="com.yourname.musicplayer"
```

## Uninstalling

```bash
rm -rf /Applications/MusicPlayer.app
rm -rf ~/Library/Application\ Support/MusicPlayer  # Remove settings
```

## Distribution

To share your app with others:

1. Build the universal version (works on all Macs)
2. Compress it: `zip -r MusicPlayer.zip MusicPlayer.app`
3. Share the .zip file
4. Recipients: unzip and move to `/Applications`

**Note**: For wider distribution, consider code signing with an Apple Developer account.

## Building for Different Versions

### For older macOS (10.15+):

Change `LSMinimumSystemVersion` in the build script:
```xml
<key>LSMinimumSystemVersion</key>
<string>10.15</string>
```

### For Intel only:

```bash
dotnet publish -c Release -r osx-x64 --self-contained true
```

Then follow the same app bundle creation steps.
