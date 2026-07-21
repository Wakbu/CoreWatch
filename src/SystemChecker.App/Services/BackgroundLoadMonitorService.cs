using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SystemChecker.Services;

public sealed record BackgroundLoadReading(
    int Rank,
    int ProcessId,
    string Name,
    string Kind,
    string Assessment,
    string Recommendation,
    double AverageCpuPercent,
    double MaximumCpuPercent,
    double AverageMemoryMb,
    double MaximumMemoryMb,
    long DiskBytes,
    double AverageDiskBytesPerSecond,
    double AverageNetworkConnections,
    int MaximumNetworkConnections,
    double CpuSeconds,
    int SampleCount)
{
    public string AverageCpuText => $"{AverageCpuPercent:0.0}%";
    public string MaximumCpuText => $"{MaximumCpuPercent:0.0}%";
    public string MemoryText => $"{AverageMemoryMb:0} / {MaximumMemoryMb:0} MB";
    public string DiskText => $"{OptimizationService.FormatBytes(DiskBytes)} · {OptimizationService.FormatBytes((long)AverageDiskBytesPerSecond)}/s";
    public string NetworkText => $"평균 {AverageNetworkConnections:0.0} · 최대 {MaximumNetworkConnections}";
}

public sealed record BackgroundLoadProgress(TimeSpan Elapsed, TimeSpan Duration, IReadOnlyList<BackgroundLoadReading> Readings)
{
    public double Percent => Duration.TotalMilliseconds <= 0 ? 0 : Math.Clamp(Elapsed.TotalMilliseconds / Duration.TotalMilliseconds * 100, 0, 100);
}

