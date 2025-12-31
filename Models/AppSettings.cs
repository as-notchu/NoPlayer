using System;
using System.Collections.Generic;
using System.IO;

namespace MusicPlayer.Models;

public class AppSettings
{
    public string MusicFolderPath { get; set; } = GetDefaultMusicFolder();
    public List<string> MusicDirectories { get; set; } = new();
    public double Volume { get; set; } = 1.0;
    public bool ShuffleEnabled { get; set; }
    public bool RepeatEnabled { get; set; }
    public List<SavedPlaylist> CustomPlaylists { get; set; } = new();

    private static string GetDefaultMusicFolder()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
    }

    public static string SettingsFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MusicPlayer",
            "settings.json"
        );
}
