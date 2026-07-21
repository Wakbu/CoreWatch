using System.Diagnostics;
using System.Management;

namespace SystemChecker.Services;

public sealed record StorageVolumeDiagnostic(string Volume, string FileSystem, string Capacity, string FreeSpace, double FreePercent, string FileSystemStatus, string TrimStatus, string Assessment, string Recommendation);
public sealed record StorageDiagnosticsResult(DateTimeOffset CapturedAt, IReadOnlyList<StorageVolumeDiagnostic> Volumes, string Summary);

public sealed class StorageDiagnosticsService
{
    public async Task<StorageDiagnosticsResult> CaptureAsync(CancellationToken token = default)
    {
        var trim = await QueryTrimAsync(token); var dirty = await Task.Run(QueryDirtyVolumes, token); var rows = new List<StorageVolumeDiagnostic>();
        foreach (var drive in DriveInfo.GetDrives().Where(item => item.DriveType == DriveType.Fixed && item.IsReady))
        {
            token.ThrowIfCancellationRequested(); var freePercent = drive.TotalSize > 0 ? drive.AvailableFreeSpace * 100d / drive.TotalSize : 0; dirty.TryGetValue(drive.Name[..2], out var isDirty);
            var (assessment, recommendation) = Evaluate(freePercent, isDirty, trim == "비활성");
            rows.Add(new(drive.Name, drive.DriveFormat, OptimizationService.FormatBytes(drive.TotalSize), OptimizationService.FormatBytes(drive.AvailableFreeSpace), freePercent, isDirty == true ? "오류 검사 필요" : isDirty == false ? "정상" : "조회 제한", trim, assessment, recommendation));
        }
        var warnings = rows.Count(item => item.Assessment != "정상"); return new(DateTimeOffset.Now, rows, rows.Count == 0 ? "고정 볼륨을 찾지 못했습니다." : $"고정 볼륨 {rows.Count}개 · 확인 필요 {warnings}개 · 읽기 전용 진단");
    }

    public static (string Assessment, string Recommendation) Evaluate(double freePercent, bool? isDirty, bool trimDisabled)
    {
        if (isDirty == true) return ("주의", "중요 파일을 백업한 뒤 Windows 오류 검사를 검토하세요.");
        if (freePercent < 10) return ("위험", "최소 10% 이상 여유 공간을 확보하세요.");
        if (freePercent < 15) return ("확인 필요", "업데이트와 임시 파일 작업을 위해 15% 이상 여유 공간을 권장합니다.");
        if (trimDisabled) return ("확인 필요", "SSD 사용 여부를 확인한 뒤 Windows TRIM 설정을 검토하세요.");
        return ("정상", "현재 즉시 변경이 필요한 항목이 없습니다.");
    }

    private static Dictionary<string, bool?> QueryDirtyVolumes()
    {
        var result = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT DeviceID,VolumeDirty FROM Win32_LogicalDisk WHERE DriveType=3");
            foreach (ManagementObject item in searcher.Get()) result[item["DeviceID"]?.ToString() ?? ""] = item["VolumeDirty"] as bool?;
        }
        catch { }
        return result;
    }

    private static async Task<string> QueryTrimAsync(CancellationToken token)
    {
        try
        {
            var start = new ProcessStartInfo("fsutil.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true }; start.ArgumentList.Add("behavior"); start.ArgumentList.Add("query"); start.ArgumentList.Add("DisableDeleteNotify");
            using var process = Process.Start(start); if (process is null) return "조회 실패"; var output = process.StandardOutput.ReadToEndAsync(token); await process.WaitForExitAsync(token); var text = await output;
            if (text.Contains("= 1", StringComparison.Ordinal)) return "비활성"; if (text.Contains("= 0", StringComparison.Ordinal)) return "활성"; return "조회 제한";
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return "조회 제한"; }
    }
}
