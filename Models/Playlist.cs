using System.Collections.ObjectModel;

namespace MusicPlayer.Models;

public class Playlist
{
    public string Name { get; set; } = string.Empty;
    public ObservableCollection<Track> Tracks { get; set; } = new();
    public bool IsDirectoryPlaylist { get; set; } // True if auto-created from a directory
    public string? DirectoryPath { get; set; } // Path if it's a directory playlist
}
