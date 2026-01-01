using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicPlayer.Models;

public partial class Track : ObservableObject
{
    public string FilePath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public uint Year { get; set; }
    public string Genre { get; set; } = string.Empty;
    public string? SourceDirectory { get; set; } // Track which directory this track came from

    [ObservableProperty]
    private bool _isSelected;

    public string DisplayName => string.IsNullOrEmpty(Title)
        ? System.IO.Path.GetFileNameWithoutExtension(FilePath)
        : Title;

    public string DisplayArtist => string.IsNullOrEmpty(Artist) ? "Unknown Artist" : Artist;

    public string DisplayAlbum => string.IsNullOrEmpty(Album) ? "Unknown Album" : Album;

    public string DurationFormatted => Duration.TotalHours >= 1
        ? Duration.ToString(@"h\:mm\:ss")
        : Duration.ToString(@"m\:ss");
}
