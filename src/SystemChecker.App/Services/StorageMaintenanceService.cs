using Microsoft.Win32;
using System.Diagnostics;

namespace SystemChecker.Services;

internal sealed record StorageMaintenanceSnapshot(string StorageSense, string ReservedStorage, string SystemDrive, string Recommendation);

internal sealed class StorageMaintenanceService
{
    public async Task<StorageMaintenanceSnapshot> CaptureAsync(CancellationToken token = default)
    {
        var sense = ReadStorageSense();
        var reserved = await ReadReservedStorageAsync(token);
        var root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        var drive = new DriveInfo(root);
        var percent = drive.TotalSize > 0 ? drive.AvailableFreeSpace * 100d / drive.TotalSize : 0;
        var recommendation = percent < 10 ? "시스템 드라이브 여유 공간이 부족합니다. 저장소 센스 설정을 확인하세요." : sense == "꺼짐" ? "자동 임시 파일 정리가 필요하면 저장소 센스를 켜세요." : "현재 저장소 유지 관리 상태가 양호합니다.";
        return new StorageMaintenanceSnapshot(sense, reserved, $"{OptimizationService.FormatBytes(drive.AvailableFreeSpace)} 여유 · {percent:0.#}%", recommendation);
    }

    internal static string ReadStorageSense()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy", false);
            return Convert.ToInt32(key?.GetValue("01", 0)) == 1 ? "켜짐" : "꺼짐";
        }
        catch { return "확인 불가"; }
    }

    internal static async Task<string> ReadReservedStorageAsync(CancellationToken token)
    {
        try
        {
            var start = new ProcessStartInfo("dism.exe", "/Online /Get-ReservedStorageState /English") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using var process = Process.Start(start) ?? throw new InvalidOperationException();
            var outputTask = process.StandardOutput.ReadToEndAsync(token);
            await process.WaitForExitAsync(token);
            var output = await outputTask;
            if (output.Contains("Enabled", StringComparison.OrdinalIgnoreCase)) return "사용 중";
            if (output.Contains("Disabled", StringComparison.OrdinalIgnoreCase)) return "사용 안 함";
            return process.ExitCode == 0 ? "상태 확인 필요" : "권한 또는 Windows 지원 확인 필요";
        }
        catch (OperationCanceledException) { throw; }
        catch { return "확인 불가"; }
    }
}
