using System.Diagnostics;
using WinRadial.Core;

namespace WinRadial.Actions;

/// <summary>
/// Opens the Windows Snipping Tool (Snip & Sketch) overlay using explorer.exe ms-screenclip: URI.
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

            // Using explorer.exe to launch the URI bypasses the issue where UWP apps 
            // fail to launch when called directly from an Administrator/elevated process.
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "ms-screenclip:",
                UseShellExecute = true
            });
            
            _log.Info("Launched Snipping Tool via explorer.exe ms-screenclip:");
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to open Snipping Tool: {ex.Message}");
        }
    }

    public IReadOnlyList<IWheelAction> GetSubActions() => [];
}
