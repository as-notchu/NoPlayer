using System;
using System.IO;
using System.Reflection;
using System.Timers;
using ManagedBass;
using MusicPlayer.Models;

namespace MusicPlayer.Services;

public class AudioPlayerService : IDisposable
{
    private int _streamHandle;
    private readonly Timer _positionTimer;
    private bool _disposed;
    private bool _bassInitialized;

    public event EventHandler? PlaybackStarted;
    public event EventHandler? PlaybackPaused;
    public event EventHandler? PlaybackStopped;
    public event EventHandler? PlaybackEnded;
    public event EventHandler<float>? PositionChanged;
    public event EventHandler<long>? TimeChanged;

    public bool IsPlaying => _streamHandle != 0 && Bass.ChannelIsActive(_streamHandle) == PlaybackState.Playing;

    public long Length => _streamHandle != 0
        ? (long)(Bass.ChannelBytes2Seconds(_streamHandle, Bass.ChannelGetLength(_streamHandle)) * 1000)
        : 0;

    public long Time => _streamHandle != 0
        ? (long)(Bass.ChannelBytes2Seconds(_streamHandle, Bass.ChannelGetPosition(_streamHandle)) * 1000)
        : 0;

    public float Position => _streamHandle != 0 && Length > 0
        ? (float)Time / Length
        : 0;

    private int _volume = 100;
    public int Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 100);
            if (_streamHandle != 0)
            {
                Bass.ChannelSetAttribute(_streamHandle, ChannelAttribute.Volume, _volume / 100.0);
            }
        }
    }

    public AudioPlayerService()
    {
        InitializeBass();

        _positionTimer = new Timer(100);
        _positionTimer.Elapsed += OnPositionTimerElapsed;
    }

    private void InitializeBass()
    {
        if (_bassInitialized) return;

        // Get the directory where the executable is located
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var baseDir = Path.GetDirectoryName(assemblyLocation) ?? AppContext.BaseDirectory;

        // Initialize BASS with default device
        if (!Bass.Init(-1, 44100, DeviceInitFlags.Default))
        {
            var error = Bass.LastError;
            if (error != Errors.Already)
            {
                throw new InvalidOperationException($"Failed to initialize BASS: {error}");
            }
        }

        // Load plugins for additional format support
        LoadPlugin(baseDir, "libbassflac.dylib");
        LoadPlugin(baseDir, "libbasswebm.dylib");

        _bassInitialized = true;
    }

    private static void LoadPlugin(string baseDir, string pluginName)
    {
        var pluginPath = Path.Combine(baseDir, pluginName);
        if (File.Exists(pluginPath))
        {
            Bass.PluginLoad(pluginPath);
        }
    }

    private void OnPositionTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_streamHandle == 0) return;

        var state = Bass.ChannelIsActive(_streamHandle);

        if (state == PlaybackState.Playing)
        {
            PositionChanged?.Invoke(this, Position);
            TimeChanged?.Invoke(this, Time);
        }
        else if (state == PlaybackState.Stopped)
        {
            // Check if we reached the end
            var pos = Bass.ChannelGetPosition(_streamHandle);
            var len = Bass.ChannelGetLength(_streamHandle);

            if (pos >= len - 1000) // Near end of file
            {
                _positionTimer.Stop();
                PlaybackEnded?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void Play(Track track)
    {
        Stop();

        _streamHandle = Bass.CreateStream(track.FilePath, 0, 0, BassFlags.Default);

        if (_streamHandle == 0)
        {
            var error = Bass.LastError;
            throw new InvalidOperationException($"Failed to create stream for '{track.FilePath}': {error}");
        }

        // Set volume
        Bass.ChannelSetAttribute(_streamHandle, ChannelAttribute.Volume, _volume / 100.0);

        // Set up end sync to detect when playback finishes
        Bass.ChannelSetSync(_streamHandle, SyncFlags.End, 0, OnPlaybackEnd);

        if (Bass.ChannelPlay(_streamHandle))
        {
            _positionTimer.Start();
            PlaybackStarted?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnPlaybackEnd(int handle, int channel, int data, IntPtr user)
    {
        _positionTimer.Stop();
        PlaybackEnded?.Invoke(this, EventArgs.Empty);
    }

    public void Play()
    {
        if (_streamHandle == 0) return;

        if (Bass.ChannelPlay(_streamHandle))
        {
            _positionTimer.Start();
            PlaybackStarted?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Pause()
    {
        if (_streamHandle == 0) return;

        if (Bass.ChannelPause(_streamHandle))
        {
            _positionTimer.Stop();
            PlaybackPaused?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Stop()
    {
        _positionTimer.Stop();

        if (_streamHandle != 0)
        {
            Bass.ChannelStop(_streamHandle);
            Bass.StreamFree(_streamHandle);
            _streamHandle = 0;
            PlaybackStopped?.Invoke(this, EventArgs.Empty);
        }
    }

    public void TogglePlayPause()
    {
        if (_streamHandle == 0) return;

        if (IsPlaying)
            Pause();
        else
            Play();
    }

    public void Seek(float position)
    {
        if (_streamHandle == 0) return;

        var length = Bass.ChannelGetLength(_streamHandle);
        var newPosition = (long)(length * Math.Clamp(position, 0f, 1f));
        Bass.ChannelSetPosition(_streamHandle, newPosition);

        PositionChanged?.Invoke(this, Position);
        TimeChanged?.Invoke(this, Time);
    }

    public void SeekRelative(long milliseconds)
    {
        if (_streamHandle == 0) return;

        var currentSeconds = Bass.ChannelBytes2Seconds(_streamHandle, Bass.ChannelGetPosition(_streamHandle));
        var newSeconds = currentSeconds + (milliseconds / 1000.0);
        var totalSeconds = Bass.ChannelBytes2Seconds(_streamHandle, Bass.ChannelGetLength(_streamHandle));

        newSeconds = Math.Clamp(newSeconds, 0, totalSeconds);
        var newPosition = Bass.ChannelSeconds2Bytes(_streamHandle, newSeconds);
        Bass.ChannelSetPosition(_streamHandle, newPosition);

        PositionChanged?.Invoke(this, Position);
        TimeChanged?.Invoke(this, Time);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _positionTimer.Stop();
        _positionTimer.Dispose();

        Stop();

        if (_bassInitialized)
        {
            Bass.Free();
        }
    }
}
