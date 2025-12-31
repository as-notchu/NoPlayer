using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicPlayer.Models;
using MusicPlayer.Services;

namespace MusicPlayer.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly AudioPlayerService _audioPlayer;
    private readonly MusicLibraryService _libraryService;
    private readonly SettingsService _settingsService;
    private readonly MediaKeyService _mediaKeyService;
    private readonly Random _random = new();
    private readonly List<int> _shuffleHistory = new();
    private int _shuffleHistoryIndex = -1;
    private bool _disposed;

    [ObservableProperty]
    private ObservableCollection<Track> _tracks = new();

    [ObservableProperty]
    private ObservableCollection<Playlist> _playlists = new();

    [ObservableProperty]
    private Playlist? _selectedPlaylist;

    [ObservableProperty]
    private bool _canRemoveFromPlaylist;

    [ObservableProperty]
    private ObservableCollection<string> _musicDirectories = new();

    [ObservableProperty]
    private Track? _currentTrack;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _shuffleEnabled;

    [ObservableProperty]
    private bool _repeatEnabled;

    [ObservableProperty]
    private double _volume = 100;

    [ObservableProperty]
    private double _position;

    [ObservableProperty]
    private long _currentTime;

    [ObservableProperty]
    private long _totalTime;

    [ObservableProperty]
    private string _currentTimeFormatted = "0:00";

    [ObservableProperty]
    private string _totalTimeFormatted = "0:00";

    [ObservableProperty]
    private string _musicFolderPath = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public MainWindowViewModel()
    {
        _audioPlayer = new AudioPlayerService();
        _libraryService = new MusicLibraryService();
        _settingsService = new SettingsService();
        _mediaKeyService = new MediaKeyService();

        // Load settings
        var settings = _settingsService.Settings;
        MusicFolderPath = settings.MusicFolderPath;
        MusicDirectories = new ObservableCollection<string>(settings.MusicDirectories);
        Volume = settings.Volume * 100;
        ShuffleEnabled = settings.ShuffleEnabled;
        RepeatEnabled = settings.RepeatEnabled;

        _audioPlayer.Volume = (int)Volume;

        // Subscribe to audio events
        _audioPlayer.PlaybackStarted += OnPlaybackStarted;
        _audioPlayer.PlaybackPaused += OnPlaybackPaused;
        _audioPlayer.PlaybackStopped += OnPlaybackStopped;
        _audioPlayer.PlaybackEnded += OnPlaybackEnded;
        _audioPlayer.PositionChanged += OnPositionChanged;
        _audioPlayer.TimeChanged += OnTimeChanged;

        // Subscribe to media key events
        _mediaKeyService.PlayPausePressed += (_, _) => Dispatcher.UIThread.Post(TogglePlayPause);
        _mediaKeyService.NextPressed += (_, _) => Dispatcher.UIThread.Post(PlayNext);
        _mediaKeyService.PreviousPressed += (_, _) => Dispatcher.UIThread.Post(PlayPrevious);

        // Auto-load music library if a directory is saved
        if (!string.IsNullOrEmpty(MusicFolderPath) && System.IO.Directory.Exists(MusicFolderPath))
        {
            _ = LoadLibraryAsync();
        }
    }

    partial void OnVolumeChanged(double value)
    {
        _audioPlayer.Volume = (int)value;
        _settingsService.UpdateVolume(value / 100.0);
    }

    partial void OnShuffleEnabledChanged(bool value)
    {
        _settingsService.UpdateShuffle(value);
        if (value)
        {
            _shuffleHistory.Clear();
            _shuffleHistoryIndex = -1;
            if (CurrentTrack != null)
            {
                var index = Tracks.IndexOf(CurrentTrack);
                if (index >= 0)
                {
                    _shuffleHistory.Add(index);
                    _shuffleHistoryIndex = 0;
                }
            }
        }
    }

    partial void OnRepeatEnabledChanged(bool value)
    {
        _settingsService.UpdateRepeat(value);
    }

    private void OnPlaybackStarted(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsPlaying = true;
            TotalTime = _audioPlayer.Length;
            TotalTimeFormatted = FormatTime(TotalTime);

            // Update media key service
            _mediaKeyService.UpdatePlaybackState(true);
            UpdateNowPlayingInfo();
        });
    }

    private void OnPlaybackPaused(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsPlaying = false;
            _mediaKeyService.UpdatePlaybackState(false);
        });
    }

    private void OnPlaybackStopped(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsPlaying = false;
            Position = 0;
            CurrentTime = 0;
            CurrentTimeFormatted = "0:00";
            _mediaKeyService.UpdatePlaybackState(false);
        });
    }

    private void UpdateNowPlayingInfo()
    {
        if (CurrentTrack == null) return;

        _mediaKeyService.UpdateNowPlayingInfo(
            CurrentTrack.DisplayName,
            CurrentTrack.DisplayArtist,
            TotalTime / 1000.0,
            CurrentTime / 1000.0
        );
    }

    private void OnPlaybackEnded(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() => PlayNext());
    }

    private void OnPositionChanged(object? sender, float position)
    {
        Dispatcher.UIThread.Post(() => Position = position * 100);
    }

    private void OnTimeChanged(object? sender, long time)
    {
        Dispatcher.UIThread.Post(() =>
        {
            CurrentTime = time;
            CurrentTimeFormatted = FormatTime(time);
        });
    }

    [RelayCommand]
    private async Task LoadLibraryAsync()
    {
        IsLoading = true;
        StatusMessage = "Scanning music folders...";

        try
        {
            var allTracks = new List<Track>();
            var newPlaylists = new List<Playlist>();

            // Get all directories to scan (include single path and multiple directories)
            var directoriesToScan = new List<string>();

            if (!string.IsNullOrEmpty(MusicFolderPath) && System.IO.Directory.Exists(MusicFolderPath))
            {
                directoriesToScan.Add(MusicFolderPath);
            }

            foreach (var dir in MusicDirectories.Where(System.IO.Directory.Exists))
            {
                if (!directoriesToScan.Contains(dir))
                {
                    directoriesToScan.Add(dir);
                }
            }

            // Scan each directory
            foreach (var directory in directoriesToScan)
            {
                var tracks = await _libraryService.ScanFolderAsync(directory);
                var dirName = System.IO.Path.GetFileName(directory.TrimEnd('/', '\\'));

                // Mark tracks with their source directory
                foreach (var track in tracks)
                {
                    track.SourceDirectory = directory;
                }

                // Create a playlist for this directory
                var directoryPlaylist = new Playlist
                {
                    Name = dirName,
                    IsDirectoryPlaylist = true,
                    DirectoryPath = directory,
                    Tracks = new ObservableCollection<Track>(tracks)
                };

                newPlaylists.Add(directoryPlaylist);
                allTracks.AddRange(tracks);
            }

            // Update tracks and playlists
            Tracks = new ObservableCollection<Track>(allTracks);
            Playlists = new ObservableCollection<Playlist>(newPlaylists);

            StatusMessage = $"Loaded {Tracks.Count} tracks from {directoriesToScan.Count} folder(s)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading library: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void PlayTrack(Track? track)
    {
        if (track == null) return;

        CurrentTrack = track;
        _audioPlayer.Play(track);

        if (ShuffleEnabled)
        {
            var index = Tracks.IndexOf(track);
            if (_shuffleHistoryIndex < _shuffleHistory.Count - 1)
            {
                _shuffleHistory.RemoveRange(_shuffleHistoryIndex + 1, _shuffleHistory.Count - _shuffleHistoryIndex - 1);
            }
            _shuffleHistory.Add(index);
            _shuffleHistoryIndex = _shuffleHistory.Count - 1;
        }
    }

    [RelayCommand]
    private void TogglePlayPause()
    {
        if (CurrentTrack == null && Tracks.Count > 0)
        {
            PlayTrack(Tracks[0]);
            return;
        }

        _audioPlayer.TogglePlayPause();
    }

    [RelayCommand]
    private void Stop()
    {
        _audioPlayer.Stop();
    }

    [RelayCommand]
    private void PlayNext()
    {
        if (Tracks.Count == 0) return;

        int nextIndex;

        if (ShuffleEnabled)
        {
            if (_shuffleHistoryIndex < _shuffleHistory.Count - 1)
            {
                _shuffleHistoryIndex++;
                nextIndex = _shuffleHistory[_shuffleHistoryIndex];
            }
            else
            {
                var availableIndices = Enumerable.Range(0, Tracks.Count)
                    .Where(i => !_shuffleHistory.TakeLast(Math.Min(5, Tracks.Count / 2)).Contains(i))
                    .ToList();

                if (availableIndices.Count == 0)
                    availableIndices = Enumerable.Range(0, Tracks.Count).ToList();

                nextIndex = availableIndices[_random.Next(availableIndices.Count)];
                _shuffleHistory.Add(nextIndex);
                _shuffleHistoryIndex = _shuffleHistory.Count - 1;

                if (_shuffleHistory.Count > 100)
                {
                    _shuffleHistory.RemoveRange(0, 50);
                    _shuffleHistoryIndex -= 50;
                }
            }
        }
        else
        {
            var currentIndex = CurrentTrack != null ? Tracks.IndexOf(CurrentTrack) : -1;
            nextIndex = currentIndex + 1;

            if (nextIndex >= Tracks.Count)
            {
                if (RepeatEnabled)
                    nextIndex = 0;
                else
                {
                    Stop();
                    return;
                }
            }
        }

        PlayTrack(Tracks[nextIndex]);
    }

    [RelayCommand]
    private void PlayPrevious()
    {
        if (Tracks.Count == 0) return;

        if (_audioPlayer.Time > 3000)
        {
            _audioPlayer.Seek(0);
            return;
        }

        int prevIndex;

        if (ShuffleEnabled && _shuffleHistoryIndex > 0)
        {
            _shuffleHistoryIndex--;
            prevIndex = _shuffleHistory[_shuffleHistoryIndex];
        }
        else
        {
            var currentIndex = CurrentTrack != null ? Tracks.IndexOf(CurrentTrack) : 0;
            prevIndex = currentIndex - 1;

            if (prevIndex < 0)
            {
                if (RepeatEnabled)
                    prevIndex = Tracks.Count - 1;
                else
                    prevIndex = 0;
            }
        }

        PlayTrack(Tracks[prevIndex]);
    }

    [RelayCommand]
    private void ToggleShuffle()
    {
        ShuffleEnabled = !ShuffleEnabled;
    }

    [RelayCommand]
    private void ToggleRepeat()
    {
        RepeatEnabled = !RepeatEnabled;
    }

    [RelayCommand]
    private void Seek(double position)
    {
        _audioPlayer.Seek((float)(position / 100.0));
    }

    public void UpdateMusicFolder(string path)
    {
        MusicFolderPath = path;
        _settingsService.UpdateMusicFolder(path);
    }

    public void AddMusicDirectory(string path)
    {
        if (!string.IsNullOrEmpty(path) && System.IO.Directory.Exists(path) && !MusicDirectories.Contains(path))
        {
            MusicDirectories.Add(path);
            _settingsService.UpdateMusicDirectories(MusicDirectories.ToList());
        }
    }

    public void RemoveMusicDirectory(string path)
    {
        if (MusicDirectories.Remove(path))
        {
            _settingsService.UpdateMusicDirectories(MusicDirectories.ToList());
        }
    }

    [RelayCommand]
    private void CreatePlaylistFromSelected(string playlistName)
    {
        var selectedTracks = Tracks.Where(t => t.IsSelected).ToList();

        if (selectedTracks.Count == 0)
        {
            StatusMessage = "No tracks selected";
            return;
        }

        var newPlaylist = new Playlist
        {
            Name = playlistName,
            IsDirectoryPlaylist = false,
            Tracks = new ObservableCollection<Track>(selectedTracks)
        };

        Playlists.Add(newPlaylist);
        StatusMessage = $"Created playlist '{playlistName}' with {selectedTracks.Count} tracks";

        // Clear selection
        foreach (var track in Tracks)
        {
            track.IsSelected = false;
        }
    }

    [RelayCommand]
    private void SelectPlaylist(Playlist? playlist)
    {
        SelectedPlaylist = playlist;

        if (playlist != null)
        {
            // Update tracks view to show only tracks from this playlist
            Tracks = new ObservableCollection<Track>(playlist.Tracks);
            StatusMessage = $"Playlist: {playlist.Name} ({playlist.Tracks.Count} tracks)";

            // Can only remove from custom playlists
            CanRemoveFromPlaylist = !playlist.IsDirectoryPlaylist;
        }
        else
        {
            // Show all tracks
            CanRemoveFromPlaylist = false;
            _ = LoadLibraryAsync();
        }
    }

    [RelayCommand]
    private void DeletePlaylist(Playlist? playlist)
    {
        if (playlist != null && !playlist.IsDirectoryPlaylist)
        {
            Playlists.Remove(playlist);
            if (SelectedPlaylist == playlist)
            {
                SelectedPlaylist = null;
                _ = LoadLibraryAsync();
            }
            StatusMessage = $"Deleted playlist '{playlist.Name}'";
        }
    }

    [RelayCommand]
    private void AddSelectedToPlaylist(Playlist targetPlaylist)
    {
        var selectedTracks = Tracks.Where(t => t.IsSelected).ToList();

        if (selectedTracks.Count == 0)
        {
            StatusMessage = "No tracks selected";
            return;
        }

        if (targetPlaylist.IsDirectoryPlaylist)
        {
            StatusMessage = "Cannot add tracks to directory playlists";
            return;
        }

        var addedCount = 0;
        foreach (var track in selectedTracks)
        {
            // Don't add duplicates
            if (!targetPlaylist.Tracks.Any(t => t.FilePath == track.FilePath))
            {
                targetPlaylist.Tracks.Add(track);
                addedCount++;
            }
        }

        StatusMessage = $"Added {addedCount} track(s) to '{targetPlaylist.Name}'";

        // Clear selection
        foreach (var track in Tracks)
        {
            track.IsSelected = false;
        }
    }

    [RelayCommand]
    private void RemoveTrackFromPlaylist(Track track)
    {
        if (SelectedPlaylist == null)
        {
            StatusMessage = "No playlist selected";
            return;
        }

        if (SelectedPlaylist.IsDirectoryPlaylist)
        {
            StatusMessage = "Cannot remove tracks from directory playlists";
            return;
        }

        if (SelectedPlaylist.Tracks.Remove(track))
        {
            // Also remove from the current view
            Tracks.Remove(track);
            StatusMessage = $"Removed from '{SelectedPlaylist.Name}'";
        }
    }

    private static string FormatTime(long milliseconds)
    {
        var time = TimeSpan.FromMilliseconds(milliseconds);
        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss")
            : time.ToString(@"m\:ss");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _audioPlayer.PlaybackStarted -= OnPlaybackStarted;
        _audioPlayer.PlaybackPaused -= OnPlaybackPaused;
        _audioPlayer.PlaybackStopped -= OnPlaybackStopped;
        _audioPlayer.PlaybackEnded -= OnPlaybackEnded;
        _audioPlayer.PositionChanged -= OnPositionChanged;
        _audioPlayer.TimeChanged -= OnTimeChanged;

        _audioPlayer.Dispose();
        _mediaKeyService.Dispose();
    }
}
