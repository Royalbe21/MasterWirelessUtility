# Master Wireless Utility v1.2

Windows 11 Wi-Fi adapter manager built with C# / .NET 8 / WPF.

## v1.2 additions

- Added a stronger **Windows 11 AP / Hotspot Helper** tab.
- Checks `netsh wlan show drivers` for the selected adapter.
- Displays driver, vendor/provider, version, radio types, Wireless Display support, and **Hosted Network Soft AP** support.
- Shows a clear recommendation when Windows 11 or the selected driver blocks old Realtek Soft AP behavior.
- Adds buttons for:
  - Refresh AP Support
  - Open Mobile Hotspot Settings
  - Open Network Connections
  - Open Device Manager
  - Copy AP Diagnostic
- Keeps old Realtek advanced AP controls visible but locked when Windows 11 does not expose them:
  - Beacon Interval
  - DTIM Period
  - Preamble Mode
  - Channel
  - Legacy Hosted Network

## Build

Open PowerShell in this folder and run:

```powershell
dotnet clean
dotnet restore
dotnet build -c Release
```

## Publish self-contained EXE

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish-selfcontained
```

Run:

```powershell
.\publish-selfcontained\MasterWirelessUtility.exe
```

## Notes

The app does not install a custom driver. It uses the current Windows 11 Wi-Fi driver and Windows networking tools.

If the AP tab shows `Hosted network supported: No`, classic Hosted Network / old Realtek Soft AP cannot be forced through normal Windows app code. Use Windows Mobile Hotspot or a Linux/OpenWrt AP setup for deeper AP controls.
