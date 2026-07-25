using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinRadial.Core;

namespace WinRadial.UI;

public partial class AppDrawerWindow : Window
{
    private List<AppInfo> _allApps = new();
    public AppInfo? SelectedApp { get; private set; }
    public bool IsInnerRingSelected => RbInnerRing.IsChecked == true;
    public string? SelectedCategoryName => CategoryCombo.SelectedItem as string;

    private bool _isSearchPlaceholder = true;
    private readonly WinRadialConfig _config;

    public AppDrawerWindow(WinRadialConfig config)
    {
        InitializeComponent();
        _config = config;
        Loaded += AppDrawerWindow_Loaded;
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            this.DragMove();
    }

    private async void AppDrawerWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Populate Categories Dropdown
        foreach (var cat in _config.Categories)
        {
            CategoryCombo.Items.Add(cat.Name);
        }
        if (CategoryCombo.Items.Count > 0)
        {
            CategoryCombo.SelectedIndex = 0;
        }

        // Load apps asynchronously so we don't freeze the UI
        _allApps = await Task.Run(() => StartMenuAppFetcher.GetInstalledApps());
        
        LoadingText.Visibility = Visibility.Collapsed;
        AppList.ItemsSource = _allApps;
    }

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
        BtnAdd.IsEnabled = AppList.SelectedItem != null;
    }

    private void AppList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (AppList.SelectedItem is AppInfo app)
        {
            SelectedApp = app;
            DialogResult = true;
            Close();
        }
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        if (AppList.SelectedItem is AppInfo app)
        {
            SelectedApp = app;
            DialogResult = true;
            Close();
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void AppList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var scrollViewer = GetScrollViewer(AppList);
        if (scrollViewer != null)
        {
            e.Handled = true;
            // Greatly reduce scroll speed (e.Delta is typically 120 per click, we reduce it significantly)
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
}
