using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinRadial.Actions;
using WinRadial.Core;

namespace WinRadial.UI;

public partial class SettingsWindow : Window
{
    private readonly ConfigService _configService;
    private WinRadialConfig _currentConfig;

    // App Drawer State
    private List<AppInfo> _allApps = new();
    public AppInfo? SelectedApp { get; private set; }
    public bool IsSuccess { get; private set; } = false;
    public bool IsInnerRingSelected => RbInnerRing.IsChecked == true;
    public string? SelectedCategoryName => CategoryCombo.SelectedItem as string;
    private bool _isSearchPlaceholder = true;

    // Editor State
    private WheelCanvas? _previewWheel;
    private ActionRegistry? _actionRegistry;
    private int _editorOpenSubmenuSlice = -1;

    private string _activeTab;

    public SettingsWindow(ConfigService configService, string initialTab = "Settings")
    {
        InitializeComponent();
        _configService = configService;
        _currentConfig = _configService.Load();
        
        _activeTab = initialTab;

        Loaded += SettingsWindow_Loaded;
        LoadCurrentSettings();
    }

    private async void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Select initial tab
        if (_activeTab == "Apps")
        {
            NavApps.IsChecked = true;
        }
        else if (_activeTab == "Editor")
        {
            NavEditor.IsChecked = true;
        }
        else
        {
            NavAppearance.IsChecked = true;
        }

        // Populate Categories Dropdown
        foreach (var cat in _currentConfig.Categories)
        {
            CategoryCombo.Items.Add(cat.Name);
        }
        if (CategoryCombo.Items.Count > 0)
        {
            int customizeIdx = -1;
            for (int i = 0; i < CategoryCombo.Items.Count; i++)
            {
                if (CategoryCombo.Items[i].ToString() == "Customize")
                {
                    customizeIdx = i;
                    break;
                }
            }
            CategoryCombo.SelectedIndex = customizeIdx >= 0 ? customizeIdx : 0;
        }

        // Load apps asynchronously
        _allApps = await Task.Run(() => StartMenuAppFetcher.GetInstalledApps());
        LoadingText.Visibility = Visibility.Collapsed;
        AppList.ItemsSource = _allApps;
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (PanelApps == null || PanelSettings == null || PanelEditor == null) return;

