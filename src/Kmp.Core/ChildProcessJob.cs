using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Kmp.Core;

/// <summary>
/// mpv süreçlerini motora bağlar: motor ne şekilde kapanırsa kapansın (çökme dahil) Windows mpv'leri de kapatır;
/// sahipsiz mpv'nin arka planda çalmaya devam etmesi önlenir.
/// </summary>
public static class ChildProcessJob
{
    const uint KillOnJobClose = 0x2000;
    const int ExtendedLimitInformation = 9;
    static readonly object Gate = new();
    static IntPtr _job;

    public static void Add(Process p)
    {
        try
        {
            lock (Gate)
            {
                if (_job == IntPtr.Zero)
                {
                    var job = CreateJobObject(IntPtr.Zero, null);
                    if (job == IntPtr.Zero) throw new InvalidOperationException($"CreateJobObject {Marshal.GetLastWin32Error()}");
                    var info = new ExtendedLimit { Basic = new BasicLimit { LimitFlags = KillOnJobClose } };
                    var size = Marshal.SizeOf(typeof(ExtendedLimit));
                    var ptr = Marshal.AllocHGlobal(size);
                    try
                    {
                        Marshal.StructureToPtr(info, ptr, false);
                        if (!SetInformationJobObject(job, ExtendedLimitInformation, ptr, (uint)size))
                            throw new InvalidOperationException($"SetInformationJobObject {Marshal.GetLastWin32Error()}");
                    }
                    finally { Marshal.FreeHGlobal(ptr); }
                    _job = job;
                }
                if (!AssignProcessToJobObject(_job, p.Handle))
                    Log.Warn($"mpv motora bağlanamadı (iş nesnesi {Marshal.GetLastWin32Error()}); motor çökerse mpv elle kapatılır");
            }
        }
        catch (Exception e)
        {
            Log.Warn($"İş nesnesi kurulamadı: {e.Message}");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct BasicLimit
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct IoCounters
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
        public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ExtendedLimit
    {
        public BasicLimit Basic;
        public IoCounters Io;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr CreateJobObject(IntPtr attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
}
