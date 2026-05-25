namespace MasterWirelessUtility.Models;

public sealed class AdapterStatus
{
    public string AdapterName { get; init; } = string.Empty;
    public string InterfaceAlias { get; init; } = string.Empty;
    public string InterfaceGuid { get; init; } = string.Empty;
    public string InterfaceState { get; init; } = string.Empty;
    public string MacAddress { get; init; } = string.Empty;
    public string Ipv4Address { get; init; } = string.Empty;
    public string Gateway { get; init; } = string.Empty;
    public string DnsServers { get; init; } = string.Empty;
}
