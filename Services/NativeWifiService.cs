using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using MasterWirelessUtility.Models;

namespace MasterWirelessUtility.Services;

public sealed class NativeWifiService : IDisposable
{
    private readonly IntPtr _clientHandle;
    private bool _disposed;

    public NativeWifiService()
    {
        uint negotiatedVersion;
        uint result = WlanOpenHandle(2, IntPtr.Zero, out negotiatedVersion, out _clientHandle);
        ThrowIfError(result, "WlanOpenHandle failed. Make sure the Windows WLAN AutoConfig service is running.");
    }

    public IReadOnlyList<WifiAdapter> GetAdapters()
    {
        EnsureNotDisposed();
        IntPtr listPtr = IntPtr.Zero;
        try
        {
            uint result = WlanEnumInterfaces(_clientHandle, IntPtr.Zero, out listPtr);
            ThrowIfError(result, "WlanEnumInterfaces failed.");

            int count = Marshal.ReadInt32(listPtr, 0);
            IntPtr itemPtr = IntPtr.Add(listPtr, 8);
            int itemSize = Marshal.SizeOf<WLAN_INTERFACE_INFO>();

            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            var adapters = new List<WifiAdapter>();

            for (int i = 0; i < count; i++)
            {
                var info = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(IntPtr.Add(itemPtr, i * itemSize));
                var nic = FindMatchingNetworkInterface(info.InterfaceGuid, info.strInterfaceDescription, networkInterfaces);

                string description = string.IsNullOrWhiteSpace(info.strInterfaceDescription)
                    ? nic?.Description ?? "Unknown Wi-Fi adapter"
                    : info.strInterfaceDescription;

                adapters.Add(new WifiAdapter
                {
                    Id = info.InterfaceGuid,
                    Name = description,
                    InterfaceAlias = nic?.Name ?? description,
                    State = info.isState.ToString(),
                    IsRealtek = description.Contains("Realtek", StringComparison.OrdinalIgnoreCase) ||
                                (nic?.Description.Contains("Realtek", StringComparison.OrdinalIgnoreCase) ?? false)
                });
            }

            return adapters
                .OrderByDescending(a => a.IsRealtek)
                .ThenBy(a => a.InterfaceAlias)
                .ToList();
        }
        finally
        {
            if (listPtr != IntPtr.Zero)
                WlanFreeMemory(listPtr);
        }
    }

    private static NetworkInterface? FindMatchingNetworkInterface(Guid interfaceId, string description, IEnumerable<NetworkInterface> networkInterfaces)
    {
        foreach (var nic in networkInterfaces)
        {
            if (Guid.TryParse(nic.Id, out Guid nicGuid) && nicGuid == interfaceId)
                return nic;

            if (Guid.TryParse(nic.Id.Trim('{', '}'), out nicGuid) && nicGuid == interfaceId)
                return nic;
        }

        return networkInterfaces.FirstOrDefault(n =>
            string.Equals(n.Description, description, StringComparison.OrdinalIgnoreCase));
    }

