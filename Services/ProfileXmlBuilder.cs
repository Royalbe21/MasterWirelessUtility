using System.Security;
using System.Text;
using System.Xml.Linq;

namespace MasterWirelessUtility.Services;

public static class ProfileXmlBuilder
{
    public static string BuildWpa2PersonalProfile(string ssid, string password, bool autoConnect)
    {
        if (string.IsNullOrWhiteSpace(ssid))
            throw new ArgumentException("SSID cannot be empty.", nameof(ssid));

        if (password.Length < 8 || password.Length > 63)
            throw new ArgumentException("WPA/WPA2 personal passwords must be 8 to 63 characters.", nameof(password));

        string safeSsid = EscapeXml(ssid.Trim());
        string safePassword = EscapeXml(password);
        string connectionMode = autoConnect ? "auto" : "manual";

        return $$"""
               <WLANProfile xmlns="http://www.microsoft.com/networking/WLAN/profile/v1">
                 <name>{{safeSsid}}</name>
                 <SSIDConfig>
                   <SSID>
                     <name>{{safeSsid}}</name>
                   </SSID>
                 </SSIDConfig>
                 <connectionType>ESS</connectionType>
                 <connectionMode>{{connectionMode}}</connectionMode>
                 <MSM>
                   <security>
                     <authEncryption>
                       <authentication>WPA2PSK</authentication>
                       <encryption>AES</encryption>
                       <useOneX>false</useOneX>
                     </authEncryption>
                     <sharedKey>
                       <keyType>passPhrase</keyType>
                       <protected>false</protected>
                       <keyMaterial>{{safePassword}}</keyMaterial>
                     </sharedKey>
                   </security>
                 </MSM>
               </WLANProfile>
               """;
    }

    public static string BuildOpenProfile(string ssid, bool autoConnect)
    {
        if (string.IsNullOrWhiteSpace(ssid))
            throw new ArgumentException("SSID cannot be empty.", nameof(ssid));

        string safeSsid = EscapeXml(ssid.Trim());
        string connectionMode = autoConnect ? "auto" : "manual";

        return $$"""
               <WLANProfile xmlns="http://www.microsoft.com/networking/WLAN/profile/v1">
                 <name>{{safeSsid}}</name>
                 <SSIDConfig>
                   <SSID>
                     <name>{{safeSsid}}</name>
                   </SSID>
                 </SSIDConfig>
                 <connectionType>ESS</connectionType>
                 <connectionMode>{{connectionMode}}</connectionMode>
                 <MSM>
                   <security>
                     <authEncryption>
                       <authentication>open</authentication>
                       <encryption>none</encryption>
                       <useOneX>false</useOneX>
                     </authEncryption>
                   </security>
                 </MSM>
               </WLANProfile>
               """;
    }

    private static string EscapeXml(string value)
    {
        return SecurityElement.Escape(value) ?? string.Empty;
    }
}
