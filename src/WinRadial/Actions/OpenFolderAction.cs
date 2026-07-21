using System.Diagnostics;
using WinRadial.Core;

namespace WinRadial.Actions;

/// <summary>
/// Opens a folder in Windows Explorer with validated path.
/// </summary>
public sealed class OpenFolderAction : IWheelAction
{
    private readonly string _path;
    private readonly LogService _log;

    public string Id => "open_folder";
    public string Label { get; }
    public string IconKey { get; }
    public bool HasSubmenu => false;

    public OpenFolderAction(string label, string iconKey, string path, LogService log)
    {
        Label = label;
        IconKey = iconKey;
        _path = path;
        _log = log;
    }

    public Task ExecuteAsync()
    {
        try
        {
            var (isValid, resolvedPath, error) = SecurityValidator.ValidateExecutablePath(_path);
            if (!isValid)
            {
                _log.Warning($"Folder open blocked — {error}");
                return Task.CompletedTask;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = true,
            };
            psi.ArgumentList.Add(resolvedPath!);

            Process.Start(psi);
            _log.Info($"Opened folder: {resolvedPath}");
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to open folder '{_path}': {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<IWheelAction> GetSubActions() => [];
}
