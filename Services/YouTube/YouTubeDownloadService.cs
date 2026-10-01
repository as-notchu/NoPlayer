using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace MusicPlayer.Services.YouTube;

public class YouTubeDownloadService
{
    private readonly YoutubeClient _youtube;
    private const int DelayBetweenDownloadsMs = 10000; // 10 seconds
    private const int MaxDownloadAttempts = 2;         // fresh manifest + retry once when YouTube answers 403
    private const int RetryDelayMs = 3000;

    public event EventHandler<DownloadProgressEventArgs>? ProgressChanged;
    public event EventHandler<DownloadCompletedEventArgs>? DownloadCompleted;
    public event EventHandler<DownloadErrorEventArgs>? DownloadError;

    public YouTubeDownloadService()
    {
        _youtube = new YoutubeClient();
    }

    public async Task DownloadPlaylistAsync(string playlistUrl, string baseOutputDirectory, CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure base output directory exists
            Directory.CreateDirectory(baseOutputDirectory);

            // Extract playlist ID
            var playlistId = PlaylistId.TryParse(playlistUrl);
            if (playlistId == null)
            {
                OnDownloadError(new DownloadErrorEventArgs("Invalid playlist URL. Please provide a valid YouTube playlist URL.", null));
                return;
            }

            // Get playlist metadata
            OnProgressChanged(new DownloadProgressEventArgs("Fetching playlist information...", 0, 0, null));
            var playlist = await _youtube.Playlists.GetAsync(playlistId.Value, cancellationToken);

            // Create a subdirectory for this playlist
            var playlistDirName = SanitizeFileName(playlist.Title);
            var playlistOutputDirectory = Path.Combine(baseOutputDirectory, playlistDirName);
            Directory.CreateDirectory(playlistOutputDirectory);

            OnProgressChanged(new DownloadProgressEventArgs($"Created directory: {playlistDirName}", 0, 0, null));

            // Get all videos in the playlist
            var videos = await _youtube.Playlists.GetVideosAsync(playlistId.Value, cancellationToken).CollectAsync();

            if (videos.Count == 0)
            {
                OnDownloadError(new DownloadErrorEventArgs("Playlist is empty or not accessible", null));
                return;
            }

            OnProgressChanged(new DownloadProgressEventArgs($"Found {videos.Count} songs in playlist: {playlist.Title}", 0, videos.Count, null));

            // Download each video to the playlist directory
            for (int i = 0; i < videos.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var video = videos[i];
                try
                {
                    OnProgressChanged(new DownloadProgressEventArgs(
                        $"Downloading {i + 1}/{videos.Count}: {video.Title}",
                        i,
                        videos.Count,
                        video.Title));

                    await DownloadVideoAsync(video, playlistOutputDirectory, cancellationToken);

                    OnDownloadCompleted(new DownloadCompletedEventArgs(video.Title, i + 1, videos.Count));

                    // Apply delay between downloads (except for the last one)
                    if (i < videos.Count - 1)
                    {
                        OnProgressChanged(new DownloadProgressEventArgs(
                            $"Waiting 10 seconds before next download... ({i + 1}/{videos.Count} completed)",
                            i + 1,
                            videos.Count,
                            null));
                        await Task.Delay(DelayBetweenDownloadsMs, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    OnDownloadError(new DownloadErrorEventArgs($"Failed to download '{video.Title}': {ex.Message}", video.Title));
                    // Continue with next video
                }
            }

            OnProgressChanged(new DownloadProgressEventArgs($"All downloads completed! {videos.Count} songs downloaded to '{playlistOutputDirectory}'", videos.Count, videos.Count, null, playlistOutputDirectory));
        }
        catch (Exception ex)
        {
            OnDownloadError(new DownloadErrorEventArgs($"Playlist download failed: {ex.Message}", null));
        }
    }

    public async Task DownloadVideosAsync(string videoUrls, string outputDirectory, CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure output directory exists
            Directory.CreateDirectory(outputDirectory);

            // Parse video URLs (separated by newlines or commas)
            var urls = videoUrls
                .Split(new[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(u => u.Trim())
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .ToList();

            if (urls.Count == 0)
            {
                OnDownloadError(new DownloadErrorEventArgs("No valid video URLs provided.", null));
                return;
            }

            OnProgressChanged(new DownloadProgressEventArgs($"Found {urls.Count} video URL(s) to download", 0, urls.Count, null));

            // Download each video directly to the output directory
            for (int i = 0; i < urls.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var url = urls[i];
                try
                {
                    // Parse video ID
                    var videoId = VideoId.TryParse(url);
                    if (videoId == null)
                    {
                        OnDownloadError(new DownloadErrorEventArgs($"Invalid video URL: {url}", null));
                        continue;
                    }

                    // Get video metadata
                    OnProgressChanged(new DownloadProgressEventArgs($"Fetching video information for {i + 1}/{urls.Count}...", i, urls.Count, null));
                    var video = await _youtube.Videos.GetAsync(videoId.Value, cancellationToken);

                    OnProgressChanged(new DownloadProgressEventArgs(
                        $"Downloading {i + 1}/{urls.Count}: {video.Title}",
                        i,
                        urls.Count,
                        video.Title));

                    await DownloadVideoAsync(video, outputDirectory, cancellationToken);

                    OnDownloadCompleted(new DownloadCompletedEventArgs(video.Title, i + 1, urls.Count));

                    // Apply delay between downloads (except for the last one)
                    if (i < urls.Count - 1)
                    {
                        OnProgressChanged(new DownloadProgressEventArgs(
                            $"Waiting 10 seconds before next download... ({i + 1}/{urls.Count} completed)",
                            i + 1,
                            urls.Count,
                            null));
                        await Task.Delay(DelayBetweenDownloadsMs, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    OnDownloadError(new DownloadErrorEventArgs($"Failed to download video from URL '{url}': {ex.Message}", null));
                    // Continue with next video
                }
            }

            OnProgressChanged(new DownloadProgressEventArgs($"All downloads completed! {urls.Count} song(s) downloaded to '{outputDirectory}'", urls.Count, urls.Count, null, outputDirectory));
        }
        catch (Exception ex)
        {
            OnDownloadError(new DownloadErrorEventArgs($"Video download failed: {ex.Message}", null));
        }
    }

    private async Task DownloadVideoAsync(IVideo video, string outputDirectory, CancellationToken cancellationToken)
    {
        var fileName = SanitizeFileName(video.Title);
        Exception? lastError = null;

        for (var attempt = 1; attempt <= MaxDownloadAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Stream URLs are short-lived and signed, so every attempt asks YouTube for a fresh manifest
                var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(video.Id, cancellationToken);
                var candidates = PickAudioStreams(streamManifest);

                if (candidates.Count == 0)
                {
                    throw new InvalidOperationException("No audio-only stream was offered for this video");
                }

                foreach (var streamInfo in candidates)
                {
                    var filePath = Path.Combine(outputDirectory, $"{fileName}.{FileExtensionFor(streamInfo.Container)}");

                    try
                    {
                        await _youtube.Videos.Streams.DownloadAsync(streamInfo, filePath, cancellationToken: cancellationToken);
                        return;
                    }
                    catch (HttpRequestException ex) when (IsForbidden(ex))
                    {
                        // YouTube refused this particular stream URL - try the next container before giving up
                        lastError = ex;
                        TryDeleteFile(filePath);
                    }
                    catch
                    {
                        // Any other failure (including cancellation): don't leave a half-written file behind
                        TryDeleteFile(filePath);
                        throw;
                    }
                }
            }
            catch (HttpRequestException ex) when (IsForbidden(ex))
            {
                // The manifest request itself was refused
                lastError = ex;
            }

            if (attempt < MaxDownloadAttempts)
            {
                await Task.Delay(RetryDelayMs, cancellationToken);
            }
        }

        throw new InvalidOperationException(
            "YouTube refused to serve the audio stream (HTTP 403 Forbidden). YouTube changes its download " +
            "protection regularly; waiting a while, or updating NoPlayer to a build with a newer YoutubeExplode, " +
            "usually fixes it.",
            lastError);
    }

    /// <summary>
    /// Best audio-only stream per container, WebM (Opus) first because that is what the player prefers,
    /// then MP4 (AAC) as a fallback in case YouTube refuses the WebM one.
    /// </summary>
    private static List<IStreamInfo> PickAudioStreams(StreamManifest manifest)
    {
        var audioStreams = manifest.GetAudioOnlyStreams().ToList();
        var candidates = new List<IStreamInfo>();

        var webm = audioStreams.Where(s => s.Container == Container.WebM).TryGetWithHighestBitrate();
        if (webm != null) candidates.Add(webm);

        var mp4 = audioStreams.Where(s => s.Container == Container.Mp4).TryGetWithHighestBitrate();
        if (mp4 != null) candidates.Add(mp4);

        // Anything else YouTube might offer, as a last resort
        var other = audioStreams
            .Where(s => s.Container != Container.WebM && s.Container != Container.Mp4)
            .TryGetWithHighestBitrate();
        if (other != null) candidates.Add(other);

        return candidates;
    }

    // Audio-only MP4 is conventionally .m4a, which is also what the library scanner looks for
    private static string FileExtensionFor(Container container) =>
        container == Container.Mp4 ? "m4a" : container.Name;

    private static bool IsForbidden(HttpRequestException ex) =>
        ex.StatusCode == HttpStatusCode.Forbidden || ex.Message.Contains("403", StringComparison.Ordinal);

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // A leftover partial file is not worth failing the download over
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

        // Limit length to avoid filesystem issues
        if (sanitized.Length > 200)
            sanitized = sanitized.Substring(0, 200);

        return sanitized.TrimEnd('.');
    }

    protected virtual void OnProgressChanged(DownloadProgressEventArgs e)
    {
        ProgressChanged?.Invoke(this, e);
    }

    protected virtual void OnDownloadCompleted(DownloadCompletedEventArgs e)
    {
        DownloadCompleted?.Invoke(this, e);
    }

    protected virtual void OnDownloadError(DownloadErrorEventArgs e)
    {
        DownloadError?.Invoke(this, e);
    }
}

public class DownloadProgressEventArgs : EventArgs
{
    public string Message { get; }
    public int CurrentIndex { get; }
    public int TotalCount { get; }
    public string? CurrentVideoTitle { get; }
    public string? OutputPath { get; }

    public DownloadProgressEventArgs(string message, int currentIndex, int totalCount, string? currentVideoTitle, string? outputPath = null)
    {
        Message = message;
        CurrentIndex = currentIndex;
        TotalCount = totalCount;
        CurrentVideoTitle = currentVideoTitle;
        OutputPath = outputPath;
    }
}

public class DownloadCompletedEventArgs : EventArgs
{
    public string VideoTitle { get; }
    public int CompletedCount { get; }
    public int TotalCount { get; }

    public DownloadCompletedEventArgs(string videoTitle, int completedCount, int totalCount)
    {
        VideoTitle = videoTitle;
        CompletedCount = completedCount;
        TotalCount = totalCount;
    }
}

public class DownloadErrorEventArgs : EventArgs
{
    public string ErrorMessage { get; }
    public string? VideoTitle { get; }

    public DownloadErrorEventArgs(string errorMessage, string? videoTitle)
    {
        ErrorMessage = errorMessage;
        VideoTitle = videoTitle;
    }
}
