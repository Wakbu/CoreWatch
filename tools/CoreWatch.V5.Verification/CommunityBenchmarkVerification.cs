using System.Runtime.CompilerServices;
using SystemChecker.Models;
using SystemChecker.Services;

namespace CoreWatch.Verification;

internal static class CommunityBenchmarkVerification
{
    [ModuleInitializer]
    internal static void Run()
    {
        static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException($"FAILED: {name}");
            Console.WriteLine($"PASS {name}");
        }

        var measuredAt = DateTimeOffset.UtcNow;
        var result = new FullBenchmarkResult(measuredAt,
            new BenchmarkResult(measuredAt, 1000, 20, 1000, 1000, 1000, "B", "test", "test"),
            new DiskBenchmarkResult(1000, 900, 10000, 9000, 1, 1000, "test"),
            new GpuBenchmarkResult(60, 125, 12, true, 1000, "test"), 1000, "B", "test");
        var profile = new CommunityHardwareProfile("CPU A", 16, "GPU A", 32, 2, 5600, 10, "Balanced");
        var submission = CommunityBenchmarkService.CreateSubmission("5.7.0", "full-v1", "standard", result, profile, new CommunityBenchmarkConditions(70, false, true));
        var preview = CommunityBenchmarkService.CreateAnonymousPreview("anonymous-installation-id", submission);
        Check(preview.Json.Contains("anonymous-installation-id") && !preview.Json.Contains(Environment.MachineName, StringComparison.OrdinalIgnoreCase), "anonymous benchmark preview");
        Check(preview.ExcludedPersonalData.Count >= 6, "personal data exclusion disclosure");

        var samples = Enumerable.Range(1, 10).Select(i => new CommunityBenchmarkSample("full-v1", "standard", "CPU A", "GPU A", 32, i * 100)).Append(new CommunityBenchmarkSample("other", "standard", "CPU A", "GPU A", 32, 9999));
        var comparison = CommunityBenchmarkService.Compare(new CommunityBenchmarkSample("full-v1", "standard", "CPU A", "GPU A", 32, 550), samples);
        Check(comparison.Overall.SampleCount == 10 && comparison.Overall.Percentile == 50 && comparison.Overall.Median == 550, "community percentile and median");
        Check(comparison.SameCpu.Status == "비교 가능", "same hardware comparison");
        Check(CommunityBenchmarkService.Compare(new CommunityBenchmarkSample("full-v1", "standard", "CPU B", "GPU B", 64, 500), samples).SameCpu.Status == "표본 부족", "minimum community sample policy");
    }
}
