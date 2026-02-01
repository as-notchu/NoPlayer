using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace MusicPlayer.Services;

public class MediaKeyService : IDisposable
{
    public event EventHandler? PlayPausePressed;
    public event EventHandler? NextPressed;
    public event EventHandler? PreviousPressed;

    private bool _disposed;
    private bool _isPlaying;
    private CancellationTokenSource? _cts;
    private Task? _messageLoopTask;
    private IntPtr _eventTap;
    private IntPtr _runLoopSource;
    private IntPtr _runLoop;
    private IntPtr _mediaPlayerHandle;
    private IntPtr _coreFoundationHandle;
    private GCHandle _callbackHandle;

    // Windows-specific
    private const int WM_HOTKEY = 0x0312;
    private const int WM_APPCOMMAND = 0x0319;
    private const int APPCOMMAND_MEDIA_PLAY_PAUSE = 14;
    private const int APPCOMMAND_MEDIA_NEXTTRACK = 11;
    private const int APPCOMMAND_MEDIA_PREVIOUSTRACK = 12;

    // CGEventTap types
    private const int kCGSessionEventTap = 1;
    private const int kCGHeadInsertEventTap = 0;
    private const int kCGEventTapOptionDefault = 0;
    private const int kCGEventTapOptionListenOnly = 1;
    private const int NSSystemDefined = 14;

    // NX_KEYTYPE values for media keys
    private const int NX_KEYTYPE_PLAY = 16;
    private const int NX_KEYTYPE_NEXT = 17;
    private const int NX_KEYTYPE_PREVIOUS = 18;
    private const int NX_KEYTYPE_FAST = 19;
    private const int NX_KEYTYPE_REWIND = 20;

    // NSEvent subtype for system defined events
    private const int NX_SUBTYPE_AUX_CONTROL_BUTTONS = 8;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr CGEventTapCallBack(IntPtr proxy, uint type, IntPtr eventRef, IntPtr userInfo);

    private CGEventTapCallBack? _callback;

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGEventTapCreate(
        int tap,
        int place,
        int options,
        ulong eventsOfInterest,
        CGEventTapCallBack callback,
        IntPtr userInfo);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern void CGEventTapEnable(IntPtr tap, bool enable);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern long CGEventGetIntegerValueField(IntPtr eventRef, int field);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern long CGEventGetData(IntPtr eventRef, int field);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFMachPortCreateRunLoopSource(IntPtr allocator, IntPtr port, int order);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFRunLoopGetCurrent();

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRunLoopAddSource(IntPtr runLoop, IntPtr source, IntPtr mode);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRunLoopRemoveSource(IntPtr runLoop, IntPtr source, IntPtr mode);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRunLoopRun();

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRunLoopStop(IntPtr runLoop);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFRunLoopGetMain();

    // kCFRunLoopCommonModes
    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr kCFRunLoopCommonModes();

    // For objc calls to set now playing info
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(string className);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName(string selector);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr dlopen(string path, int mode);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern int dlclose(IntPtr handle);

    private const int RTLD_LAZY = 1;
    private const int RTLD_NOW = 2;

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_Int(IntPtr receiver, IntPtr selector, int arg1);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_Double(IntPtr receiver, IntPtr selector, double arg1);

    // Windows P/Invoke
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    // Virtual key codes for Windows media keys
    private const uint VK_MEDIA_PLAY_PAUSE = 0xB3;
    private const uint VK_MEDIA_NEXT_TRACK = 0xB0;
    private const uint VK_MEDIA_PREV_TRACK = 0xB1;

    private const int MPNowPlayingPlaybackStatePlaying = 1;
    private const int MPNowPlayingPlaybackStatePaused = 2;

