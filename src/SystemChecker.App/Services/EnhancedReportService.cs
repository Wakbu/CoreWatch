using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SystemChecker.Models;

namespace SystemChecker.Services;

public sealed class EnhancedReportService
{
    private static bool _configured;

    public static ReportSnapshot BuildSnapshot(IEnumerable<HardwareItem> hardware, HealthSnapshot? health, IEnumerable<DiagnosticItem> diagnostics, FullBenchmarkResult? benchmark, IEnumerable<BenchmarkHistoryItem> history, IEnumerable<double> cpu, IEnumerable<double> memory, IEnumerable<double> disk, IEnumerable<double> network)
    {
        var hw = hardware.ToList();
        var issues = diagnostics.Concat(health?.DriverDiagnostics ?? []).Distinct().ToList();
        var recent = history.Take(20).ToList();
        var cpuValues = cpu.ToList(); var memoryValues = memory.ToList(); var diskValues = disk.ToList(); var networkValues = network.ToList();
        var warnings = issues.Count(item => !item.Severity.Equals("INFO", StringComparison.OrdinalIgnoreCase));
        var storageWarnings = health?.Storage.Count(item => !IsHealthy(item.Status)) ?? 0;
        var assessment = hw.Count == 0 ? "데이터 준비 중" : warnings + storageWarnings > 0 ? "확인 필요" : "정상";
        var reason = assessment == "데이터 준비 중" ? "하드웨어 및 상태 데이터 수집이 끝난 뒤 보고서를 생성하세요."
            : assessment == "정상" ? "현재 수집 범위에서 주의 또는 위험으로 분류된 항목이 없습니다."
            : $"진단 {warnings}개와 저장장치 상태 {storageWarnings}개를 보고서에서 확인하세요.";
        var samples = Math.Max(Math.Max(cpuValues.Count, memoryValues.Count), Math.Max(diskValues.Count, networkValues.Count));
        return new("6.1.1", DateTimeOffset.Now, Environment.MachineName, RuntimeInformation.OSDescription, assessment, reason,
            $"HW {hw.Count} · 진단 {issues.Count} · 이력 {recent.Count} · 표본 {samples}",
            "이 보고서는 로컬에서 생성되며 CoreWatch가 자동 업로드하거나 외부 서버로 전송하지 않습니다.",
            hw, health, issues, benchmark, recent,
            new(samples, Average(cpuValues), Peak(cpuValues), Average(memoryValues), Peak(memoryValues), Average(diskValues), Peak(diskValues), Average(networkValues), Peak(networkValues)));
    }

    public Task ExportJsonAsync(string path, ReportSnapshot report) => File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

