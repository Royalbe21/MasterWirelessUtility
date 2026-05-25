using System.Net.NetworkInformation;
using MasterWirelessUtility.Models;

namespace MasterWirelessUtility.Services;

public static class AdapterStatusService
{
    public static AdapterStatus GetStatus(WifiAdapter adapter)
    {
        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n =>
                    (Guid.TryParse(n.Id, out Guid nicGuid) && nicGuid == adapter.Id) ||
                    (Guid.TryParse(n.Id.Trim('{', '}'), out nicGuid) && nicGuid == adapter.Id) ||
                    string.Equals(n.Name, adapter.InterfaceAlias, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(n.Description, adapter.Name, StringComparison.OrdinalIgnoreCase));

            if (nic == null)
                return UnknownStatus(adapter, "Adapter not currently visible to Windows.");

            var ipProps = nic.GetIPProperties();
            string ipv4 = string.Join(", ", ipProps.UnicastAddresses
                .Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(a => a.Address.ToString()));

            string gateway = string.Join(", ", ipProps.GatewayAddresses
                .Where(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(g => g.Address.ToString()));

            string dns = string.Join(", ", ipProps.DnsAddresses
                .Where(d => d.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(d => d.ToString()));

            return new AdapterStatus
            {
                AdapterName = adapter.Name,
                InterfaceAlias = nic.Name,
                InterfaceGuid = adapter.Id.ToString(),
                InterfaceState = adapter.State,
                MacAddress = FormatMac(nic.GetPhysicalAddress().GetAddressBytes()),
                Ipv4Address = string.IsNullOrWhiteSpace(ipv4) ? "Not assigned" : ipv4,
                Gateway = string.IsNullOrWhiteSpace(gateway) ? "Not assigned" : gateway,
                DnsServers = string.IsNullOrWhiteSpace(dns) ? "Not assigned" : dns
            };
        }
        catch (Exception ex)
        {
            CrashLogger.Error(ex, "AdapterStatusService.GetStatus failed. The adapter may have been inserted or removed while status was being read.");
            return UnknownStatus(adapter, "Temporarily unavailable after hardware change.");
        }
    }

    private static AdapterStatus UnknownStatus(WifiAdapter adapter, string state)
    {
        return new AdapterStatus
        {
            AdapterName = adapter.Name,
            InterfaceAlias = string.IsNullOrWhiteSpace(adapter.InterfaceAlias) ? "Unknown" : adapter.InterfaceAlias,
            InterfaceGuid = adapter.Id.ToString(),
            InterfaceState = state,
            MacAddress = "Unknown",
            Ipv4Address = "Unknown",
            Gateway = "Unknown",
            DnsServers = "Unknown"
        };
    }

    private static string FormatMac(byte[] bytes)
    {
        return bytes.Length == 0 ? "Unknown" : string.Join(":", bytes.Select(b => b.ToString("X2")));
    }
}
