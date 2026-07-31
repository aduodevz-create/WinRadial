using WinRadial.Core;

namespace WinRadial.Actions;

/// <summary>
/// Empties the Windows Recycle Bin using SHEmptyRecycleBin P/Invoke.
/// Uses silent mode (no confirmation dialog, no progress UI, no sound).
/// </summary>
public sealed class EmptyRecycleBinAction : IWheelAction
{
    private readonly LogService _log;

    public string Id => "empty_recycle_bin";
    public string Label { get; }
    public string IconKey { get; }
    public bool HasSubmenu => false;
    public bool CloseWheelOnExecute => true;

    public EmptyRecycleBinAction(string label, string iconKey, LogService log)
    {
        Label = label;
        IconKey = iconKey;
        _log = log;
    }

    public Task ExecuteAsync()
    {
        try
        {
            var flags = WindowInterop.SHERB_NOCONFIRMATION
                      | WindowInterop.SHERB_NOPROGRESSUI
                      | WindowInterop.SHERB_NOSOUND;

            var result = WindowInterop.SHEmptyRecycleBinW(IntPtr.Zero, IntPtr.Zero, flags);

            if (result == 0)
            {
                _log.Info("Recycle bin emptied successfully.");
            }
            else
            {
                // S_FALSE (1) means bin was already empty — not an error
                _log.Info($"SHEmptyRecycleBin returned HRESULT: 0x{result:X8}");
            }
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to empty recycle bin: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<IWheelAction> GetSubActions() => [];
}
