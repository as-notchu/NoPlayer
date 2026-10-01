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
    /// <summary>How far the fast-forward / rewind buttons and arrow keys jump.</summary>
    public const long SeekStepMilliseconds = 10_000;

    private readonly AudioPlayerService _audioPlayer;
    private readonly MusicLibraryService _libraryService;
    private readonly SettingsService _settingsService;
    private readonly MediaKeyService _mediaKeyService;
    private readonly Random _random = new();

    // Store event handler delegates for proper cleanup
    private readonly EventHandler _mediaPlayPauseHandler;
    private readonly EventHandler _mediaNextHandler;
    private readonly EventHandler _mediaPreviousHandler;

    // Shuffle is a plain random queue: the current playback source (playlist or whole library)
    // shuffled once. Next / Previous simply walk forwards and backwards through it.
    private List<Track> _shuffleQueue = new();
    private int _shuffleQueuePosition = -1;
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

    /// <summary>The track that will play after the current one (null when playback will stop).</summary>
    [ObservableProperty]
    private Track? _upNextTrack;

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

    /// <summary>True while the user drags the progress slider; position updates from the player are paused.</summary>
    [ObservableProperty]
    private bool _isSeeking;

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

    /// <summary>Hint shown in place of the track list when it is empty (null hides it).</summary>
    [ObservableProperty]
    private string? _emptyStateText = "Add a music folder in the sidebar to get started";

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

    /// <summary>
    /// The list playback walks through: the selected playlist, or the whole library.
    /// Search filtering never changes this, so a filtered view does not reshuffle the queue.
    /// </summary>
    private IReadOnlyList<Track> PlaybackSource => SelectedPlaylist != null ? SelectedPlaylist.Tracks : _allTracks;

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
            // Build one random queue starting from whatever is playing right now
            RebuildShuffleQueue(CurrentTrack);
        }
        else
        {
            _shuffleQueue.Clear();
            _shuffleQueuePosition = -1;
            UpdateUpNext();
        }
    }

    partial void OnRepeatEnabledChanged(bool value)
    {
        _settingsService.UpdateRepeat(value);
        UpdateUpNext();
    }

    partial void OnRepeatOneEnabledChanged(bool value)
    {
        _settingsService.UpdateRepeatOne(value);
        UpdateUpNext();
    }

    partial void OnPositionChanged(double value)
    {
        // While scrubbing, let the time label follow the thumb instead of the player
        if (IsSeeking && TotalTime > 0)
        {
            CurrentTimeFormatted = FormatTime((long)(TotalTime * value / 100.0));
        }
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
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsSeeking)
            {
                Position = position * 100;
            }
        });
    }

    private void OnTimeChanged(object? sender, long time)
    {
        Dispatcher.UIThread.Post(() =>
        {
            CurrentTime = time;
            if (!IsSeeking)
            {
                CurrentTimeFormatted = FormatTime(time);
            }
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

            // A reload always shows the whole library, so drop any stale playlist selection
            SelectedPlaylist = null;
            CanRemoveFromPlaylist = false;

            // Update tracks and playlists
            _allTracks = uniqueTracks; // Store all tracks for search/filtering
            UpdateTracksCollection(uniqueTracks);
            Playlists = new ObservableCollection<Playlist>(newPlaylists);

            // Load custom playlists from settings
            LoadCustomPlaylists();

            // Keep the currently playing track (if any) at the front of the new queue
            CurrentTrack = ResolveCurrentTrack(uniqueTracks);
            OnPlaybackSourceChanged();

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

    /// <summary>After a rescan the Track objects are new instances; find the one matching what is playing.</summary>
    private Track? ResolveCurrentTrack(List<Track> freshTracks)
    {
        if (CurrentTrack == null) return null;
        var path = CurrentTrack.FilePath;
        return freshTracks.FirstOrDefault(t => t.FilePath == path) ?? CurrentTrack;
    }

    [RelayCommand]
    private void PlayTrack(Track? track)
    {
        if (track == null) return;

        try
        {
            CurrentTrack = track;
            _audioPlayer.Play(track);

            if (ShuffleEnabled)
            {
                // Jump to the track inside the queue so Next / Previous continue from here.
                // A track that is not part of the queue (e.g. played from another view) starts a fresh one.
                var queueIndex = _shuffleQueue.IndexOf(track);
                if (queueIndex >= 0)
                {
                    _shuffleQueuePosition = queueIndex;
                }
                else
                {
                    RebuildShuffleQueue(track);
                }
            }

            UpdateUpNext();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to play track: {ex.Message}";
            CurrentTrack = null;
            IsPlaying = false;
            UpdateUpNext();
        }
    }

    [RelayCommand]
    private void TogglePlayPause()
    {
        if (CurrentTrack == null)
        {
            if (Tracks.Count > 0)
            {
                PlayTrack(Tracks[0]);
            }
            return;
        }

        // Playback finished (or was stopped) - start the current track again instead of doing nothing
        if (!_audioPlayer.HasStream)
        {
            PlayTrack(CurrentTrack);
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

        // If Repeat One is enabled, just replay the current track
        if (RepeatOneEnabled && CurrentTrack != null)
        {
            PlayTrack(CurrentTrack);
            return;
        }

        var next = ShuffleEnabled ? NextInShuffleQueue() : NextSequential();

        if (next == null)
        {
            Stop();
            UpdateUpNext();
            return;
        }

        PlayTrack(next);
    }

    private Track? NextInShuffleQueue()
    {
        if (_shuffleQueue.Count == 0)
        {
            RebuildShuffleQueue(CurrentTrack);
        }

        if (_shuffleQueue.Count == 0) return null;

        var nextPosition = _shuffleQueuePosition + 1;

        if (nextPosition >= _shuffleQueue.Count)
        {
            // End of the queue: stop, or deal a fresh random queue when repeating
            if (!RepeatEnabled) return null;

            RebuildShuffleQueue();

            // Don't play the same song twice in a row across the boundary
            if (_shuffleQueue.Count > 1 && ReferenceEquals(_shuffleQueue[0], CurrentTrack))
            {
                var swapWith = _random.Next(1, _shuffleQueue.Count);
                (_shuffleQueue[0], _shuffleQueue[swapWith]) = (_shuffleQueue[swapWith], _shuffleQueue[0]);
            }

            nextPosition = 0;
        }

        _shuffleQueuePosition = nextPosition;
        return _shuffleQueue[nextPosition];
    }

    private Track? NextSequential()
    {
        var source = PlaybackSource;
        if (source.Count == 0) return null;

        var index = CurrentTrack != null ? IndexOfTrack(source, CurrentTrack) : -1;
        index++;

        if (index >= source.Count)
        {
            if (!RepeatEnabled) return null;
            index = 0;
        }

        return source[index];
    }

    [RelayCommand]
    private void PlayPrevious()
    {
        // Clear search to restore full playlist/library view
        SearchText = string.Empty;

        // If more than 3 seconds into track, restart current track
        if (_audioPlayer.Time > 3000)
        {
            _audioPlayer.Seek(0);
            return;
        }

        Track? previous;

        if (ShuffleEnabled)
        {
            var previousPosition = _shuffleQueuePosition - 1;

            if (previousPosition < 0)
            {
                if (RepeatEnabled && _shuffleQueue.Count > 0)
                {
                    previousPosition = _shuffleQueue.Count - 1;
                }
                else
                {
                    // Start of the queue: just restart the current track
                    _audioPlayer.Seek(0);
                    return;
                }
            }

            previous = _shuffleQueue[previousPosition];
        }
        else
        {
            var source = PlaybackSource;
            if (source.Count == 0) return;

            var index = CurrentTrack != null ? IndexOfTrack(source, CurrentTrack) : 0;
            index--;

            if (index < 0)
            {
                index = RepeatEnabled ? source.Count - 1 : 0;
            }

            previous = source[index];
        }

        PlayTrack(previous);
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

    /// <summary>Fast-forward the current track by <see cref="SeekStepMilliseconds"/>.</summary>
    [RelayCommand]
    private void SeekForward()
    {
        _audioPlayer.SeekRelative(SeekStepMilliseconds);
    }

    /// <summary>Rewind the current track by <see cref="SeekStepMilliseconds"/>.</summary>
    [RelayCommand]
    private void SeekBackward()
    {
        _audioPlayer.SeekRelative(-SeekStepMilliseconds);
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
        SearchText = string.Empty;

        if (playlist != null)
        {
            // Mark the selected playlist
            playlist.IsSelected = true;

            // Update tracks view to show only tracks from this playlist
            UpdateTracksCollection(playlist.Tracks);
            StatusMessage = $"Playlist: {playlist.Name} ({playlist.Tracks.Count} tracks)";

            // Can only remove from custom playlists
            CanRemoveFromPlaylist = !playlist.IsDirectoryPlaylist;

            OnPlaybackSourceChanged();
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

        // Newly added tracks should be part of the queue if that playlist is playing
        if (SelectedPlaylist == targetPlaylist)
        {
            OnPlaybackSourceChanged();
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

        // Use efficient RemoveTrack method that maintains the HashSet
        if (SelectedPlaylist.RemoveTrack(track))
        {
            // Also remove from the current view
            Tracks.Remove(track);
            RefreshEmptyState();
            StatusMessage = $"Removed from '{SelectedPlaylist.Name}'";

            // Drop it from the shuffle queue too, keeping the position pointing at the same song
            var queueIndex = _shuffleQueue.IndexOf(track);
            if (queueIndex >= 0)
            {
                _shuffleQueue.RemoveAt(queueIndex);
                if (queueIndex <= _shuffleQueuePosition)
                {
                    _shuffleQueuePosition--;
                }
            }
            UpdateUpNext();

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

        RefreshEmptyState();
    }

    private void RefreshEmptyState()
    {
        if (Tracks.Count > 0)
        {
            EmptyStateText = null;
        }
        else if (!string.IsNullOrWhiteSpace(SearchText))
        {
            EmptyStateText = "No songs match your search";
        }
        else if (SelectedPlaylist != null)
        {
            EmptyStateText = "This playlist is empty";
        }
        else
        {
            EmptyStateText = "Add a music folder in the sidebar to get started";
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

        OnPlaybackSourceChanged();
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

    /// <summary>Called whenever the playlist / library that playback walks through changes.</summary>
    private void OnPlaybackSourceChanged()
    {
        if (ShuffleEnabled)
        {
            RebuildShuffleQueue(CurrentTrack);
        }
        else
        {
            UpdateUpNext();
        }
    }

    /// <summary>
    /// Deal a new random queue from the playback source (Fisher-Yates).
    /// When <paramref name="startWith"/> is part of the source it is moved to the front and
    /// marked as already playing, so Next continues with a song that hasn't been heard yet.
    /// </summary>
    private void RebuildShuffleQueue(Track? startWith = null)
    {
        _shuffleQueue = PlaybackSource.ToList();

        for (var i = _shuffleQueue.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (_shuffleQueue[i], _shuffleQueue[j]) = (_shuffleQueue[j], _shuffleQueue[i]);
        }

        _shuffleQueuePosition = -1;

        if (startWith != null)
        {
            var index = _shuffleQueue.IndexOf(startWith);
            if (index > 0)
            {
                _shuffleQueue.RemoveAt(index);
                _shuffleQueue.Insert(0, startWith);
            }
            if (index >= 0)
            {
                _shuffleQueuePosition = 0;
            }
        }

        UpdateUpNext();
    }

    /// <summary>Recompute which track plays after the current one, for the "Up next" hint.</summary>
    private void UpdateUpNext()
    {
        Track? next = null;

        if (RepeatOneEnabled)
        {
            next = CurrentTrack;
        }
        else if (ShuffleEnabled)
        {
            var nextPosition = _shuffleQueuePosition + 1;
            if (nextPosition < _shuffleQueue.Count)
            {
                next = _shuffleQueue[nextPosition];
            }
            // At the end of the queue with repeat on, the next pass is dealt lazily - nothing to show yet
        }
        else
        {
            var source = PlaybackSource;
            if (source.Count > 0)
            {
                var index = CurrentTrack != null ? IndexOfTrack(source, CurrentTrack) : -1;
                if (index + 1 < source.Count)
                {
                    next = source[index + 1];
                }
                else if (RepeatEnabled)
                {
                    next = source[0];
                }
            }
        }

        UpNextTrack = next;
    }

    private static int IndexOfTrack(IReadOnlyList<Track> list, Track track)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (ReferenceEquals(list[i], track)) return i;
        }
        return -1;
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
