using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WinRadial.Actions;
using WinRadial.Core;

namespace WinRadial.UI;

/// <summary>
/// The main radial wheel overlay window. Created once at startup, hidden/shown on hotkey.
/// Handles all mouse/keyboard input, delegates rendering to WheelCanvas,
/// and delegates geometry math to WheelRenderer.
/// Multi-monitor aware: positions itself on the monitor under the cursor.
/// </summary>
public partial class WheelWindow : Window
{
    private readonly ActionRegistry _actionRegistry;
    private readonly LogService _log;
    private WinRadialConfig _config;
    private WheelCanvas _canvas;

    // State
    private List<CategoryConfig> _categories = [];
    private int _currentCategoryIndex;
    private List<IWheelAction> _currentActions = [];
    private int _hoveredSlice = -1;
    private int _hoveredSubSlice = -1;
    private bool _submenuOpen;
    private int _submenuParentSlice = -1;
    private int _lockedSlice = -1;
    private List<IWheelAction> _subActions = [];
    private int _hoveredHubArea = -1;

    // Wheel center in screen coordinates
    private double _wheelCenterX;
    private double _wheelCenterY;

    public WheelWindow(WinRadialConfig config, ActionRegistry actionRegistry, LogService log)
    {
        InitializeComponent();

        _config = config;
        _actionRegistry = actionRegistry;
        _log = log;

        _canvas = new WheelCanvas(config.Appearance);
        RootGrid.Children.Add(_canvas);

        _categories = config.Categories;
        LoadCategory(0);

        // Hide initially
        Visibility = Visibility.Hidden;
        Opacity = 0;
    }

    /// <summary>
    /// Updates the window with a new config (called on config reload).
    /// </summary>
    public void UpdateConfig(WinRadialConfig config)
    {
        _config = config;
        _categories = config.Categories;

        RootGrid.Children.Clear();
        _canvas = new WheelCanvas(config.Appearance);
        RootGrid.Children.Add(_canvas);

        if (IsVisible)
        {
            _canvas.Width = Width;
            _canvas.Height = Height;
            _canvas.RenderTransform = new TranslateTransform(
                _wheelCenterX - Width / 2,
                _wheelCenterY - Height / 2);
        }

        _currentCategoryIndex = 0;
        LoadCategory(0);
    }

    /// <summary>
    /// Shows the wheel at the cursor position, on the correct monitor.
    /// </summary>
    public void ShowWheel()
    {
        try
        {
            var (cursorX, cursorY) = WindowInterop.GetCursorPosition();
            var (monitorBounds, _, dpiX, _) = WindowInterop.GetMonitorInfoForPoint(cursorX, cursorY);

            var scale = dpiX / 96.0;

            // Size the window to fill the monitor (in WPF logical units)
            var monWidth = (monitorBounds.Right - monitorBounds.Left) / scale;
            var monHeight = (monitorBounds.Bottom - monitorBounds.Top) / scale;

            Left = monitorBounds.Left / scale;
            Top = monitorBounds.Top / scale;
            Width = monWidth;
            Height = monHeight;

            // Wheel center = cursor position relative to window
            _wheelCenterX = (cursorX - monitorBounds.Left) / scale;
            _wheelCenterY = (cursorY - monitorBounds.Top) / scale;

            // Clamp center so the wheel doesn't go off-screen
            var maxR = _config.Appearance.SubMenuRadius + 20;
            _wheelCenterX = Math.Clamp(_wheelCenterX, maxR, monWidth - maxR);
            _wheelCenterY = Math.Clamp(_wheelCenterY, maxR, monHeight - maxR);

            // Reset state
            _hoveredSlice = -1;
            _hoveredSubSlice = -1;
            _submenuOpen = false;
            _submenuParentSlice = -1;
            _lockedSlice = -1;
            _subActions.Clear();
            _hoveredHubArea = -1;

            // Position canvas center
            _canvas.Width = monWidth;
            _canvas.Height = monHeight;
            _canvas.RenderTransform = new TranslateTransform(
                _wheelCenterX - monWidth / 2,
                _wheelCenterY - monHeight / 2);

            RefreshCanvas();

            // Show with fade-in
            Visibility = Visibility.Visible;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(OpacityProperty, fadeIn);

            Activate();
            Focus();
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to show wheel: {ex}");
        }
    }

