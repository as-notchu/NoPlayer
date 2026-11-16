using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TagLib;
using YoutubeExplode.Videos;

namespace MusicPlayer.Services.YouTube;

/// <summary>
/// Downloads a video's thumbnail and stores it as the front cover inside the downloaded audio file,
/// so the song shows its picture in NoPlayer and in any other player or file manager.
/// </summary>
internal static class CoverArtEmbedder
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    /// <summary>
    /// Returns false when no thumbnail could be fetched or written. A missing cover never fails the download,
    /// only cancellation is passed on.
    /// </summary>
    public static async Task<bool> TryEmbedAsync(IVideo video, string audioFilePath, CancellationToken cancellationToken)
    {
        try
        {
            var image = await DownloadBestThumbnailAsync(video, cancellationToken);
            if (image == null)
                return false;

            // TagLib is synchronous file IO - keep it off the UI thread
            await Task.Run(() =>
            {
                using var file = TagLib.File.Create(audioFilePath);
                file.Tag.Pictures = new IPicture[]
                {
                    new Picture(new ByteVector(image.Value.Data))
                    {
                        Type = PictureType.FrontCover,
                        MimeType = image.Value.MimeType,
                        Description = "Cover"
                    }
                };
                file.Save();
            }, cancellationToken);

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<(byte[] Data, string MimeType)?> DownloadBestThumbnailAsync(IVideo video, CancellationToken cancellationToken)
    {
        (byte[] Data, string MimeType)? webpFallback = null;

        foreach (var url in ThumbnailCandidates(video))
        {
            try
            {
                using var response = await Http.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    continue; // maxresdefault does not exist for every video

                var data = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                var mimeType = DetectImageMimeType(data);

                if (mimeType == "image/jpeg" || mimeType == "image/png")
                    return (data, mimeType);

                // WebP covers are not shown by every player, so only use one when nothing else is available
                if (mimeType == "image/webp")
                    webpFallback ??= (data, mimeType);
            }
            catch (HttpRequestException)
            {
                // Try the next size
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // HttpClient timeout - try the next size
            }
        }

        return webpFallback;
    }

    /// <summary>
    /// Biggest thumbnail first. maxresdefault is 16:9 without black bars, the smaller 4:3 ones are letterboxed.
    /// The well-known i.ytimg.com URLs are appended in case YouTube returned no thumbnail list at all.
    /// </summary>
    private static IEnumerable<string> ThumbnailCandidates(IVideo video)
    {
        var fromMetadata = video.Thumbnails
            .OrderByDescending(t => t.Resolution.Area)
            .Select(t => t.Url);

        var wellKnown = new[] { "maxresdefault", "sddefault", "hqdefault" }
            .Select(name => $"https://i.ytimg.com/vi/{video.Id}/{name}.jpg");

        return fromMetadata.Concat(wellKnown).Distinct(StringComparer.Ordinal);
    }

    private static string? DetectImageMimeType(byte[] data)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return "image/jpeg";

        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return "image/png";

        if (data.Length >= 12 &&
            data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F' &&
            data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P')
            return "image/webp";

        return null; // not an image (e.g. an HTML error page)
    }
}
