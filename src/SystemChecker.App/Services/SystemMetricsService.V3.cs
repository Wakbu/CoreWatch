using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using SystemChecker.Models;

namespace SystemChecker.Services;

public sealed class SystemMetricsService : IDisposable
{
    private ulong _previousIdle;
    private ulong _previousKernel;
    private ulong _previousUser;
    private long _previousReceived;
    private long _previousSent;
    private DateTimeOffset _previousNetworkAt = DateTimeOffset.UtcNow;
    private bool _initialized;
    private readonly DiskIoSampler _diskIo = new();

    public SystemSnapshot Capture()
    {
        var now = DateTimeOffset.UtcNow;
        var cpu = ReadCpuUsage();
        var memory = ReadMemory();
        var (received, sent) = ReadNetwork(now);
        var (read, write) = _diskIo.Capture();
        return new(now, cpu, memory.UsagePercent, memory.UsedBytes, memory.TotalBytes, received, sent,
            ReadLowestDiskFreePercent(), read, write);
    }

    private double ReadCpuUsage()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return 0;
        var idleValue = ToUInt64(idle); var kernelValue = ToUInt64(kernel); var userValue = ToUInt64(user);
        if (!_initialized)
        {
            _previousIdle = idleValue; _previousKernel = kernelValue; _previousUser = userValue; _initialized = true;
            return 0;
        }
        var idleDelta = idleValue - _previousIdle; var kernelDelta = kernelValue - _previousKernel; var userDelta = userValue - _previousUser;
        var total = kernelDelta + userDelta;
        _previousIdle = idleValue; _previousKernel = kernelValue; _previousUser = userValue;
        return total == 0 ? 0 : Math.Clamp((total - idleDelta) * 100d / total, 0, 100);
    }

    private static (ulong TotalBytes, ulong UsedBytes, double UsagePercent) ReadMemory()
    {
        var status = new MemoryStatusEx();
        if (!GlobalMemoryStatusEx(status)) return (0, 0, 0);
        return (status.TotalPhysical, status.TotalPhysical - status.AvailablePhysical, status.MemoryLoad);
    }

    private (double ReceivedMbps, double SentMbps) ReadNetwork(DateTimeOffset now)
    {
        long received = 0, sent = 0;
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            try { var stats = adapter.GetIPv4Statistics(); received += stats.BytesReceived; sent += stats.BytesSent; }
            catch (NetworkInformationException) { }
        }
        var seconds = Math.Max((now - _previousNetworkAt).TotalSeconds, .1);
        var rx = _previousReceived == 0 ? 0 : (received - _previousReceived) * 8d / seconds / 1_000_000d;
        var tx = _previousSent == 0 ? 0 : (sent - _previousSent) * 8d / seconds / 1_000_000d;
        _previousReceived = received; _previousSent = sent; _previousNetworkAt = now;
        return (Math.Max(0, rx), Math.Max(0, tx));
    }

    private static double ReadLowestDiskFreePercent()
    {
        var values = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed && d.TotalSize > 0)
            .Select(d => d.AvailableFreeSpace * 100d / d.TotalSize).ToArray();
        return values.Length == 0 ? 100 : values.Min();
    }

    public void Dispose() => _diskIo.Dispose();
    private static ulong ToUInt64(FileTime time) => ((ulong)time.High << 32) | time.Low;
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx buffer);
    [StructLayout(LayoutKind.Sequential)] private struct FileTime { public uint Low; public uint High; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint Length = (uint)Marshal.SizeOf<MemoryStatusEx>(); public uint MemoryLoad;
        public ulong TotalPhysical; public ulong AvailablePhysical; public ulong TotalPageFile; public ulong AvailablePageFile;
        public ulong TotalVirtual; public ulong AvailableVirtual; public ulong AvailableExtendedVirtual;
    }

    private sealed class DiskIoSampler : IDisposable
    {
        private const uint PdhFmtDouble = 0x00000200;
        private IntPtr _query, _readCounter, _writeCounter;
        private bool _ready;

        public DiskIoSampler()
        {
            if (PdhOpenQuery(null, IntPtr.Zero, out _query) != 0) return;
            if (PdhAddEnglishCounter(_query, @"\PhysicalDisk(_Total)\Disk Read Bytes/sec", IntPtr.Zero, out _readCounter) != 0) return;
            if (PdhAddEnglishCounter(_query, @"\PhysicalDisk(_Total)\Disk Write Bytes/sec", IntPtr.Zero, out _writeCounter) != 0) return;
            _ready = PdhCollectQueryData(_query) == 0;
        }

        public (double ReadMbps, double WriteMbps) Capture()
        {
            if (!_ready || PdhCollectQueryData(_query) != 0) return (0, 0);
            return (Read(_readCounter) / 1024d / 1024d, Read(_writeCounter) / 1024d / 1024d);
        }

        private static double Read(IntPtr counter) => PdhGetFormattedCounterValue(counter, PdhFmtDouble, out _, out var value) == 0 ? Math.Max(0, value.DoubleValue) : 0;
        public void Dispose() { if (_query != IntPtr.Zero) PdhCloseQuery(_query); _query = IntPtr.Zero; }
        [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern uint PdhOpenQuery(string? dataSource, IntPtr userData, out IntPtr query);
        [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern uint PdhAddEnglishCounter(IntPtr query, string path, IntPtr userData, out IntPtr counter);
        [DllImport("pdh.dll")] private static extern uint PdhCollectQueryData(IntPtr query);
        [DllImport("pdh.dll")] private static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint type, out PdhFormattedCounterValue value);
        [DllImport("pdh.dll")] private static extern uint PdhCloseQuery(IntPtr query);
        [StructLayout(LayoutKind.Explicit)] private struct PdhFormattedCounterValue { [FieldOffset(0)] public uint Status; [FieldOffset(8)] public double DoubleValue; }
    }
}
