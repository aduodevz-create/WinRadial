using System.IO;
using System.Reflection;
using System.Text.Json;

namespace WinRadial.Core;

/// <summary>
/// Loads and validates WinRadial configuration from %APPDATA%\WinRadial\config.json.
/// Falls back to embedded default config on any validation failure.
/// Validates action IDs against the registered action set.
/// </summary>
public sealed class ConfigService
{
    private readonly LogService _log;
    private readonly string _configDirectory;
    private readonly string _configFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public string ConfigDirectory => _configDirectory;

    public ConfigService(LogService log)
    {
        _log = log;
        _configDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinRadial");
        _configFilePath = Path.Combine(_configDirectory, "config.json");
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
    }

    /// <summary>
    /// Loads config from disk. On any failure, falls back to safe built-in defaults.
    /// </summary>
    public WinRadialConfig Load()
    {
        try
        {
            // Ensure config directory exists
            Directory.CreateDirectory(_configDirectory);

            // If user config doesn't exist, write the default
            if (!File.Exists(_configFilePath))
            {
                _log.Info("No user config found — writing default config.");
                WriteDefaultConfig();
            }

            var json = File.ReadAllText(_configFilePath);
            var config = JsonSerializer.Deserialize<WinRadialConfig>(json, _jsonOptions);

            if (config == null)
            {
                _log.Warning("Config deserialized to null — using defaults.");
                return LoadEmbeddedDefault();
            }

            // Validate the loaded config
            var (isValid, errors) = Validate(config);
            if (!isValid)
            {
                foreach (var err in errors)
                {
                    _log.Warning($"Config validation: {err}");
                }
                _log.Warning("Config validation failed — falling back to defaults.");
                return LoadEmbeddedDefault();
            }

            return config;
        }
        catch (JsonException ex)
        {
            _log.Error($"Malformed config JSON: {ex.Message} — using defaults.");
            return LoadEmbeddedDefault();
        }
        catch (Exception ex)
        {
            _log.Error($"Config load error: {ex.Message} — using defaults.");
            return LoadEmbeddedDefault();
        }
    }

    /// <summary>
    /// Validates config against business rules (field presence, value ranges, action ID whitelist).
    /// </summary>
    public (bool IsValid, List<string> Errors) Validate(WinRadialConfig config)
    {
        var errors = new List<string>();

        // Hotkey validation
        if (string.IsNullOrWhiteSpace(config.Hotkey.Modifiers))
            errors.Add("hotkey.modifiers is required.");
        if (string.IsNullOrWhiteSpace(config.Hotkey.Key))
            errors.Add("hotkey.key is required.");

        // Categories
        if (config.Categories.Count == 0)
            errors.Add("At least one category is required.");

        foreach (var category in config.Categories)
        {
            if (string.IsNullOrWhiteSpace(category.Name))
                errors.Add("Category name is required.");

            if (category.Slots.Count > 8)
                errors.Add($"Category '{category.Name}' has {category.Slots.Count} slots (max 8).");

            foreach (var slot in category.Slots)
            {
                var (idValid, idError) = SecurityValidator.ValidateActionId(slot.ActionId);
                if (!idValid)
                    errors.Add($"Category '{category.Name}': {idError}");

                // Validate paths for app_launch and open_folder
                if (slot.ActionId is "app_launch" or "open_folder")
                {
                    if (string.IsNullOrWhiteSpace(slot.Path))
                    {
                        errors.Add($"Category '{category.Name}', action '{slot.ActionId}': path is required.");
                    }
                    // Note: We validate path existence at execution time, not config load time,
                    // since apps may be installed/uninstalled between config loads.
                }

                // Validate children recursively
                if (slot.Children != null)
                {
                    foreach (var child in slot.Children)
                    {
                        var (childValid, childError) = SecurityValidator.ValidateActionId(child.ActionId);
                        if (!childValid)
                            errors.Add($"Submenu child: {childError}");
                    }
                }
            }
        }

        // Appearance validation
        var appearance = config.Appearance;
        ValidateRange(appearance.InnerRadius, 20, 200, "appearance.innerRadius", errors);
        ValidateRange(appearance.OuterRadius, 100, 500, "appearance.outerRadius", errors);
        ValidateRange(appearance.SubMenuRadius, 150, 600, "appearance.subMenuRadius", errors);
        ValidateRange(appearance.Opacity, 0.1, 1.0, "appearance.opacity", errors);

        if (appearance.InnerRadius >= appearance.OuterRadius)
            errors.Add("appearance.innerRadius must be less than appearance.outerRadius.");
        if (appearance.OuterRadius >= appearance.SubMenuRadius)
            errors.Add("appearance.outerRadius must be less than appearance.subMenuRadius.");

        return (errors.Count == 0, errors);
    }

    private static void ValidateRange(double value, double min, double max, string field, List<string> errors)
    {
        var (isValid, error) = SecurityValidator.ValidateRange(value, min, max, field);
        if (!isValid) errors.Add(error!);
    }

    /// <summary>
    /// Loads the embedded default config from assembly resources.
    /// </summary>
    public WinRadialConfig LoadEmbeddedDefault()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("WinRadial.Resources.config.json");
            if (stream != null)
            {
                var config = JsonSerializer.Deserialize<WinRadialConfig>(stream, _jsonOptions);
                if (config != null) return config;
            }
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to load embedded default config: {ex.Message}");
        }

        // Ultimate fallback: hardcoded minimal config
        _log.Warning("Using hardcoded minimal config as final fallback.");
        return CreateMinimalFallback();
    }

    private static WinRadialConfig CreateMinimalFallback()
    {
        return new WinRadialConfig
        {
            Hotkey = new HotkeyConfig { Modifiers = "Ctrl+Alt", Key = "Space" },
            Categories =
            [
                new CategoryConfig
                {
                    Name = "Quick Actions",
                    IconKey = "\uE7C1",
                    Slots =
                    [
                        new ActionSlotConfig { ActionId = "lock_screen", Label = "Lock", IconKey = "\uE72E" },
                        new ActionSlotConfig { ActionId = "screenshot", Label = "Screenshot", IconKey = "\uE722" },
                        new ActionSlotConfig { ActionId = "toggle_dark_mode", Label = "Dark Mode", IconKey = "\uE708" },
                        new ActionSlotConfig { ActionId = "empty_recycle_bin", Label = "Empty Bin", IconKey = "\uE74D" },
                    ]
                }
            ],
            Appearance = new AppearanceConfig()
        };
    }

    /// <summary>
    /// Writes the embedded default config to the user's config directory.
    /// </summary>
    private void WriteDefaultConfig()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("WinRadial.Resources.config.json");
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                File.WriteAllText(_configFilePath, json);
                _log.Info($"Default config written to {_configFilePath}");
                return;
            }

            // If embedded resource missing, write minimal JSON
            var fallbackJson = JsonSerializer.Serialize(CreateMinimalFallback(), new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            File.WriteAllText(_configFilePath, fallbackJson);
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to write default config: {ex.Message}");
        }
    }
}