    public void ExportPdf(string path, ReportSnapshot report)
    {
        Configure();
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4); page.Margin(28); page.DefaultTextStyle(style => style.FontSize(9).FontFamily("Malgun Gothic"));
            page.Header().Column(column =>
            {
                column.Item().Text("COREWATCH 6 / LOCAL DIAGNOSTIC REPORT").FontSize(18).SemiBold().FontColor(Colors.Blue.Darken2);
                column.Item().Text($"{report.GeneratedAt:yyyy-MM-dd HH:mm:ss zzz} · {report.MachineName} · {report.OperatingSystem}").FontColor(Colors.Grey.Darken1);
            });
            page.Content().PaddingVertical(14).Column(column =>
            {
                column.Spacing(8); Section(column, "EXECUTIVE SUMMARY");
                column.Item().Background(AssessmentColor(report.Assessment)).Padding(10).Column(summary =>
                {
                    summary.Item().Text($"{report.Assessment} · {report.Coverage}").SemiBold();
                    summary.Item().Text(report.AssessmentReason);
                    summary.Item().Text(report.PrivacyNotice).FontSize(8).FontColor(Colors.Grey.Darken1);
                });
                Section(column, "RECENT TELEMETRY"); column.Item().Text(TelemetryText(report.Telemetry));
                Section(column, "DIAGNOSTICS");
                if (report.Diagnostics.Count == 0) column.Item().Text("진단 항목 없음");
                foreach (var item in report.Diagnostics) column.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(3).Text($"[{item.Severity}] {item.Title} · {item.Detail}");
                Section(column, "BENCHMARK"); column.Item().Text(BenchmarkText(report.Benchmark));
                Section(column, "STORAGE / BATTERY HEALTH"); AddHealth(column, report.Health);
                Section(column, "HARDWARE INVENTORY");
                foreach (var item in report.Hardware) column.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(3).Text($"[{item.Category}] {item.Name} · {item.Value}");
                Section(column, "RECENT BENCHMARKS");
                if (report.History.Count == 0) column.Item().Text("벤치마크 이력 없음");
                foreach (var item in report.History) column.Item().Text($"{item.MeasuredAt:yyyy-MM-dd HH:mm}  {item.Score:N0}점 {item.Grade}  CPU {item.CpuScore} / MEM {item.MemoryScore} / DISK {item.DiskScore} / GPU {item.GpuScore}");
            });
            page.Footer().AlignCenter().Text(text => { text.Span("CoreWatch 6 · "); text.CurrentPageNumber(); text.Span(" / "); text.TotalPages(); });
        })).GeneratePdf(path);
    }

    public void ExportHtml(string path, ReportSnapshot report)
    {
        static string H(object? value) => WebUtility.HtmlEncode(value?.ToString() ?? "—");
        var b = new StringBuilder("<!doctype html><html lang=ko><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'><title>CoreWatch 6 Report</title><style>body{font:14px Segoe UI,sans-serif;max-width:1100px;margin:40px auto;padding:0 20px;color:#181b20}h1{font-size:26px}h2{margin-top:30px;border-bottom:1px solid #ddd;padding-bottom:8px}table{border-collapse:collapse;width:100%}td,th{padding:9px;border-bottom:1px solid #e5e7eb;text-align:left}.summary{padding:18px;background:#f3f6f9;border-left:5px solid #2478a8}.muted{color:#68707d}.privacy{color:#237056}</style><body>");
        b.Append($"<h1>COREWATCH 6 / LOCAL DIAGNOSTIC REPORT</h1><p class=muted>{report.GeneratedAt:yyyy-MM-dd HH:mm:ss zzz} · {H(report.MachineName)} · {H(report.OperatingSystem)}</p>");
        b.Append($"<div class=summary><strong>{H(report.Assessment)}</strong><p>{H(report.AssessmentReason)}</p><span>{H(report.Coverage)}</span></div><p class=privacy>{H(report.PrivacyNotice)}</p><h2>Recent telemetry</h2><p>{H(TelemetryText(report.Telemetry))}</p>");
        b.Append("<h2>Diagnostics</h2><table><tr><th>Severity</th><th>Title</th><th>Detail</th></tr>");
        foreach (var item in report.Diagnostics) b.Append($"<tr><td>{H(item.Severity)}</td><td>{H(item.Title)}</td><td>{H(item.Detail)}</td></tr>");
        b.Append($"</table><h2>Benchmark</h2><p>{H(BenchmarkText(report.Benchmark))}</p><h2>Storage / Battery</h2><table><tr><th>Device</th><th>Status</th><th>Temperature</th><th>Wear</th><th>Errors</th></tr>");
        if (report.Health is not null) foreach (var item in report.Health.Storage) b.Append($"<tr><td>{H(item.Device)}</td><td>{H(item.Status)}</td><td>{H(item.Temperature)}</td><td>{H(item.Wear)}</td><td>{H(item.ReadErrors)} / {H(item.WriteErrors)}</td></tr>");
        b.Append("</table><h2>Hardware</h2><table><tr><th>Category</th><th>Name</th><th>Value</th></tr>");
        foreach (var item in report.Hardware) b.Append($"<tr><td>{H(item.Category)}</td><td>{H(item.Name)}</td><td>{H(item.Value)}</td></tr>");
        b.Append("</table><h2>History</h2><table><tr><th>Time</th><th>Score</th><th>Grade</th><th>CPU / MEM / DISK / GPU</th></tr>");
        foreach (var item in report.History) b.Append($"<tr><td>{item.MeasuredAt:yyyy-MM-dd HH:mm}</td><td>{item.Score}</td><td>{H(item.Grade)}</td><td>{item.CpuScore} / {item.MemoryScore} / {item.DiskScore} / {item.GpuScore}</td></tr>");
        b.Append("</table></body></html>"); File.WriteAllText(path, b.ToString(), Encoding.UTF8);
    }

    private static void AddHealth(ColumnDescriptor column, HealthSnapshot? health)
    {
        if (health is null) { column.Item().Text("상태 정보 없음"); return; }
        foreach (var item in health.Storage) column.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).Text($"{item.Device} | {item.Status} | 온도 {item.Temperature} | 마모 {item.Wear} | 오류 R {item.ReadErrors} / W {item.WriteErrors}");
        column.Item().Text($"배터리: {health.Battery.Status}, 충전 {health.Battery.Charge}, 건강도 {health.Battery.Health}, 사이클 {health.Battery.CycleCount}");
    }
    private static string TelemetryText(ReportTelemetrySummary t) => $"표본 {t.SampleCount} · CPU 평균/최고 {t.CpuAverage:0.0}/{t.CpuPeak:0.0}% · 메모리 {t.MemoryAverage:0.0}/{t.MemoryPeak:0.0}% · 디스크 {t.DiskAverageMbps:0.00}/{t.DiskPeakMbps:0.00} MB/s · 네트워크 {t.NetworkAverageMbps:0.00}/{t.NetworkPeakMbps:0.00} Mbps";
    private static string BenchmarkText(FullBenchmarkResult? value) => value is null ? "아직 실행된 벤치마크가 없습니다." : $"종합 {value.OverallScore:N0}점 / {value.Grade}등급 | CPU {value.CpuMemory.CpuScore:N0} | MEMORY {value.CpuMemory.MemoryScore:N0} | DISK {value.Disk.Score:N0} | GPU {value.Gpu.Score:N0} · DiskSpd R/W {value.Disk.SequentialReadMbps:N0}/{value.Disk.SequentialWriteMbps:N0} MB/s · GPU {value.Gpu.FramesPerSecond:N1} FPS";
    private static double Average(IReadOnlyCollection<double> values) => values.Count == 0 ? 0 : values.Average();
    private static double Peak(IReadOnlyCollection<double> values) => values.Count == 0 ? 0 : values.Max();
    private static bool IsHealthy(string status) => status.Contains("정상", StringComparison.OrdinalIgnoreCase) || status.Contains("OK", StringComparison.OrdinalIgnoreCase) || status.Contains("Good", StringComparison.OrdinalIgnoreCase) || status.Contains("Healthy", StringComparison.OrdinalIgnoreCase);
    private static string AssessmentColor(string value) => value == "정상" ? Colors.Green.Lighten4 : value == "확인 필요" ? Colors.Orange.Lighten4 : Colors.Grey.Lighten4;
    private static void Section(ColumnDescriptor column, string title) => column.Item().PaddingTop(4).Text(title).Bold();
    private static void Configure() { if (_configured) return; QuestPDF.Settings.License = LicenseType.Community; var font = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "malgun.ttf"); if (File.Exists(font)) using (var stream = File.OpenRead(font)) FontManager.RegisterFont(stream); _configured = true; }
}
