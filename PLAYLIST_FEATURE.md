# Playlist Feature Guide

## Overview

The playlist feature allows you to organize music from multiple directories and create custom playlists.

## Features

### 1. **Multiple Music Directories**

You can now add music from multiple folders:

- **Example Setup**:
  ```
  /Users/music/calm/
  /Users/music/energetic/
  /Users/music/classical/
  ```

- Each directory automatically creates its own playlist
- All tracks from all directories appear in the main Library view

### 2. **Auto-Created Directory Playlists**

When you add a music directory, the app automatically creates a playlist with the directory's name:

- **Calm** playlist (from `/Users/music/calm/`)
- **Energetic** playlist (from `/Users/music/energetic/`)
- **Classical** playlist (from `/Users/music/classical/`)

These playlists **cannot be deleted** as they represent actual directories.

### 3. **Custom Playlists**

Create your own playlists by selecting tracks:

1. **Select tracks**: Click the checkboxes next to tracks you want
2. **Click "Create Playlist"** button
3. **Enter a name** for your playlist
4. **Done!** Your custom playlist appears in the Playlists panel

Custom playlists can be deleted using the **×** button.

## How to Use

### Adding Multiple Music Directories

Currently, directories are added through the settings file. Here's how:

1. **Find your settings file**:
   - macOS: `~/Library/Application Support/MusicPlayer/settings.json`
   - Windows: `%APPDATA%\MusicPlayer\settings.json`
   - Linux: `~/.config/MusicPlayer/settings.json`

2. **Edit the JSON file**:
   ```json
   {
     "MusicFolderPath": "/Users/yourname/Music",
     "MusicDirectories": [
       "/Users/yourname/Music/calm",
       "/Users/yourname/Music/energetic",
       "/Users/yourname/Music/workout"
     ],
     "Volume": 1.0,
     "ShuffleEnabled": false,
     "RepeatEnabled": false,
     "CustomPlaylists": []
   }
   ```

3. **Restart the app** or click **Refresh**

### Creating a Custom Playlist

1. **Browse your library** in the left panel
2. **Check the boxes** next to tracks you want in your playlist
3. Click the **"Create Playlist"** button at the top
4. **Enter a name** (e.g., "My Favorites", "Road Trip", "Study Music")
5. Click **Create**

Your custom playlist now appears in the Playlists panel on the right!

### Adding Tracks to an Existing Playlist

1. **Select tracks** by checking the boxes next to them
2. Click the **"Add to Playlist"** button
3. **Choose a playlist** from the dialog (only custom playlists shown)
4. Click **Add**

Tracks are added to the selected playlist (duplicates are automatically prevented).

### Removing Tracks from a Custom Playlist

1. **Click on a custom playlist** to view its tracks
2. A **remove button (−)** appears next to each track
3. **Click the − button** to remove that track from the playlist

**Note**: The remove button only appears for custom playlists. You cannot remove tracks from directory playlists (they always show all tracks from that folder).

### Using Playlists

- **Click any playlist** to view only its tracks
- **Click "Library"** (or refresh) to see all tracks again
- **Double-click a track** to play it
- **Delete custom playlists** with the × button (directory playlists can't be deleted)

## UI Layout

```
┌─────────────────────────────────────────────────────┐
│  Music Folder Settings (Browse/Refresh)            │
├──────────────────────────┬──────────────────────────┤
│                          │                          │
│  Library (60%)           │  Playlists (40%)        │
│                          │                          │
│  ☐ Track 1          −    │  📁 Calm (15 tracks)    │
│  ☐ Track 2          −    │  📁 Energetic (23 tracks)│
│  ☐ Track 3          −    │  🎵 Favorites (12) ×    │
│  ...                     │  🎵 Workout (8) ×       │
│                          │                          │
│  [Create] [Add to...]    │  (− button only shows   │
│                          │   for custom playlists) │
└──────────────────────────┴──────────────────────────┘
│           Now Playing Controls                      │
└─────────────────────────────────────────────────────┘
```

## Playlist Types

| Icon | Type | Can Delete? | Source |
|------|------|-------------|--------|
| 📁 | Directory | No | Auto-created from folder |
| 🎵 | Custom | Yes | User-created from selection |

## Tips

1. **Organize by mood**: Create directories like `calm`, `energetic`, `focus`
2. **Mix and match**: Select tracks from different directory playlists to create custom ones
3. **Quick access**: Click a playlist to instantly filter to just those tracks
4. **Persistent**: Custom playlists are saved and will be there next time you launch the app

## Technical Details

- Playlists are stored in `settings.json`
- Directory playlists refresh when you click Refresh
- Custom playlists save track file paths
- Selection state is cleared after creating a playlist
- All tracks remain in the main Library view regardless of playlist filtering

## Future Enhancements

Possible features for future updates:
- UI button to add directories (instead of editing JSON)
- Drag and drop to add tracks to playlists
- Playlist reordering
- Export/import playlists
- Smart playlists based on metadata
