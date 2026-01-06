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
- Download playlist from youtube

---
## Screenshots
<img width="1280" height="945" alt="image" src="https://github.com/user-attachments/assets/c5bf6a89-4a39-4fb6-9a04-46a42abcb17e" />
<img width="1280" height="1215" alt="image" src="https://github.com/user-attachments/assets/aa56fd08-5e68-4d35-8019-da8a81870ce8" />


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
