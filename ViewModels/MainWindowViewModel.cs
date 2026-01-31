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

    // Store event handler delegates for proper cleanup
    private readonly EventHandler _mediaPlayPauseHandler;
    private readonly EventHandler _mediaNextHandler;
    private readonly EventHandler _mediaPreviousHandler;
    private List<int> _shuffleQueue = new();          // Pre-shuffled queue of track indices
    private int _shuffleQueuePosition = -1;            // Current position in queue
    private List<int> _playbackHistory = new(100);     // Full playback history for Previous
    private int _playbackHistoryIndex = -1;            // Position in playback history
    private bool _disposed;

    [ObservableProperty]
    private ObservableCollection<Track> _tracks = new();

    private List<Track> _allTracks = new(); // Store all tracks for filtering

    [ObservableProperty]
    private string _searchText = string.Empty;

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
    private bool _repeatOneEnabled;

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

        // Initialize media key event handlers
        _mediaPlayPauseHandler = (_, _) => Dispatcher.UIThread.Post(TogglePlayPause);
        _mediaNextHandler = (_, _) => Dispatcher.UIThread.Post(PlayNext);
        _mediaPreviousHandler = (_, _) => Dispatcher.UIThread.Post(PlayPrevious);

        // Load settings
        var settings = _settingsService.Settings;
        MusicFolderPath = settings.MusicFolderPath;
        MusicDirectories = new ObservableCollection<string>(settings.MusicDirectories);
        Volume = settings.Volume * 100;
        ShuffleEnabled = settings.ShuffleEnabled;
        RepeatEnabled = settings.RepeatEnabled;
        RepeatOneEnabled = settings.RepeatOneEnabled;

        _audioPlayer.Volume = (int)Volume;

        // Subscribe to audio events
        _audioPlayer.PlaybackStarted += OnPlaybackStarted;
        _audioPlayer.PlaybackPaused += OnPlaybackPaused;
        _audioPlayer.PlaybackStopped += OnPlaybackStopped;
        _audioPlayer.PlaybackEnded += OnPlaybackEnded;
        _audioPlayer.PositionChanged += OnPositionChanged;
        _audioPlayer.TimeChanged += OnTimeChanged;

        // Subscribe to media key events
        _mediaKeyService.PlayPausePressed += _mediaPlayPauseHandler;
        _mediaKeyService.NextPressed += _mediaNextHandler;
        _mediaKeyService.PreviousPressed += _mediaPreviousHandler;

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
            // Generate shuffle queue when shuffle enabled
            GenerateShuffleQueue();

            // Add current track to playback history if playing
            if (CurrentTrack != null)
            {
                var index = Tracks.IndexOf(CurrentTrack);
                if (index >= 0)
                {
                    AddToPlaybackHistory(index);
                }
            }
        }
        else
        {
            // Clear shuffle queue when disabled
            _shuffleQueue.Clear();
            _shuffleQueuePosition = -1;
            // Keep playback history for Previous button
        }
    }

    partial void OnRepeatEnabledChanged(bool value)
    {
        _settingsService.UpdateRepeat(value);
    }

    partial void OnRepeatOneEnabledChanged(bool value)
    {
        _settingsService.UpdateRepeatOne(value);
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

                // Rebuild the HashSet index for efficient lookups
                directoryPlaylist.RebuildTrackIndex();

                newPlaylists.Add(directoryPlaylist);
                allTracks.AddRange(tracks);
            }

            // Remove duplicates based on song metadata (title, artist, album, duration)
            // This handles the case where the same song exists in multiple folders
            var uniqueTracks = allTracks
                .GroupBy(t => new { t.Title, t.Artist, t.Album, t.Duration })
                .Select(g => g.First())
                .ToList();

            // Update tracks and playlists
            _allTracks = uniqueTracks; // Store all tracks for search/filtering
            UpdateTracksCollection(uniqueTracks);
            Playlists = new ObservableCollection<Playlist>(newPlaylists);

            // Load custom playlists from settings
            LoadCustomPlaylists();

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

        try
        {
            CurrentTrack = track;
            _audioPlayer.Play(track);

            var trackIndex = Tracks.IndexOf(track);
            if (trackIndex >= 0)
            {
                // When shuffle enabled, remove track from remaining queue if it exists ahead
                if (ShuffleEnabled)
                {
                    // Find track in remaining queue (positions after current position)
                    for (int i = _shuffleQueuePosition + 1; i < _shuffleQueue.Count; i++)
                    {
                        if (_shuffleQueue[i] == trackIndex)
                        {
                            _shuffleQueue.RemoveAt(i);
                            break;
                        }
                    }
                }

                // Add to playback history
                AddToPlaybackHistory(trackIndex);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to play track: {ex.Message}";
            CurrentTrack = null;
            IsPlaying = false;
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
        // Clear search to restore full playlist/library view
        SearchText = string.Empty;

        if (Tracks.Count == 0) return;

        // If Repeat One is enabled, just replay the current track
        if (RepeatOneEnabled && CurrentTrack != null)
        {
            PlayTrack(CurrentTrack);
            return;
        }

        int nextIndex;

        if (ShuffleEnabled)
        {
            // First check if can move forward in playback history (user previously went back)
            if (_playbackHistoryIndex < _playbackHistory.Count - 1)
            {
                _playbackHistoryIndex++;
                nextIndex = _playbackHistory[_playbackHistoryIndex];
            }
            else
            {
                // Advance in shuffle queue
                _shuffleQueuePosition++;

                // If reached end of queue, regenerate and start over
                if (_shuffleQueuePosition >= _shuffleQueue.Count)
                {
                    // Check if repeat is enabled or if we should stop
                    if (!RepeatEnabled)
                    {
                        Stop();
                        return;
                    }

                    GenerateShuffleQueue();
                    _shuffleQueuePosition = 0;
                }

                // Handle empty queue (e.g., single track playlist)
                if (_shuffleQueue.Count == 0)
                {
                    // For single track or current track only, replay if repeat enabled
                    if (RepeatEnabled && CurrentTrack != null)
                    {
                        nextIndex = Tracks.IndexOf(CurrentTrack);
                    }
                    else
                    {
                        Stop();
                        return;
                    }
                }
                else
                {
                    nextIndex = _shuffleQueue[_shuffleQueuePosition];
                }

                // Add to playback history (only for new tracks, not when navigating existing history)
                AddToPlaybackHistory(nextIndex);
            }
        }
        else
        {
            // Non-shuffle mode: sequential playback
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

            // Add to playback history for non-shuffle mode too
            AddToPlaybackHistory(nextIndex);
        }

        PlayTrack(Tracks[nextIndex]);
    }

    [RelayCommand]
    private void PlayPrevious()
    {
        // Clear search to restore full playlist/library view
        SearchText = string.Empty;

        if (Tracks.Count == 0) return;

        // If more than 3 seconds into track, restart current track
        if (_audioPlayer.Time > 3000)
        {
            _audioPlayer.Seek(0);
            return;
        }

        int prevIndex;

        // Use playback history for both shuffle and non-shuffle modes
        if (_playbackHistoryIndex > 0)
        {
            _playbackHistoryIndex--;
            prevIndex = _playbackHistory[_playbackHistoryIndex];
        }
        else
        {
            // At beginning of history
            if (ShuffleEnabled)
            {
                // In shuffle mode, can't go back further, restart current track
                if (CurrentTrack != null)
                {
                    _audioPlayer.Seek(0);
                    return;
                }
                prevIndex = 0;
            }
            else
            {
                // In non-shuffle mode, wrap to last track if repeat enabled
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
    private void ToggleRepeatOne()
    {
        RepeatOneEnabled = !RepeatOneEnabled;
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

        // Rebuild the HashSet index for efficient lookups
        newPlaylist.RebuildTrackIndex();

        Playlists.Add(newPlaylist);
        StatusMessage = $"Created playlist '{playlistName}' with {selectedTracks.Count} tracks";

        // Clear selection
        foreach (var track in Tracks)
        {
            track.IsSelected = false;
        }

        // Save custom playlists
        SaveCustomPlaylists();
    }

    [RelayCommand]
    private void SelectPlaylist(Playlist? playlist)
    {
        // Clear selection from all playlists
        foreach (var p in Playlists)
        {
            p.IsSelected = false;
        }

        SelectedPlaylist = playlist;

        // Reset shuffle state when switching playlists
        _shuffleQueue.Clear();
        _shuffleQueuePosition = -1;
        _playbackHistory.Clear();
        _playbackHistoryIndex = -1;

        if (playlist != null)
        {
            // Mark the selected playlist
            playlist.IsSelected = true;

            // Update tracks view to show only tracks from this playlist
            UpdateTracksCollection(playlist.Tracks);
            StatusMessage = $"Playlist: {playlist.Name} ({playlist.Tracks.Count} tracks)";

            // Can only remove from custom playlists
            CanRemoveFromPlaylist = !playlist.IsDirectoryPlaylist;

            // Generate shuffle queue if shuffle is enabled
            if (ShuffleEnabled)
            {
                GenerateShuffleQueue();
            }
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

            // Save custom playlists
            SaveCustomPlaylists();
        }
    }

    [RelayCommand]
    private void RemoveDirectoryPlaylist(Playlist? playlist)
    {
        if (playlist != null && playlist.IsDirectoryPlaylist && !string.IsNullOrEmpty(playlist.DirectoryPath))
        {
            // Remove from playlists
            Playlists.Remove(playlist);

            // Remove directory from MusicDirectories
            RemoveMusicDirectory(playlist.DirectoryPath);

            // If this was the selected playlist, clear selection
            if (SelectedPlaylist == playlist)
            {
                SelectedPlaylist = null;
            }

            StatusMessage = $"Removed directory '{playlist.DirectoryPath}'";
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
            // Use efficient O(1) AddTrack method instead of O(n) .Any() check
            if (targetPlaylist.AddTrack(track))
            {
                addedCount++;
            }
        }

        StatusMessage = $"Added {addedCount} track(s) to '{targetPlaylist.Name}'";

        // Clear selection
        foreach (var track in Tracks)
        {
            track.IsSelected = false;
        }

        // Save custom playlists
        SaveCustomPlaylists();
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

        // Use efficient RemoveTrack method that maintains the HashSet
        if (SelectedPlaylist.RemoveTrack(track))
        {
            // Also remove from the current view
            Tracks.Remove(track);
            StatusMessage = $"Removed from '{SelectedPlaylist.Name}'";

            // Save custom playlists
            SaveCustomPlaylists();
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplySearch();
    }

    private void ApplySearch()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            // No search - show current view (playlist or all tracks)
            if (SelectedPlaylist != null)
            {
                UpdateTracksCollection(SelectedPlaylist.Tracks);
                StatusMessage = $"Playlist: {SelectedPlaylist.Name} ({SelectedPlaylist.Tracks.Count} tracks)";
            }
            else
            {
                UpdateTracksCollection(_allTracks);
                StatusMessage = $"Showing all {_allTracks.Count} tracks";
            }
        }
        else
        {
            // Search through selected playlist tracks or all tracks
            var searchLower = SearchText.ToLower();
            IEnumerable<Track> sourceCollection = SelectedPlaylist != null ? SelectedPlaylist.Tracks : _allTracks;

            var filtered = sourceCollection.Where(t =>
                t.DisplayNameLower.Contains(searchLower) ||
                t.DisplayArtistLower.Contains(searchLower) ||
                t.DisplayAlbumLower.Contains(searchLower)
            ).ToList();

            UpdateTracksCollection(filtered);

            if (SelectedPlaylist != null)
            {
                StatusMessage = $"Found {filtered.Count} track(s) in '{SelectedPlaylist.Name}'";
            }
            else
            {
                StatusMessage = $"Found {filtered.Count} track(s)";
            }
        }
    }

    // Helper method to update the Tracks collection efficiently
    // Modifies the existing collection instead of creating a new one
    private void UpdateTracksCollection(IEnumerable<Track> newTracks)
    {
        // Clear and re-add is more memory efficient than creating new ObservableCollection
        Tracks.Clear();
        foreach (var track in newTracks)
        {
            Tracks.Add(track);
        }

        // Regenerate shuffle queue if shuffle is enabled (tracks changed)
        if (ShuffleEnabled)
        {
            GenerateShuffleQueue();
        }
    }

    [RelayCommand]
    private void ShowAllSongs()
    {
        // Clear selection from all playlists
        foreach (var p in Playlists)
        {
            p.IsSelected = false;
        }

        SelectedPlaylist = null;
        CanRemoveFromPlaylist = false;
        SearchText = string.Empty;
        UpdateTracksCollection(_allTracks);
        StatusMessage = $"Showing all {_allTracks.Count} tracks";
    }

    private static string FormatTime(long milliseconds)
    {
        var time = TimeSpan.FromMilliseconds(milliseconds);
        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss")
            : time.ToString(@"m\:ss");
    }

    private void SaveCustomPlaylists()
    {
        var customPlaylists = Playlists
            .Where(p => !p.IsDirectoryPlaylist)
            .Select(p => new SavedPlaylist
            {
                Name = p.Name,
                TrackPaths = p.Tracks.Select(t => t.FilePath).ToList()
            })
            .ToList();

        _settingsService.UpdateCustomPlaylists(customPlaylists);
    }

    private void LoadCustomPlaylists()
    {
        var savedPlaylists = _settingsService.Settings.CustomPlaylists;

        foreach (var savedPlaylist in savedPlaylists)
        {
            // Find tracks that match the saved paths
            var playlistTracks = _allTracks
                .Where(t => savedPlaylist.TrackPaths.Contains(t.FilePath))
                .ToList();

            // Only create playlist if it has at least one valid track
            if (playlistTracks.Count > 0)
            {
                var playlist = new Playlist
                {
                    Name = savedPlaylist.Name,
                    IsDirectoryPlaylist = false,
                    Tracks = new ObservableCollection<Track>(playlistTracks)
                };

                // Rebuild the HashSet index for efficient lookups
                playlist.RebuildTrackIndex();

                Playlists.Add(playlist);
            }
        }
    }

    private void GenerateShuffleQueue()
    {
        // Empty playlist check
        if (Tracks.Count == 0)
        {
            _shuffleQueue.Clear();
            _shuffleQueuePosition = -1;
            return;
        }

        // Create list of all track indices
        var indices = Enumerable.Range(0, Tracks.Count).ToList();

        // Remove current track from pool if it exists (don't re-shuffle currently playing song)
        if (CurrentTrack != null)
        {
            var currentIndex = Tracks.IndexOf(CurrentTrack);
            if (currentIndex >= 0)
            {
                indices.Remove(currentIndex);
            }
        }

        // Apply Fisher-Yates shuffle algorithm
        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        // Store shuffled queue and reset position
        _shuffleQueue = indices;
        _shuffleQueuePosition = -1;
    }

    private void AddToPlaybackHistory(int trackIndex)
    {
        // If in middle of history, truncate future entries
        if (_playbackHistoryIndex < _playbackHistory.Count - 1)
        {
            _playbackHistory.RemoveRange(_playbackHistoryIndex + 1, _playbackHistory.Count - _playbackHistoryIndex - 1);
        }

        // Append track index to history
        _playbackHistory.Add(trackIndex);

        // Enforce max size of 100 entries (remove oldest)
        if (_playbackHistory.Count > 100)
        {
            _playbackHistory.RemoveAt(0);
        }
        else
        {
            _playbackHistoryIndex++;
        }

        // Update index to latest position
        _playbackHistoryIndex = _playbackHistory.Count - 1;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Unsubscribe from audio player events
        _audioPlayer.PlaybackStarted -= OnPlaybackStarted;
        _audioPlayer.PlaybackPaused -= OnPlaybackPaused;
        _audioPlayer.PlaybackStopped -= OnPlaybackStopped;
        _audioPlayer.PlaybackEnded -= OnPlaybackEnded;
        _audioPlayer.PositionChanged -= OnPositionChanged;
        _audioPlayer.TimeChanged -= OnTimeChanged;

        // Unsubscribe from media key events before disposing
        _mediaKeyService.PlayPausePressed -= _mediaPlayPauseHandler;
        _mediaKeyService.NextPressed -= _mediaNextHandler;
        _mediaKeyService.PreviousPressed -= _mediaPreviousHandler;

        // Dispose services
        _audioPlayer.Dispose();
        _mediaKeyService.Dispose();
    }
}
