using System.Diagnostics;
using System.Text;
using MasterWirelessUtility.Models;

namespace MasterWirelessUtility.Services;

public static class ApSupportService
{
    public static ApSupportInfo GetSupportInfo(WifiAdapter adapter)
    {
        string output = RunNetsh("wlan show drivers");
        string block = ExtractInterfaceBlock(output, adapter.InterfaceAlias);

        if (string.IsNullOrWhiteSpace(block))
        {
            // Fallback: Realtek adapter may not match exactly by alias on some systems.
            block = ExtractInterfaceBlockByDriver(output, adapter.Name);
        }

        if (string.IsNullOrWhiteSpace(block))
        {
            block = output;
        }

        return new ApSupportInfo
        {
            InterfaceAlias = adapter.InterfaceAlias,
            Driver = ReadValue(block, "Driver"),
            Vendor = ReadValue(block, "Vendor"),
            Provider = ReadValue(block, "Provider"),
            Version = ReadValue(block, "Version"),
            RadioTypesSupported = ReadValue(block, "Radio types supported"),
            HostedNetworkSupported = ReadValue(block, "Hosted network supported"),
            WirelessDisplaySupported = ReadValue(block, "Wireless Display Supported"),
            RawOutput = block.Trim().Length > 0 ? block.Trim() : output.Trim()
        };
    }

    private static string RunNetsh(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "netsh.exe",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start netsh.exe.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit(6000);

        if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "netsh wlan show drivers failed." : error.Trim());

        return output;
    }

    private static string ExtractInterfaceBlock(string output, string interfaceAlias)
    {
        if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(interfaceAlias))
            return string.Empty;

        var blocks = SplitInterfaceBlocks(output);
        return blocks.FirstOrDefault(block =>
            block.Split('\n').Any(line =>
                line.Trim().StartsWith("Interface name:", StringComparison.OrdinalIgnoreCase) &&
                line.Contains(interfaceAlias, StringComparison.OrdinalIgnoreCase))) ?? string.Empty;
    }

    private static string ExtractInterfaceBlockByDriver(string output, string adapterName)
    {
        if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(adapterName))
            return string.Empty;

        var keyTerms = adapterName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length >= 4)
            .Take(4)
            .ToArray();

        var blocks = SplitInterfaceBlocks(output);
        return blocks.FirstOrDefault(block => keyTerms.Any(term => block.Contains(term, StringComparison.OrdinalIgnoreCase))) ?? string.Empty;
    }

    private static List<string> SplitInterfaceBlocks(string output)
    {
        var lines = output.Replace("\r\n", "\n").Split('\n');
        var blocks = new List<string>();
        var current = new StringBuilder();

        foreach (string line in lines)
        {
            if (line.Trim().StartsWith("Interface name:", StringComparison.OrdinalIgnoreCase) && current.Length > 0)
            {
                blocks.Add(current.ToString());
                current.Clear();
            }

            current.AppendLine(line);
        }

        if (current.Length > 0)
            blocks.Add(current.ToString());

        return blocks;
    }

    private static string ReadValue(string block, string key)
    {
        if (string.IsNullOrWhiteSpace(block))
            return "Unknown";

        foreach (string rawLine in block.Replace("\r\n", "\n").Split('\n'))
        {
            string line = rawLine.Trim();
            if (!line.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                continue;

            int colon = line.IndexOf(':');
            if (colon < 0 || colon + 1 >= line.Length)
                return "Unknown";

            return line[(colon + 1)..].Trim();
        }

        return "Unknown";
    }
}
