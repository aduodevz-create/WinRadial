namespace WinRadial.Core;

/// <summary>
/// Configuration POCO models deserialized from config.json.
/// Immutable records for thread safety.
/// </summary>

public sealed class WinRadialConfig
{
    public HotkeyConfig Hotkey { get; init; } = new();
    public List<CategoryConfig> Categories { get; init; } = [];
    public AppearanceConfig Appearance { get; init; } = new();
}

public sealed class HotkeyConfig
{
    public string Modifiers { get; init; } = "Ctrl+Alt";
    public string Key { get; init; } = "Space";
}

public sealed class CategoryConfig
{
    public string Name { get; init; } = "Untitled";
    public string IconKey { get; init; } = "\uE700"; // Default gear icon
    public List<ActionSlotConfig> Slots { get; init; } = [];
}

public sealed class ActionSlotConfig
{
    public string ActionId { get; init; } = "";
    public string? Label { get; init; }
    public string? IconKey { get; init; }
    public string? Path { get; set; }
    public string? Arguments { get; init; }
    public List<ActionSlotConfig>? Children { get; init; }
}

public sealed class AppearanceConfig
{
    public double InnerRadius { get; init; } = 60.0;
    public double OuterRadius { get; init; } = 200.0;
    public double SubMenuRadius { get; init; } = 280.0;

    // Wedge fill gradient (inner → outer)
    public string BackgroundColor { get; init; } = "#D9101018";
    public string BackgroundColorEnd { get; init; } = "#D9181828";

    // Hover gradient (bright, near-white)
    public string HoverColor { get; init; } = "#F0E8E8F0";
    public string HoverColorEnd { get; init; } = "#D0D0D0E0";

    // Accent & glow
    public string AccentColor { get; init; } = "#FF8B83FF";
    public string GlowColor { get; init; } = "#606C63FF";

    // Text
    public string TextColor { get; init; } = "#FFFFFFFF";
    public string SubTextColor { get; init; } = "#99AAAAAA";
    public string HoveredTextColor { get; init; } = "#FF1A1A2E";

    public double Opacity { get; init; } = 0.97;

    // Hub
    public string HubColor { get; init; } = "#F0101018";
    public string HubBorderColor { get; init; } = "#80606080";

    // Rings & separators
    public string SeparatorColor { get; init; } = "#18FFFFFF";
    public string OuterRingColor { get; init; } = "#30808098";

    // Slice gaps & badges
    public double SliceGapDegrees { get; init; } = 1.5;
    public bool ShowSliceNumbers { get; init; } = true;
}
