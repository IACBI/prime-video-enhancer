using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

internal static class AppIconHelper
{
    // Absolute path, because CreateProcess resolves a bare name against the
    // calling application's directory first: a powershell.exe dropped next to
    // the exe would otherwise be launched instead of the system one.
    private static readonly string WindowsPowerShellPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "WindowsPowerShell",
        "v1.0",
        "powershell.exe");

    const uint IMAGE_ICON = 1;
    const uint LR_LOADFROMFILE = 0x00000010;
    const uint LR_DEFAULTSIZE = 0x00000040;
    const uint WM_SETICON = 0x0080;
    const nint ICON_SMALL = 0;
    const nint ICON_BIG = 1;
    const ushort VT_LPWSTR = 31;
    const uint SMTO_ABORTIFHUNG = 0x0002;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern nint LoadImage(nint hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll", SetLastError = true)]
    static extern nint SendMessageTimeout(nint hWnd, uint Msg, nint wParam, nint lParam, uint fuFlags, uint uTimeout, out nint lpdwResult);

    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc enumProc, nint lParam);

    delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("shell32.dll", SetLastError = true)]
    static extern int SHGetPropertyStoreForWindow(nint hwnd, ref Guid iid, out IPropertyStore? propertyStore);

    [DllImport("ole32.dll")]
    static extern int PropVariantClear(ref PROPVARIANT pvar);

    [StructLayout(LayoutKind.Sequential)]
    public struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
        public PROPERTYKEY(Guid guid, uint pid)
        {
            fmtid = guid;
            this.pid = pid;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROPVARIANT
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public nint pwszVal;
        // The native union is two pointers wide (24 bytes in total on x64, 16 on
        // x86). Without this, PropVariantClear zeroes 8 bytes past the struct -
        // i.e. whatever sits next to it on the stack.
        public nint padding;
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyStore
    {
        int GetCount(out uint cProps);
        int GetAt(uint iProp, out PROPERTYKEY pkey);
        int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        int SetValue(ref PROPERTYKEY key, ref PROPVARIANT propvar);
        int Commit();
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    public class ShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    public interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, nint pfd, int fFlags);
        void GetIDList(out nint ppidl);
        void SetIDList(nint pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(nint hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport, Guid("0000010b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig]
        int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string? pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }

    private static nint appIconHandle = nint.Zero;
    public static Process? EdgeProcess { get; set; }

    private static bool ConvertPngToIco(string pngPath, string targetIcoPath)
    {
        try
        {
            var pngBytes = File.ReadAllBytes(pngPath);
            using var fs = File.Create(targetIcoPath);
            using var writer = new BinaryWriter(fs);
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)1);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write((uint)pngBytes.Length);
            writer.Write((uint)22);
            writer.Write(pngBytes);
            return true;
        }
        catch { }
        return false;
    }

    private static bool TryExtractSystemPrimeVideoIcon(string targetIcoPath)
    {
        try
        {
            string? installDir = null;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = WindowsPowerShellPath,
                    Arguments = "-NoProfile -Command \"(Get-AppxPackage *AmazonVideo* -ErrorAction SilentlyContinue).InstallLocation\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    installDir = proc.StandardOutput.ReadToEnd().Trim();
                    proc.WaitForExit(2000);
                }
            }
            catch { }

            if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
            {
                var assetsDir = Path.Combine(installDir, "Assets");
                if (Directory.Exists(assetsDir))
                {
                    string[] candidates = [
                        "Square44x44Logo.targetsize-256.png",
                        "Square150x150Logo.scale-100.png",
                        "StoreLogo.scale-100.png",
                        "LargeTile.scale-100.png"
                    ];
                    foreach (var cand in candidates)
                    {
                        var pngPath = Path.Combine(assetsDir, cand);
                        if (File.Exists(pngPath))
                        {
                            if (ConvertPngToIco(pngPath, targetIcoPath))
                                return true;
                        }
                    }
                }
            }

            var edgeWebApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Microsoft\Edge\User Data\Default\Web Applications");
            if (Directory.Exists(edgeWebApps))
            {
                var icoFiles = Directory.GetFiles(edgeWebApps, "*.ico", SearchOption.AllDirectories);
                foreach (var ico in icoFiles)
                {
                    if (ico.Contains("prime", StringComparison.OrdinalIgnoreCase) || ico.Contains("amazon", StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(ico, targetIcoPath, true);
                        return true;
                    }
                }
            }
        }
        catch { }
        return false;
    }

    public static string GetOrCreateIconPath()
    {
        try
        {
            var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrimeVideoSpeedController");
            Directory.CreateDirectory(cacheDir);
            var cachedIconPath = Path.Combine(cacheDir, "AppIcon.ico");

            if (!File.Exists(cachedIconPath))
            {
                if (TryExtractSystemPrimeVideoIcon(cachedIconPath))
                    return cachedIconPath;
            }
            else
            {
                return cachedIconPath;
            }
        }
        catch { }

        var localPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(localPath)) return localPath;

        // A loose Assets\generate-app-icon.ps1 used to be executed here when the
        // icon was missing. Released builds are single-file and never ship that
        // script, so any copy sitting next to the exe was put there after
        // download - in a shared directory, by anyone holding create-file rights.
        // Running it handed that person code execution as whoever launched the
        // app. The icon is generated at build time (see the csproj target) and
        // falls back to the embedded resource below, so nothing is lost.

        try
        {
            var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrimeVideoSpeedController");
            Directory.CreateDirectory(cacheDir);
            var cachedIconPath = Path.Combine(cacheDir, "AppIcon.ico");
            if (!File.Exists(cachedIconPath))
            {
                using var stream = typeof(AppIconHelper).Assembly.GetManifestResourceStream("PrimeVideoSpeedApp.Assets.AppIcon.ico");
                if (stream != null)
                {
                    using var fs = File.Create(cachedIconPath);
                    stream.CopyTo(fs);
                }
            }
            if (File.Exists(cachedIconPath)) return cachedIconPath;
        }
        catch { }

        return localPath;
    }

    private static string? resolvedIconPath;

    /// <summary>The icon file, resolved once; resolving touches the file system and may extract files.</summary>
    public static string IconPath => resolvedIconPath ??= GetOrCreateIconPath();

    public static void EnsureAppIconLoaded()
    {
        if (appIconHandle != nint.Zero) return;
        var iconPath = IconPath;
        if (File.Exists(iconPath))
        {
            appIconHandle = LoadImage(nint.Zero, iconPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<uint, bool> DedicatedPidCache = new();

    private static bool IsOurDedicatedEdgeProcess(uint windowPid, string titleStr)
    {
        if (EdgeProcess != null && !EdgeProcess.HasExited && windowPid == EdgeProcess.Id)
            return true;

        // 1. Must match Prime Video or Amazon in the title. Checked before opening a
        // process handle: this runs every poll for every Chromium-family window.
        if (!titleStr.Contains("Prime Video", StringComparison.OrdinalIgnoreCase) && !titleStr.Contains("Amazon", StringComparison.OrdinalIgnoreCase))
            return false;

        // Every pid that gets past the title check is cached, dedicated or not, so
        // an editor or another Chromium app whose title happens to mention Prime
        // Video costs one process lookup rather than one per poll.
        if (DedicatedPidCache.TryGetValue(windowPid, out var isDedicated))
            return isDedicated;

        try
        {
            using var proc = Process.GetProcessById((int)windowPid);
            // 2. MUST be msedge.exe (excludes VS Code, Chrome, Electron apps, etc.)
            if (!proc.ProcessName.Equals("msedge", StringComparison.OrdinalIgnoreCase))
            {
                DedicatedPidCache[windowPid] = false;
                return false;
            }

            bool verified = false;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = WindowsPowerShellPath,
                    Arguments = $"-NoProfile -Command \"(Get-CimInstance Win32_Process -Filter 'ProcessId = {windowPid}').CommandLine\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using var cmdProc = Process.Start(psi);
                if (cmdProc != null)
                {
                    var cmdLine = cmdProc.StandardOutput.ReadToEnd();
                    cmdProc.WaitForExit(1000);
                    if (cmdLine.Contains("9223") || cmdLine.Contains("PrimeVideoSpeedController"))
                    {
                        verified = true;
                    }
                }
            }
            catch { }

            if (verified)
            {
                DedicatedPidCache[windowPid] = true;
                return true;
            }
            else
            {
                DedicatedPidCache[windowPid] = false;
                return false;
            }
        }
        catch { }

        return false;
    }

    public static void ApplyToEdgeWindows()
    {
        EnsureAppIconLoaded();
        if (appIconHandle == nint.Zero) return;

        var iconPath = IconPath;
        var exePath = Environment.ProcessPath ?? "";
        var storeGuid = new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
        var pkeyAumid = new PROPERTYKEY(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);
        var pkeyIcon = new PROPERTYKEY(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 3);
        var pkeyCmd = new PROPERTYKEY(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 2);
        var pkeyDisplayName = new PROPERTYKEY(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 4);

        EnumWindows((hWnd, lParam) =>
        {
            var className = new StringBuilder(256);
            if (GetClassName(hWnd, className, 256) > 0 && className.ToString().Contains("Chrome_WidgetWin_1", StringComparison.OrdinalIgnoreCase))
            {
                GetWindowThreadProcessId(hWnd, out uint windowPid);
                var title = new StringBuilder(512);
                GetWindowText(hWnd, title, 512);
                var titleStr = title.ToString();

                if (IsOurDedicatedEdgeProcess(windowPid, titleStr))
                {
                    // Bounded: a plain SendMessage to a hung Edge UI thread would
                    // freeze the poll loop, and with it injection and ad blocking.
                    SendMessageTimeout(hWnd, WM_SETICON, ICON_SMALL, appIconHandle, SMTO_ABORTIFHUNG, 500, out _);
                    SendMessageTimeout(hWnd, WM_SETICON, ICON_BIG, appIconHandle, SMTO_ABORTIFHUNG, 500, out _);

                    try
                    {
                        if (SHGetPropertyStoreForWindow(hWnd, ref storeGuid, out var propStore) == 0 && propStore != null)
                        {
                            try
                            {
                                var pvAumid = new PROPVARIANT { vt = VT_LPWSTR, pwszVal = Marshal.StringToCoTaskMemUni("PrimeVideoSpeedController.App") };
                                propStore.SetValue(ref pkeyAumid, ref pvAumid);
                                PropVariantClear(ref pvAumid);

                                var pvIcon = new PROPVARIANT { vt = VT_LPWSTR, pwszVal = Marshal.StringToCoTaskMemUni(iconPath) };
                                propStore.SetValue(ref pkeyIcon, ref pvIcon);
                                PropVariantClear(ref pvIcon);

                                if (!string.IsNullOrEmpty(exePath))
                                {
                                    var pvCmd = new PROPVARIANT { vt = VT_LPWSTR, pwszVal = Marshal.StringToCoTaskMemUni(exePath) };
                                    propStore.SetValue(ref pkeyCmd, ref pvCmd);
                                    PropVariantClear(ref pvCmd);

                                    // Windows ignores the relaunch command unless its
                                    // display name is set alongside it.
                                    var pvName = new PROPVARIANT { vt = VT_LPWSTR, pwszVal = Marshal.StringToCoTaskMemUni("Prime Video Enhancer") };
                                    propStore.SetValue(ref pkeyDisplayName, ref pvName);
                                    PropVariantClear(ref pvName);
                                }

                                propStore.Commit();
                            }
                            finally
                            {
                                if (Marshal.IsComObject(propStore))
                                {
                                    if (OperatingSystem.IsWindows())
                                    {
                                        Marshal.ReleaseComObject(propStore);
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            return true;
        }, nint.Zero);
    }
}
