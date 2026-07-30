using System.Windows;
using System.Windows.Input;
using WinRadial.Core;

namespace WinRadial.UI;

public partial class SettingsWindow : Window
{
    private readonly ConfigService _configService;
    private readonly WinRadialConfig _currentConfig;

    public SettingsWindow(ConfigService configService)
    {
        InitializeComponent();
        _configService = configService;
        _currentConfig = _configService.Load();
        
        LoadCurrentSettings();
    }

    private void LoadCurrentSettings()
    {
        var h = _currentConfig.Hotkey;
        TxtModifiers.Text = h.Modifiers;
        TxtKey.Text = h.Key;

        var a = _currentConfig.Appearance;
        SldInner.Value = a.InnerRadius;
        SldOuter.Value = a.OuterRadius;
        SldSub.Value = a.SubMenuRadius;
        SldOpacity.Value = a.Opacity;

        TxtBgColor.Text = a.BackgroundColor;
        TxtAccentColor.Text = a.AccentColor;
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LblInner == null || LblOuter == null || LblSub == null || LblOpacity == null) return; // Not initialized yet

        LblInner.Text = Math.Round(SldInner.Value).ToString();
        LblOuter.Text = Math.Round(SldOuter.Value).ToString();
        LblSub.Text = Math.Round(SldSub.Value).ToString();
        LblOpacity.Text = SldOpacity.Value.ToString("0.00");
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        LblError.Visibility = Visibility.Collapsed;

        // Validation
        if (!SecurityValidator.IsValidHexColor(TxtBgColor.Text))
        {
            ShowError("Invalid Background Color. Must be in #AARRGGBB format.");
            return;
        }

        if (!SecurityValidator.IsValidHexColor(TxtAccentColor.Text))
        {
            ShowError("Invalid Accent Color. Must be in #AARRGGBB format.");
            return;
        }

        var mods = TxtModifiers.Text.Trim();
        var key = TxtKey.Text.Trim();

        if (string.IsNullOrWhiteSpace(mods) || string.IsNullOrWhiteSpace(key))
        {
            ShowError("Hotkey modifiers and key cannot be empty.");
            return;
        }

        // Build new config
        var newConfig = new WinRadialConfig
        {
            Hotkey = new HotkeyConfig
            {
                Modifiers = mods,
                Key = key
            },
            Appearance = new AppearanceConfig
            {
                InnerRadius = SldInner.Value,
                OuterRadius = SldOuter.Value,
                SubMenuRadius = SldSub.Value,
                Opacity = SldOpacity.Value,
                BackgroundColor = TxtBgColor.Text.Trim(),
                AccentColor = TxtAccentColor.Text.Trim(),

                // Copy over the rest from existing config
                BackgroundColorEnd = _currentConfig.Appearance.BackgroundColorEnd,
                HoverColor = _currentConfig.Appearance.HoverColor,
                HoverColorEnd = _currentConfig.Appearance.HoverColorEnd,
                GlowColor = _currentConfig.Appearance.GlowColor,
                TextColor = _currentConfig.Appearance.TextColor,
                SubTextColor = _currentConfig.Appearance.SubTextColor,
                HoveredTextColor = _currentConfig.Appearance.HoveredTextColor,
                HubColor = _currentConfig.Appearance.HubColor,
                HubBorderColor = _currentConfig.Appearance.HubBorderColor,
                SeparatorColor = _currentConfig.Appearance.SeparatorColor,
                OuterRingColor = _currentConfig.Appearance.OuterRingColor,
                SliceGapDegrees = _currentConfig.Appearance.SliceGapDegrees,
                ShowSliceNumbers = _currentConfig.Appearance.ShowSliceNumbers
            },
            // Keep existing categories
            Categories = _currentConfig.Categories
        };

        try
        {
            _configService.Save(newConfig);
            
            // App instance handles the global ReloadConfig
            if (Application.Current is App app)
            {
                app.ReloadConfig();
            }

            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Failed to save config: {ex.Message}");
        }
    }

    private void ShowError(string message)
    {
        LblError.Text = message;
        LblError.Visibility = Visibility.Visible;
    }
}
