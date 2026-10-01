# 🎵 Music Player

A modern, cross-platform music player built with .NET and Avalonia.

> **Note on YouTube downloads:** they rely on [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode), and YouTube changes its download protection regularly. If downloads start failing with a `403 Forbidden` error, updating the `YoutubeExplode` package in `MusicPlayer.csproj` to the latest version is usually the fix.

**Download:** [Latest Release](https://github.com/as-notchu/Music-Player/releases)

---

## ✨ Features

- 🎧 Play music files (MP3, FLAC, WAV, and more)
- 📝 Create and manage playlists
- 🔍 Search your music library
- 📁 Support for multiple music directories
- 🔀 Shuffle that deals a random queue of the current list, with an "Up next" hint
- ⏩ Fast-forward / rewind 10 seconds, plus click-and-drag scrubbing on the progress bar
- ⌨️ Keyboard transport controls (see below)
- 🎨 Dark neon interface with a sidebar for playlists and sources
- ⚡ Fast and lightweight
- 📥 Download music from YouTube playlists

### Keyboard shortcuts

| Key | Action |
| --- | --- |
| `Space` | Play / pause |
| `←` / `→` | Rewind / fast-forward 10 seconds |
| `Ctrl+←` / `Ctrl+→` (`⌘` on macOS) | Previous / next track |

Shortcuts are ignored while you are typing in a text box.

### How shuffle works

Turning shuffle on deals a random queue from whatever you are looking at (a playlist or the whole library), starting with the song that is already playing. **Next** and **Previous** simply walk forwards and backwards through that queue, so going back always returns to the song you just heard. Picking a song from the list jumps to its spot in the queue. When the queue runs out, playback stops, or a fresh queue is dealt if **Repeat** is on. Searching never reshuffles the queue.

---

## 📥 Downloading Music from YouTube Playlists

This feature allows you to download entire YouTube playlists directly into your music player.

### How to Use:

1. **Open the Download Window**
   - Click the **YouTube** button under **Sources** in the sidebar
   - A new window will appear with the download options

2. **Enter Playlist URL**
   - In the first text box, paste the link to your YouTube playlist
   - **Note:** The playlist must be set to public for this feature to work

3. **Choose Save Location**
   - In the second text box, specify the directory where you want the downloaded music files to be saved
   - You can type the path manually or use the browse button (if available)

4. **Start the Download**
   - Click the "Start Download" button
   - The download process will begin, and you may see progress indicators
   - Wait for the download to complete

5. **Add to Your Music Player**
   - Once the download finishes, a "Copy Path" button will appear
   - Click "Copy Path" to copy the save directory location to your clipboard
   - Return to the main player window
   - Paste the copied path into the "Paste folder paths" box under **Sources** in the sidebar and click **+**
   - Your downloaded playlist will now be available in your music library

### Tips:
- Ensure you have a stable internet connection during the download
- Make sure you have sufficient disk space in your chosen save directory
- Only public YouTube playlists can be downloaded

---

## Screenshots
<img width="600" height="490" alt="image" src="https://github.com/user-attachments/assets/c5bf6a89-4a39-4fb6-9a04-46a42abcb17e" />
<img width="600" height="600" alt="image" src="https://github.com/user-attachments/assets/aa56fd08-5e68-4d35-8019-da8a81870ce8" />


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

---

## 🤝 Contributing

Contributions are welcome! Feel free to open issues or submit pull requests.

---

## 📄 License

[MIT License](LICENSE)

---

**Created by [@as-notchu](https://github.com/as-notchu)**
