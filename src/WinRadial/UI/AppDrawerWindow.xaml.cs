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
    private bool _isSearchPlaceholder = true;

    public AppDrawerWindow()
    {
        InitializeComponent();
        Loaded += AppDrawerWindow_Loaded;
    }

    private async void AppDrawerWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Load apps asynchronously so we don't freeze the UI
        _allApps = await Task.Run(() => StartMenuAppFetcher.GetInstalledApps());
        
        LoadingText.Visibility = Visibility.Collapsed;
        AppList.ItemsSource = _allApps;
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
