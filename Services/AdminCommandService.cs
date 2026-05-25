using System.Diagnostics;

namespace MasterWirelessUtility.Services;

public static class AdminCommandService
{
    public static void DisableAdapter(string adapterName)
    {
        RunElevatedPowerShell($"Disable-NetAdapter -Name {Quote(adapterName)} -Confirm:$false");
    }

    public static void EnableAdapter(string adapterName)
    {
        RunElevatedPowerShell($"Enable-NetAdapter -Name {Quote(adapterName)} -Confirm:$false");
    }

    public static void OpenMobileHotspotSettings()
    {
        Process.Start(new ProcessStartInfo("ms-settings:network-mobilehotspot") { UseShellExecute = true });
    }

    public static void OpenNetworkConnections()
    {
        Process.Start(new ProcessStartInfo("ncpa.cpl") { UseShellExecute = true });
    }

    public static void OpenDeviceManager()
    {
        Process.Start(new ProcessStartInfo("devmgmt.msc") { UseShellExecute = true });
    }

    public static void OpenNetworkStatusSettings()
    {
        Process.Start(new ProcessStartInfo("ms-settings:network-status") { UseShellExecute = true });
    }

    private static string Quote(string value)
    {
        return "'" + value.Replace("'", "''") + "'";
    }

    private static void RunElevatedPowerShell(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Normal
        };

        Process.Start(psi);
    }
}
