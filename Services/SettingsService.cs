using System;
using System.IO;
using MusicPlayer.Models;
using Newtonsoft.Json;

namespace MusicPlayer.Services;

public class SettingsService
{
    private AppSettings? _settings;

    public AppSettings Settings => _settings ??= Load();

    public AppSettings Load()
    {
        try
        {
            if (System.IO.File.Exists(AppSettings.SettingsFilePath))
            {
                var json = System.IO.File.ReadAllText(AppSettings.SettingsFilePath);
                var settings = JsonConvert.DeserializeObject<AppSettings>(json);
                return settings ?? new AppSettings();
            }
        }
        catch
        {
            // Fall through to return default settings
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(AppSettings.SettingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonConvert.SerializeObject(Settings, Formatting.Indented);
            System.IO.File.WriteAllText(AppSettings.SettingsFilePath, json);
        }
        catch
        {
            // Silently fail on save errors
        }
    }

    public void UpdateMusicFolder(string path)
    {
        Settings.MusicFolderPath = path;
        Save();
    }

    public void UpdateVolume(double volume)
    {
        Settings.Volume = volume;
        Save();
    }

    public void UpdateShuffle(bool enabled)
    {
        Settings.ShuffleEnabled = enabled;
        Save();
    }

    public void UpdateRepeat(bool enabled)
    {
        Settings.RepeatEnabled = enabled;
        Save();
    }

    public void UpdateMusicDirectories(System.Collections.Generic.List<string> directories)
    {
        Settings.MusicDirectories = directories;
        Save();
    }

    public void UpdateCustomPlaylists(System.Collections.Generic.List<SavedPlaylist> playlists)
    {
        Settings.CustomPlaylists = playlists;
        Save();
    }
}
