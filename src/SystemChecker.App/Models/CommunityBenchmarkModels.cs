namespace SystemChecker.Models;

public sealed record CommunityHardwareProfile(string CpuModel, int LogicalProcessorCount, string GpuModel, int MemoryCapacityBucketGb, int MemoryModuleCount, int MemorySpeedMtps, int OsMajorVersion, string PowerPlan);
public sealed record CommunityBenchmarkMeasurements(double CpuThroughputMbps, double MemoryBandwidthGbps, double DiskSequentialReadMbps, double DiskSequentialWriteMbps, double DiskRandomReadIops, double DiskRandomWriteIops, double GpuFramesPerSecond, double GpuMegaPixelsPerSecond, int CpuScore, int MemoryScore, int DiskScore, int GpuScore, int OverallScore);
public sealed record CommunityBenchmarkConditions(double? MaximumTemperatureC, bool WasThrottled, bool Completed);
public sealed record CommunityBenchmarkSubmission(int SchemaVersion, string AppVersion, string BenchmarkVersion, string TestProfile, DateTimeOffset MeasuredAt, CommunityHardwareProfile Hardware, CommunityBenchmarkMeasurements Measurements, CommunityBenchmarkConditions Conditions);
public sealed record CommunityBenchmarkEnvelope(string InstallationId, CommunityBenchmarkSubmission Result);
public sealed record CommunityBenchmarkSample(string BenchmarkVersion, string TestProfile, string CpuModel, string GpuModel, int MemoryCapacityBucketGb, int OverallScore);
public sealed record BenchmarkPercentile(int SampleCount, double? Percentile, double? Median, string Status);
public sealed record CommunityBenchmarkComparison(BenchmarkPercentile Overall, BenchmarkPercentile SameCpu, BenchmarkPercentile SameGpu, BenchmarkPercentile SimilarMemory);
public sealed record AnonymousSubmissionPreview(string Json, IReadOnlyList<string> ExcludedPersonalData);
