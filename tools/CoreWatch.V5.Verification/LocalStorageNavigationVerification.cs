using System.IO;
using System.Runtime.CompilerServices;
using SystemChecker.Models;
using SystemChecker.Services;

namespace CoreWatch.Verification;

internal static class LocalStorageNavigationVerification
{
    [ModuleInitializer]
    internal static void Run()
    {
        static void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException($"FAILED: {name}"); Console.WriteLine($"PASS {name}"); }
        var local = LocalBenchmarkComparisonService.Compare(550, Enumerable.Range(1, 10).Select(value => value * 100)); Check(local.SampleCount == 10 && local.Percentile == 50 && local.Median == 550, "local-only benchmark percentile");
        var database = Path.Combine(Path.GetTempPath(), $"corewatch-local-{Guid.NewGuid():N}.db"); try { var store = new LocalBenchmarkComparisonStore(database); var sample = new LocalBenchmarkSample("v1", "standard", 1000); store.SaveAsync(DateTimeOffset.UtcNow, sample).GetAwaiter().GetResult(); Check(store.GetScoresAsync("v1", "standard").GetAwaiter().GetResult().SequenceEqual([1000]), "local-only benchmark persistence"); } finally { if (File.Exists(database)) File.Delete(database); }
        Check(StorageDiagnosticsService.Evaluate(9, false, false).Assessment == "위험" && StorageDiagnosticsService.Evaluate(20, true, false).Assessment == "주의" && StorageDiagnosticsService.Evaluate(20, false, false).Assessment == "정상", "storage diagnostic policy");
        var storage = new StorageDiagnosticsService().CaptureAsync().GetAwaiter().GetResult(); Check(storage.Volumes.All(item => item.FreePercent is >= 0 and <= 100) && !string.IsNullOrWhiteSpace(storage.Summary), "storage read-only diagnostics");
    }
}
