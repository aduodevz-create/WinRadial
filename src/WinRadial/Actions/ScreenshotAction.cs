using System.Diagnostics;
using System.Runtime.InteropServices;
using WinRadial.Core;

namespace WinRadial.Actions;

/// <summary>
/// Opens the Windows Snipping Tool (Snip & Sketch) overlay using Win+Shift+S simulation.
/// </summary>
public sealed class ScreenshotAction : IWheelAction
{
    private readonly LogService _log;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

    private const byte VK_LWIN = 0x5B;
    private const byte VK_SHIFT = 0x10;
    private const byte VK_S = 0x53;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public string Id => "screenshot";
    public string Label { get; }
    public string IconKey { get; }
    public bool HasSubmenu => false;
    public bool CloseWheelOnExecute => true;

    public ScreenshotAction(string label, string iconKey, LogService log)
    {
        Label = label;
        IconKey = iconKey;
        _log = log;
    }

    public async Task ExecuteAsync()
    {
        try
        {
            // Give the UI a moment to completely hide the radial menu
            // before the Snipping Tool captures the screen.
            await Task.Delay(150);

            // Simulate Win + Shift + S
            keybd_event(VK_LWIN, 0, 0, 0);
            keybd_event(VK_SHIFT, 0, 0, 0);
            keybd_event(VK_S, 0, 0, 0);

            keybd_event(VK_S, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, 0);

            _log.Info("Simulated Win+Shift+S to open Snipping Tool overlay.");
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to simulate Win+Shift+S: {ex.Message}");
        }
    }

    public IReadOnlyList<IWheelAction> GetSubActions() => [];
}
