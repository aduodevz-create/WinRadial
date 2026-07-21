using WinRadial.Core;

namespace WinRadial.Actions;

/// <summary>
/// Puts the system to sleep using SetSuspendState P/Invoke.
/// </summary>
public sealed class SleepAction : IWheelAction
{
    public string Id => "sleep";
    public string Label { get; }
    public string IconKey { get; }
    public bool HasSubmenu => false;

    public SleepAction(string label, string iconKey)
    {
        Label = label;
        IconKey = iconKey;
    }

    public Task ExecuteAsync()
    {
        // bHibernate=false (sleep, not hibernate), bForce=false, bWakeupEventsDisabled=false
        WindowInterop.SetSuspendState(false, false, false);
        return Task.CompletedTask;
    }

    public IReadOnlyList<IWheelAction> GetSubActions() => [];
}
