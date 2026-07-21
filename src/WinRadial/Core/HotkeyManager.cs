using System.Windows.Input;
using System.Windows.Interop;

namespace WinRadial.Core;

/// <summary>
/// Manages a global hotkey registration using RegisterHotKey P/Invoke.
/// Implements IDisposable for clean unregistration on shutdown.
/// Includes debounce to prevent double-fire within 200ms.
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    private const int HOTKEY_ID = 0x0001;
    private const int DEBOUNCE_MS = 200;

    private readonly LogService _log;
    private HotkeyConfig _config;
    private HwndSource? _hwndSource;
    private DateTime _lastFired = DateTime.MinValue;
    private bool _registered;
    private bool _disposed;

    public event EventHandler? HotkeyPressed;

    public HotkeyManager(HotkeyConfig config, LogService log)
    {
        _config = config;
        _log = log;
    }

    /// <summary>
    /// Registers the global hotkey. Must be called from the UI thread.
    /// </summary>
    public void Register()
    {
        if (_disposed) return;

        // Create a hidden window to receive WM_HOTKEY messages
        var parameters = new HwndSourceParameters("WinRadialHotkeyWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0, // Hidden
        };

        _hwndSource = new HwndSource(parameters);
        _hwndSource.AddHook(WndProc);

        var (modifiers, key) = ParseHotkey(_config);

        _registered = WindowInterop.RegisterHotKey(
            _hwndSource.Handle, HOTKEY_ID, modifiers | WindowInterop.MOD_NOREPEAT, key);

        if (_registered)
        {
            _log.Info($"Global hotkey registered: {_config.Modifiers}+{_config.Key}");
        }
        else
        {
            var err = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
            _log.Error($"Failed to register global hotkey (error {err}). Another app may have claimed {_config.Modifiers}+{_config.Key}.");
        }
    }

    /// <summary>
    /// Unregisters the current hotkey and releases the hidden window.
    /// </summary>
    public void Unregister()
    {
        if (_hwndSource != null && _registered)
        {
            WindowInterop.UnregisterHotKey(_hwndSource.Handle, HOTKEY_ID);
            _registered = false;
            _log.Info("Global hotkey unregistered.");
        }

        _hwndSource?.RemoveHook(WndProc);
        _hwndSource?.Dispose();
        _hwndSource = null;
    }

    /// <summary>
    /// Updates the hotkey config (call Unregister first, then Register after).
    /// </summary>
    public void UpdateHotkey(HotkeyConfig config)
    {
        _config = config;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WindowInterop.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            // Debounce: ignore if fired within 200ms
            var now = DateTime.UtcNow;
            if ((now - _lastFired).TotalMilliseconds >= DEBOUNCE_MS)
            {
                _lastFired = now;
                HotkeyPressed?.Invoke(this, EventArgs.Empty);
            }
            handled = true;
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Parses modifier string ("Ctrl+Alt") and key string ("Space") into Win32 constants.
    /// </summary>
    private static (uint Modifiers, uint VirtualKey) ParseHotkey(HotkeyConfig config)
    {
        uint modifiers = 0;
        var modParts = config.Modifiers.Split('+', StringSplitOptions.TrimEntries);
        foreach (var mod in modParts)
        {
            modifiers |= mod.ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => WindowInterop.MOD_CONTROL,
                "ALT" => WindowInterop.MOD_ALT,
                "SHIFT" => WindowInterop.MOD_SHIFT,
                "WIN" or "WINDOWS" => WindowInterop.MOD_WIN,
                _ => 0
            };
        }

        // Parse the key using WPF's Key enum, then convert to Win32 VK
        if (Enum.TryParse<Key>(config.Key, ignoreCase: true, out var wpfKey))
        {
            var vk = (uint)KeyInterop.VirtualKeyFromKey(wpfKey);
            return (modifiers, vk);
        }

        // Fallback: try direct VK code for special keys
        var vkFallback = config.Key.ToUpperInvariant() switch
        {
            "SPACE" => 0x20u,
            "ENTER" or "RETURN" => 0x0Du,
            "TAB" => 0x09u,
            "ESCAPE" or "ESC" => 0x1Bu,
            _ => 0x20u // Default to Space
        };

        return (modifiers, vkFallback);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Unregister();
    }
}
