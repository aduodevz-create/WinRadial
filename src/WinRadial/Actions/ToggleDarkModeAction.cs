using Microsoft.Win32;
using WinRadial.Core;

namespace WinRadial.Actions;

/// <summary>
/// Toggles Windows dark/light mode by writing to HKCU registry.
/// Scoped to HKCU only, read-before-write, wrapped in try/catch.
/// Supports Windows 10 1903+ and Windows 11.
/// </summary>
public sealed class ToggleDarkModeAction : IWheelAction
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsThemeValue = "AppsUseLightTheme";
    private const string SystemThemeValue = "SystemUsesLightTheme";

    private readonly LogService _log;

    public string Id => "toggle_dark_mode";
    public string Label { get; }
    public string IconKey { get; }
    public bool HasSubmenu => false;

    public ToggleDarkModeAction(string label, string iconKey, LogService log)
    {
        Label = label;
        IconKey = iconKey;
        _log = log;
    }

    public Task ExecuteAsync()
    {
        try
        {
            // HKCU only — read current value before writing
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey, writable: true);
            if (key == null)
            {
                _log.Warning("Dark mode registry key not found — Windows 10 1903+ required.");
                return Task.CompletedTask;
            }

            // Read current state (1 = light, 0 = dark)
            var currentValue = key.GetValue(AppsThemeValue) as int?;
            var newValue = (currentValue == 0) ? 1 : 0; // Toggle

            // Write both app and system theme
            key.SetValue(AppsThemeValue, newValue, RegistryValueKind.DWord);
            key.SetValue(SystemThemeValue, newValue, RegistryValueKind.DWord);

            var mode = newValue == 0 ? "Dark" : "Light";
            _log.Info($"Toggled to {mode} mode.");

            // Broadcast settings change to refresh UI
            WindowInterop.SystemParametersInfo(
                WindowInterop.SPI_SETDESKWALLPAPER, 0, IntPtr.Zero,
                WindowInterop.SPIF_UPDATEINIFILE | WindowInterop.SPIF_SENDCHANGE);
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to toggle dark mode: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<IWheelAction> GetSubActions() => [];
}
