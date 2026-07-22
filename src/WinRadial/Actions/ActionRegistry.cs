using WinRadial.Core;

namespace WinRadial.Actions;

/// <summary>
/// Factory that creates IWheelAction instances from config.
/// Maintains the fixed set of valid action IDs and their constructors.
/// </summary>
public sealed class ActionRegistry
{
    private readonly LogService _log;
    private readonly ConfigService _configService;

    public ActionRegistry(LogService log, ConfigService configService)
    {
        _log = log;
        _configService = configService;
    }

    /// <summary>
    /// Creates an IWheelAction from a config slot definition.
    /// Returns null for unknown action IDs (already validated at config load time).
    /// </summary>
    public IWheelAction? Create(ActionSlotConfig slot)
    {
        return slot.ActionId.ToLowerInvariant() switch
        {
            "lock_screen" => new LockScreenAction(
                slot.Label ?? "Lock",
                slot.IconKey ?? "\uE72E"),

            "sleep" => new SleepAction(
                slot.Label ?? "Sleep",
                slot.IconKey ?? "\uE708"),

            "toggle_dark_mode" => new ToggleDarkModeAction(
                slot.Label ?? "Dark Mode",
                slot.IconKey ?? "\uE793",
                _log),

            "empty_recycle_bin" => new EmptyRecycleBinAction(
                slot.Label ?? "Empty Bin",
                slot.IconKey ?? "\uE74D",
                _log),

            "screenshot" => new ScreenshotAction(
                slot.Label ?? "Screenshot",
                slot.IconKey ?? "\uE722",
                _log),

            "app_launch" => new AppLaunchAction(
                slot.Label ?? "App",
                slot.IconKey ?? "\uE737",
                slot.Path ?? "",
                slot.Arguments,
                slot.Children,
                this,
                _log),

            "open_folder" => new OpenFolderAction(
                slot.Label ?? "Folder",
                slot.IconKey ?? "\uE838",
                slot.Path ?? "",
                _log),

            "add_program" => new AddProgramAction(
                slot.Label ?? "Add Program",
                slot.IconKey ?? "\uE710",
                _configService,
                _log),

            "separator" => null, // Separators are visual-only, no action

            _ => null
        };
    }

    /// <summary>
    /// Creates a list of IWheelAction from a category's slot definitions.
    /// Filters out null results (separators, unknown IDs).
    /// </summary>
    public List<IWheelAction> CreateFromSlots(IEnumerable<ActionSlotConfig> slots)
    {
        var actions = new List<IWheelAction>();
        foreach (var slot in slots)
        {
            var action = Create(slot);
            if (action != null)
                actions.Add(action);
        }
        return actions;
    }
}
