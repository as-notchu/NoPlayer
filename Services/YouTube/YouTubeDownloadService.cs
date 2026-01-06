using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            OnProgressChanged(new DownloadProgressEventArgs($"All downloads completed! {videos.Count} songs downloaded to '{playlistDirName}'", videos.Count, videos.Count, null));
        }
        catch (Exception ex)
        {
            OnDownloadError(new DownloadErrorEventArgs($"Playlist download failed: {ex.Message}", null));
        }
    }

    private async Task DownloadVideoAsync(IVideo video, string outputDirectory, CancellationToken cancellationToken)
    {
        var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(video.Id, cancellationToken);

        // Get the best WebM audio-only stream
        var audioStreamInfo = streamManifest
            .GetAudioOnlyStreams()
            .Where(s => s.Container == Container.WebM)
            .GetWithHighestBitrate();

        if (audioStreamInfo == null)
        {
            throw new Exception("No WebM audio stream found for this video");
        }

        // Sanitize filename
        var fileName = SanitizeFileName(video.Title);
        var filePath = Path.Combine(outputDirectory, $"{fileName}.webm");

        // Download the stream
        await _youtube.Videos.Streams.DownloadAsync(audioStreamInfo, filePath, cancellationToken: cancellationToken);
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

    public DownloadProgressEventArgs(string message, int currentIndex, int totalCount, string? currentVideoTitle)
    {
        Message = message;
        CurrentIndex = currentIndex;
        TotalCount = totalCount;
        CurrentVideoTitle = currentVideoTitle;
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
