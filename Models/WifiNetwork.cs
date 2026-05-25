namespace MasterWirelessUtility.Models;

public sealed class WifiNetwork
{
    public string Ssid { get; init; } = string.Empty;
    public uint SignalQuality { get; init; }
    public string Authentication { get; init; } = string.Empty;
    public string Encryption { get; init; } = string.Empty;
    public bool SecurityEnabled { get; init; }
    public bool Connectable { get; init; }
    public string BssType { get; init; } = string.Empty;
    public uint BssidCount { get; init; }
    public string ProfileName { get; init; } = string.Empty;
}
