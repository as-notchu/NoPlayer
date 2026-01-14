using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicPlayer.Services;
using MusicPlayer.Services.YouTube;

namespace MusicPlayer.ViewModels;

public partial class DownloadWindowViewModel : ViewModelBase, IDisposable
{
    private readonly YouTubeDownloadService _downloadService;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly SettingsService _settingsService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UrlLabelText))]
    [NotifyPropertyChangedFor(nameof(UrlWatermarkText))]
    [NotifyPropertyChangedFor(nameof(UrlHelperText))]
    [NotifyPropertyChangedFor(nameof(OutputHelperText))]
    private bool _isPlaylistMode = true;

    [ObservableProperty]
    private string _youtubeUrl = string.Empty;

    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Ready to download";

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private bool _canStartDownload = true;

    [ObservableProperty]
    private int _currentProgress;

    [ObservableProperty]
    private int _totalVideos;

    [ObservableProperty]
    private string _currentVideoTitle = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _downloadLog = new();

    [ObservableProperty]
    private string _completedOutputDirectory = string.Empty;

    public string UrlLabelText => IsPlaylistMode ? "YouTube Playlist URL" : "YouTube Video URL(s)";

    public string UrlWatermarkText => IsPlaylistMode
        ? "https://www.youtube.com/playlist?list=..."
        : "https://www.youtube.com/watch?v=... (one per line or comma-separated)";

    public string UrlHelperText => IsPlaylistMode
        ? "The playlist will be saved in its own subfolder."
        : "Enter one or more video URLs. You can paste multiple URLs separated by newlines or commas.";

    public string OutputHelperText => IsPlaylistMode
        ? "A subfolder will be created for the playlist"
        : "Songs will be saved directly to this folder";

    public DownloadWindowViewModel()
    {
        _settingsService = new SettingsService();
        _downloadService = new YouTubeDownloadService();

        // Subscribe to download service events
        _downloadService.ProgressChanged += OnDownloadProgressChanged;
        _downloadService.DownloadCompleted += OnDownloadCompleted;
        _downloadService.DownloadError += OnDownloadError;

        // Set default output directory to Music folder
        OutputDirectory = _settingsService.Settings.MusicFolderPath;
    }

    [RelayCommand]
    private async Task StartDownloadAsync()
    {
        if (string.IsNullOrWhiteSpace(YoutubeUrl))
        {
            StatusMessage = IsPlaylistMode ? "Please enter a YouTube playlist URL" : "Please enter YouTube video URL(s)";
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            StatusMessage = "Please select an output directory";
            return;
        }

        // Validate URL based on mode
        if (IsPlaylistMode)
        {
            if (!YoutubeUrl.Contains("playlist") && !YoutubeUrl.Contains("list="))
            {
                StatusMessage = "Please provide a valid YouTube playlist URL (must contain 'playlist' or 'list=')";
                AddLog("ERROR: Playlist mode requires a valid playlist URL.");
                return;
            }
        }
        else
        {
            // For individual mode, check if it looks like a video URL
            if (!YoutubeUrl.Contains("youtube.com") && !YoutubeUrl.Contains("youtu.be"))
            {
                StatusMessage = "Please provide valid YouTube video URL(s)";
                AddLog("ERROR: Please provide valid YouTube video URLs.");
                return;
            }
        }

        try
        {
            IsDownloading = true;
            CanStartDownload = false;
            CurrentProgress = 0;
            TotalVideos = 0;
            CurrentVideoTitle = string.Empty;
            CompletedOutputDirectory = string.Empty;
            DownloadLog.Clear();

            _cancellationTokenSource = new CancellationTokenSource();

            if (IsPlaylistMode)
            {
                AddLog($"Starting playlist download from: {YoutubeUrl}");
                AddLog($"Base output directory: {OutputDirectory}");
                AddLog("A subdirectory will be created for this playlist");

                await _downloadService.DownloadPlaylistAsync(YoutubeUrl, OutputDirectory, _cancellationTokenSource.Token);
            }
            else
            {
                AddLog($"Starting individual video download(s)");
                AddLog($"Output directory: {OutputDirectory}");
                AddLog("Songs will be saved directly to the output folder");

                await _downloadService.DownloadVideosAsync(YoutubeUrl, OutputDirectory, _cancellationTokenSource.Token);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            AddLog($"ERROR: {ex.Message}");
        }
        finally
        {
            IsDownloading = false;
            CanStartDownload = true;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    [RelayCommand]
    private void CancelDownload()
    {
        if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
        {
            StatusMessage = "Cancelling download...";
            AddLog("Download cancelled by user");
            _cancellationTokenSource.Cancel();
        }
    }

    [RelayCommand]
    private void ClearLog()
    {
        DownloadLog.Clear();
        StatusMessage = "Ready to download";
        CurrentProgress = 0;
        TotalVideos = 0;
        CurrentVideoTitle = string.Empty;
        CompletedOutputDirectory = string.Empty;
    }

    [RelayCommand]
    private async Task CopyOutputPathAsync()
    {
        if (!string.IsNullOrEmpty(CompletedOutputDirectory))
        {
            var clipboard = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow?.Clipboard ?? TopLevel.GetTopLevel(desktop.MainWindow)?.Clipboard
                : null;
            
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(CompletedOutputDirectory);
                AddLog($"Copied to clipboard: {CompletedOutputDirectory}");
            }
        }
    }

    private void OnDownloadProgressChanged(object? sender, DownloadProgressEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusMessage = e.Message;
            CurrentProgress = e.CurrentIndex;
            TotalVideos = e.TotalCount;
            CurrentVideoTitle = e.CurrentVideoTitle ?? string.Empty;

            if (!string.IsNullOrEmpty(e.OutputPath))
            {
                CompletedOutputDirectory = e.OutputPath;
            }

            if (!string.IsNullOrEmpty(e.Message))
            {
                AddLog(e.Message);
            }
        });
    }

    private void OnDownloadCompleted(object? sender, DownloadCompletedEventArgs e)
    {
        
        Dispatcher.UIThread.Post(() =>
        {
            AddLog($"✓ Completed: {e.VideoTitle} ({e.CompletedCount}/{e.TotalCount})");
        });
    }

    private void OnDownloadError(object? sender, DownloadErrorEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var message = string.IsNullOrEmpty(e.VideoTitle)
                ? $"✗ Error: {e.ErrorMessage}"
                : $"✗ Error downloading '{e.VideoTitle}': {e.ErrorMessage}";

            AddLog(message);
            StatusMessage = e.ErrorMessage;
        });
    }

    private void AddLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        DownloadLog.Add($"[{timestamp}] {message}");

        // Keep log size reasonable (limit to 100 entries)
        while (DownloadLog.Count > 100)
        {
            DownloadLog.RemoveAt(0);
        }
    }

    public void Dispose()
    {
        _downloadService.ProgressChanged -= OnDownloadProgressChanged;
        _downloadService.DownloadCompleted -= OnDownloadCompleted;
        _downloadService.DownloadError -= OnDownloadError;

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }
}
