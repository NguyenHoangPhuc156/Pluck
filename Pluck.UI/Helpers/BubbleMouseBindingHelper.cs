using System.Windows.Input;
using Pluck.Data.Models;

namespace Pluck.UI.Helpers;

internal static class BubbleMouseBindingHelper
{
    public static BubbleMouseBinding GetBinding(PluckSettings settings, MouseButton button) =>
        button switch
        {
            MouseButton.Left => settings.MouseLeft,
            MouseButton.Right => settings.MouseRight,
            MouseButton.Middle => settings.MouseMiddle,
            _ => settings.MouseLeft
        };

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
