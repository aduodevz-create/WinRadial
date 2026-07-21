using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using WinRadial.Core;

namespace WinRadial.Tray;

/// <summary>
/// System tray icon using Shell_NotifyIcon P/Invoke (pure WPF, no WinForms NotifyIcon).
/// Provides context menu: Settings (open config folder), Reload Config, About, Exit.
/// Implements IDisposable to remove tray icon on shutdown.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private const uint TRAY_ID = 1;

    private readonly LogService _log;
    private readonly ConfigService _configService;
    private readonly Action _onReloadConfig;
    private readonly Action _onExit;
    private HwndSource? _hwndSource;
    private WindowInterop.NOTIFYICONDATA _nid;
    private ContextMenu? _contextMenu;
    private bool _disposed;

    public TrayIconManager(LogService log, ConfigService configService,
        Action onReloadConfig, Action onExit)
    {
        _log = log;
        _configService = configService;
        _onReloadConfig = onReloadConfig;
        _onExit = onExit;

        CreateTrayIcon();
        BuildContextMenu();
    }

    private void CreateTrayIcon()
    {
        try
        {
            // Create hidden window for tray messages
            var parameters = new HwndSourceParameters("WinRadialTrayWindow")
            {
                Width = 0,
                Height = 0,
                WindowStyle = 0,
            };

            _hwndSource = new HwndSource(parameters);
            _hwndSource.AddHook(WndProc);

            // Get the app icon from the assembly
            var iconHandle = GetAppIconHandle();

            _nid = new WindowInterop.NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<WindowInterop.NOTIFYICONDATA>(),
                hWnd = _hwndSource.Handle,
                uID = TRAY_ID,
                uFlags = WindowInterop.NIF_MESSAGE | WindowInterop.NIF_ICON | WindowInterop.NIF_TIP,
                uCallbackMessage = (uint)WindowInterop.WM_TRAYICON,
                hIcon = iconHandle,
                szTip = "WinRadial — Ctrl+Alt+Space to open"
            };

            WindowInterop.Shell_NotifyIcon(WindowInterop.NIM_ADD, ref _nid);
            _log.Info("Tray icon created.");
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to create tray icon: {ex}");
        }
    }

    private void BuildContextMenu()
    {
        _contextMenu = new ContextMenu();

        var headerItem = new MenuItem
        {
            Header = "WinRadial",
            IsEnabled = false,
            FontWeight = FontWeights.Bold
        };
        _contextMenu.Items.Add(headerItem);
        _contextMenu.Items.Add(new Separator());

        var settingsItem = new MenuItem { Header = "Open Config Folder" };
        settingsItem.Click += (_, _) => OpenConfigFolder();
        _contextMenu.Items.Add(settingsItem);

        var reloadItem = new MenuItem { Header = "Reload Config" };
        reloadItem.Click += (_, _) => _onReloadConfig();
        _contextMenu.Items.Add(reloadItem);

        _contextMenu.Items.Add(new Separator());

        var aboutItem = new MenuItem { Header = "About WinRadial" };
        aboutItem.Click += (_, _) => ShowAbout();
        _contextMenu.Items.Add(aboutItem);

        _contextMenu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => _onExit();
        _contextMenu.Items.Add(exitItem);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WindowInterop.WM_TRAYICON)
        {
            var mouseMsg = lParam.ToInt32();
            switch (mouseMsg)
            {
                case WindowInterop.WM_RBUTTONUP:
                    ShowContextMenu();
                    handled = true;
                    break;

                case WindowInterop.WM_LBUTTONDBLCLK:
                    OpenConfigFolder();
                    handled = true;
                    break;
            }
        }
        return IntPtr.Zero;
    }

    private void ShowContextMenu()
    {
        if (_contextMenu == null || _hwndSource == null) return;

        // Set foreground to ensure menu closes when clicking elsewhere
        WindowInterop.SetForegroundWindow(_hwndSource.Handle);

        _contextMenu.IsOpen = true;
    }

    private void OpenConfigFolder()
    {
        try
        {
            var configDir = _configService.ConfigDirectory;
            if (System.IO.Directory.Exists(configDir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    UseShellExecute = true,
                    ArgumentList = { configDir }
                });
            }
            else
            {
                System.IO.Directory.CreateDirectory(configDir);
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    UseShellExecute = true,
                    ArgumentList = { configDir }
                });
            }
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to open config folder: {ex}");
        }
    }

    private static void ShowAbout()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        MessageBox.Show(
            $"WinRadial v{version}\n\n" +
            "A radial pie-menu launcher for Windows.\n\n" +
            "Hotkey: Ctrl+Alt+Space\n" +
            "Config: %APPDATA%\\WinRadial\\config.json\n\n" +
            "No network access • No elevation • Local only",
            "About WinRadial",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static IntPtr GetAppIconHandle()
    {
        try
        {
            // Try to load from the application's icon resource
            var uri = new Uri("pack://application:,,,/Resources/app.ico", UriKind.Absolute);
            var decoder = BitmapDecoder.Create(uri, BitmapCreateOptions.None, BitmapCacheOption.Default);
            if (decoder.Frames.Count > 0)
            {
                var frame = decoder.Frames[0];
                // Convert to icon handle - use a simple approach
                var bitmap = new System.Drawing.Bitmap(
                    frame.PixelWidth, frame.PixelHeight,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                var data = new byte[frame.PixelWidth * frame.PixelHeight * 4];
                frame.CopyPixels(data, frame.PixelWidth * 4, 0);

                var bmpData = bitmap.LockBits(
                    new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    bitmap.PixelFormat);
                Marshal.Copy(data, 0, bmpData.Scan0, data.Length);
                bitmap.UnlockBits(bmpData);

                return bitmap.GetHicon();
            }
        }
        catch
        {
            // Silently fall through to default
        }

        // Fallback: use default application icon
        try
        {
            var exePath = Environment.ProcessPath;
            if (exePath != null)
            {
                var shinfo = new WindowInterop.SHFILEINFO();
                WindowInterop.SHGetFileInfo(exePath, 0, ref shinfo,
                    (uint)Marshal.SizeOf<WindowInterop.SHFILEINFO>(),
                    WindowInterop.SHGFI_ICON | WindowInterop.SHGFI_SMALLICON);
                return shinfo.hIcon;
            }
        }
        catch
        {
            // Ignore
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            WindowInterop.Shell_NotifyIcon(WindowInterop.NIM_DELETE, ref _nid);
            _log.Info("Tray icon removed.");
        }
        catch
        {
            // Best-effort cleanup
        }

        _hwndSource?.RemoveHook(WndProc);
        _hwndSource?.Dispose();
        _hwndSource = null;
    }
}