    /// <summary>
    /// Hides the wheel with a fade-out animation.
    /// </summary>
    public void HideWheel()
    {
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(100));
        fadeOut.Completed += (_, _) =>
        {
            Visibility = Visibility.Hidden;
            _submenuOpen = false;
            _submenuParentSlice = -1;
            _lockedSlice = -1;
            _subActions.Clear();
        };
        BeginAnimation(OpacityProperty, fadeOut);
    }

    private void LoadCategory(int index)
    {
        if (_categories.Count == 0) return;

        _currentCategoryIndex = 0; // We don't paginate categories anymore, they are all in the inner ring
        _currentActions = _categories.Select(c => (IWheelAction)new CategoryAction(c, _actionRegistry)).ToList();

        // Pad to 8 slots if needed (empty actions for visual consistency)
        while (_currentActions.Count < WheelRenderer.SliceCount)
        {
            // Leave gaps as null-like — handled in rendering
            break;
        }

        _submenuOpen = false;
        _submenuParentSlice = -1;
        _lockedSlice = -1;
        _subActions.Clear();

        RefreshCanvas();
    }

    private void RefreshCanvas()
    {
        var category = _categories.Count > 0 ? _categories[_currentCategoryIndex] : null;
        _canvas.UpdateState(
            _currentActions,
            _hoveredSlice,
            _hoveredSubSlice,
            _submenuOpen,
            _submenuParentSlice,
            _subActions,
            category?.Name ?? "WinRadial",
            0,
            1,
            _hoveredHubArea
        );
    }

    // ─── Mouse Input ───────────────────────────────────

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var pos = e.GetPosition(_canvas);
        var dx = pos.X - _canvas.CenterX;
        var dy = pos.Y - _canvas.CenterY;

        var innerR = _config.Appearance.InnerRadius;
        var outerR = _config.Appearance.OuterRadius;
        var subR = _config.Appearance.SubMenuRadius;

        // Check hub area (pagination arrows)
        _hoveredHubArea = -1;
        if (WheelRenderer.IsInCenterHub(dx, dy, innerR))
        {
            if (_categories.Count > 1)
            {
                // Left half = left arrow, right half = right arrow (only lower portion)
                if (dy > 0) // Only the lower half of the hub has arrows
                {
                    _hoveredHubArea = dx < 0 ? 0 : 1;
                }
            }
            _hoveredSlice = -1;
            _hoveredSubSlice = -1;
        }
        else if (_submenuOpen && WheelRenderer.IsInSubRing(dx, dy, outerR, subR))
        {
            var visualSlice = WheelRenderer.GetSliceIndex(dx, dy);
            _hoveredSubSlice = WheelRenderer.GetSubmenuActionIndex(visualSlice, _subActions.Count, _submenuParentSlice);
            _hoveredSlice = _submenuParentSlice;
            _hoveredHubArea = -1;
        }
        else if (WheelRenderer.IsInMainRing(dx, dy, innerR, outerR))
        {
            var slice = WheelRenderer.GetSliceIndex(dx, dy);
            _hoveredSlice = slice;
            _hoveredSubSlice = -1;
            _hoveredHubArea = -1;

            if (slice >= 0 && slice < _currentActions.Count)
            {
                var action = _currentActions[slice];
                
                // Only change submenu if not locked, or if hovering the locked slice itself
                if (_lockedSlice == -1 || _lockedSlice == slice)
                {
                    if (action.HasSubmenu)
                    {
                        if (!_submenuOpen || _submenuParentSlice != slice)
                        {
                            OpenSubmenu(slice, action);
                        }
                    }
                    else
                    {
                        // Close submenu if hovered slice has no submenu and we aren't locked
                        if (_submenuOpen)
                        {
                            _submenuOpen = false;
                            _submenuParentSlice = -1;
                            _subActions.Clear();
                        }
                    }
                }
            }
        }
        else
        {
            // Outside the wheel entirely
            _hoveredSlice = -1;
            _hoveredSubSlice = -1;
            _hoveredHubArea = -1;
        }

        RefreshCanvas();
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        if (e.Handled) return;

        var pos = e.GetPosition(_canvas);
        var dx = pos.X - _canvas.CenterX;
        var dy = pos.Y - _canvas.CenterY;

        var innerR = _config.Appearance.InnerRadius;
        var outerR = _config.Appearance.OuterRadius;
        var subR = _config.Appearance.SubMenuRadius;

        var app = Application.Current as App;
        if (app == null || app.ConfigService == null) return;

        if (_submenuOpen && WheelRenderer.IsInSubRing(dx, dy, outerR, subR))
        {
            var visualSlice = WheelRenderer.GetSliceIndex(dx, dy);
            var actionIdx = WheelRenderer.GetSubmenuActionIndex(visualSlice, _subActions.Count, _submenuParentSlice);
            
            if (actionIdx >= 0 && actionIdx < _subActions.Count && _submenuParentSlice >= 0 && _submenuParentSlice < _categories.Count)
            {
                var cat = _categories[_submenuParentSlice];
                if (actionIdx < cat.Slots.Count)
                {
                    cat.Slots.RemoveAt(actionIdx);
                    app.ConfigService.Save(_config);
                    app.ReloadConfig();
                }
            }
        }
        else if (WheelRenderer.IsInMainRing(dx, dy, innerR, outerR))
        {
            var slice = WheelRenderer.GetSliceIndex(dx, dy);
            if (slice >= 0 && slice < _categories.Count)
            {
                _categories.RemoveAt(slice);
                app.ConfigService.Save(_config);
                app.ReloadConfig();
            }
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (e.Handled) return;

        var pos = e.GetPosition(_canvas);
        var dx = pos.X - _canvas.CenterX;
        var dy = pos.Y - _canvas.CenterY;

        var innerR = _config.Appearance.InnerRadius;
        var outerR = _config.Appearance.OuterRadius;
        var subR = _config.Appearance.SubMenuRadius;

        if (WheelRenderer.IsInCenterHub(dx, dy, innerR))
        {
            // Hub click — check pagination arrows
            if (_hoveredHubArea == 0)
            {
                NavigateCategory(-1);
            }
            else if (_hoveredHubArea == 1)
            {
                NavigateCategory(1);
            }
        }
        else if (_submenuOpen && WheelRenderer.IsInSubRing(dx, dy, outerR, subR))
        {
            // Submenu slice click
            var visualSlice = WheelRenderer.GetSliceIndex(dx, dy);
            var actionIdx = WheelRenderer.GetSubmenuActionIndex(visualSlice, _subActions.Count, _submenuParentSlice);
            if (actionIdx >= 0 && actionIdx < _subActions.Count)
            {
                ExecuteAction(_subActions[actionIdx]);
            }
        }
        else if (WheelRenderer.IsInMainRing(dx, dy, innerR, outerR))
        {
            // Main slice click
            var slice = WheelRenderer.GetSliceIndex(dx, dy);
            if (slice >= 0 && slice < _currentActions.Count)
            {
                var action = _currentActions[slice];
                if (action.HasSubmenu)
                {
                    // Toggle lock
                    if (_lockedSlice == slice)
                    {
                        _lockedSlice = -1; // Unlock
                    }
                    else
                    {
                        _lockedSlice = slice; // Lock
                        if (!_submenuOpen || _submenuParentSlice != slice)
                        {
                            OpenSubmenu(slice, action);
                        }
                    }
                    RefreshCanvas();
                }
                else
                {
                    ExecuteAction(action);
                }
            }
        }
        else
        {
            // Click outside — hide wheel
            HideWheel();
        }
    }

    // ─── Keyboard Input ────────────────────────────────

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.Key)
        {
            case Key.Escape:
                if (_submenuOpen)
                {
                    _submenuOpen = false;
                    _submenuParentSlice = -1;
                    _lockedSlice = -1;
                    _subActions.Clear();
                    RefreshCanvas();
                }
                else
                {
                    HideWheel();
                }
                e.Handled = true;
                break;

            case Key.Left:
                NavigateCategory(-1);
                e.Handled = true;
                break;

            case Key.Right:
                NavigateCategory(1);
                e.Handled = true;
                break;

            case Key.Enter:
                ConfirmHovered();
                e.Handled = true;
                break;

            // Number keys 1-8 select wedges
            case Key.D1 or Key.NumPad1: SelectSlice(0); e.Handled = true; break;
            case Key.D2 or Key.NumPad2: SelectSlice(1); e.Handled = true; break;
            case Key.D3 or Key.NumPad3: SelectSlice(2); e.Handled = true; break;
            case Key.D4 or Key.NumPad4: SelectSlice(3); e.Handled = true; break;
            case Key.D5 or Key.NumPad5: SelectSlice(4); e.Handled = true; break;
            case Key.D6 or Key.NumPad6: SelectSlice(5); e.Handled = true; break;
            case Key.D7 or Key.NumPad7: SelectSlice(6); e.Handled = true; break;
            case Key.D8 or Key.NumPad8: SelectSlice(7); e.Handled = true; break;
        }
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        // Keep wheel visible even when it loses focus (e.g. after an action launches an app)
    }

    // ─── Actions ───────────────────────────────────────

    private void NavigateCategory(int delta)
    {
        if (_categories.Count <= 1) return;
        var newIndex = (_currentCategoryIndex + delta + _categories.Count) % _categories.Count;
        LoadCategory(newIndex);
    }

    private void SelectSlice(int index)
    {
        var actions = _submenuOpen ? _subActions : _currentActions;
        if (index >= 0 && index < actions.Count)
        {
            var action = actions[index];
            if (action.HasSubmenu && !_submenuOpen)
            {
                OpenSubmenu(index, action);
            }
            else
            {
                ExecuteAction(action);
            }
        }
    }

    private void ConfirmHovered()
    {
        if (_submenuOpen && _hoveredSubSlice >= 0 && _hoveredSubSlice < _subActions.Count)
        {
            ExecuteAction(_subActions[_hoveredSubSlice]);
        }
        else if (_hoveredSlice >= 0 && _hoveredSlice < _currentActions.Count)
        {
            var action = _currentActions[_hoveredSlice];
            if (action.HasSubmenu)
            {
                OpenSubmenu(_hoveredSlice, action);
            }
            else
            {
                ExecuteAction(action);
            }
        }
    }

    private void OpenSubmenu(int parentSlice, IWheelAction action)
    {
        _submenuOpen = true;
        _submenuParentSlice = parentSlice;
        _subActions = new List<IWheelAction>(action.GetSubActions());
        _hoveredSubSlice = -1;
        RefreshCanvas();
        _log.Debug($"Submenu opened for: {action.Label}");
    }

    private async void ExecuteAction(IWheelAction action)
    {
        if (action.CloseWheelOnExecute)
        {
            Visibility = Visibility.Hidden;
            _submenuOpen = false;
            _submenuParentSlice = -1;
            _lockedSlice = -1;
            _subActions.Clear();
        }

        try
        {
            _log.Info($"Executing action: {action.Id} ({action.Label})");
            await action.ExecuteAsync();
        }
        catch (Exception ex)
        {
            _log.Error($"Action '{action.Id}' failed: {ex}");
        }
        finally
        {
            // Re-assert topmost and bring wheel back to front after action
            // (some actions launch apps that steal focus/z-order)
            if (IsVisible)
            {
                Topmost = false;
                Topmost = true;
                Activate();
            }
        }
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        HideWheel();
        var app = Application.Current as App;
        if (app != null && app.ConfigService != null)
        {
            var win = new SettingsWindow(app.ConfigService);
            win.ShowDialog();
        }
    }
}
