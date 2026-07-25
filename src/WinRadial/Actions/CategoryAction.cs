using System.Collections.Generic;
using System.Threading.Tasks;
using WinRadial.Core;

namespace WinRadial.Actions;

public sealed class CategoryAction : IWheelAction
{
    private readonly CategoryConfig _category;
    private readonly ActionRegistry _registry;

    public string Id => "category_" + _category.Name;
    public string Label => _category.Name;
    public string IconKey => _category.IconKey;
    public bool HasSubmenu => _category.Slots.Count > 0;

    public CategoryAction(CategoryConfig category, ActionRegistry registry)
    {
        _category = category;
        _registry = registry;
    }

    public Task ExecuteAsync()
    {
        return Task.CompletedTask;
    }

    public IReadOnlyList<IWheelAction> GetSubActions()
    {
        return _registry.CreateFromSlots(_category.Slots);
    }
}
