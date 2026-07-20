using System.Diagnostics;
using System.Text;
using System.Text.Json;
using SystemChecker.Models;

namespace SystemChecker.Services;

public sealed class HardwareInventoryService
{
    public async Task<IReadOnlyList<HardwareItem>> CollectAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<HardwareItem>
        {
            new("운영체제", "Windows", Environment.OSVersion.VersionString),
            new("운영체제", ".NET", Environment.Version.ToString()),
            new("CPU", "논리 프로세서", $"{Environment.ProcessorCount}개"),
            new("시스템", "컴퓨터 이름", Environment.MachineName)
        };
        try
        {
            using var document = await QueryCimAsync(cancellationToken);
            AddCimItems(document.RootElement, items);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            items.Add(new("수집 상태", "상세 정보", $"일부 정보를 가져오지 못했습니다: {ex.Message}"));
        }
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            items.Add(new("볼륨", drive.Name, $"{FormatBytes((ulong)drive.TotalSize)} / 여유 {FormatBytes((ulong)drive.AvailableFreeSpace)} ({drive.DriveFormat})"));
        return items;
    }

    private static async Task<JsonDocument> QueryCimAsync(CancellationToken cancellationToken)
    {
        const string script = "[Console]::OutputEncoding=[Text.Encoding]::UTF8; $ErrorActionPreference='Stop'; " +
            "[pscustomobject]@{" +
            "Cpu=@(Get-CimInstance Win32_Processor|Select-Object Name,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed);" +
            "Gpu=@(Get-CimInstance Win32_VideoController|Select-Object Name,DriverVersion);" +
            "Board=@(Get-CimInstance Win32_BaseBoard|Select-Object Manufacturer,Product);" +
            "Bios=@(Get-CimInstance Win32_BIOS|Select-Object SMBIOSBIOSVersion,Manufacturer);" +
            "Memory=@(Get-CimInstance Win32_PhysicalMemory|Select-Object Manufacturer,Capacity,Speed,PartNumber);" +
            "Disk=@(Get-CimInstance Win32_DiskDrive|Select-Object Model,Size,InterfaceType)" +
            "}|ConvertTo-Json -Depth 4 -Compress";
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe", UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-Command", script }) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("PowerShell을 실행할 수 없습니다.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "CIM 조회 실패" : error.Trim());
        return JsonDocument.Parse(output);
    }

    private static void AddCimItems(JsonElement root, ICollection<HardwareItem> items)
    {
        foreach (var cpu in Enumerate(root, "Cpu")) items.Add(new("CPU", Text(cpu, "Name"), $"{Text(cpu, "NumberOfCores")}코어 / {Text(cpu, "NumberOfLogicalProcessors")}스레드 / 최대 {Text(cpu, "MaxClockSpeed")} MHz"));
        foreach (var gpu in Enumerate(root, "Gpu")) items.Add(new("GPU", Text(gpu, "Name"), $"드라이버 {Text(gpu, "DriverVersion")}"));
        foreach (var board in Enumerate(root, "Board")) items.Add(new("메인보드", Text(board, "Product"), Text(board, "Manufacturer")));
        foreach (var bios in Enumerate(root, "Bios")) items.Add(new("BIOS", Text(bios, "SMBIOSBIOSVersion"), Text(bios, "Manufacturer")));
        foreach (var memory in Enumerate(root, "Memory")) items.Add(new("메모리", Text(memory, "PartNumber"), $"{FormatBytes(ULong(memory, "Capacity"))} / {Text(memory, "Speed")} MT/s / {Text(memory, "Manufacturer")}"));
        foreach (var disk in Enumerate(root, "Disk")) items.Add(new("저장장치", Text(disk, "Model"), $"{FormatBytes(ULong(disk, "Size"))} / {Text(disk, "InterfaceType")}"));
    }

    private static IEnumerable<JsonElement> Enumerate(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value)) yield break;
        if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) yield return item;
        else if (value.ValueKind == JsonValueKind.Object) yield return value;
    }

    private static string Text(JsonElement element, string property) => element.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString().Trim() : "알 수 없음";
    private static ulong ULong(JsonElement element, string property) => element.TryGetProperty(property, out var value) && value.TryGetUInt64(out var number) ? number : 0;

    public static string FormatBytes(ulong bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB", "PB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.##} {units[unit]}";
    }
}
