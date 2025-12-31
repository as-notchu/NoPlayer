using System;
using System.IO;

namespace MusicPlayer.Models;

public class AppSettings
{
    public string MusicFolderPath { get; set; } = GetDefaultMusicFolder();
    public double Volume { get; set; } = 1.0;
    public bool ShuffleEnabled { get; set; }
    public bool RepeatEnabled { get; set; }

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
