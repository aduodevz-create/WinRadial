using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WinRadial.Core;
using WinRadial.UI;

namespace WinRadial.Actions;

public sealed class AddProgramAction : IWheelAction
{
    private readonly LogService _log;
    private readonly ConfigService _configService;

    public string Id => "add_program";
    public string Label { get; }
    public string IconKey { get; }
    public bool HasSubmenu => false;

    public AddProgramAction(string label, string iconKey, ConfigService configService, LogService log)
    {
        Label = label;
        IconKey = iconKey;
        _configService = configService;
        _log = log;
    }

    public async Task ExecuteAsync()
    {
        try
        {
            var window = new AppDrawerWindow();
            if (window.ShowDialog() == true && window.SelectedApp != null)
            {
                var app = window.SelectedApp;
                
                var config = _configService.Load();
                
                // Find Customize category
                var category = config.Categories.FirstOrDefault(c => c.Name == "Customize");
                if (category == null)
                {
                    _log.Warning("Customize category not found in config.");
                    return;
                }

                if (category.Slots.Count >= 8)
                {
                    // Replace the item just before the last one (assuming last is "add_program")
                    category.Slots.Insert(category.Slots.Count - 1, new ActionSlotConfig
                    {
                        ActionId = "app_launch",
                        Label = app.Name,
                        IconKey = "\uE737", // Default app icon
                        Path = app.ExecutablePath
                    });
                    
                    // Maintain exactly 8 items, prefer keeping "add_program" at the end
                    if (category.Slots.Count > 8)
                    {
                        category.Slots.RemoveAt(0);
                    }
                }
                else
                {
                    // Insert before the last item (the Add Program button itself)
                    var insertIndex = Math.Max(0, category.Slots.Count - 1);
                    category.Slots.Insert(insertIndex, new ActionSlotConfig
                    {
                        ActionId = "app_launch",
                        Label = app.Name,
                        IconKey = "\uE737", 
                        Path = app.ExecutablePath
                    });
                }

                _configService.Save(config);

                // Reload config on main thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ((App)Application.Current).ReloadConfig();
                });
            }
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to execute AddProgramAction: {ex.Message}");
        }
        
        await Task.CompletedTask;
    }

    public IReadOnlyList<IWheelAction> GetSubActions() => [];
}
