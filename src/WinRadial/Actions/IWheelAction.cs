namespace WinRadial.Actions;

/// <summary>
/// Interface for all wheel actions. Each wedge in the radial menu
/// is backed by an IWheelAction implementation.
/// </summary>
public interface IWheelAction
{
    /// <summary>Unique action identifier matching config actionId values.</summary>
    string Id { get; }

    /// <summary>Display label shown on the wedge.</summary>
    string Label { get; }

    /// <summary>Icon key (Segoe MDL2 glyph or icon identifier).</summary>
    string IconKey { get; }

    /// <summary>Whether this action opens a submenu ring instead of executing directly.</summary>
    bool HasSubmenu { get; }

    /// <summary>Executes the action asynchronously.</summary>
    Task ExecuteAsync();

    /// <summary>Returns child actions for the submenu ring (empty if HasSubmenu is false).</summary>
    IReadOnlyList<IWheelAction> GetSubActions();

    /// <summary>Whether the wheel should be closed immediately before executing this action.</summary>
    bool CloseWheelOnExecute => false;
}
