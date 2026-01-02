# Windows BASS Library Setup

## Problem
The Music Player app suspends/crashes on Windows because it's missing the native BASS audio library DLL files.

## Solution
You need to download and add the Windows BASS libraries to your project.

## Steps to Fix

### 1. Download BASS Libraries

Download these files from the official BASS website (https://www.un4seen.com/):

**Main BASS library:**
- Go to: https://www.un4seen.com/bass.html
- Download: "BASS 2.4 - Windows" (bass24.zip)
- Extract and get: `bass.dll` (64-bit version from x64 folder)

**BASS FLAC plugin (for FLAC support):**
- Go to: https://www.un4seen.com/bass.html#addons
- Download: "BASSFLAC 2.4" (bassflac24.zip)
- Extract and get: `bassflac.dll` (64-bit version from x64 folder)

**BASS WEBM plugin (for WEBM/OGG support):**
- Go to: https://www.un4seen.com/bass.html#addons
- Download: "BASSWEBM" (basswebm.zip)
- Extract and get: `basswebm.dll` (64-bit version from x64 folder)

**BASS MIX plugin (for mixing):**
- Go to: https://www.un4seen.com/bass.html#addons
- Download: "BASSMIX 2.4" (bassmix24.zip)
- Extract and get: `bassmix.dll` (64-bit version from x64 folder)

### 2. Create Directory Structure

In your project root, create:
```
runtimes/
  win-x64/
    native/
      (place DLL files here)
```

### 3. Add the DLL Files

Copy these files to `runtimes/win-x64/native/`:
- `bass.dll`
- `bassflac.dll`
- `basswebm.dll`
- `bassmix.dll`

### 4. Verify Structure

Your project should now have:
```
runtimes/
  osx-x64/
    native/
      libbass.dylib
      libbassflac.dylib
      libbasswebm.dylib
      libbassmix.dylib
  win-x64/
    native/
      bass.dll
      bassflac.dll
      basswebm.dll
      bassmix.dll
```

### 5. Rebuild for Windows

Now run your Windows publish script:
```powershell
.\publish-windows.ps1 1.0.0
```

The DLL files will be automatically included in the build output.

## What Was Fixed

1. **Platform-specific plugin loading**: Updated `AudioPlayerService.cs` to detect Windows vs macOS and load the correct library files (.dll vs .dylib)

2. **Project configuration**: Updated `MusicPlayer.csproj` to include Windows native libraries in the build

3. **Documentation**: Created this guide to help you set up the required libraries

## License Note

BASS is free for non-commercial use. If you plan to distribute this commercially, you'll need to purchase a license from un4seen.com.

## Testing

After adding the libraries and rebuilding:
1. Extract the generated ZIP file on a Windows machine
2. Run `MusicPlayer.exe`
3. The app should now start without being suspended by Task Manager

## Common Issues

**"Failed to initialize BASS" error:**
- Make sure `bass.dll` is in the same directory as `MusicPlayer.exe`
- Verify you're using the 64-bit version of the DLL

**"Missing DLL" error:**
- Check that all required DLLs are present
- Ensure the DLLs match the platform architecture (x64)

**Still crashes:**
- Check Windows Event Viewer for detailed error messages
- Ensure you have Visual C++ Redistributable installed
