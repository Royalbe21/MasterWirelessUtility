namespace MasterWirelessUtility.Models;

public sealed class ApSupportInfo
{
    public string InterfaceAlias { get; init; } = string.Empty;
    public string Driver { get; init; } = "Unknown";
    public string Vendor { get; init; } = "Unknown";
    public string Provider { get; init; } = "Unknown";
    public string Version { get; init; } = "Unknown";
    public string RadioTypesSupported { get; init; } = "Unknown";
    public string HostedNetworkSupported { get; init; } = "Unknown";
    public string WirelessDisplaySupported { get; init; } = "Unknown";
    public string RawOutput { get; init; } = string.Empty;

    public bool SupportsHostedNetwork => HostedNetworkSupported.Equals("Yes", StringComparison.OrdinalIgnoreCase);

    public string Recommendation
    {
        get
        {
            if (SupportsHostedNetwork)
                return "Legacy Hosted Network appears supported. You may be able to test classic Soft AP commands, but Windows 11 Mobile Hotspot is still the safer path.";

            if (HostedNetworkSupported.Equals("No", StringComparison.OrdinalIgnoreCase))
                return "Legacy Hosted Network Soft AP is not supported by this driver. Use Windows 11 Mobile Hotspot or Wi-Fi Direct instead.";

            return "Hosted Network support could not be confirmed. Refresh the check, verify WLAN AutoConfig is running, and confirm the adapter is selected.";
        }
    }
}
