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

    // Cache display properties to avoid recomputation
    private string? _displayName;
    private string? _displayArtist;
    private string? _displayAlbum;

    // Cache lowercase versions for efficient searching
    private string? _displayNameLower;
    private string? _displayArtistLower;
    private string? _displayAlbumLower;

    public string DisplayName
    {
        get
        {
            if (_displayName == null)
            {
                _displayName = string.IsNullOrEmpty(Title)
                    ? System.IO.Path.GetFileNameWithoutExtension(FilePath)
                    : Title;
            }
            return _displayName;
        }
    }

    public string DisplayArtist
    {
        get
        {
            if (_displayArtist == null)
            {
                _displayArtist = string.IsNullOrEmpty(Artist) ? "Unknown Artist" : Artist;
            }
            return _displayArtist;
        }
    }

    public string DisplayAlbum
    {
        get
        {
            if (_displayAlbum == null)
            {
                _displayAlbum = string.IsNullOrEmpty(Album) ? "Unknown Album" : Album;
            }
            return _displayAlbum;
        }
    }

    // Lowercase versions for efficient searching without repeated allocations
    public string DisplayNameLower
    {
        get
        {
            if (_displayNameLower == null)
            {
                _displayNameLower = DisplayName.ToLower();
            }
            return _displayNameLower;
        }
    }

    public string DisplayArtistLower
    {
        get
        {
            if (_displayArtistLower == null)
            {
                _displayArtistLower = DisplayArtist.ToLower();
            }
            return _displayArtistLower;
        }
    }

    public string DisplayAlbumLower
    {
        get
        {
            if (_displayAlbumLower == null)
            {
                _displayAlbumLower = DisplayAlbum.ToLower();
            }
            return _displayAlbumLower;
        }
    }

    public string DurationFormatted => Duration.TotalHours >= 1
        ? Duration.ToString(@"h\:mm\:ss")
        : Duration.ToString(@"m\:ss");
}
