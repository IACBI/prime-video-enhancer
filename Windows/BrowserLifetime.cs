using System.Diagnostics;
using System.Runtime.InteropServices;

/// <summary>
/// Ties the dedicated browser to this helper's own lifetime.
/// </summary>
/// <remarks>
/// The browser exposes an unauthenticated DevTools endpoint on loopback, which
/// any process on the machine can drive - including processes of other OS users,
/// against whom the profile directory on disk is ACL-protected but the port is
/// not. Left to itself the browser outlives the helper (the documented shutdown
/// is "close this console window"), so the endpoint would keep serving the
/// signed-in profile with nothing left that needs it.
///
/// A job object with JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE is used rather than an
/// exit handler because the kernel closes the job handle however the helper
/// dies - console close, Ctrl+C, task kill or crash - whereas managed exit
/// handlers do not run reliably for all of those.
/// </remarks>
internal static class BrowserLifetime
{
    private const uint JobObjectExtendedLimitInformation = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x2000;

    // Held for the process lifetime: closing this handle is what kills the browser.
    private static nint jobHandle;

    public static void BindToHelper(Process? browser)
    {
        if (browser is null || !OperatingSystem.IsWindows()) return;

        try
        {
            if (jobHandle == nint.Zero)
            {
                jobHandle = CreateJob();
            }

            if (jobHandle != nint.Zero)
            {
                AssignProcessToJobObject(jobHandle, browser.Handle);
            }
        }
        catch { }
    }

    private static nint CreateJob()
    {
        var handle = CreateJobObject(nint.Zero, null);
        if (handle == nint.Zero) return nint.Zero;

        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
        info.BasicLimitInformation.LimitFlags = JobObjectLimitKillOnJobClose;

        var length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        var pointer = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(info, pointer, false);
            if (!SetInformationJobObject(handle, JobObjectExtendedLimitInformation, pointer, (uint)length))
            {
                CloseHandle(handle);
                return nint.Zero;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }

        return handle;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateJobObject(nint lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(nint hJob, uint infoClass, nint lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(nint hJob, nint hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint hObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }
}
