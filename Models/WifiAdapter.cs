namespace MasterWirelessUtility.Models;

public sealed class WifiAdapter
{
    public Guid Id { get; init; }

    // Windows Wi-Fi API description, for example:
    // Realtek RTL8192FU Wireless LAN 802.11n USB 2.0 Network Adapter
    public string Name { get; init; } = string.Empty;

    // Network adapter alias, for example: Wi-Fi or Wi-Fi 2.
    // This is what PowerShell Disable-NetAdapter / Enable-NetAdapter expects.
    public string InterfaceAlias { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;

    public bool IsRealtek { get; init; }

    public string Badge => IsRealtek ? "REALTEK USB" : "WINDOWS WI-FI";

    public string DisplayName
    {
        get
        {
            string alias = string.IsNullOrWhiteSpace(InterfaceAlias) ? "Unknown alias" : InterfaceAlias;
            string name = string.IsNullOrWhiteSpace(Name) ? "Unknown Wi-Fi adapter" : Name;
            return $"{alias} • {name} ({State})";
        }
    }

    public override string ToString() => DisplayName;
}
