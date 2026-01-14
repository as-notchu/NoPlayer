using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicPlayer.Models;

public partial class Playlist : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public ObservableCollection<Track> Tracks { get; set; } = new();
    public bool IsDirectoryPlaylist { get; set; } // True if auto-created from a directory
    public string? DirectoryPath { get; set; } // Path if it's a directory playlist

    [ObservableProperty]
    private bool _isSelected;

    // HashSet for O(1) track existence checks to avoid O(n) .Any() calls
    private readonly HashSet<string> _trackFilePaths = new();

    // Check if a track with the given file path exists in this playlist
    public bool ContainsTrack(string filePath) => _trackFilePaths.Contains(filePath);

    // Add a track to the playlist (returns true if added, false if already exists)
    public bool AddTrack(Track track)
    {
        if (_trackFilePaths.Add(track.FilePath))
        {
            Tracks.Add(track);
            return true;
        }
        return false;
    }

    // Remove a track from the playlist
    public bool RemoveTrack(Track track)
    {
        if (_trackFilePaths.Remove(track.FilePath))
        {
            return Tracks.Remove(track);
        }
        return false;
    }

    // Sync the HashSet when tracks are added directly to the ObservableCollection
    public void RebuildTrackIndex()
    {
        _trackFilePaths.Clear();
        foreach (var track in Tracks)
        {
            _trackFilePaths.Add(track.FilePath);
        }
    }
}
