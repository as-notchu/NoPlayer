using System.Collections.Generic;

namespace MusicPlayer.Models;

public class SavedPlaylist
{
    public string Name { get; set; } = string.Empty;
    public List<string> TrackPaths { get; set; } = new();
}
