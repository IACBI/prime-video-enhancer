using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

/// <summary>
/// A Chromium browser started with <c>--remote-debugging-pipe</c>, and the
/// DevTools connection over that pipe.
/// </summary>
/// <remarks>
/// The DevTools endpoint used to be a TCP port on loopback. It had no
/// authentication and Windows does not restrict loopback by user, so any process
/// on the machine - another Windows account's included - could drive the
/// signed-in browser and read its cookies. A pipe has no address to connect to:
/// its two ends are anonymous pipe handles, created with this user's default
/// security and inherited by the browser alone.
///
/// Chromium reads DevTools commands from file descriptor 3 and writes to 4. On
/// Windows, descriptors beyond the standard three are handed to a child through
/// the C runtime's block in <c>STARTUPINFO.lpReserved2</c>, which is how Node -
/// and so Puppeteer and Playwright - pass the same pipe. Handle inheritance is
/// limited to those two handles with <c>PROC_THREAD_ATTRIBUTE_HANDLE_LIST</c>,
/// so nothing else of the helper's leaks into the browser.
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class PipeBrowser : IAsyncDisposable
{
    private PipeBrowser(Process process, CdpConnection connection)
    {
        Process = process;
        Connection = connection;
    }

    public Process Process { get; }

    public CdpConnection Connection { get; }

    /// <summary>
    /// Starts <paramref name="executable"/> with <paramref name="arguments"/> (a
    /// command-line string, already quoted) and <c>--remote-debugging-pipe</c>.
    /// </summary>
    /// <param name="beforeResume">
    /// Runs while the browser's first thread is still suspended, so that anything
    /// it does to the process - such as placing it in a job - applies before the
    /// browser can start children of its own.
    /// </param>
    public static PipeBrowser Launch(string executable, string arguments, Action<Process>? beforeResume = null)
    {
        var toBrowser = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
        var fromBrowser = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        Process? process = null;
        try
        {
            process = Start(
                executable,
                arguments,
                browserReads: toBrowser.ClientSafePipeHandle.DangerousGetHandle(),
                browserWrites: fromBrowser.ClientSafePipeHandle.DangerousGetHandle(),
                beforeResume);

            // Only the browser may hold its ends now. If the helper kept them, the
            // pipe would stay open after the browser exits and the end of the
            // conversation would never be seen.
            toBrowser.DisposeLocalCopyOfClientHandle();
            fromBrowser.DisposeLocalCopyOfClientHandle();
            return new PipeBrowser(process, new CdpConnection(input: fromBrowser, output: toBrowser));
        }
        catch
        {
            process?.Dispose();
            toBrowser.Dispose();
            fromBrowser.Dispose();
            throw;
        }
    }

    private static Process Start(string executable, string arguments, nint browserReads, nint browserWrites, Action<Process>? beforeResume)
    {
        const int DescriptorCount = 5;
        const byte FOPEN = 0x01;
        const byte FPIPE = 0x08;

        // The C runtime's layout: a descriptor count, one flag byte per
        // descriptor, then one handle per descriptor, unaligned. Descriptors 0-2
        // are left closed; the browser has no console to write to.
        var crtBlockSize = sizeof(int) + DescriptorCount + DescriptorCount * nint.Size;
        var crtBlock = Marshal.AllocHGlobal(crtBlockSize);
        var inheritedHandles = Marshal.AllocHGlobal(2 * nint.Size);
        nint attributeList = 0;
        var attributeListInitialized = false;
        try
        {
            Marshal.WriteInt32(crtBlock, DescriptorCount);
            nint[] handles = [-1, -1, -1, browserReads, browserWrites];
            for (var fd = 0; fd < DescriptorCount; fd++)
            {
                Marshal.WriteByte(crtBlock, sizeof(int) + fd, fd >= 3 ? (byte)(FOPEN | FPIPE) : (byte)0);
                Marshal.WriteIntPtr(crtBlock, sizeof(int) + DescriptorCount + fd * nint.Size, handles[fd]);
            }

            Marshal.WriteIntPtr(inheritedHandles, 0, browserReads);
            Marshal.WriteIntPtr(inheritedHandles, nint.Size, browserWrites);

            nint attributeListSize = 0;
            InitializeProcThreadAttributeList(0, 1, 0, ref attributeListSize);
            attributeList = Marshal.AllocHGlobal(attributeListSize);
            if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref attributeListSize)) throw new Win32Exception();
            attributeListInitialized = true;
            if (!UpdateProcThreadAttribute(attributeList, 0, ProcThreadAttributeHandleList, inheritedHandles, 2 * nint.Size, 0, 0))
            {
                throw new Win32Exception();
            }

            var startupInfo = new STARTUPINFOEX
            {
                StartupInfo = new STARTUPINFO
                {
                    cb = Marshal.SizeOf<STARTUPINFOEX>(),
                    cbReserved2 = (short)crtBlockSize,
                    lpReserved2 = crtBlock
                },
                lpAttributeList = attributeList
            };

            // Edge otherwise relaunches itself under an application-compatibility
            // layer at startup; the relaunched browser does not get the pipe, and
            // the one that did exits with code 0 before answering anything.
            var commandLine = new StringBuilder($"\"{executable}\" --remote-debugging-pipe --edge-skip-compat-layer-relaunch {arguments}");
            if (!CreateProcess(null, commandLine, 0, 0, true, ExtendedStartupInfoPresent | CreateSuspended, 0, null, ref startupInfo, out var info))
            {
                throw new Win32Exception();
            }

            try
            {
                // Taken while the native process handle is still open, so the id
                // cannot have been reused by the time it is looked up.
                var process = Process.GetProcessById(info.dwProcessId);

                // Process opens its own handle lazily, which after an early exit
                // would be too late to read the exit code or wait on.
                _ = process.Handle;
                try
                {
                    beforeResume?.Invoke(process);
                }
                finally
                {
                    ResumeThread(info.hThread);
                }

                return process;
            }
            catch
            {
                TerminateProcess(info.hProcess, 1);
                throw;
            }
            finally
            {
                CloseHandle(info.hThread);
                CloseHandle(info.hProcess);
            }
        }
        finally
        {
            if (attributeListInitialized) DeleteProcThreadAttributeList(attributeList);
            if (attributeList != 0) Marshal.FreeHGlobal(attributeList);
            Marshal.FreeHGlobal(inheritedHandles);
            Marshal.FreeHGlobal(crtBlock);
        }
    }

    /// <summary>Closes the connection; the process is left to its owner.</summary>
    public async ValueTask DisposeAsync()
    {
        await Connection.DisposeAsync();
        Process.Dispose();
    }

    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const uint CreateSuspended = 0x00000004;
    private const nint ProcThreadAttributeHandleList = 0x00020002;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateProcessW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcess(
        string? lpApplicationName,
        StringBuilder lpCommandLine,
        nint lpProcessAttributes,
        nint lpThreadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
        uint dwCreationFlags,
        nint lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFOEX lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeProcThreadAttributeList(nint lpAttributeList, int dwAttributeCount, int dwFlags, ref nint lpSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateProcThreadAttribute(nint lpAttributeList, uint dwFlags, nint attribute, nint lpValue, nint cbSize, nint lpPreviousValue, nint lpReturnSize);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(nint lpAttributeList);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(nint hThread);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(nint hProcess, uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint hObject);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public nint lpReserved2;
        public nint hStdInput;
        public nint hStdOutput;
        public nint hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public nint lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public nint hProcess;
        public nint hThread;
        public int dwProcessId;
        public int dwThreadId;
    }
}
