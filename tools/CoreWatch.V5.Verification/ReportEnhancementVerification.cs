using System.IO;
using System.Runtime.CompilerServices;
using SystemChecker.Models;
using SystemChecker.Services;

namespace CoreWatch.Verification;

internal static class ReportEnhancementVerification
{
    [ModuleInitializer]
    internal static void Run()
    {
        static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException($"FAILED: {name}");
            Console.WriteLine($"PASS {name}");
        }

        var report = EnhancedReportService.BuildSnapshot(
            [new HardwareItem("CPU", "Test CPU", "8 cores")],
            null,
            [new DiagnosticItem("WARN", "<확인 필요>", "테스트 진단")],
            null,
            [],
            [10d, 20d, 30d],
            [40d, 50d],
            [1d, 3d],
            [2d, 4d]);
        Check(report.CoreWatchVersion == "6.0.0" && report.Assessment == "확인 필요", "report executive assessment");
        Check(report.Telemetry.SampleCount == 3 && report.Telemetry.CpuAverage == 20 && report.Telemetry.NetworkPeakMbps == 4, "report telemetry summary");

        var directory = Path.Combine(Path.GetTempPath(), $"corewatch-report-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var service = new EnhancedReportService();
            var json = Path.Combine(directory, "report.json");
            var html = Path.Combine(directory, "report.html");
            var pdf = Path.Combine(directory, "report.pdf");
            service.ExportJsonAsync(json, report).GetAwaiter().GetResult();
            service.ExportHtml(html, report);
            service.ExportPdf(pdf, report);
            Check(File.ReadAllText(json).Contains("\"CoreWatchVersion\": \"6.0.0\"", StringComparison.Ordinal), "report JSON export");
            Check(File.ReadAllText(html).Contains("&lt;확인 필요&gt;", StringComparison.Ordinal) && new FileInfo(pdf).Length > 1_000, "report HTML and PDF export");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