    public void Scan(Guid interfaceId)
    {
        EnsureNotDisposed();
        uint result = WlanScan(_clientHandle, ref interfaceId, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        ThrowIfError(result, "WlanScan failed.");
    }

    public IReadOnlyList<WifiNetwork> GetAvailableNetworks(Guid interfaceId)
    {
        EnsureNotDisposed();
        IntPtr listPtr = IntPtr.Zero;
        try
        {
            const int wlanAvailableNetworkIncludeAllAdhocProfiles = 0x00000001;
            const int wlanAvailableNetworkIncludeAllManualHiddenProfiles = 0x00000002;

            uint result = WlanGetAvailableNetworkList(
                _clientHandle,
                ref interfaceId,
                wlanAvailableNetworkIncludeAllAdhocProfiles | wlanAvailableNetworkIncludeAllManualHiddenProfiles,
                IntPtr.Zero,
                out listPtr);

            ThrowIfError(result, "WlanGetAvailableNetworkList failed.");

            int count = Marshal.ReadInt32(listPtr, 0);
            IntPtr itemPtr = IntPtr.Add(listPtr, 8);
            int itemSize = Marshal.SizeOf<WLAN_AVAILABLE_NETWORK>();

            var networks = new List<WifiNetwork>();
            for (int i = 0; i < count; i++)
            {
                var network = Marshal.PtrToStructure<WLAN_AVAILABLE_NETWORK>(IntPtr.Add(itemPtr, i * itemSize));
                networks.Add(new WifiNetwork
                {
                    Ssid = DecodeSsid(network.dot11Ssid),
                    ProfileName = network.strProfileName,
                    SignalQuality = network.wlanSignalQuality,
                    Authentication = network.dot11DefaultAuthAlgorithm.ToString(),
                    Encryption = network.dot11DefaultCipherAlgorithm.ToString(),
                    SecurityEnabled = network.bSecurityEnabled,
                    Connectable = network.bNetworkConnectable,
                    BssType = network.dot11BssType.ToString(),
                    BssidCount = network.uNumberOfBssids
                });
            }

            return networks
                .Where(n => !string.IsNullOrWhiteSpace(n.Ssid))
                .GroupBy(n => n.Ssid)
                .Select(g => g.OrderByDescending(n => n.SignalQuality).First())
                .OrderByDescending(n => n.SignalQuality)
                .ThenBy(n => n.Ssid)
                .ToList();
        }
        finally
        {
            if (listPtr != IntPtr.Zero)
                WlanFreeMemory(listPtr);
        }
    }

    public IReadOnlyList<WifiProfile> GetProfiles(Guid interfaceId)
    {
        EnsureNotDisposed();
        IntPtr listPtr = IntPtr.Zero;
        try
        {
            uint result = WlanGetProfileList(_clientHandle, ref interfaceId, IntPtr.Zero, out listPtr);
            ThrowIfError(result, "WlanGetProfileList failed.");

            int count = Marshal.ReadInt32(listPtr, 0);
            IntPtr itemPtr = IntPtr.Add(listPtr, 8);
            int itemSize = Marshal.SizeOf<WLAN_PROFILE_INFO>();

            var profiles = new List<WifiProfile>();
            for (int i = 0; i < count; i++)
            {
                var profile = Marshal.PtrToStructure<WLAN_PROFILE_INFO>(IntPtr.Add(itemPtr, i * itemSize));
                profiles.Add(new WifiProfile
                {
                    Name = profile.strProfileName,
                    Flags = profile.dwFlags
                });
            }

            return profiles.OrderBy(p => p.Name).ToList();
        }
        finally
        {
            if (listPtr != IntPtr.Zero)
                WlanFreeMemory(listPtr);
        }
    }

    public string GetProfileXml(Guid interfaceId, string profileName)
    {
        EnsureNotDisposed();
        IntPtr profileXmlPtr = IntPtr.Zero;
        try
        {
            uint flags = 0;
            uint access = 0;
            uint result = WlanGetProfile(
                _clientHandle,
                ref interfaceId,
                profileName,
                IntPtr.Zero,
                out profileXmlPtr,
                ref flags,
                out access);

            ThrowIfError(result, "WlanGetProfile failed.");
            return Marshal.PtrToStringUni(profileXmlPtr) ?? string.Empty;
        }
        finally
        {
            if (profileXmlPtr != IntPtr.Zero)
                WlanFreeMemory(profileXmlPtr);
        }
    }

    public void SaveProfile(Guid interfaceId, string profileXml, bool overwrite = true)
    {
        EnsureNotDisposed();
        uint reasonCode;
        uint result = WlanSetProfile(
            _clientHandle,
            ref interfaceId,
            0,
            profileXml,
            null,
            overwrite,
            IntPtr.Zero,
            out reasonCode);

        if (result != 0)
            throw new Win32Exception((int)result, $"WlanSetProfile failed. Reason code: {reasonCode}");
    }

    public void DeleteProfile(Guid interfaceId, string profileName)
    {
        EnsureNotDisposed();
        uint result = WlanDeleteProfile(_clientHandle, ref interfaceId, profileName, IntPtr.Zero);
        ThrowIfError(result, "WlanDeleteProfile failed.");
    }

    public void Connect(Guid interfaceId, string profileName)
    {
        EnsureNotDisposed();
        var parameters = new WLAN_CONNECTION_PARAMETERS
        {
            wlanConnectionMode = WLAN_CONNECTION_MODE.wlan_connection_mode_profile,
            strProfile = profileName,
            pDot11Ssid = IntPtr.Zero,
            pDesiredBssidList = IntPtr.Zero,
            dot11BssType = DOT11_BSS_TYPE.dot11_BSS_type_infrastructure,
            dwFlags = 0
        };

        uint result = WlanConnect(_clientHandle, ref interfaceId, ref parameters, IntPtr.Zero);
        ThrowIfError(result, "WlanConnect failed.");
    }

    public void Disconnect(Guid interfaceId)
    {
        EnsureNotDisposed();
        uint result = WlanDisconnect(_clientHandle, ref interfaceId, IntPtr.Zero);
        ThrowIfError(result, "WlanDisconnect failed.");
    }

    private static string DecodeSsid(DOT11_SSID ssid)
    {
        if (ssid.ucSSID == null || ssid.uSSIDLength == 0)
            return string.Empty;

        int length = (int)Math.Min(ssid.uSSIDLength, (uint)ssid.ucSSID.Length);
        return Encoding.UTF8.GetString(ssid.ucSSID, 0, length).TrimEnd('\0');
    }

    private static void ThrowIfError(uint result, string message)
    {
        if (result != 0)
            throw new Win32Exception((int)result, message);
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(NativeWifiService));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        if (_clientHandle != IntPtr.Zero)
            WlanCloseHandle(_clientHandle, IntPtr.Zero);

        _disposed = true;
    }

