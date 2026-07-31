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
    public bool CloseWheelOnExecute => true;

    public SleepAction(string label, string iconKey)
    {
        Label = label;
        IconKey = iconKey;
    }

    public Task ExecuteAsync()
    {
        bool wasHibernationEnabled = false;
        try
        {
            // Windows SetSuspendState has a long-standing issue where it will hibernate
            // instead of sleep if hibernation is enabled. To force sleep, we temporarily disable it.
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power");
            if (key != null)
            {
                var val = key.GetValue("HibernateEnabled");
                if (val is int intVal && intVal != 0)
                {
                    wasHibernationEnabled = true;
                }
            }

            if (wasHibernationEnabled)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "/h off",
                    CreateNoWindow = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                })?.WaitForExit();
            }
        }
        catch { }

        // bHibernate=false (sleep, not hibernate), bForce=false, bWakeupEventsDisabled=false
        WindowInterop.SetSuspendState(false, false, false);

        if (wasHibernationEnabled)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "/h on",
                    CreateNoWindow = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                })?.WaitForExit();
            }
            catch { }
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<IWheelAction> GetSubActions() => [];
}
