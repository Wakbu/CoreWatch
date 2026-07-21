namespace SystemChecker.Models;

public sealed record ExternalBenchmarkComparisonResponse(
    int SchemaVersion,
    string BenchmarkVersion,
    string TestProfile,
    CommunityBenchmarkComparison Comparison);

public sealed record SecureComparisonSnapshot(
    CommunityBenchmarkComparison Local,
    CommunityBenchmarkComparison? External,
    string Source,
    string Status);
