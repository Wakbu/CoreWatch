namespace SystemChecker.Models;

public sealed record StorageHealthItem(string Device, string Status, string Temperature, string Wear, string ReadErrors, string WriteErrors, string Detail);
public sealed record BatteryHealthInfo(string Status, string Charge, string Health, string CycleCount, string Detail);
public sealed record HealthSnapshot(DateTimeOffset CapturedAt, IReadOnlyList<StorageHealthItem> Storage, BatteryHealthInfo Battery, IReadOnlyList<DiagnosticItem> DriverDiagnostics);

public sealed record DiskBenchmarkResult(double SequentialReadMbps, double SequentialWriteMbps, double RandomReadIops, double RandomWriteIops, double AverageLatencyMs, int Score, string Engine);
public sealed record GpuBenchmarkResult(double FramesPerSecond, double MegaPixelsPerSecond, int RenderTier, bool HardwareAccelerated, int Score, string Detail);
public sealed record FullBenchmarkResult(DateTimeOffset MeasuredAt, BenchmarkResult CpuMemory, DiskBenchmarkResult Disk, GpuBenchmarkResult Gpu, int OverallScore, string Grade, string Comparison);
public sealed record BenchmarkHistoryItem(long Id, DateTimeOffset MeasuredAt, int Score, string Grade, int CpuScore, int MemoryScore, int DiskScore, int GpuScore, string Difference);