public sealed class BackgroundLoadMonitorService
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);
    private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "Memory Compression", "smss", "csrss", "wininit", "services", "lsass", "svchost", "winlogon", "dwm", "fontdrvhost", "audiodg", "MsMpEng"
    };

    public Task<IReadOnlyList<BackgroundLoadReading>> MonitorAsync(TimeSpan duration, IProgress<BackgroundLoadProgress>? progress = null, CancellationToken token = default)
    {
        if (duration < TimeSpan.FromSeconds(2) || duration > TimeSpan.FromMinutes(10)) throw new ArgumentOutOfRangeException(nameof(duration));
        return Task.Run(async () =>
        {
            var trackers = new Dictionary<ProcessIdentity, LoadTracker>();
            var previous = CaptureSamples();
            var timer = Stopwatch.StartNew();
            var previousElapsed = TimeSpan.Zero;
            progress?.Report(new BackgroundLoadProgress(TimeSpan.Zero, duration, []));

            while (timer.Elapsed < duration)
            {
                var delay = duration - timer.Elapsed < SampleInterval ? duration - timer.Elapsed : SampleInterval;
                if (delay > TimeSpan.Zero) await Task.Delay(delay, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                var elapsed = timer.Elapsed > duration ? duration : timer.Elapsed;
                var interval = elapsed - previousElapsed;
                if (interval.TotalMilliseconds < 100) continue;
                var current = CaptureSamples();
                Accumulate(previous, current, interval, trackers);
                previous = current;
                previousElapsed = elapsed;
                progress?.Report(new BackgroundLoadProgress(elapsed, duration, BuildReadings(trackers)));
            }

            return BuildReadings(trackers);
        }, token);
    }

    public static string Assess(double averageCpu, double maximumCpu, long diskBytes, double averageDiskBytesPerSecond, double averageConnections, bool protectedProcess, int processId)
    {
        if (processId == Environment.ProcessId) return "측정 도구";
        if (protectedProcess) return "Windows 구성 요소";
        if (averageCpu >= 10 || averageDiskBytesPerSecond >= 10 * 1024 * 1024) return "지속 부하 높음";
        if (averageCpu >= 3 || maximumCpu >= 25 || diskBytes >= 20 * 1024 * 1024 || averageConnections >= 3) return "검토 권장";
        return "정상 범위";
    }

    private static Dictionary<ProcessIdentity, RawSample> CaptureSamples()
    {
        var network = CaptureNetworkConnectionCounts();
        var samples = new Dictionary<ProcessIdentity, RawSample>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    var id = process.Id;
                    var name = process.ProcessName;
                    var startTicks = TryGetStartTicks(process);
                    var identity = new ProcessIdentity(id, startTicks);
                    var cpu = process.TotalProcessorTime;
                    var memory = Math.Max(0, process.WorkingSet64);
                    var sessionId = TryGetSessionId(process);
                    var hasWindow = process.MainWindowHandle != IntPtr.Zero;
                    var ioBytes = TryGetIoBytes(process);
                    var connections = network.GetValueOrDefault(id);
                    var protectedProcess = sessionId == 0 || ProtectedNames.Contains(name);
                    var kind = protectedProcess ? "Windows" : hasWindow ? "앱" : "백그라운드";
                    samples[identity] = new RawSample(identity, name, kind, protectedProcess, cpu, memory, ioBytes, connections);
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
                catch (NotSupportedException) { }
            }
        }
        return samples;
    }

    private static void Accumulate(Dictionary<ProcessIdentity, RawSample> previous, Dictionary<ProcessIdentity, RawSample> current, TimeSpan interval, Dictionary<ProcessIdentity, LoadTracker> trackers)
    {
        var logicalProcessors = Math.Max(1, Environment.ProcessorCount);
        foreach (var pair in current)
        {
            if (!previous.TryGetValue(pair.Key, out var before)) continue;
            var now = pair.Value;
            var cpuDelta = now.CpuTime - before.CpuTime;
            var cpuPercent = cpuDelta.TotalMilliseconds <= 0 ? 0 : Math.Clamp(cpuDelta.TotalMilliseconds / interval.TotalMilliseconds / logicalProcessors * 100, 0, 100);
            var diskDelta = now.IoBytes >= before.IoBytes ? now.IoBytes - before.IoBytes : 0;
            var diskRate = interval.TotalSeconds <= 0 ? 0 : diskDelta / interval.TotalSeconds;
            if (!trackers.TryGetValue(pair.Key, out var tracker)) trackers[pair.Key] = tracker = new LoadTracker(now);
            tracker.Add(now, cpuPercent, cpuDelta.TotalSeconds, diskDelta, diskRate);
        }
    }

    private static IReadOnlyList<BackgroundLoadReading> BuildReadings(Dictionary<ProcessIdentity, LoadTracker> trackers)
    {
        var candidates = trackers.Values
            .Where(item => item.SampleCount > 0)
            .Select(item => item.ToReading())
            .Where(item => item.AverageCpuPercent >= .1 || item.DiskBytes >= 256 * 1024 || item.MaximumNetworkConnections > 0 || item.AverageMemoryMb >= 25)
            .OrderByDescending(Score)
            .ThenByDescending(item => item.AverageCpuPercent)
            .Take(50)
            .ToList();
        return candidates.Select((item, index) => item with { Rank = index + 1 }).ToList();
    }

    private static double Score(BackgroundLoadReading item) => item.AverageCpuPercent * 8 + item.MaximumCpuPercent * 1.5 + Math.Log10(Math.Max(1, item.DiskBytes)) * 2 + item.AverageNetworkConnections * 3 + item.AverageMemoryMb / 512;

    private static long TryGetStartTicks(Process process)
    {
        try { return process.StartTime.ToUniversalTime().Ticks; }
        catch { return 0; }
    }

    private static int TryGetSessionId(Process process)
    {
        try { return process.SessionId; }
        catch { return -1; }
    }

    private static ulong TryGetIoBytes(Process process)
    {
        try
        {
            return GetProcessIoCounters(process.Handle, out var counters) ? counters.ReadTransferCount + counters.WriteTransferCount : 0;
        }
        catch { return 0; }
    }

    private static Dictionary<int, int> CaptureNetworkConnectionCounts()
    {
        var result = new Dictionary<int, int>();
        CaptureTcpTable(2, 24, 0, 20, result);
        CaptureTcpTable(23, 56, 48, 52, result);
        return result;
    }

    private static void CaptureTcpTable(int addressFamily, int rowSize, int stateOffset, int processIdOffset, Dictionary<int, int> counts)
    {
        var size = 0;
        var status = GetExtendedTcpTable(IntPtr.Zero, ref size, false, addressFamily, 5, 0);
        if (status != 122 || size <= 4) return;
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            status = GetExtendedTcpTable(buffer, ref size, false, addressFamily, 5, 0);
            if (status != 0) return;
            var rows = Marshal.ReadInt32(buffer);
            var pointer = IntPtr.Add(buffer, 4);
            for (var index = 0; index < rows; index++)
            {
                var row = IntPtr.Add(pointer, index * rowSize);
                var state = Marshal.ReadInt32(row, stateOffset);
                if (state is 1 or 2 or 12) continue;
                var processId = Marshal.ReadInt32(row, processIdOffset);
                if (processId > 0) counts[processId] = counts.GetValueOrDefault(processId) + 1;
            }
        }
        catch { }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private sealed record ProcessIdentity(int ProcessId, long StartTicks);
    private sealed record RawSample(ProcessIdentity Identity, string Name, string Kind, bool ProtectedProcess, TimeSpan CpuTime, long MemoryBytes, ulong IoBytes, int NetworkConnections);

    private sealed class LoadTracker(RawSample first)
    {
        private RawSample _latest = first;
        private double _cpuTotal;
        private double _cpuMaximum;
        private double _memoryTotal;
        private double _memoryMaximum;
        private long _diskBytes;
        private double _diskRateTotal;
        private double _networkTotal;
        private int _networkMaximum;
        private double _cpuSeconds;

        public int SampleCount { get; private set; }

        public void Add(RawSample sample, double cpuPercent, double cpuSeconds, ulong diskBytes, double diskRate)
        {
            _latest = sample;
            SampleCount++;
            _cpuTotal += cpuPercent;
            _cpuMaximum = Math.Max(_cpuMaximum, cpuPercent);
            var memoryMb = sample.MemoryBytes / 1024d / 1024d;
            _memoryTotal += memoryMb;
            _memoryMaximum = Math.Max(_memoryMaximum, memoryMb);
            _diskBytes += (long)Math.Min(diskBytes, long.MaxValue);
            _diskRateTotal += diskRate;
            _networkTotal += sample.NetworkConnections;
            _networkMaximum = Math.Max(_networkMaximum, sample.NetworkConnections);
            _cpuSeconds += Math.Max(0, cpuSeconds);
        }

        public BackgroundLoadReading ToReading()
        {
            var averageCpu = _cpuTotal / SampleCount;
            var averageDisk = _diskRateTotal / SampleCount;
            var averageNetwork = _networkTotal / SampleCount;
            var assessment = Assess(averageCpu, _cpuMaximum, _diskBytes, averageDisk, averageNetwork, _latest.ProtectedProcess, _latest.Identity.ProcessId);
            var recommendation = assessment switch
            {
                "지속 부하 높음" => "사용 목적을 확인하고 불필요하면 종료 또는 시작 설정을 검토하세요.",
                "검토 권장" => "장시간 반복되는지 다시 측정한 뒤 설정 변경을 검토하세요.",
                "Windows 구성 요소" => "Windows 구성 요소이므로 CoreWatch에서 종료하지 마세요.",
                "측정 도구" => "CoreWatch 자체 측정 부하입니다.",
                _ => "현재 측정 구간에서는 지속 부하가 낮습니다."
            };
            return new BackgroundLoadReading(0, _latest.Identity.ProcessId, _latest.Name, _latest.Kind, assessment, recommendation,
                averageCpu, _cpuMaximum, _memoryTotal / SampleCount, _memoryMaximum, _diskBytes, averageDisk, averageNetwork, _networkMaximum, _cpuSeconds, SampleCount);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessIoCounters(IntPtr processHandle, out IoCounters counters);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(IntPtr tcpTable, ref int size, [MarshalAs(UnmanagedType.Bool)] bool order, int ipVersion, int tableClass, uint reserved);
}
