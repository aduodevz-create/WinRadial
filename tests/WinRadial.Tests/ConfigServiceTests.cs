using FluentAssertions;
using WinRadial.Core;
using Xunit;

namespace WinRadial.Tests;

/// <summary>
/// Unit tests for ConfigService validation logic.
/// Tests valid configs, missing fields, invalid action IDs, malformed JSON, and bounds.
/// </summary>
public class ConfigServiceTests
{
    private static LogService CreateLog() => new();

    // ─── Valid Config ──────────────────────────────────

    [Fact]
    public void Validate_ValidConfig_ReturnsValid()
    {
        var config = CreateValidConfig();
        var service = new ConfigService(CreateLog());

        var (isValid, errors) = service.Validate(config);

        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    // ─── Missing Required Fields ───────────────────────

    [Fact]
    public void Validate_EmptyModifiers_ReturnsError()
    {
        var config = CreateValidConfig();
        config.Hotkey.GetType().GetProperty("Modifiers")!.SetValue(config.Hotkey, "");
        var service = new ConfigService(CreateLog());

        var (isValid, errors) = service.Validate(config);

        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("modifiers"));
    }

    [Fact]
    public void Validate_EmptyKey_ReturnsError()
    {
        var config = CreateValidConfig();
        config.Hotkey.GetType().GetProperty("Key")!.SetValue(config.Hotkey, "");
        var service = new ConfigService(CreateLog());

        var (isValid, errors) = service.Validate(config);

        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("key"));
    }

    [Fact]
    public void Validate_NoCategories_ReturnsError()
    {
        var config = new WinRadialConfig
        {
            Hotkey = new HotkeyConfig(),
            Categories = [],
            Appearance = new AppearanceConfig()
        };
        var service = new ConfigService(CreateLog());

        var (isValid, errors) = service.Validate(config);

        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("category"));
    }

    // ─── Invalid Action IDs ───────────────────────────

    [Fact]
    public void Validate_UnknownActionId_ReturnsError()
    {
        var config = CreateValidConfig();
        config.Categories[0].Slots[0].GetType().GetProperty("ActionId")!
            .SetValue(config.Categories[0].Slots[0], "hack_the_planet");
        var service = new ConfigService(CreateLog());

        var (isValid, errors) = service.Validate(config);

        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("hack_the_planet"));
    }

    // ─── Too Many Slots ───────────────────────────────

    [Fact]
    public void Validate_MoreThan8Slots_ReturnsError()
    {
        var slots = new List<ActionSlotConfig>();
        for (int i = 0; i < 9; i++)
        {
            slots.Add(new ActionSlotConfig { ActionId = "lock_screen", Label = $"Slot {i}" });
        }

        var config = new WinRadialConfig
        {
            Hotkey = new HotkeyConfig(),
            Categories = [new CategoryConfig { Name = "Test", Slots = slots }],
            Appearance = new AppearanceConfig()
        };
        var service = new ConfigService(CreateLog());

        var (isValid, errors) = service.Validate(config);

        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("9") && e.Contains("max 8"));
    }

    // ─── Appearance Bounds ─────────────────────────────

    [Fact]
    public void Validate_InnerRadiusTooSmall_ReturnsError()
    {
        var config = CreateValidConfig();
        typeof(AppearanceConfig).GetProperty("InnerRadius")!.SetValue(config.Appearance, 5.0);
        var service = new ConfigService(CreateLog());

        var (isValid, errors) = service.Validate(config);

        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("innerRadius"));
    }

    [Fact]
    public void Validate_InnerGreaterThanOuter_ReturnsError()
    {
        var config = CreateValidConfig();
        typeof(AppearanceConfig).GetProperty("InnerRadius")!.SetValue(config.Appearance, 150.0);
        typeof(AppearanceConfig).GetProperty("OuterRadius")!.SetValue(config.Appearance, 120.0);
        var service = new ConfigService(CreateLog());

        var (isValid, errors) = service.Validate(config);

        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("innerRadius") && e.Contains("outerRadius"));
    }

    [Fact]
    public void Validate_OpacityOutOfRange_ReturnsError()
    {
        var config = CreateValidConfig();
        typeof(AppearanceConfig).GetProperty("Opacity")!.SetValue(config.Appearance, 1.5);
        var service = new ConfigService(CreateLog());

        var (isValid, errors) = service.Validate(config);

        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("opacity"));
    }

    // ─── Fallback Defaults ─────────────────────────────

    [Fact]
    public void LoadEmbeddedDefault_ReturnsValidConfig()
    {
        var service = new ConfigService(CreateLog());
        var config = service.LoadEmbeddedDefault();

        config.Should().NotBeNull();
        config.Hotkey.Should().NotBeNull();
        config.Categories.Should().NotBeEmpty();
        config.Hotkey.Modifiers.Should().NotBeNullOrEmpty();
        config.Hotkey.Key.Should().NotBeNullOrEmpty();
    }

    // ─── App Launch Path Validation ────────────────────

    [Fact]
    public void Validate_AppLaunchWithNoPath_ReturnsError()
    {
        var config = new WinRadialConfig
        {
            Hotkey = new HotkeyConfig(),
            Categories =
            [
                new CategoryConfig
                {
                    Name = "Test",
                    Slots =
                    [
                        new ActionSlotConfig { ActionId = "app_launch", Label = "No Path" }
                    ]
                }
            ],
            Appearance = new AppearanceConfig()
        };
        var service = new ConfigService(CreateLog());

        var (isValid, errors) = service.Validate(config);

        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("path is required"));
    }

    // ─── Helper ────────────────────────────────────────

    private static WinRadialConfig CreateValidConfig()
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
                    ]
                }
            ],
            Appearance = new AppearanceConfig()
        };
    }
}
