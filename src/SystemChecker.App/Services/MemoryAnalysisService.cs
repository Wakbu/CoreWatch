using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace SystemChecker.Services;

public sealed record PageFileSnapshot(bool AutomaticManagement, ulong AllocatedBytes, ulong CurrentUsageBytes, ulong PeakUsageBytes, IReadOnlyList<string> Files)
{
    public string Status => Files.Count == 0
        ? "페이지 파일 없음"
        : AutomaticManagement ? $"시스템 관리 · {OptimizationService.FormatBytes((long)AllocatedBytes)}" : $"사용자 설정 · {OptimizationService.FormatBytes((long)AllocatedBytes)}";
}

public sealed record MemoryProcessSnapshot(int ProcessId, string Name, string Kind, ulong WorkingSetBytes, ulong PrivateCommitBytes, ulong PeakWorkingSetBytes, uint PageFaultCount, int ThreadCount, string Assessment)
{
    public string ProcessText => $"{Name}  ·  PID {ProcessId}";
    public string WorkingSetText => OptimizationService.FormatBytes((long)WorkingSetBytes);
    public string PrivateCommitText => OptimizationService.FormatBytes((long)PrivateCommitBytes);
    public string PeakWorkingSetText => OptimizationService.FormatBytes((long)PeakWorkingSetBytes);
}

public sealed record MemoryAnalysisResult(
    DateTimeOffset CapturedAt,
    ulong PhysicalTotalBytes,
    ulong PhysicalAvailableBytes,
    ulong CommitTotalBytes,
    ulong CommitLimitBytes,
    ulong CommitPeakBytes,
    ulong SystemCacheBytes,
    ulong KernelPagedBytes,
    ulong KernelNonPagedBytes,
    bool? MemoryCompressionEnabled,
    PageFileSnapshot PageFile,
    IReadOnlyList<MemoryProcessSnapshot> Processes,
    string Assessment,
    string Recommendation)
{
    public ulong PhysicalUsedBytes => PhysicalTotalBytes >= PhysicalAvailableBytes ? PhysicalTotalBytes - PhysicalAvailableBytes : 0;
    public double PhysicalUsagePercent => PhysicalTotalBytes == 0 ? 0 : PhysicalUsedBytes * 100d / PhysicalTotalBytes;
    public double CommitUsagePercent => CommitLimitBytes == 0 ? 0 : CommitTotalBytes * 100d / CommitLimitBytes;
    public string PhysicalText => $"{OptimizationService.FormatBytes((long)PhysicalUsedBytes)} / {OptimizationService.FormatBytes((long)PhysicalTotalBytes)} · {PhysicalUsagePercent:0.0}%";
    public string AvailableText => OptimizationService.FormatBytes((long)PhysicalAvailableBytes);
    public string CommitText => $"{OptimizationService.FormatBytes((long)CommitTotalBytes)} / {OptimizationService.FormatBytes((long)CommitLimitBytes)} · {CommitUsagePercent:0.0}%";
    public string CacheText => OptimizationService.FormatBytes((long)SystemCacheBytes);
    public string KernelText => $"Paged {OptimizationService.FormatBytes((long)KernelPagedBytes)} · Non-paged {OptimizationService.FormatBytes((long)KernelNonPagedBytes)}";
    public string CompressionText => MemoryCompressionEnabled switch { true => "사용 중", false => "사용 안 함", _ => "일반 권한 조회 제한" };
}

