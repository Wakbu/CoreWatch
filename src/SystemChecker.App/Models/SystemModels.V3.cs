namespace SystemChecker.Models;

public sealed record HardwareItem(string Category, string Name, string Value);
public sealed record DiagnosticItem(string Severity, string Title, string Detail);

public sealed record BenchmarkResult(
    DateTimeOffset MeasuredAt,
    double CpuThroughputMbps,
    double MemoryBandwidthGbps,
    int CpuScore,
    int MemoryScore,
    int OverallScore,
    string Grade,
    string Environment,
    string ScoringBasis);

public sealed record SystemSnapshot(
    DateTimeOffset CapturedAt,
    double CpuUsagePercent,
    double MemoryUsagePercent,
    ulong UsedMemoryBytes,
    ulong TotalMemoryBytes,
    double NetworkReceiveMbps,
    double NetworkSendMbps,
    double LowestDiskFreePercent,
    double DiskReadMbps = 0,
    double DiskWriteMbps = 0);
