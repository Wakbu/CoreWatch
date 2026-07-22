namespace SystemChecker.Models;

public sealed record ReportTelemetrySummary(
    int SampleCount,
    double CpuAverage,
    double CpuPeak,
    double MemoryAverage,
    double MemoryPeak,
    double DiskAverageMbps,
    double DiskPeakMbps,
    double NetworkAverageMbps,
    double NetworkPeakMbps);

public sealed record ReportSnapshot(
    string CoreWatchVersion,
    DateTimeOffset GeneratedAt,
    string MachineName,
    string OperatingSystem,
    string Assessment,
    string AssessmentReason,
    string Coverage,
    string PrivacyNotice,
    IReadOnlyList<HardwareItem> Hardware,
    HealthSnapshot? Health,
    IReadOnlyList<DiagnosticItem> Diagnostics,
    FullBenchmarkResult? Benchmark,
    IReadOnlyList<BenchmarkHistoryItem> History,
    ReportTelemetrySummary Telemetry);
