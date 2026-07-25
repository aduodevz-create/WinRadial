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
            var config = _configService.Load();
            var window = new AppDrawerWindow(config);
            if (window.ShowDialog() == true && window.SelectedApp != null)
            {
                var app = window.SelectedApp;
                
                if (window.IsInnerRingSelected)
                {
                    // Add as a new Category
                    if (config.Categories.Count < 8)
                    {
                        config.Categories.Add(new CategoryConfig
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
                        _log.Warning("Maximum of 8 categories reached. Cannot add inner ring item.");
                        MessageBox.Show("Maximum of 8 categories already reached on the inner ring.", "Limit Reached", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                else
                {
                    // Add to existing category
                    var categoryName = window.SelectedCategoryName ?? "Customize";
                    var category = config.Categories.FirstOrDefault(c => c.Name == categoryName);
                    
                    if (category == null)
                    {
                        _log.Warning($"Category {categoryName} not found.");
                        return;
                    }

                    // Insert the new app
                    if (category.Slots.Count >= 8)
                    {
                        // Replace the item just before the last one
                        category.Slots.Insert(category.Slots.Count - 1, new ActionSlotConfig
                        {
                            ActionId = "app_launch",
                            Label = app.Name,
                            IconKey = "\uE737", // Default app icon
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
                        // If it's customize category, insert before the last item (Add Program). Else insert at end.
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
