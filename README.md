# 🎵 Music Player

A modern, cross-platform music player built with .NET and Avalonia.

**Download:** [Latest Release](https://github.com/as-notchu/Music-Player/releases)

---

## ✨ Features

- 🎧 Play music files (MP3, FLAC, WAV, and more)
- 📝 Create and manage playlists
- 🔍 Search your music library
- 📁 Support for multiple music directories
- 🎨 Clean, intuitive interface
- ⚡ Fast and lightweight

---

## 📥 Installation

### For Users

**macOS:**
1. Download `MusicPlayer-x.x.x-macOS.dmg` from [Releases](https://github.com/as-notchu/Music-Player/releases)
2. Open the DMG and drag MusicPlayer to Applications
3. Launch from Spotlight or Applications
4. On first launch: Right-click → Open (to bypass Gatekeeper)

**Windows:**
1. Download `MusicPlayer-x.x.x-Windows-x64.zip` from [Releases](https://github.com/as-notchu/Music-Player/releases)
2. Extract the ZIP file
3. Run `MusicPlayer.exe`
4. If Windows shows a warning: Click "More info" → "Run anyway"

---

## 🛠️ For Developers

### Quick Start

**Prerequisites:**
- .NET 10 SDK
- macOS 11.0+ (for macOS builds) or Windows 10+ (for Windows builds)

**Build from source:**
```bash
git clone https://github.com/as-notchu/Music-Player.git
cd Music-Player
dotnet run
```

### Publishing

Create distributable packages:

**macOS DMG:**
```bash
./publish-macos-dmg.sh 1.0.0
```

**Windows ZIP:**
```bash
./publish-windows.sh 1.0.0
# Or on Windows:
.\publish-windows.ps1 1.0.0
```

See [PUBLISHING.md](PUBLISHING.md) for detailed instructions.

---

## 🤝 Contributing

Contributions are welcome! Feel free to open issues or submit pull requests.

---

## 📄 License

[MIT License](LICENSE)

---

**Created by [@as-notchu](https://github.com/as-notchu)**