public sealed class MemoryAnalysisService
{
    private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "Memory Compression", "smss", "csrss", "wininit", "services", "lsass", "svchost", "winlogon", "dwm", "MsMpEng"
    };

    public Task<MemoryAnalysisResult> CaptureAsync(CancellationToken token = default) => Task.Run(() => Capture(token), token);

    public static (string Assessment, string Recommendation) Evaluate(double physicalUsagePercent, double commitUsagePercent, bool hasPageFile)
    {
        if (commitUsagePercent >= 90) return ("위험", "커밋 한계에 근접했습니다. 메모리를 많이 사용하는 앱을 종료하고 페이지 파일을 시스템 관리로 설정했는지 확인하세요.");
        if (physicalUsagePercent >= 90 || commitUsagePercent >= 75) return ("주의", "메모리 사용량이 높습니다. 상위 프로세스의 전용 커밋과 작업 집합을 확인하세요.");
        if (!hasPageFile && commitUsagePercent >= 60) return ("확인 필요", "페이지 파일이 없어 커밋 여유가 작을 수 있습니다. 특별한 이유가 없다면 시스템 관리 페이지 파일을 권장합니다.");
        return ("정상", "현재 물리 메모리와 커밋 여유가 안정적인 범위입니다.");
    }

    private static MemoryAnalysisResult Capture(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var information = new PerformanceInformation { Size = (uint)Marshal.SizeOf<PerformanceInformation>() };
        if (!GetPerformanceInfo(out information, information.Size)) throw new InvalidOperationException("Windows 메모리 성능 정보를 읽을 수 없습니다.");
        var pageSize = (ulong)information.PageSize;
        var total = MultiplyPages(information.PhysicalTotal, pageSize);
        var available = MultiplyPages(information.PhysicalAvailable, pageSize);
        var commitTotal = MultiplyPages(information.CommitTotal, pageSize);
        var commitLimit = MultiplyPages(information.CommitLimit, pageSize);
        var commitPeak = MultiplyPages(information.CommitPeak, pageSize);
        var cache = MultiplyPages(information.SystemCache, pageSize);
        var paged = MultiplyPages(information.KernelPaged, pageSize);
        var nonPaged = MultiplyPages(information.KernelNonPaged, pageSize);
        var pageFile = CapturePageFile();
        var compression = CaptureMemoryCompression();
        var processes = CaptureProcesses(token);
        var physicalPercent = total == 0 ? 0 : (total - Math.Min(total, available)) * 100d / total;
        var commitPercent = commitLimit == 0 ? 0 : commitTotal * 100d / commitLimit;
        var evaluation = Evaluate(physicalPercent, commitPercent, pageFile.Files.Count > 0);
        return new MemoryAnalysisResult(DateTimeOffset.Now, total, available, commitTotal, commitLimit, commitPeak, cache, paged, nonPaged, compression, pageFile, processes, evaluation.Assessment, evaluation.Recommendation);
    }

    private static IReadOnlyList<MemoryProcessSnapshot> CaptureProcesses(CancellationToken token)
    {
        var result = new List<MemoryProcessSnapshot>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    var counters = new ProcessMemoryCountersEx { Size = (uint)Marshal.SizeOf<ProcessMemoryCountersEx>() };
                    if (!GetProcessMemoryInfo(process.Handle, out counters, counters.Size)) continue;
                    var name = process.ProcessName;
                    var protectedProcess = process.SessionId == 0 || ProtectedNames.Contains(name);
                    var kind = protectedProcess ? "Windows" : process.MainWindowHandle != IntPtr.Zero ? "앱" : "백그라운드";
                    var privateBytes = (ulong)counters.PrivateUsage;
                    var workingSet = (ulong)counters.WorkingSetSize;
                    var assessment = protectedProcess ? "Windows 구성요소" : privateBytes >= 2UL * 1024 * 1024 * 1024 ? "매우 높음" : privateBytes >= 1024UL * 1024 * 1024 ? "검토 권장" : "정상 범위";
                    result.Add(new MemoryProcessSnapshot(process.Id, name, kind, workingSet, privateBytes, (ulong)counters.PeakWorkingSetSize, counters.PageFaultCount, process.Threads.Count, assessment));
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
                catch (NotSupportedException) { }
            }
        }
        return result.OrderByDescending(item => item.PrivateCommitBytes).ThenByDescending(item => item.WorkingSetBytes).Take(50).ToList();
    }

    private static PageFileSnapshot CapturePageFile()
    {
        var automatic = false;
        var files = new List<string>();
        ulong allocated = 0, current = 0, peak = 0;
        try
        {
            using (var searcher = new ManagementObjectSearcher("SELECT AutomaticManagedPagefile FROM Win32_ComputerSystem"))
                foreach (ManagementObject item in searcher.Get()) { automatic = Convert.ToBoolean(item["AutomaticManagedPagefile"] ?? false); break; }
            using var pageFiles = new ManagementObjectSearcher("SELECT Name, AllocatedBaseSize, CurrentUsage, PeakUsage FROM Win32_PageFileUsage");
            foreach (ManagementObject item in pageFiles.Get())
            {
                if (item["Name"] is string name && !string.IsNullOrWhiteSpace(name)) files.Add(name);
                allocated += Convert.ToUInt64(item["AllocatedBaseSize"] ?? 0) * 1024 * 1024;
                current += Convert.ToUInt64(item["CurrentUsage"] ?? 0) * 1024 * 1024;
                peak += Convert.ToUInt64(item["PeakUsage"] ?? 0) * 1024 * 1024;
            }
        }
        catch { }
        return new PageFileSnapshot(automatic, allocated, current, peak, files);
    }

    private static bool? CaptureMemoryCompression()
    {
        try
        {
            var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Management");
            scope.Connect();
            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT MemoryCompression FROM MSFT_MMAgent"));
            foreach (ManagementObject item in searcher.Get()) return Convert.ToBoolean(item["MemoryCompression"]);
        }
        catch { }
        return null;
    }

    private static ulong MultiplyPages(nuint pages, ulong pageSize)
    {
        var value = (ulong)pages;
        return value > ulong.MaxValue / Math.Max(1, pageSize) ? ulong.MaxValue : value * pageSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PerformanceInformation
    {
        public uint Size;
        public nuint CommitTotal;
        public nuint CommitLimit;
        public nuint CommitPeak;
        public nuint PhysicalTotal;
        public nuint PhysicalAvailable;
        public nuint SystemCache;
        public nuint KernelTotal;
        public nuint KernelPaged;
        public nuint KernelNonPaged;
        public nuint PageSize;
        public uint HandleCount;
        public uint ProcessCount;
        public uint ThreadCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessMemoryCountersEx
    {
        public uint Size;
        public uint PageFaultCount;
        public nuint PeakWorkingSetSize;
        public nuint WorkingSetSize;
        public nuint QuotaPeakPagedPoolUsage;
        public nuint QuotaPagedPoolUsage;
        public nuint QuotaPeakNonPagedPoolUsage;
        public nuint QuotaNonPagedPoolUsage;
        public nuint PagefileUsage;
        public nuint PeakPagefileUsage;
        public nuint PrivateUsage;
    }

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPerformanceInfo(out PerformanceInformation performanceInformation, uint size);

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessMemoryInfo(IntPtr process, out ProcessMemoryCountersEx counters, uint size);
}

