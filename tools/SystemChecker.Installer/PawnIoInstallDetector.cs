using Microsoft.Win32;

namespace CoreWatch.Installer;

internal static class PawnIoInstallDetector
{
    internal static readonly Version MinimumVersion = new(2, 2, 0);

    internal static bool RequiresInstall(Version? installedVersion) => installedVersion is null || installedVersion < MinimumVersion;

    internal static Version? GetInstalledVersion()
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO");
                if (Version.TryParse(key?.GetValue("DisplayVersion")?.ToString(), out var version)) return version;
            }
            catch { }
        }
        return null;
    }
}
