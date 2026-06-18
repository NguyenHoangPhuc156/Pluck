using System.Windows.Input;
using Pluck.Data.Models;

namespace Pluck.UI.Helpers;

/// <summary>
/// Resolves configured mouse bindings and modifier-key requirements for bubble interaction.
/// </summary>
internal static class BubbleMouseBindingHelper
{
    /// <summary>
    /// Returns the mouse binding configured for the specified button.
    /// </summary>
    /// <param name="settings">Application settings containing per-button bindings.</param>
    /// <param name="button">The mouse button being evaluated.</param>
    /// <returns>The binding for <paramref name="button"/>, or the left-button binding for unknown buttons.</returns>
    public static BubbleMouseBinding GetBinding(PluckSettings settings, MouseButton button) =>
        button switch
        {
            MouseButton.Left => settings.MouseLeft,
            MouseButton.Right => settings.MouseRight,
            MouseButton.Middle => settings.MouseMiddle,
            _ => settings.MouseLeft
        };

    /// <summary>
    /// Determines whether the pressed modifier keys satisfy a binding's requirements.
    /// </summary>
    /// <param name="binding">The binding whose modifier requirements are checked.</param>
    /// <param name="mods">Currently pressed keyboard modifiers.</param>
    /// <returns><see langword="true"/> when all required modifiers match; otherwise <see langword="false"/>.</returns>
    public static bool ModifiersMatch(BubbleMouseBinding binding, ModifierKeys mods)
    {
        var ctrl = (mods & ModifierKeys.Control) != 0;
        var shift = (mods & ModifierKeys.Shift) != 0;
        var alt = (mods & ModifierKeys.Alt) != 0;
        return ctrl == binding.RequireCtrl
               && shift == binding.RequireShift
               && alt == binding.RequireAlt;
    }
}
