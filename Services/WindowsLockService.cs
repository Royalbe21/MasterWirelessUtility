using System.Runtime.InteropServices;

namespace MasterWirelessUtility.Services;

public static class WindowsLockService
{
    public static void LockComputer() => LockWorkStation();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool LockWorkStation();
}
