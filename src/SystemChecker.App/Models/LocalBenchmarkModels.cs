namespace SystemChecker.Models;

public sealed record LocalBenchmarkSample(string BenchmarkVersion, string TestProfile, int OverallScore);
public sealed record LocalBenchmarkComparison(int SampleCount, double Percentile, double Median, string Status);
