using System.Diagnostics;
using System.Text;
using System.Text.Json;
using SystemChecker.Models;

namespace SystemChecker.Services;

public sealed class HealthService
{
    public async Task<HealthSnapshot> CaptureAsync(CancellationToken token = default)
    {
        const string script = "[Console]::OutputEncoding=[Text.Encoding]::UTF8;$ErrorActionPreference='SilentlyContinue';" +
            "$storage=@(Get-PhysicalDisk|ForEach-Object{$d=$_;$r=$d|Get-StorageReliabilityCounter;[pscustomobject]@{Device=$d.FriendlyName;Status=$d.HealthStatus;Temperature=$r.Temperature;Wear=$r.Wear;ReadErrors=$r.ReadErrorsTotal;WriteErrors=$r.WriteErrorsTotal;MediaType=$d.MediaType;BusType=$d.BusType}});" +
            "$b=Get-CimInstance Win32_Battery|Select-Object -First 1;$full=Get-CimInstance -Namespace root/wmi BatteryFullChargedCapacity|Select-Object -First 1;$design=Get-CimInstance -Namespace root/wmi BatteryStaticData|Select-Object -First 1;" +
            "$battery=if($b){[pscustomobject]@{Present=$true;Status=$b.Status;Charge=$b.EstimatedChargeRemaining;Cycles=$b.CycleCount;Full=$full.FullChargedCapacity;Design=$design.DesignedCapacity}}else{[pscustomobject]@{Present=$false}};" +
            "$drivers=@(Get-CimInstance Win32_PnPEntity|Where-Object ConfigManagerErrorCode -ne 0|Select-Object -First 30 Name,ConfigManagerErrorCode);[pscustomobject]@{Storage=$storage;Battery=$battery;Drivers=$drivers}|ConvertTo-Json -Depth 5 -Compress";
        try
        {
            using var doc = await RunPowerShellAsync(script, token);
            return Parse(doc.RootElement);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new(DateTimeOffset.Now, [], new("조회 실패", "—", "—", "—", ex.Message), [new("INFO", "상태 정보 수집 제한", ex.Message)]);
        }
    }

    private static HealthSnapshot Parse(JsonElement root)
    {
        var disks = new List<StorageHealthItem>();
        foreach (var d in Items(root, "Storage"))
        {
            var wear = Number(d, "Wear");
            disks.Add(new(Text(d, "Device"), Text(d, "Status"), Unit(d, "Temperature", "°C"), wear.HasValue ? $"{wear:0.#}% 사용" : "—",
                Text(d, "ReadErrors"), Text(d, "WriteErrors"), $"{Text(d, "MediaType")} · {Text(d, "BusType")}"));
        }
        var battery = new BatteryHealthInfo("배터리 없음", "—", "—", "—", "데스크톱 또는 배터리 미탑재 시스템");
        if (root.TryGetProperty("Battery", out var b) && b.TryGetProperty("Present", out var present) && present.ValueKind == JsonValueKind.True)
        {
            var full = Number(b, "Full"); var design = Number(b, "Design");
            var health = full.HasValue && design > 0 ? $"{full / design * 100:0.0}%" : "—";
            battery = new(Text(b, "Status"), Unit(b, "Charge", "%"), health, Text(b, "Cycles"), full.HasValue && design.HasValue ? $"완전 충전 {full:0} / 설계 {design:0} mWh" : "용량 정보 미지원");
        }
        var drivers = Items(root, "Drivers").Select(d => new DiagnosticItem("WARN", Text(d, "Name"), $"장치 관리자 오류 코드 {Text(d, "ConfigManagerErrorCode")}")).ToList();
        return new(DateTimeOffset.Now, disks, battery, drivers);
    }

    private static async Task<JsonDocument> RunPowerShellAsync(string script, CancellationToken token)
    {
        var info = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 };
        foreach (var a in new[] { "-NoProfile", "-NonInteractive", "-Command", script }) info.ArgumentList.Add(a);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("PowerShell 실행 실패");
        var output = process.StandardOutput.ReadToEndAsync(token); var error = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        if (process.ExitCode != 0) throw new InvalidOperationException((await error).Trim());
        return JsonDocument.Parse(await output);
    }
    private static IEnumerable<JsonElement> Items(JsonElement root, string name) { if (!root.TryGetProperty(name, out var v)) yield break; if (v.ValueKind == JsonValueKind.Array) foreach (var i in v.EnumerateArray()) yield return i; else if (v.ValueKind == JsonValueKind.Object) yield return v; }
    private static string Text(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined && !string.IsNullOrWhiteSpace(v.ToString()) ? v.ToString() : "—";
    private static double? Number(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.TryGetDouble(out var d) ? d : null;
    private static string Unit(JsonElement e, string n, string unit) => Number(e, n) is { } d ? $"{d:0.#}{unit}" : "—";
}

