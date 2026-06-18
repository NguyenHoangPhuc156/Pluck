using Microsoft.Win32;

namespace Pluck.Core.Services;

/// <summary>
/// Manages Windows startup registration for Pluck via the current-user Run registry key.
/// </summary>
public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Pluck";

    /// <summary>
    /// Enables or disables launching Pluck automatically when the current user signs in.
    /// </summary>
    /// <param name="enabled"><see langword="true"/> to register Pluck for startup; <see langword="false"/> to remove the entry.</param>
    /// <param name="executablePath">Optional path to the executable; defaults to the current process path when enabling.</param>
    /// <exception cref="InvalidOperationException">Thrown when the registry key or executable path cannot be resolved.</exception>
    public static void SetEnabled(bool enabled, string? executablePath = null)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Cannot open Run registry key.");

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var path = executablePath ?? Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot resolve executable path.");
        key.SetValue(ValueName, $"\"{path}\"");
    }

    /// <summary>
    /// Determines whether Pluck is registered to run at Windows startup for the current user.
    /// </summary>
    /// <returns><see langword="true"/> if a startup entry exists; otherwise, <see langword="false"/>.</returns>
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        return key?.GetValue(ValueName) is string;
    }
}