        if (NavApps.IsChecked == true)
        {
            PanelApps.Visibility = Visibility.Visible;
            PanelSettings.Visibility = Visibility.Collapsed;
            PanelEditor.Visibility = Visibility.Collapsed;
            LblTitle.Text = "Add Programs";
            BtnPrimary.Content = "Add Program";
            BtnPrimary.IsEnabled = AppList.SelectedItem != null;
            _activeTab = "Apps";
        }
        else if (NavAppearance.IsChecked == true)
        {
            PanelApps.Visibility = Visibility.Collapsed;
            PanelSettings.Visibility = Visibility.Visible;
            PanelEditor.Visibility = Visibility.Collapsed;
            LblTitle.Text = "Appearance & Behavior";
            BtnPrimary.Content = "Save Settings";
            BtnPrimary.IsEnabled = true;
            _activeTab = "Settings";
        }
        else if (NavEditor?.IsChecked == true)
        {
            PanelApps.Visibility = Visibility.Collapsed;
            PanelSettings.Visibility = Visibility.Collapsed;
            PanelEditor.Visibility = Visibility.Visible;
            LblTitle.Text = "Wheel Layout";
            BtnPrimary.Content = "Save Layout";
            BtnPrimary.IsEnabled = true;
            BtnRestoreDefaults.Visibility = Visibility.Visible;
            _activeTab = "Editor";
            RefreshPreviewWheel();
        }
    }

    private void RebuildPreviewWheel()
    {
        if (PreviewWheelHost != null)
        {
            PreviewWheelHost.Children.Clear();
        }
        _previewWheel = null;
        RefreshPreviewWheel();
    }

    private void RefreshPreviewWheel()
    {
        if (_previewWheel == null)
        {
            _previewWheel = new WheelCanvas(_currentConfig.Appearance)
            {
                RenderTransform = new System.Windows.Media.ScaleTransform(0.5, 0.5),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            PreviewWheelHost.Children.Add(_previewWheel);
            if (Application.Current is App app)
            {
                _actionRegistry = app.ActionRegistry;
            }
        }

        if (_actionRegistry == null) return;

        // Build mock actions based on current categories
        var mockActions = new List<IWheelAction>();
        foreach (var cat in _currentConfig.Categories)
        {
            if (cat.Slots.Count > 0)
            {
                // Just use the first item to represent the category visually
                mockActions.Add(_actionRegistry.Create(cat.Slots[0])!);
            }
            else
            {
                mockActions.Add(null!);
            }
        }
        
        while (mockActions.Count < WheelRenderer.SliceCount)
        {
            mockActions.Add(null!);
        }

        var mockSubActions = new List<IWheelAction>();
        bool isSubmenuOpen = _editorOpenSubmenuSlice >= 0;
        string categoryName = "";

        if (isSubmenuOpen && _editorOpenSubmenuSlice < _currentConfig.Categories.Count)
        {
            var cat = _currentConfig.Categories[_editorOpenSubmenuSlice];
            categoryName = cat.Name;
            foreach (var slot in cat.Slots)
            {
                mockSubActions.Add(_actionRegistry.Create(slot)!);
            }
            while (mockSubActions.Count < WheelRenderer.SliceCount)
            {
                mockSubActions.Add(null!);
            }
        }

        _previewWheel.UpdateState(mockActions, -1, -1, isSubmenuOpen, _editorOpenSubmenuSlice, mockSubActions, categoryName, 0, 0, -1);
    }

    private void PreviewWheelHost_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_previewWheel == null) return;

        var pos = e.GetPosition(_previewWheel);
        var dx = pos.X - _previewWheel.CenterX;
        var dy = pos.Y - _previewWheel.CenterY;

        var innerR = _currentConfig.Appearance.InnerRadius;
        var outerR = _currentConfig.Appearance.OuterRadius;
        var subR = _currentConfig.Appearance.SubMenuRadius;
        var distSq = dx * dx + dy * dy;

        if (distSq >= innerR * innerR && distSq <= outerR * outerR)
        {
            // Inner ring clicked
            int sliceIndex = WheelRenderer.GetSliceIndex(dx, dy);
            ShowSegmentContextMenu(sliceIndex, isInnerRing: true);
        }
        else if (_editorOpenSubmenuSlice >= 0 && distSq > outerR * outerR && distSq <= subR * subR)
        {
            // Outer ring clicked
            int sliceIndex = WheelRenderer.GetSliceIndex(dx, dy);
            ShowSegmentContextMenu(sliceIndex, isInnerRing: false);
        }
    }

    private void ShowSegmentContextMenu(int sliceIndex, bool isInnerRing)
    {
        var menu = new ContextMenu();

        if (isInnerRing)
        {
            while (_currentConfig.Categories.Count <= sliceIndex)
                _currentConfig.Categories.Add(new CategoryConfig { Name = $"Category {sliceIndex + 1}" });
            var cat = _currentConfig.Categories[sliceIndex];
            
            var openSubItem = new MenuItem { Header = _editorOpenSubmenuSlice == sliceIndex ? "Close Submenu" : "Open Submenu", FontWeight = FontWeights.Bold };
            openSubItem.Click += (s, e) =>
            {
                _editorOpenSubmenuSlice = (_editorOpenSubmenuSlice == sliceIndex) ? -1 : sliceIndex;
                RefreshPreviewWheel();
            };
            menu.Items.Add(openSubItem);

            var editItem = new MenuItem { Header = "Edit Category Name/Icon" };
            editItem.Click += (s, e) =>
            {
                CategoryCombo.SelectedItem = cat.Name;
                RbInnerRing.IsChecked = true;
                NavApps.IsChecked = true;
            };
            menu.Items.Add(editItem);

            if (cat.Slots.Count > 0)
            {
                var removeItem = new MenuItem { Header = "Remove Entire Category", Foreground = System.Windows.Media.Brushes.Red };
                removeItem.Click += (s, e) =>
                {
                    cat.Slots.Clear();
                    if (_editorOpenSubmenuSlice == sliceIndex) _editorOpenSubmenuSlice = -1;
                    RefreshPreviewWheel();
                };
                menu.Items.Add(removeItem);
            }
        }
        else
        {
            var cat = _currentConfig.Categories[_editorOpenSubmenuSlice];
            bool hasProgram = sliceIndex < cat.Slots.Count;

            var editItem = new MenuItem { Header = hasProgram ? "Edit Program" : "Add Program", FontWeight = FontWeights.Bold };
            editItem.Click += (s, e) =>
            {
                CategoryCombo.SelectedItem = cat.Name;
                RbOuterRing.IsChecked = true;
                NavApps.IsChecked = true;
            };
            menu.Items.Add(editItem);

            if (hasProgram)
            {
                var removeItem = new MenuItem { Header = "Remove Program", Foreground = System.Windows.Media.Brushes.Red };
                removeItem.Click += (s, e) =>
                {
                    cat.Slots.RemoveAt(sliceIndex);
                    RefreshPreviewWheel();
                };
                menu.Items.Add(removeItem);
            }
        }
        
        menu.IsOpen = true;
    }

    private void BtnRestoreDefaults_Click(object sender, RoutedEventArgs e)
    {
        _currentConfig.Categories.Clear();
        _currentConfig.Categories.AddRange(_configService.LoadEmbeddedDefault().Categories);
        _editorOpenSubmenuSlice = -1;
        RefreshPreviewWheel();
    }

    // ─── Settings Logic ───────────────────────────────────────────

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
        if (LblInner == null || LblOuter == null || LblSub == null || LblOpacity == null) return;

        LblInner.Text = Math.Round(SldInner.Value).ToString();
        LblOuter.Text = Math.Round(SldOuter.Value).ToString();
        LblSub.Text = Math.Round(SldSub.Value).ToString();
        LblOpacity.Text = SldOpacity.Value.ToString("0.00");
    }

    private void SaveSettings()
    {
        LblError.Visibility = Visibility.Collapsed;

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

        var newConfig = new WinRadialConfig
        {
            Hotkey = new HotkeyConfig { Modifiers = mods, Key = key },
            Appearance = new AppearanceConfig
            {
                InnerRadius = SldInner.Value,
                OuterRadius = SldOuter.Value,
                SubMenuRadius = SldSub.Value,
                Opacity = SldOpacity.Value,
                BackgroundColor = TxtBgColor.Text.Trim(),
                AccentColor = TxtAccentColor.Text.Trim(),

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
            Categories = _currentConfig.Categories
        };

        try
        {
            _configService.Save(newConfig);
            
            if (Application.Current is App app)
            {
                app.ReloadConfig();
            }

            IsSuccess = true;
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

    // ─── App Drawer Logic ─────────────────────────────────────────

    private void RbLocation_Checked(object sender, RoutedEventArgs e)
    {
        if (CategoryCombo != null)
        {
            CategoryCombo.IsEnabled = RbOuterRing.IsChecked == true;
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSearchPlaceholder || _allApps == null) return;

        var query = SearchBox.Text.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(query))
        {
            AppList.ItemsSource = _allApps;
        }
        else
        {
            AppList.ItemsSource = _allApps.Where(a => a.Name.ToLowerInvariant().Contains(query)).ToList();
        }
    }

    private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (_isSearchPlaceholder)
        {
            _isSearchPlaceholder = false;
            SearchBox.Text = "";
        }
    }

    private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            _isSearchPlaceholder = true;
            SearchBox.Text = "Search...";
        }
    }

    private void AppList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_activeTab == "Apps" && BtnPrimary != null)
        {
            BtnPrimary.IsEnabled = AppList.SelectedItem != null;
        }
    }

    private void AppList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (AppList.SelectedItem is AppInfo app)
        {
            AddProgramToConfig(app);
        }
    }

    private void AppList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var scrollViewer = GetScrollViewer(AppList);
        if (scrollViewer != null)
        {
            e.Handled = true;
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - (e.Delta * 0.15));
        }
    }

    private static ScrollViewer? GetScrollViewer(DependencyObject depObj)
    {
        if (depObj is ScrollViewer viewer) return viewer;

        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
            var result = GetScrollViewer(child);
            if (result != null) return result;
        }
        return null;
    }

    // ─── Shared Logic ─────────────────────────────────────────────

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void BtnPrimary_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTab == "Apps")
        {
            if (AppList.SelectedItem is AppInfo app)
            {
                AddProgramToConfig(app);
            }
        }
        else if (_activeTab == "Editor")
        {
            // Save Editor layout
            try
            {
                _configService.Save(_currentConfig);
                if (Application.Current is App app) app.ReloadConfig();
                IsSuccess = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowError($"Failed to save layout: {ex.Message}");
            }
        }
        else
        {
            SaveSettings();
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        IsSuccess = false;
        Close();
    }

    private void AddProgramToConfig(AppInfo app)
    {
        if (IsInnerRingSelected)
        {
            if (_currentConfig.Categories.Count < 8)
            {
                _currentConfig.Categories.Add(new CategoryConfig
                {
                    Name = app.Name,
                    IconKey = "\uE737", // Default app icon
                    Slots = new List<ActionSlotConfig>
                    {
                        new ActionSlotConfig
                        {
                            ActionId = "app_launch",
                            Label = app.Name,
                            IconKey = "\uE737",
                            Path = app.ExecutablePath
                        }
                    }
                });
            }
            else
            {
                MessageBox.Show("Maximum of 8 categories already reached on the inner ring.", "Limit Reached", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        else
        {
            var categoryName = SelectedCategoryName ?? "Customize";
            var category = _currentConfig.Categories.FirstOrDefault(c => c.Name == categoryName);
            
            if (category == null)
            {
                MessageBox.Show($"Category {categoryName} not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (category.Slots.Count >= 8)
            {
                // Replace the item just before the last one
                category.Slots.Insert(category.Slots.Count - 1, new ActionSlotConfig
                {
                    ActionId = "app_launch",
                    Label = app.Name,
                    IconKey = "\uE737",
                    Path = app.ExecutablePath
                });
                
                // Maintain exactly 8 items
                if (category.Slots.Count > 8)
                {
                    category.Slots.RemoveAt(0);
                }
            }
            else
            {
                var isCustomizeCategory = category.Name == "Customize";
                var insertIndex = isCustomizeCategory ? Math.Max(0, category.Slots.Count - 1) : category.Slots.Count;
                category.Slots.Insert(insertIndex, new ActionSlotConfig
                {
                    ActionId = "app_launch",
                    Label = app.Name,
                    IconKey = "\uE737", 
                    Path = app.ExecutablePath
                });
            }
        }

        try
        {
            _configService.Save(_currentConfig);
            if (Application.Current is App winApp) winApp.ReloadConfig();
            
            // Show toast or label
            LblTitle.Text = $"Added {app.Name}!";
            Task.Delay(2000).ContinueWith(_ => Dispatcher.Invoke(() => 
            {
                if (_activeTab == "Apps") LblTitle.Text = "Add Programs";
            }));

            // Refresh combo box if we added a category
            if (IsInnerRingSelected)
            {
                CategoryCombo.Items.Clear();
                foreach (var cat in _currentConfig.Categories) CategoryCombo.Items.Add(cat.Name);
                CategoryCombo.SelectedItem = app.Name;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save layout: {ex.Message}");
        }
    }
}
