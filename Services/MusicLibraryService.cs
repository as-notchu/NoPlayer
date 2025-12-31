using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MusicPlayer.Models;
using TagLib;

namespace MusicPlayer.Services;

public class MusicLibraryService
{
    private static readonly string[] SupportedExtensions =
    {
        ".mp3", ".wav", ".flac", ".webm", ".ogg", ".m4a", ".aac", ".wma", ".opus"
    };

    public async Task<List<Track>> ScanFolderAsync(string folderPath)
    {
        var tracks = new List<Track>();

        if (!Directory.Exists(folderPath))
            return tracks;

        await Task.Run(() =>
        {
            var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

            foreach (var file in files)
            {
                try
                {
                    var track = LoadTrackMetadata(file);
                    tracks.Add(track);
                }
                catch
                {
                    // If metadata fails, add with basic info
                    tracks.Add(new Track
                    {
                        FilePath = file,
                        Title = Path.GetFileNameWithoutExtension(file)
                    });
                }
            }
        });

        return tracks.OrderBy(t => t.Artist)
                     .ThenBy(t => t.Album)
                     .ThenBy(t => t.Title)
                     .ToList();
    }

    private Track LoadTrackMetadata(string filePath)
    {
        using var tagFile = TagLib.File.Create(filePath);

        return new Track
        {
            FilePath = filePath,
            Title = tagFile.Tag.Title ?? Path.GetFileNameWithoutExtension(filePath),
            Artist = tagFile.Tag.FirstPerformer ?? string.Empty,
            Album = tagFile.Tag.Album ?? string.Empty,
            Duration = tagFile.Properties.Duration,
            Year = tagFile.Tag.Year,
            Genre = tagFile.Tag.FirstGenre ?? string.Empty
        };
    }

    public static bool IsSupportedFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }
}
