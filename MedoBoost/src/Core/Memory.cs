using System.Diagnostics;

namespace MedoBoost.Core;

/// <summary>RAM cleaner: trims process working sets and purges the standby list (same as Microsoft's RAMMap / ISLC).</summary>
public static class Memory
{
    const int SE_INCREASE_QUOTA = 5, SE_PROF_SINGLE_PROCESS = 13, SE_DEBUG = 20;
    const int SystemMemoryListInformation = 80;
    const int MemoryFlushModifiedList = 3, MemoryPurgeStandbyList = 4;

    public static ulong AvailBytes()
    {
        var m = new Native.MEMORYSTATUSEX();
        return Native.GlobalMemoryStatusEx(m) ? m.ullAvailPhys : 0;
    }

    static void Privileges()
    {
        Native.RtlAdjustPrivilege(SE_INCREASE_QUOTA, true, false, out _);
        Native.RtlAdjustPrivilege(SE_PROF_SINGLE_PROCESS, true, false, out _);
        Native.RtlAdjustPrivilege(SE_DEBUG, true, false, out _);
    }

    public static int TrimProcess(int pid)
    {
        IntPtr h = Native.OpenProcess(Native.PROCESS_QUERY_INFORMATION | Native.PROCESS_SET_QUOTA, false, pid);
        if (h == IntPtr.Zero) return 0;
        try { return Native.EmptyWorkingSet(h) ? 1 : 0; }
        finally { Native.CloseHandle(h); }
    }

    public static int TrimAll()
    {
        Privileges();
        int n = 0;
        foreach (var p in Process.GetProcesses())
        {
            try { if (p.Id > 4) n += TrimProcess(p.Id); } catch { }
            finally { p.Dispose(); }
        }
        return n;
    }

    public static bool PurgeStandby()
    {
        Privileges();
        int cmd = MemoryFlushModifiedList;
        Native.NtSetSystemInformation(SystemMemoryListInformation, ref cmd, sizeof(int));
        cmd = MemoryPurgeStandbyList;
        return Native.NtSetSystemInformation(SystemMemoryListInformation, ref cmd, sizeof(int)) == 0;
    }

    /// <summary>Full RAM clean. Returns bytes that became available.</summary>
    public static long CleanAll(bool purgeStandby = true)
    {
        var before = AvailBytes();
        int n = TrimAll();
        bool purged = purgeStandby && PurgeStandby();
        Thread.Sleep(400);
        var after = AvailBytes();
        long freed = (long)after - (long)before;
        Log.Ok($"تنظيف الرام: {n} عملية" + (purged ? " + Standby List" : "") + $" → تحرير {Util.Size(Math.Max(0, freed))}");
        return Math.Max(0, freed);
    }
}
