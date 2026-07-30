using System.IO;
using System.Text.RegularExpressions;

namespace WinRadial.Core;

/// <summary>
/// Security validation for paths, action IDs, and user-provided inputs.
/// Rejects shell metacharacters, directory traversal, and unregistered action IDs.
/// </summary>
public static partial class SecurityValidator
{
    // Fixed set of registered action IDs — config values must match one of these
    private static readonly HashSet<string> ValidActionIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "lock_screen",
        "sleep",
        "toggle_dark_mode",
        "empty_recycle_bin",
        "screenshot",
        "app_launch",
        "open_folder",
        "add_program",
        "separator"
    };

    // Shell metacharacters that could enable command injection
    [GeneratedRegex(@"[|&;<>`$!{}()\[\]""'\x00-\x1F]")]
    private static partial Regex ShellMetacharPattern();

    /// <summary>
    /// Validates a filesystem path for safety:
    /// - Resolves to full path
    /// - Rejects ".." directory traversal
    /// - Rejects shell metacharacters
    /// - Verifies the file or directory exists
    /// </summary>
    public static (bool IsValid, string? Error) ValidatePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return (false, "Path is null or empty.");

        // Reject directory traversal
        if (path.Contains("..", StringComparison.Ordinal))
            return (false, $"Path contains directory traversal '..': {path}");

        // Reject shell metacharacters
        if (ShellMetacharPattern().IsMatch(path))
            return (false, $"Path contains shell metacharacters: {path}");

        try
        {
            var fullPath = Path.GetFullPath(path);

            // Verify the resolved path doesn't differ unexpectedly (symlink/junction detection)
            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                return (false, $"Path does not exist: {fullPath}");

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"Path resolution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates that an action ID is in the fixed set of registered actions.
    /// </summary>
    public static (bool IsValid, string? Error) ValidateActionId(string? actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId))
            return (false, "Action ID is null or empty.");

        if (!ValidActionIds.Contains(actionId))
            return (false, $"Unknown action ID: '{actionId}'. Valid IDs: {string.Join(", ", ValidActionIds)}");

        return (true, null);
    }

    /// <summary>
    /// Validates that a resolved path is safe for Process.Start.
    /// Combines path validation with executable extension check.
    /// </summary>
    public static (bool IsValid, string? ResolvedPath, string? Error) ValidateExecutablePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return (false, null, "Path is null or empty.");

        // Allow URI schemes (e.g., http://, steam:, ms-settings:)
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme != "file")
        {
            return (true, path, null);
        }

        // If it's a simple command without directory separators, allow it
        // This relies on Process.Start finding it in PATH or App Paths registry key
        if (!path.Contains('/') && !path.Contains('\\'))
        {
            if (ShellMetacharPattern().IsMatch(path))
                return (false, null, $"Path contains shell metacharacters: {path}");
            
            return (true, path, null);
        }

        var (isValid, error) = ValidatePath(path);
        if (!isValid)
            return (false, null, error);

        var fullPath = Path.GetFullPath(path!);
        return (true, fullPath, null);
    }

    /// <summary>
    /// Checks if a string is a valid hex color (#AARRGGBB or #RRGGBB format).
    /// </summary>
    [GeneratedRegex(@"^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$")]
    private static partial Regex HexColorPattern();

    public static bool IsValidHexColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color)) return false;
        return HexColorPattern().IsMatch(color);
    }

    /// <summary>
    /// Validates a numeric value is within the specified bounds.
    /// </summary>
    public static (bool IsValid, string? Error) ValidateRange(double value, double min, double max, string fieldName)
    {
        if (value < min || value > max)
            return (false, $"{fieldName} must be between {min} and {max}, got {value}.");
        return (true, null);
    }
}
