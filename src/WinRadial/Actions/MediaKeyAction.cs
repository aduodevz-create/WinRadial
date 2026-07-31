using System.Runtime.InteropServices;
using WinRadial.Core;

namespace WinRadial.Actions;

/// <summary>
/// Simulates media and volume key presses.
/// </summary>
public sealed class MediaKeyAction : IWheelAction
{
    private readonly LogService _log;
    private readonly string? _keyType;

    public string Id => "media_key";
    public string Label { get; }
    public string IconKey { get; }
    public bool HasSubmenu => false;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

    private const byte VK_VOLUME_MUTE = 0xAD;
    private const byte VK_VOLUME_DOWN = 0xAE;
    private const byte VK_VOLUME_UP = 0xAF;
    private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
    private const byte VK_MEDIA_PREV_TRACK = 0xB1;
    private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;

    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public MediaKeyAction(string label, string iconKey, string? arguments, LogService log)
    {
        Label = label;
        IconKey = iconKey;
        _keyType = arguments?.ToLowerInvariant();
        _log = log;
    }

    public Task ExecuteAsync()
    {
        byte vk = _keyType switch
        {
            "volume_up" => VK_VOLUME_UP,
            "volume_down" => VK_VOLUME_DOWN,
            "volume_mute" => VK_VOLUME_MUTE,
            "next_track" => VK_MEDIA_NEXT_TRACK,
            "prev_track" => VK_MEDIA_PREV_TRACK,
            "play_pause" => VK_MEDIA_PLAY_PAUSE,
            _ => 0
        };

        if (vk != 0)
        {
            try
            {
                keybd_event(vk, 0, KEYEVENTF_EXTENDEDKEY, 0);
                keybd_event(vk, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
                _log.Info($"Simulated media key: {_keyType}");
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to simulate media key: {ex.Message}");
            }
        }
        else
        {
            _log.Warning($"Unknown media key type: {_keyType}");
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<IWheelAction> GetSubActions() => [];
}
