using System.Windows;
using System.Windows.Threading;
using WinRadial.Actions;
using WinRadial.Core;
using WinRadial.Tray;
using WinRadial.UI;

namespace WinRadial;

/// <summary>
/// Application entry point. Bootstraps all services, registers global exception
/// handlers, and manages application lifetime via system tray.
/// </summary>
public partial class App : Application
{
    private Mutex? _singleInstanceMutex;
    private LogService? _logService;
    private ConfigService? _configService;
    private HotkeyManager? _hotkeyManager;
    private WheelWindow? _wheelWindow;
    private TrayIconManager? _trayIconManager;
    private ActionRegistry? _actionRegistry;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single-instance check
        _singleInstanceMutex = new Mutex(true, "WinRadial_SingleInstance_Mutex", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("WinRadial is already running.", "WinRadial",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Global exception handlers — log and show non-blocking toast, never crash silently
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            Bootstrap();
        }
        catch (Exception ex)
        {
            _logService?.Error($"Fatal startup error: {ex}");
            MessageBox.Show($"WinRadial failed to start:\n{ex.Message}", "WinRadial Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void Bootstrap()
    {
        // 1. Logging first — everything else can log
        _logService = new LogService();
        _logService.Info("WinRadial starting up...");

        // 2. Load and validate configuration
        _configService = new ConfigService(_logService);
        var config = _configService.Load();
        _logService.Info($"Config loaded: {config.Categories.Count} categories, hotkey={config.Hotkey.Modifiers}+{config.Hotkey.Key}");

        // 3. Action registry
        _actionRegistry = new ActionRegistry(_logService);

        // 4. Create the wheel window (hidden, reused across activations)
        _wheelWindow = new WheelWindow(config, _actionRegistry, _logService);

        // 5. Register global hotkey
        _hotkeyManager = new HotkeyManager(config.Hotkey, _logService);
        _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
        _hotkeyManager.Register();

        // 6. System tray icon
        _trayIconManager = new TrayIconManager(_logService, _configService, OnReloadConfig, OnExit);

        _logService.Info("WinRadial startup complete.");
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        if (_wheelWindow == null) return;

        if (_wheelWindow.IsVisible)
        {
            _wheelWindow.HideWheel();
        }
        else
        {
            _wheelWindow.ShowWheel();
        }
    }

    private void OnReloadConfig()
    {
        try
        {
            if (_configService == null || _wheelWindow == null || _hotkeyManager == null) return;

            _logService?.Info("Reloading configuration...");
            var config = _configService.Load();

            // Re-register hotkey if changed
            _hotkeyManager.Unregister();
            _hotkeyManager.UpdateHotkey(config.Hotkey);
            _hotkeyManager.Register();

            // Update wheel with new config
            _wheelWindow.UpdateConfig(config);

            _logService?.Info("Configuration reloaded successfully.");
        }
        catch (Exception ex)
        {
            _logService?.Error($"Config reload failed: {ex}");
        }
    }

    private void OnExit()
    {
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logService?.Info("WinRadial shutting down...");

        // Clean disposal in reverse order
        _trayIconManager?.Dispose();
        _hotkeyManager?.Dispose();
        _wheelWindow?.Close();
        _logService?.Dispose();

        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();

        base.OnExit(e);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        _logService?.Error($"Unhandled AppDomain exception: {ex}");

        if (e.IsTerminating)
        {
            MessageBox.Show($"WinRadial encountered a fatal error and must close.\n\n{ex?.Message}",
                "WinRadial Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logService?.Error($"Unhandled Dispatcher exception: {e.Exception}");
        e.Handled = true; // Prevent crash, log and continue
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logService?.Error($"Unobserved Task exception: {e.Exception}");
        e.SetObserved(); // Prevent crash
    }
}
