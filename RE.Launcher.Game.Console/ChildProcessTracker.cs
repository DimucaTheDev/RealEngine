using System.Diagnostics;
using System.Runtime.InteropServices;

public static class ChildProcessTracker
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

    [DllImport("kernel32.dll")]
    static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoType infoType, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll")]
    static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    private static readonly IntPtr s_jobHandle;

    static ChildProcessTracker()
    {
        s_jobHandle = CreateJobObject(IntPtr.Zero, null);

        var info = new JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            LimitFlags = JobObjectLimitFlags.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        };

        var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = info
        };

        int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
        IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);
            if (!SetInformationJobObject(s_jobHandle, JobObjectInfoType.ExtendedLimitInformation, extendedInfoPtr, (uint)length))
            {
                throw new Exception($"{Marshal.GetLastWin32Error()}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(extendedInfoPtr);
        }
    }

    public static void AddProcess(Process process)
    {
        if (!AssignProcessToJobObject(s_jobHandle, process.Handle))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 6) throw new Exception($"{error}");
        }
    }

    #region Win32 Structures

    enum JobObjectInfoType
    {
        ExtendedLimitInformation = 9
    }

    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public JobObjectLimitFlags LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public long Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [Flags]
    enum JobObjectLimitFlags : uint
    {
        JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000
    }

    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoCounters;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryLimit;
        public UIntPtr PeakJobMemoryLimit;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }
    #endregion
}