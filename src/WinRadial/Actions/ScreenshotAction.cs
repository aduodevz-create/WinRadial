using System.Diagnostics;
using WinRadial.Core;

namespace WinRadial.Actions;

/// <summary>
/// Opens the Windows Snipping Tool (Snip & Sketch) overlay using ms-screenclip: URI.
/// </summary>
public sealed class ScreenshotAction : IWheelAction
{
    private readonly LogService _log;

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

            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-screenclip:",
                UseShellExecute = true
            });
            _log.Info("Opened Windows Snipping Tool overlay.");
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to open Snipping Tool: {ex.Message}");
        }
    }

    public IReadOnlyList<IWheelAction> GetSubActions() => [];
}