    public MediaKeyService()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                InitializeMacOS();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                InitializeWindows();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                InitializeLinux();
            }
            else
            {
                Console.WriteLine("Media keys not supported on this platform");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize media keys: {ex.Message}");
        }
    }

    private void InitializeMacOS()
    {
        // Load MediaPlayer framework and store handle for cleanup
        _mediaPlayerHandle = dlopen("/System/Library/Frameworks/MediaPlayer.framework/MediaPlayer", RTLD_NOW);
        if (_mediaPlayerHandle == IntPtr.Zero)
        {
            Console.WriteLine("Warning: Could not load MediaPlayer framework");
        }
        else
        {
            Console.WriteLine("MediaPlayer framework loaded successfully");
        }

        StartEventTap();
        InitializeNowPlaying();
    }

    private void InitializeWindows()
    {
        Console.WriteLine("Initializing Windows media key support...");
        StartWindowsMessageLoop();
    }

    private void InitializeLinux()
    {
        Console.WriteLine("Linux media key support via D-Bus MPRIS");
        Console.WriteLine("Note: Linux media keys work through MPRIS D-Bus interface");
        Console.WriteLine("Your desktop environment should automatically route media keys to MPRIS-enabled apps");
        // Linux doesn't need explicit hooking - MPRIS handles it automatically through D-Bus
        // The system will send commands to our app when we register as an MPRIS player
    }

    private void StartWindowsMessageLoop()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        _cts = new CancellationTokenSource();

        _messageLoopTask = Task.Run(() =>
        {
            try
            {
                // Register global hotkeys for media keys
                // Using hotkey IDs 1, 2, 3
                var registered = false;
                registered |= RegisterHotKey(IntPtr.Zero, 1, 0, VK_MEDIA_PLAY_PAUSE);
                registered |= RegisterHotKey(IntPtr.Zero, 2, 0, VK_MEDIA_NEXT_TRACK);
                registered |= RegisterHotKey(IntPtr.Zero, 3, 0, VK_MEDIA_PREV_TRACK);

                if (!registered)
                {
                    Console.WriteLine("Warning: Failed to register Windows media key hotkeys");
                    Console.WriteLine("Media keys may not work if another app is using them");
                }
                else
                {
                    Console.WriteLine("Windows media key hotkeys registered successfully");
                }

                // Message loop to receive hotkey messages
                while (!_cts.Token.IsCancellationRequested)
                {
                    if (GetMessage(out MSG msg, IntPtr.Zero, 0, 0) > 0)
                    {
                        if (msg.message == WM_HOTKEY)
                        {
                            int hotkeyId = msg.wParam.ToInt32();
                            Console.WriteLine($"Hotkey pressed: ID {hotkeyId}");

                            switch (hotkeyId)
                            {
                                case 1:
                                    Console.WriteLine("Play/Pause key pressed (Windows)");
                                    PlayPausePressed?.Invoke(this, EventArgs.Empty);
                                    break;
                                case 2:
                                    Console.WriteLine("Next key pressed (Windows)");
                                    NextPressed?.Invoke(this, EventArgs.Empty);
                                    break;
                                case 3:
                                    Console.WriteLine("Previous key pressed (Windows)");
                                    PreviousPressed?.Invoke(this, EventArgs.Empty);
                                    break;
                            }
                        }

                        TranslateMessage(ref msg);
                        DispatchMessage(ref msg);
                    }
                }

                // Cleanup
                UnregisterHotKey(IntPtr.Zero, 1);
                UnregisterHotKey(IntPtr.Zero, 2);
                UnregisterHotKey(IntPtr.Zero, 3);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Windows message loop error: {ex.Message}");
            }
        }, _cts.Token);
    }

    private void InitializeNowPlaying()
    {
        try
        {
            // Enable remote command center to register as a media player
            var commandCenterClass = objc_getClass("MPRemoteCommandCenter");
            if (commandCenterClass == IntPtr.Zero)
            {
                Console.WriteLine("MPRemoteCommandCenter not available");
                return;
            }

            var sharedCommandCenterSel = sel_registerName("sharedCommandCenter");
            var commandCenter = objc_msgSend(commandCenterClass, sharedCommandCenterSel);
            if (commandCenter == IntPtr.Zero)
            {
                Console.WriteLine("Failed to get shared command center");
                return;
            }

            // Enable play/pause command
            var playCommandSel = sel_registerName("playCommand");
            var pauseCommandSel = sel_registerName("pauseCommand");
            var nextCommandSel = sel_registerName("nextTrackCommand");
            var prevCommandSel = sel_registerName("previousTrackCommand");

            var playCommand = objc_msgSend(commandCenter, playCommandSel);
            var pauseCommand = objc_msgSend(commandCenter, pauseCommandSel);
            var nextCommand = objc_msgSend(commandCenter, nextCommandSel);
            var prevCommand = objc_msgSend(commandCenter, prevCommandSel);

            var setEnabledSel = sel_registerName("setEnabled:");
            if (playCommand != IntPtr.Zero)
                objc_msgSend_Int(playCommand, setEnabledSel, 1);
            if (pauseCommand != IntPtr.Zero)
                objc_msgSend_Int(pauseCommand, setEnabledSel, 1);
            if (nextCommand != IntPtr.Zero)
                objc_msgSend_Int(nextCommand, setEnabledSel, 1);
            if (prevCommand != IntPtr.Zero)
                objc_msgSend_Int(prevCommand, setEnabledSel, 1);

            Console.WriteLine("Remote Command Center initialized");

            // Set initial now playing info
            UpdateNowPlayingInfo("NoPlayer", "Unknown", 0, 0);
            UpdatePlaybackState(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize now playing: {ex.Message}");
        }
    }

    private void StartEventTap()
    {
        _callback = EventTapCallback;
        _callbackHandle = GCHandle.Alloc(_callback);
        _cts = new CancellationTokenSource();

        _messageLoopTask = Task.Run(() =>
        {
            try
            {
                // Create event mask for NSSystemDefined events (which include media keys)
                ulong eventMask = 1UL << NSSystemDefined;

                _eventTap = CGEventTapCreate(
                    kCGSessionEventTap,
                    kCGHeadInsertEventTap,
                    kCGEventTapOptionDefault, // Intercept events to prevent other apps from receiving them
                    eventMask,
                    _callback,
                    IntPtr.Zero);

                if (_eventTap == IntPtr.Zero)
                {
                    Console.WriteLine("Failed to create event tap. You may need to grant Accessibility permissions in System Settings > Privacy & Security > Accessibility");
                    return;
                }

                _runLoopSource = CFMachPortCreateRunLoopSource(IntPtr.Zero, _eventTap, 0);
                if (_runLoopSource == IntPtr.Zero)
                {
                    Console.WriteLine("Failed to create run loop source");
                    // Clean up event tap if run loop source creation fails
                    CFRelease(_eventTap);
                    _eventTap = IntPtr.Zero;
                    return;
                }

                _runLoop = CFRunLoopGetCurrent();

                // Get kCFRunLoopCommonModes - it's a global constant
                var commonModes = GetCFRunLoopCommonModes();

                CFRunLoopAddSource(_runLoop, _runLoopSource, commonModes);
                CGEventTapEnable(_eventTap, true);

                Console.WriteLine("Media key event tap started successfully");

                // Run the loop
                CFRunLoopRun();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Event tap error: {ex.Message}");
            }
        }, _cts.Token);
    }

    private IntPtr GetCFRunLoopCommonModes()
    {
        // kCFRunLoopCommonModes is a global symbol
        // Store the handle for cleanup
        _coreFoundationHandle = NativeLibrary.Load("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation");
        var ptr = NativeLibrary.GetExport(_coreFoundationHandle, "kCFRunLoopCommonModes");
        return Marshal.ReadIntPtr(ptr);
    }

    private IntPtr EventTapCallback(IntPtr proxy, uint type, IntPtr eventRef, IntPtr userInfo)
    {
        try
        {
            if (type != NSSystemDefined)
                return eventRef;

            // Create NSEvent from CGEvent to properly access system-defined event data
            var nsEventClass = objc_getClass("NSEvent");
            var eventWithCGEventSel = sel_registerName("eventWithCGEvent:");
            var nsEvent = objc_msgSend_IntPtr(nsEventClass, eventWithCGEventSel, eventRef);

            if (nsEvent == IntPtr.Zero)
            {
                Console.WriteLine("Failed to create NSEvent from CGEvent");
                return eventRef;
            }

            // Get subtype
            var subtypeSel = sel_registerName("subtype");
            var subtype = (short)objc_msgSend(nsEvent, subtypeSel);

            // Check if this is a media key event (subtype 8 = AUX control buttons)
            if (subtype != NX_SUBTYPE_AUX_CONTROL_BUTTONS)
            {
                return eventRef;
            }

            // Get data1 which contains the key code and flags
            var data1Sel = sel_registerName("data1");
            var data1 = (long)objc_msgSend(nsEvent, data1Sel);

            // Extract key info from data1
            // Format: keyCode in bits 16-23, keyFlags in bits 0-15
            // keyState: 0x0A = key down, 0x0B = key up
            int keyCode = (int)((data1 >> 16) & 0xFF);
            int keyFlags = (int)(data1 & 0xFFFF);
            int keyState = (keyFlags >> 8) & 0xFF;
            bool keyDown = keyState == 0x0A;
            bool keyUp = keyState == 0x0B;

            Console.WriteLine($"Media key event - KeyCode: {keyCode}, KeyState: 0x{keyState:X}, KeyDown: {keyDown}");

            // Only process key down events
            if (keyDown)
            {
                bool handled = false;
                switch (keyCode)
                {
                    case NX_KEYTYPE_PLAY:
                        Console.WriteLine("Play/Pause key pressed");
                        PlayPausePressed?.Invoke(this, EventArgs.Empty);
                        handled = true;
                        break;
                    case NX_KEYTYPE_NEXT:
                    case NX_KEYTYPE_FAST:
                        Console.WriteLine("Next key pressed");
                        NextPressed?.Invoke(this, EventArgs.Empty);
                        handled = true;
                        break;
                    case NX_KEYTYPE_PREVIOUS:
                    case NX_KEYTYPE_REWIND:
                        Console.WriteLine("Previous key pressed");
                        PreviousPressed?.Invoke(this, EventArgs.Empty);
                        handled = true;
                        break;
                }

                // Return null to consume the event and prevent other apps (like Apple Music) from receiving it
                if (handled)
                    return IntPtr.Zero;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in event tap callback: {ex.Message}");
        }

        return eventRef;
    }

    public void UpdatePlaybackState(bool isPlaying)
    {
        _isPlaying = isPlaying;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;

        try
        {
            var infoCenterClass = objc_getClass("MPNowPlayingInfoCenter");
            if (infoCenterClass == IntPtr.Zero) return;

            var defaultCenterSel = sel_registerName("defaultCenter");
            var infoCenter = objc_msgSend(infoCenterClass, defaultCenterSel);
            if (infoCenter == IntPtr.Zero) return;

            var setPlaybackStateSel = sel_registerName("setPlaybackState:");
            objc_msgSend_Int(infoCenter, setPlaybackStateSel,
                isPlaying ? MPNowPlayingPlaybackStatePlaying : MPNowPlayingPlaybackStatePaused);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to update playback state: {ex.Message}");
        }
    }

    public void UpdateNowPlayingInfo(string title, string artist, double duration, double currentTime)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;

        try
        {
            var infoCenterClass = objc_getClass("MPNowPlayingInfoCenter");
            if (infoCenterClass == IntPtr.Zero)
            {
                Console.WriteLine("MPNowPlayingInfoCenter not available");
                return;
            }

            var defaultCenterSel = sel_registerName("defaultCenter");
            var infoCenter = objc_msgSend(infoCenterClass, defaultCenterSel);
            if (infoCenter == IntPtr.Zero)
            {
                Console.WriteLine("Failed to get default center");
                return;
            }

            // Create dictionary
            var dictClass = objc_getClass("NSMutableDictionary");
            var allocSel = sel_registerName("alloc");
            var initSel = sel_registerName("init");
            var dict = objc_msgSend(objc_msgSend(dictClass, allocSel), initSel);

            // Use NSString keys - simpler approach
            SetDictionaryValue(dict, CreateNSString("kMPMediaItemPropertyTitle"), CreateNSString(title));
            SetDictionaryValue(dict, CreateNSString("kMPMediaItemPropertyArtist"), CreateNSString(artist ?? "Unknown"));

            if (duration > 0)
            {
                var nsNumberClass = objc_getClass("NSNumber");
                var durationNum = objc_msgSend_Double(nsNumberClass, sel_registerName("numberWithDouble:"), duration);
                var currentTimeNum = objc_msgSend_Double(nsNumberClass, sel_registerName("numberWithDouble:"), currentTime);
                var rateNum = objc_msgSend_Double(nsNumberClass, sel_registerName("numberWithDouble:"), _isPlaying ? 1.0 : 0.0);

                SetDictionaryValue(dict, CreateNSString("kMPMediaItemPropertyPlaybackDuration"), durationNum);
                SetDictionaryValue(dict, CreateNSString("kMPNowPlayingInfoPropertyElapsedPlaybackTime"), currentTimeNum);
                SetDictionaryValue(dict, CreateNSString("kMPNowPlayingInfoPropertyPlaybackRate"), rateNum);
            }

            var setNowPlayingInfoSel = sel_registerName("setNowPlayingInfo:");
            objc_msgSend_IntPtr(infoCenter, setNowPlayingInfoSel, dict);

            Console.WriteLine($"Now playing info updated: {title} - {artist}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to update now playing info: {ex.Message}");
        }
    }

    private void SetDictionaryValue(IntPtr dict, IntPtr key, IntPtr value)
    {
        objc_msgSend_IntPtr_IntPtr(dict, sel_registerName("setObject:forKey:"), value, key);
    }

    private IntPtr CreateNSString(string str)
    {
        var nsStringClass = objc_getClass("NSString");
        var ptr = Marshal.StringToHGlobalAnsi(str);
        try
        {
            return objc_msgSend_IntPtr(nsStringClass, sel_registerName("stringWithUTF8String:"), ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();

        // Stop the run loop first to allow the background task to exit
        if (_runLoop != IntPtr.Zero)
        {
            CFRunLoopStop(_runLoop);
        }

        // Wait for the background task to complete with a timeout
        try
        {
            _messageLoopTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Task was cancelled or failed, which is expected during shutdown
        }

        // Clean up macOS resources
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Remove the run loop source before releasing
            if (_runLoopSource != IntPtr.Zero && _runLoop != IntPtr.Zero)
            {
                var commonModes = IntPtr.Zero;
                try
                {
                    if (_coreFoundationHandle != IntPtr.Zero)
                    {
                        var ptr = NativeLibrary.GetExport(_coreFoundationHandle, "kCFRunLoopCommonModes");
                        commonModes = Marshal.ReadIntPtr(ptr);
                    }
                }
                catch { /* Ignore errors getting common modes during cleanup */ }

                if (commonModes != IntPtr.Zero)
                {
                    CFRunLoopRemoveSource(_runLoop, _runLoopSource, commonModes);
                }
            }

            // Disable and release the event tap
            if (_eventTap != IntPtr.Zero)
            {
                CGEventTapEnable(_eventTap, false);
                CFRelease(_eventTap);
                _eventTap = IntPtr.Zero;
            }

            // Release the run loop source
            if (_runLoopSource != IntPtr.Zero)
            {
                CFRelease(_runLoopSource);
                _runLoopSource = IntPtr.Zero;
            }

            // Free the GC handle for the callback
            if (_callbackHandle.IsAllocated)
            {
                _callbackHandle.Free();
            }

            // Close the MediaPlayer framework handle
            if (_mediaPlayerHandle != IntPtr.Zero)
            {
                dlclose(_mediaPlayerHandle);
                _mediaPlayerHandle = IntPtr.Zero;
            }

            // Free the CoreFoundation library handle
            if (_coreFoundationHandle != IntPtr.Zero)
            {
                NativeLibrary.Free(_coreFoundationHandle);
                _coreFoundationHandle = IntPtr.Zero;
            }
        }

        _runLoop = IntPtr.Zero;
        _cts?.Dispose();
    }
}
