using System.Net;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SystemChecker.Models;

namespace SystemChecker.Services;

public sealed class ReportService
{
    private static bool _configured;
    public void ExportPdf(string path, IEnumerable<HardwareItem> hardware, HealthSnapshot? health, FullBenchmarkResult? benchmark, IEnumerable<BenchmarkHistoryItem> history)
    {
        Configure(); var hw=hardware.ToList();var hist=history.ToList();
        Document.Create(document=>document.Page(page=>
        {
            page.Size(PageSizes.A4);page.Margin(28);page.DefaultTextStyle(x=>x.FontSize(9).FontFamily("Malgun Gothic"));
            page.Header().Column(c=>{c.Item().Text("SYSTEM CHECKER / LOCAL REPORT").FontSize(18).SemiBold().FontColor(Colors.Blue.Darken2);c.Item().Text($"생성 시각 {DateTime.Now:yyyy-MM-dd HH:mm:ss} · {Environment.MachineName}").FontColor(Colors.Grey.Darken1);});
            page.Content().PaddingVertical(14).Column(c=>
            {
                c.Spacing(8);c.Item().Text("BENCHMARK").Bold();
                c.Item().Background(Colors.Grey.Lighten4).Padding(10).Text(benchmark is null?"아직 실행된 벤치마크가 없습니다.":$"종합 {benchmark.OverallScore:N0}점 / {benchmark.Grade}등급 | CPU {benchmark.CpuMemory.CpuScore:N0} | MEMORY {benchmark.CpuMemory.MemoryScore:N0} | DISK {benchmark.Disk.Score:N0} | GPU {benchmark.Gpu.Score:N0}\nDiskSpd: 읽기 {benchmark.Disk.SequentialReadMbps:N0} MB/s, 쓰기 {benchmark.Disk.SequentialWriteMbps:N0} MB/s, 4K {benchmark.Disk.RandomReadIops:N0}/{benchmark.Disk.RandomWriteIops:N0} IOPS\nGPU: {benchmark.Gpu.FramesPerSecond:N1} FPS, {benchmark.Gpu.MegaPixelsPerSecond:N1} MPix/s ({benchmark.Gpu.Detail})");
                c.Item().Text("STORAGE / BATTERY HEALTH").Bold();
                if(health is null)c.Item().Text("상태 정보 없음");else{foreach(var d in health.Storage)c.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).Text($"{d.Device} | {d.Status} | 온도 {d.Temperature} | 마모 {d.Wear} | 오류 R {d.ReadErrors} / W {d.WriteErrors}");c.Item().Text($"배터리: {health.Battery.Status}, 충전 {health.Battery.Charge}, 건강도 {health.Battery.Health}, 사이클 {health.Battery.CycleCount}");}
                c.Item().Text("HARDWARE INVENTORY").Bold();foreach(var item in hw)c.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(3).Text($"[{item.Category}] {item.Name} · {item.Value}");
                c.Item().Text("RECENT BENCHMARKS").Bold();foreach(var h in hist)c.Item().Text($"{h.MeasuredAt:yyyy-MM-dd HH:mm}  {h.Score:N0}점 {h.Grade}  CPU {h.CpuScore} / MEM {h.MemoryScore} / DISK {h.DiskScore} / GPU {h.GpuScore}");
            });page.Footer().AlignCenter().Text(x=>{x.Span("System Checker · ");x.CurrentPageNumber();x.Span(" / ");x.TotalPages();});
        })).GeneratePdf(path);
    }
    public void ExportHtml(string path,IEnumerable<HardwareItem> hardware,HealthSnapshot? health,FullBenchmarkResult? benchmark,IEnumerable<BenchmarkHistoryItem> history)
    {
        static string H(object? v)=>WebUtility.HtmlEncode(v?.ToString()??"—");var b=new StringBuilder("<!doctype html><meta charset=utf-8><title>System Checker Report</title><style>body{font:14px Segoe UI,sans-serif;max-width:1100px;margin:40px auto;color:#181b20}h1{font-size:26px}h2{margin-top:30px;border-bottom:1px solid #ddd;padding-bottom:8px}table{border-collapse:collapse;width:100%}td,th{padding:9px;border-bottom:1px solid #e5e7eb;text-align:left}.score{font:600 42px Consolas;color:#1687c8}.muted{color:#68707d}</style>");
        b.Append($"<h1>SYSTEM CHECKER</h1><p class=muted>{DateTime.Now:yyyy-MM-dd HH:mm:ss} · {H(Environment.MachineName)}</p><h2>Benchmark</h2>");if(benchmark is null)b.Append("<p>측정 결과 없음</p>");else b.Append($"<div class=score>{benchmark.OverallScore:N0} / {H(benchmark.Grade)}</div><p>CPU {benchmark.CpuMemory.CpuScore:N0} · MEMORY {benchmark.CpuMemory.MemoryScore:N0} · DISK {benchmark.Disk.Score:N0} · GPU {benchmark.Gpu.Score:N0}</p><p>DiskSpd R/W {benchmark.Disk.SequentialReadMbps:N0}/{benchmark.Disk.SequentialWriteMbps:N0} MB/s · 4K {benchmark.Disk.RandomReadIops:N0}/{benchmark.Disk.RandomWriteIops:N0} IOPS · GPU {benchmark.Gpu.FramesPerSecond:N1} FPS</p>");
        b.Append("<h2>Storage / Battery</h2><table><tr><th>Device</th><th>Status</th><th>Temperature</th><th>Wear</th><th>Errors</th></tr>");if(health is not null)foreach(var d in health.Storage)b.Append($"<tr><td>{H(d.Device)}</td><td>{H(d.Status)}</td><td>{H(d.Temperature)}</td><td>{H(d.Wear)}</td><td>{H(d.ReadErrors)} / {H(d.WriteErrors)}</td></tr>");b.Append("</table>");if(health is not null)b.Append($"<p>Battery: {H(health.Battery.Status)} · {H(health.Battery.Charge)} · health {H(health.Battery.Health)}</p>");
        b.Append("<h2>Hardware</h2><table><tr><th>Category</th><th>Name</th><th>Value</th></tr>");foreach(var h in hardware)b.Append($"<tr><td>{H(h.Category)}</td><td>{H(h.Name)}</td><td>{H(h.Value)}</td></tr>");b.Append("</table><h2>History</h2><table><tr><th>Time</th><th>Score</th><th>Grade</th><th>CPU / MEM / DISK / GPU</th></tr>");foreach(var h in history)b.Append($"<tr><td>{h.MeasuredAt:yyyy-MM-dd HH:mm}</td><td>{h.Score}</td><td>{H(h.Grade)}</td><td>{h.CpuScore} / {h.MemoryScore} / {h.DiskScore} / {h.GpuScore}</td></tr>");b.Append("</table>");File.WriteAllText(path,b.ToString(),Encoding.UTF8);
    }
    private static void Configure(){if(_configured)return;QuestPDF.Settings.License=LicenseType.Community;var font=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"malgun.ttf");if(File.Exists(font))using(var stream=File.OpenRead(font))FontManager.RegisterFont(stream);_configured=true;}
}

