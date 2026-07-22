using System.Diagnostics;
using System.IO;
using WinRadial.Core;

namespace WinRadial.Actions;

/// <summary>
/// Launches an application with validated path and ArgumentList (never concatenated command strings).
/// Supports submenu children for grouping related apps under one wedge.
/// </summary>
public sealed class AppLaunchAction : IWheelAction
{
    private readonly string _path;
    private readonly string? _arguments;
    private readonly List<ActionSlotConfig>? _children;
    private readonly ActionRegistry _registry;
    private readonly LogService _log;

    public string Id => "app_launch";
    public string Label { get; }
    public string IconKey { get; }
    public string Path => _path;
    public bool HasSubmenu => _children is { Count: > 0 };

    public AppLaunchAction(string label, string iconKey, string path,
        string? arguments, List<ActionSlotConfig>? children,
        ActionRegistry registry, LogService log)
    {
        Label = label;
        IconKey = iconKey;
        _path = path;
        _arguments = arguments;
        _children = children;
        _registry = registry;
        _log = log;
    }

    public async Task ExecuteAsync()
    {
        if (HasSubmenu)
        {
            // Submenu actions are handled by the UI layer opening the submenu ring
            return;
        }

        try
        {
            // Security: validate path before launching
            var (isValid, resolvedPath, error) = SecurityValidator.ValidateExecutablePath(_path);
            if (!isValid)
            {
                _log.Warning($"App launch blocked — {error}");
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = resolvedPath!,
                UseShellExecute = true, // Let Windows handle the file association
            };

            // Use ArgumentList for safe argument passing (never concatenated)
            if (!string.IsNullOrWhiteSpace(_arguments))
            {
                // Split on spaces, respecting quoted strings
                foreach (var arg in SplitArguments(_arguments))
                {
                    psi.ArgumentList.Add(arg);
                }
            }

            Process.Start(psi);
            _log.Info($"Launched: {resolvedPath}");
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to launch '{_path}': {ex.Message}");
        }

        await Task.CompletedTask;
    }

    public IReadOnlyList<IWheelAction> GetSubActions()
    {
        if (_children == null) return [];
        return _registry.CreateFromSlots(_children);
    }

    /// <summary>
    /// Splits argument string respecting quoted segments.
    /// </summary>
    private static IEnumerable<string> SplitArguments(string args)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var c in args)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ' ' && !inQuotes)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
            result.Add(current.ToString());

        return result;
    }
}