    [DllImport("wlanapi.dll")]
    private static extern uint WlanOpenHandle(uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(IntPtr pMemory);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanScan(IntPtr hClientHandle, ref Guid pInterfaceGuid, IntPtr pDot11Ssid, IntPtr pIeData, IntPtr pReserved);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanGetAvailableNetworkList(IntPtr hClientHandle, ref Guid pInterfaceGuid, int dwFlags, IntPtr pReserved, out IntPtr ppAvailableNetworkList);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanGetProfileList(IntPtr hClientHandle, ref Guid pInterfaceGuid, IntPtr pReserved, out IntPtr ppProfileList);

    [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)]
    private static extern uint WlanGetProfile(IntPtr hClientHandle, ref Guid pInterfaceGuid, string strProfileName, IntPtr pReserved, out IntPtr pstrProfileXml, ref uint pdwFlags, out uint pdwGrantedAccess);

    [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)]
    private static extern uint WlanSetProfile(IntPtr hClientHandle, ref Guid pInterfaceGuid, uint dwFlags, string strProfileXml, string? strAllUserProfileSecurity, bool bOverwrite, IntPtr pReserved, out uint pdwReasonCode);

    [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)]
    private static extern uint WlanDeleteProfile(IntPtr hClientHandle, ref Guid pInterfaceGuid, string strProfileName, IntPtr pReserved);

    [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)]
    private static extern uint WlanConnect(IntPtr hClientHandle, ref Guid pInterfaceGuid, ref WLAN_CONNECTION_PARAMETERS pConnectionParameters, IntPtr pReserved);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanDisconnect(IntPtr hClientHandle, ref Guid pInterfaceGuid, IntPtr pReserved);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_INTERFACE_INFO
    {
        public Guid InterfaceGuid;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strInterfaceDescription;

        public WLAN_INTERFACE_STATE isState;
    }

    private enum WLAN_INTERFACE_STATE
    {
        wlan_interface_state_not_ready = 0,
        wlan_interface_state_connected = 1,
        wlan_interface_state_ad_hoc_network_formed = 2,
        wlan_interface_state_disconnecting = 3,
        wlan_interface_state_disconnected = 4,
        wlan_interface_state_associating = 5,
        wlan_interface_state_discovering = 6,
        wlan_interface_state_authenticating = 7
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DOT11_SSID
    {
        public uint uSSIDLength;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] ucSSID;
    }

    private enum DOT11_BSS_TYPE
    {
        dot11_BSS_type_infrastructure = 1,
        dot11_BSS_type_independent = 2,
        dot11_BSS_type_any = 3
    }

    private enum DOT11_AUTH_ALGORITHM
    {
        DOT11_AUTH_ALGO_80211_OPEN = 1,
        DOT11_AUTH_ALGO_80211_SHARED_KEY = 2,
        DOT11_AUTH_ALGO_WPA = 3,
        DOT11_AUTH_ALGO_WPA_PSK = 4,
        DOT11_AUTH_ALGO_WPA_NONE = 5,
        DOT11_AUTH_ALGO_RSNA = 6,
        DOT11_AUTH_ALGO_RSNA_PSK = 7,
        DOT11_AUTH_ALGO_WPA3 = 8,
        DOT11_AUTH_ALGO_WPA3_SAE = 9,
        DOT11_AUTH_ALGO_OWE = 10
    }

    private enum DOT11_CIPHER_ALGORITHM
    {
        DOT11_CIPHER_ALGO_NONE = 0x00,
        DOT11_CIPHER_ALGO_WEP40 = 0x01,
        DOT11_CIPHER_ALGO_TKIP = 0x02,
        DOT11_CIPHER_ALGO_CCMP = 0x04,
        DOT11_CIPHER_ALGO_WEP104 = 0x05,
        DOT11_CIPHER_ALGO_BIP = 0x06,
        DOT11_CIPHER_ALGO_GCMP = 0x08,
        DOT11_CIPHER_ALGO_GCMP_256 = 0x09,
        DOT11_CIPHER_ALGO_CCMP_256 = 0x0a,
        DOT11_CIPHER_ALGO_WPA_USE_GROUP = 0x100,
        DOT11_CIPHER_ALGO_RSN_USE_GROUP = 0x100,
        DOT11_CIPHER_ALGO_WEP = 0x101
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_AVAILABLE_NETWORK
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strProfileName;

        public DOT11_SSID dot11Ssid;
        public DOT11_BSS_TYPE dot11BssType;
        public uint uNumberOfBssids;

        [MarshalAs(UnmanagedType.Bool)]
        public bool bNetworkConnectable;

        public uint wlanNotConnectableReason;
        public uint uNumberOfPhyTypes;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public uint[] dot11PhyTypes;

        [MarshalAs(UnmanagedType.Bool)]
        public bool bMorePhyTypes;

        public uint wlanSignalQuality;

        [MarshalAs(UnmanagedType.Bool)]
        public bool bSecurityEnabled;

        public DOT11_AUTH_ALGORITHM dot11DefaultAuthAlgorithm;
        public DOT11_CIPHER_ALGORITHM dot11DefaultCipherAlgorithm;
        public uint dwFlags;
        public uint dwReserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_PROFILE_INFO
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strProfileName;

        public uint dwFlags;
    }

    private enum WLAN_CONNECTION_MODE
    {
        wlan_connection_mode_profile = 0,
        wlan_connection_mode_temporary_profile = 1,
        wlan_connection_mode_discovery_secure = 2,
        wlan_connection_mode_discovery_unsecure = 3,
        wlan_connection_mode_auto = 4,
        wlan_connection_mode_invalid = 5
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_CONNECTION_PARAMETERS
    {
        public WLAN_CONNECTION_MODE wlanConnectionMode;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? strProfile;

        public IntPtr pDot11Ssid;
        public IntPtr pDesiredBssidList;
        public DOT11_BSS_TYPE dot11BssType;
        public uint dwFlags;
    }
}
