using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using MasterWirelessUtility.Models;
using MasterWirelessUtility.Services;
using MessageBox = System.Windows.MessageBox;

namespace MasterWirelessUtility;

public partial class MainWindow : Window
{
    private readonly NativeWifiService _wifiService = new();
    private NotifyIcon? _notifyIcon;
    private HwndSource? _hwndSource;
    private string _lastApDiagnostic = string.Empty;
    private readonly System.Windows.Threading.DispatcherTimer _adapterRefreshTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(1200)
    };

    private const int WmDeviceChange = 0x0219;
    private const int DbtDeviceArrival = 0x8000;
    private const int DbtDeviceRemoveComplete = 0x8004;
    private const int DbtDevNodesChanged = 0x0007;

    public MainWindow()
    {
        InitializeComponent();
        _adapterRefreshTimer.Tick += (_, _) =>
        {
            _adapterRefreshTimer.Stop();
            ReloadAdaptersAfterDeviceChange();
        };

        CreateTrayIcon();
        LoadAdapters();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _hwndSource?.AddHook(WndProc);
        CrashLogger.Info("Device-change hook attached.");
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmDeviceChange)
        {
            int eventCode = wParam.ToInt32();
            if (eventCode is DbtDeviceArrival or DbtDeviceRemoveComplete or DbtDevNodesChanged)
            {
                string reason = eventCode switch
                {
                    DbtDeviceArrival => "device arrival",
                    DbtDeviceRemoveComplete => "device removal",
                    DbtDevNodesChanged => "device tree changed",
                    _ => "device change"
                };

                ScheduleAdapterRefresh(reason);
            }
        }

        return IntPtr.Zero;
    }

    private void ScheduleAdapterRefresh(string reason)
    {
        CrashLogger.Info($"Windows reported {reason}. Scheduling adapter refresh.");
        SetStatus($"Windows reported {reason}. Refreshing adapter list...");
        _adapterRefreshTimer.Stop();
        _adapterRefreshTimer.Start();
    }

    private void ReloadAdaptersAfterDeviceChange()
    {
        try
        {
            Guid? previousId = SelectedAdapter?.Id;
            LoadAdapters(previousId);
            SetStatus("Adapter list refreshed after hardware change.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private WifiAdapter? SelectedAdapter => AdaptersListBox.SelectedItem as WifiAdapter;
    private WifiNetwork? SelectedNetwork => NetworksGrid.SelectedItem as WifiNetwork;
    private WifiProfile? SelectedProfile => ProfilesGrid.SelectedItem as WifiProfile;

    private void LoadAdapters(Guid? preferredAdapterId = null)
    {
        try
        {
            Guid? currentId = preferredAdapterId ?? SelectedAdapter?.Id;
            var adapters = _wifiService.GetAdapters().ToList();

            AdaptersListBox.ItemsSource = null;
            AdaptersListBox.ItemsSource = adapters;

            if (adapters.Count > 0)
            {
                int selectedIndex = -1;

                if (currentId.HasValue)
                    selectedIndex = adapters.FindIndex(a => a.Id == currentId.Value);

                if (selectedIndex < 0)
                {
                    // Prefer the Realtek USB adapter since this project is focused on the external adapter.
                    selectedIndex = adapters.FindIndex(a =>
                        a.IsRealtek ||
                        string.Equals(a.InterfaceAlias, "Wi-Fi 2", StringComparison.OrdinalIgnoreCase));
                }

                AdaptersListBox.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
            }
            else
            {
                ClearAdapterDetails();
            }

            int realtekCount = adapters.Count(a => a.IsRealtek);
            SetStatus(realtekCount > 0
                ? $"Found {adapters.Count} Wi-Fi adapter(s). Realtek USB adapter detected."
                : $"Found {adapters.Count} Wi-Fi adapter(s). Realtek USB adapter was not detected.");

            CrashLogger.Info($"Loaded {adapters.Count} adapter(s). RealtekCount={realtekCount}.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ClearAdapterDetails()
    {
        GeneralAdapterNameText.Text = "No Wi-Fi adapter selected";
        GeneralAdapterAliasText.Text = "Windows interface alias: Unknown";
        GeneralAdapterBadgeText.Text = "Plug in a Wi-Fi adapter, then click Refresh Adapters.";
        GeneralAdapterGuidText.Text = "Interface GUID: Unknown";
        GeneralAdapterStateText.Text = "State: Unknown";

        NetworksGrid.ItemsSource = null;
        ProfilesGrid.ItemsSource = null;

        StatusAdapterText.Text = "Unknown";
        StatusAliasText.Text = "Unknown";
        StatusGuidText.Text = "Unknown";
        StatusStateText.Text = "Unknown";
        StatusMacText.Text = "Unknown";
        StatusIpText.Text = "Unknown";
        StatusGatewayText.Text = "Unknown";
        StatusDnsText.Text = "Unknown";

        ApInterfaceText.Text = "Unknown";
        ApDriverText.Text = "Unknown";
        ApVendorText.Text = "Unknown";
        ApVersionText.Text = "Unknown";
        ApRadioTypesText.Text = "Unknown";
        ApHostedNetworkText.Text = "Unknown";
        ApWirelessDisplayText.Text = "Unknown";
        ApRecommendationText.Text = "No Wi-Fi adapter selected.";
        ApDiagnosticTextBox.Text = string.Empty;
    }

    private void LoadNetworks(bool scanFirst)
    {
        var adapter = RequireAdapter();
        if (adapter == null) return;

        try
        {
            if (scanFirst)
            {
                _wifiService.Scan(adapter.Id);
                SetStatus("Scan started. Waiting 2 seconds for results...");
                Task.Delay(2000).ContinueWith(_ => Dispatcher.Invoke(() => LoadNetworks(false)));
                return;
            }

            var networks = _wifiService.GetAvailableNetworks(adapter.Id);
            NetworksGrid.ItemsSource = networks;
            SetStatus($"Loaded {networks.Count} available network(s).");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void LoadProfiles()
    {
        var adapter = RequireAdapter();
        if (adapter == null) return;

        try
        {
            var profiles = _wifiService.GetProfiles(adapter.Id);
            ProfilesGrid.ItemsSource = profiles;
            SetStatus($"Loaded {profiles.Count} saved profile(s).");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void LoadStatus()
    {
        var adapter = RequireAdapter();
        if (adapter == null) return;

        try
        {
            var status = AdapterStatusService.GetStatus(adapter);
            StatusAdapterText.Text = status.AdapterName;
            StatusAliasText.Text = status.InterfaceAlias;
            StatusGuidText.Text = status.InterfaceGuid;
            StatusStateText.Text = status.InterfaceState;
            StatusMacText.Text = status.MacAddress;
            StatusIpText.Text = status.Ipv4Address;
            StatusGatewayText.Text = status.Gateway;
            StatusDnsText.Text = status.DnsServers;

            SetStatus("Status refreshed.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private WifiAdapter? RequireAdapter()
    {
        if (SelectedAdapter != null)
            return SelectedAdapter;

        MessageBox.Show("Select a Wi-Fi adapter first.", "No adapter selected", MessageBoxButton.OK, MessageBoxImage.Information);
        return null;
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private void ShowError(Exception ex)
    {
        CrashLogger.Error(ex);
        SetStatus(ex.Message);
        MessageBox.Show($"{ex.Message}\n\nCrash/details log:\n{CrashLogger.CurrentLogFile}", "Master Wireless Utility", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void CreateTrayIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Text = "Master Wireless Utility",
            Icon = LoadTrayIcon(),
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };

        _notifyIcon.ContextMenuStrip.Items.Add("Open", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
        _notifyIcon.ContextMenuStrip.Items.Add("Scan Networks", null, (_, _) => Dispatcher.Invoke(() => LoadNetworks(true)));
        _notifyIcon.ContextMenuStrip.Items.Add("Disconnect", null, (_, _) => Dispatcher.Invoke(DisconnectSelectedAdapter));
        _notifyIcon.ContextMenuStrip.Items.Add("Lock Computer", null, (_, _) => WindowsLockService.LockComputer());
        _notifyIcon.ContextMenuStrip.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(Close));
        _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/AppIcon.ico"));
        if (resource?.Stream == null)
            return System.Drawing.SystemIcons.Application;

        using (resource.Stream)
        {
            using var icon = new System.Drawing.Icon(resource.Stream);
            return (System.Drawing.Icon)icon.Clone();
        }
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void DisconnectSelectedAdapter()
    {
        var adapter = RequireAdapter();
        if (adapter == null) return;

        try
        {
            _wifiService.Disconnect(adapter.Id);
            SetStatus("Disconnect command sent.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void RefreshAdapters_Click(object sender, RoutedEventArgs e) => LoadAdapters();

    private void AdaptersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedAdapter == null)
        {
            ClearAdapterDetails();
            return;
        }

        try
        {
            CrashLogger.Info($"Selected adapter: {SelectedAdapter.DisplayName} ({SelectedAdapter.Id})");
            GeneralAdapterNameText.Text = SelectedAdapter.Name;
            GeneralAdapterAliasText.Text = $"Windows interface alias: {SelectedAdapter.InterfaceAlias}";
            GeneralAdapterBadgeText.Text = SelectedAdapter.IsRealtek
                ? "Realtek USB adapter detected — this app will target Wi-Fi 2 by default."
                : "Standard Windows Wi-Fi adapter selected.";
            GeneralAdapterGuidText.Text = $"Interface GUID: {SelectedAdapter.Id}";
            GeneralAdapterStateText.Text = $"State: {SelectedAdapter.State}";
            LoadProfiles();
            LoadStatus();
            LoadApSupport();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ScanNetworks_Click(object sender, RoutedEventArgs e) => LoadNetworks(true);

    private void Disconnect_Click(object sender, RoutedEventArgs e) => DisconnectSelectedAdapter();

    private void ReloadProfiles_Click(object sender, RoutedEventArgs e) => LoadProfiles();

    private void RefreshStatus_Click(object sender, RoutedEventArgs e) => LoadStatus();

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        var adapter = RequireAdapter();
        if (adapter == null) return;

        try
        {
            string ssid = ProfileSsidTextBox.Text.Trim();
            bool autoConnect = AutoConnectCheckBox.IsChecked == true;
            bool isOpen = SecurityComboBox.SelectedIndex == 1;
            string xml = isOpen
                ? ProfileXmlBuilder.BuildOpenProfile(ssid, autoConnect)
                : ProfileXmlBuilder.BuildWpa2PersonalProfile(ssid, ProfilePasswordBox.Password, autoConnect);

            _wifiService.SaveProfile(adapter.Id, xml, overwrite: true);
            LoadProfiles();
            SetStatus($"Saved profile: {ssid}");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ConnectSelectedProfile_Click(object sender, RoutedEventArgs e)
    {
        var adapter = RequireAdapter();
        if (adapter == null) return;

        if (SelectedProfile == null)
        {
            MessageBox.Show("Select a saved profile first.", "No profile selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            _wifiService.Connect(adapter.Id, SelectedProfile.Name);
            SetStatus($"Connect command sent for profile: {SelectedProfile.Name}");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ConnectSelectedNetwork_Click(object sender, RoutedEventArgs e)
    {
        var adapter = RequireAdapter();
        if (adapter == null) return;

        if (SelectedNetwork == null)
        {
            MessageBox.Show("Select an available network first.", "No network selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var matchingProfile = _wifiService.GetProfiles(adapter.Id)
                .FirstOrDefault(p => string.Equals(p.Name, SelectedNetwork.Ssid, StringComparison.OrdinalIgnoreCase));

            if (matchingProfile == null)
            {
                ProfileSsidTextBox.Text = SelectedNetwork.Ssid;
                MessageBox.Show("No saved profile exists for this network. The SSID was copied to the Profiles tab. Enter the password and save the profile first.",
                    "Profile needed", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _wifiService.Connect(adapter.Id, matchingProfile.Name);
            SetStatus($"Connect command sent for network: {SelectedNetwork.Ssid}");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void DeleteSelectedProfile_Click(object sender, RoutedEventArgs e)
    {
        var adapter = RequireAdapter();
        if (adapter == null) return;

        if (SelectedProfile == null)
        {
            MessageBox.Show("Select a saved profile first.", "No profile selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show($"Delete profile '{SelectedProfile.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            _wifiService.DeleteProfile(adapter.Id, SelectedProfile.Name);
            LoadProfiles();
            SetStatus($"Deleted profile: {SelectedProfile.Name}");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void DuplicateSelectedProfile_Click(object sender, RoutedEventArgs e)
    {
        var adapter = RequireAdapter();
        if (adapter == null) return;

        if (SelectedProfile == null)
        {
            MessageBox.Show("Select a saved profile first.", "No profile selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            string xml = _wifiService.GetProfileXml(adapter.Id, SelectedProfile.Name);
            string newName = SelectedProfile.Name + " Copy";
            xml = xml.Replace($"<name>{SelectedProfile.Name}</name>", $"<name>{newName}</name>");
            _wifiService.SaveProfile(adapter.Id, xml, overwrite: true);
            LoadProfiles();
            SetStatus($"Duplicated profile: {newName}");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void NetworksGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (SelectedNetwork == null)
            return;

        ProfileSsidTextBox.Text = SelectedNetwork.Ssid;
        SetStatus($"Copied SSID to profile form: {SelectedNetwork.Ssid}");
    }


    private void LoadApSupport()
    {
        var adapter = RequireAdapter();
        if (adapter == null) return;

        try
        {
            var info = ApSupportService.GetSupportInfo(adapter);
            ApInterfaceText.Text = string.IsNullOrWhiteSpace(info.InterfaceAlias) ? adapter.InterfaceAlias : info.InterfaceAlias;
            ApDriverText.Text = info.Driver;
            ApVendorText.Text = $"{info.Vendor} / {info.Provider}";
            ApVersionText.Text = info.Version;
            ApRadioTypesText.Text = info.RadioTypesSupported;
            ApHostedNetworkText.Text = info.HostedNetworkSupported;
            ApWirelessDisplayText.Text = info.WirelessDisplaySupported;
            ApRecommendationText.Text = info.Recommendation;

            _lastApDiagnostic = $"Adapter: {adapter.DisplayName}{Environment.NewLine}{Environment.NewLine}{info.RawOutput}";
            ApDiagnosticTextBox.Text = _lastApDiagnostic;

            SetStatus($"AP support checked for {adapter.InterfaceAlias}. Hosted Network: {info.HostedNetworkSupported}");
        }
        catch (Exception ex)
        {
            ApRecommendationText.Text = "Could not check AP support. Make sure WLAN AutoConfig is running and netsh.exe is available.";
            ApDiagnosticTextBox.Text = ex.ToString();
            ShowError(ex);
        }
    }

    private void RefreshApSupport_Click(object sender, RoutedEventArgs e)
    {
        LoadApSupport();
    }

    private void OpenHotspotSettings_Click(object sender, RoutedEventArgs e)
    {
        AdminCommandService.OpenMobileHotspotSettings();
    }

    private void OpenNetworkConnections_Click(object sender, RoutedEventArgs e)
    {
        AdminCommandService.OpenNetworkConnections();
    }

    private void OpenDeviceManager_Click(object sender, RoutedEventArgs e)
    {
        AdminCommandService.OpenDeviceManager();
    }

    private void CopyApDiagnostic_Click(object sender, RoutedEventArgs e)
    {
        string text = string.IsNullOrWhiteSpace(_lastApDiagnostic) ? ApDiagnosticTextBox.Text : _lastApDiagnostic;
        if (string.IsNullOrWhiteSpace(text))
        {
            MessageBox.Show("No AP diagnostic text to copy yet. Click Refresh AP Support first.", "Nothing to copy", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        System.Windows.Clipboard.SetText(text);
        SetStatus("AP diagnostic copied to clipboard.");
    }

    private void DisableAdapter_Click(object sender, RoutedEventArgs e)
    {
        var adapter = RequireAdapter();
        if (adapter == null) return;

        if (MessageBox.Show($"Disable adapter '{adapter.InterfaceAlias}'? This requires admin permission.", "Confirm Disable", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            AdminCommandService.DisableAdapter(adapter.InterfaceAlias);
            SetStatus("Disable command launched with administrator permission request.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void EnableAdapter_Click(object sender, RoutedEventArgs e)
    {
        var adapter = RequireAdapter();
        if (adapter == null) return;

        try
        {
            AdminCommandService.EnableAdapter(adapter.InterfaceAlias);
            SetStatus("Enable command launched with administrator permission request.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void LockComputer_Click(object sender, RoutedEventArgs e)
    {
        WindowsLockService.LockComputer();
    }

    private void ShowTrayCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_notifyIcon != null)
            _notifyIcon.Visible = ShowTrayCheckBox.IsChecked == true;
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (ShowTrayCheckBox.IsChecked == true && _notifyIcon != null)
        {
            e.Cancel = true;
            Hide();
            _notifyIcon.ShowBalloonTip(1500, "Master Wireless Utility", "Still running in the tray.", ToolTipIcon.Info);
            return;
        }

        _adapterRefreshTimer.Stop();
        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }

        _notifyIcon?.Dispose();
        _wifiService.Dispose();
    }
}
