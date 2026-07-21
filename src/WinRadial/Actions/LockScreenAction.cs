using WinRadial.Core;

namespace WinRadial.Actions;

/// <summary>
/// Locks the workstation using LockWorkStation P/Invoke.
/// </summary>
public sealed class LockScreenAction : IWheelAction
{
    public string Id => "lock_screen";
    public string Label { get; }
    public string IconKey { get; }
    public bool HasSubmenu => false;

    public LockScreenAction(string label, string iconKey)
    {
        Label = label;
        IconKey = iconKey;
    }

    public Task ExecuteAsync()
    {
        WindowInterop.LockWorkStation();
        return Task.CompletedTask;
    }

    public IReadOnlyList<IWheelAction> GetSubActions() => [];
}
