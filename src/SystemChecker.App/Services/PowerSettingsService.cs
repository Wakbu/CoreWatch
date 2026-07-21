using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace SystemChecker.Services;

public sealed record PowerProfileOption(string Key, string Name, Guid SchemeId, string Description, bool IsAvailable, bool IsActive, bool IsRecommended);

public sealed record PowerProfileAnalysis(
    DateTimeOffset CapturedAt,
    Guid ActiveSchemeId,
    string ActiveSchemeName,
    bool BatteryPresent,
    bool OnAcPower,
    int? BatteryPercent,
    uint CpuMinimumAc,
    uint CpuMaximumAc,
    uint CpuMinimumDc,
    uint CpuMaximumDc,
    uint BoostModeAc,
    uint BoostModeDc,
    string RecommendedProfileKey,
    string Recommendation,
    string RecommendationReason,
    IReadOnlyList<PowerProfileOption> Profiles)
{
    public string PowerSourceText => BatteryPresent
        ? OnAcPower ? $"전원 연결 · 배터리 {BatteryPercent?.ToString() ?? "-"}%" : $"배터리 사용 · {BatteryPercent?.ToString() ?? "-"}%"
        : "데스크톱 전원";

    public string CpuLimitsText => $"AC {CpuMinimumAc}–{CpuMaximumAc}% · 배터리 {CpuMinimumDc}–{CpuMaximumDc}%";
    public string BoostText => $"AC {PowerSettingsService.FormatBoostMode(BoostModeAc)} · 배터리 {PowerSettingsService.FormatBoostMode(BoostModeDc)}";
}

public sealed record PowerChangeResult(PowerProfileAnalysis Analysis, string BackupPath, string Message);

internal sealed record PowerSettingsBackup(int FormatVersion, DateTimeOffset CapturedAt, Guid SchemeId, string SchemeName);

public sealed class PowerSettingsService
{
    public static readonly Guid BalancedScheme = new("381b4222-f694-41f0-9685-ff5bb260df2e");
    public static readonly Guid HighPerformanceScheme = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    public static readonly Guid PowerSaverScheme = new("a1841308-3541-4fab-bc81-f71556f20b4a");

    private static readonly Guid ProcessorSubgroup = new("54533251-82be-4824-96c1-47b60b740d00");
    private static readonly Guid MinimumProcessorState = new("893dee8e-2bef-41e0-89c6-b55d0929964c");
    private static readonly Guid MaximumProcessorState = new("bc5038f7-23e0-4960-96da-33abaf5935ec");
    private static readonly Guid ProcessorBoostMode = new("be337238-0d82-4146-a960-4f3749d470c7");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string BackupDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CoreWatch", "PowerBackups");
    public string LatestBackupPath => Path.Combine(BackupDirectory, "latest.json");
    public bool CanRestore => File.Exists(LatestBackupPath);

    public Task<PowerProfileAnalysis> AnalyzeAsync(CancellationToken token = default) => Task.Run(() => Analyze(token), token);

    public async Task<PowerChangeResult> ApplyProfileAsync(string profileKey, CancellationToken token = default)
    {
        var before = await AnalyzeAsync(token);
        var profile = before.Profiles.FirstOrDefault(item => string.Equals(item.Key, profileKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("알 수 없는 전원 프로필입니다.");
        if (!profile.IsAvailable) throw new InvalidOperationException($"이 PC에는 '{profile.Name}' 전원 계획이 없습니다.");
        if (profile.IsActive) return new PowerChangeResult(before, string.Empty, $"이미 '{profile.Name}' 전원 계획을 사용 중입니다.");

        var backupPath = SaveBackup(before);
        token.ThrowIfCancellationRequested();
        SetActiveScheme(profile.SchemeId);
        var after = await AnalyzeAsync(token);
        return new PowerChangeResult(after, backupPath, $"'{profile.Name}' 전원 계획을 적용했습니다.");
    }

    public async Task<PowerChangeResult> RestoreLatestAsync(CancellationToken token = default)
    {
        if (!File.Exists(LatestBackupPath)) throw new InvalidOperationException("복원할 전원 설정 백업이 없습니다.");
        var json = await File.ReadAllTextAsync(LatestBackupPath, token);
        var backup = JsonSerializer.Deserialize<PowerSettingsBackup>(json, JsonOptions)
            ?? throw new InvalidOperationException("전원 설정 백업을 읽을 수 없습니다.");
        if (backup.FormatVersion != 1 || backup.SchemeId == Guid.Empty) throw new InvalidOperationException("지원하지 않는 전원 설정 백업입니다.");

        var available = GetAvailableSchemes();
        if (!available.Contains(backup.SchemeId)) throw new InvalidOperationException("백업된 전원 계획이 현재 Windows에 없습니다.");
        SetActiveScheme(backup.SchemeId);
        var after = await AnalyzeAsync(token);
        return new PowerChangeResult(after, LatestBackupPath, $"'{backup.SchemeName}' 전원 계획으로 복원했습니다.");
    }

    public static string SelectRecommendation(bool batteryPresent, bool onAcPower, uint maximumAc, uint boostModeAc)
    {
        if (batteryPresent && !onAcPower) return "saver";
        if (maximumAc < 90 || boostModeAc == 0) return "balanced";
        return "balanced";
    }

    public static string FormatBoostMode(uint value) => value switch
    {
        0 => "사용 안 함",
        1 => "사용",
        2 => "적극적",
        3 => "효율 우선",
        4 => "효율적 적극",
        5 => "보장 클록 이상",
        6 => "효율적 보장 클록",
        _ => $"모드 {value}"
    };

    private PowerProfileAnalysis Analyze(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var activeScheme = GetActiveScheme();
        var available = GetAvailableSchemes();
        available.Add(activeScheme);
        var powerStatus = GetPowerStatus();
        var minAc = ReadValue(activeScheme, MinimumProcessorState, true, 0);
        var maxAc = ReadValue(activeScheme, MaximumProcessorState, true, 100);
        var minDc = ReadValue(activeScheme, MinimumProcessorState, false, minAc);
        var maxDc = ReadValue(activeScheme, MaximumProcessorState, false, maxAc);
        var boostAc = ReadValue(activeScheme, ProcessorBoostMode, true, 0);
        var boostDc = ReadValue(activeScheme, ProcessorBoostMode, false, boostAc);
        var recommendedKey = SelectRecommendation(powerStatus.BatteryPresent, powerStatus.OnAcPower, maxAc, boostAc);
        var recommendation = recommendedKey == "saver" ? "배터리 사용 중에는 절전 프로필을 권장합니다." : "일반적인 사용에는 균형 조정 프로필을 권장합니다.";
        var reason = BuildRecommendationReason(powerStatus, maxAc, boostAc);

        var profiles = new[]
        {
            CreateProfile("balanced", "균형 조정", BalancedScheme, "성능과 전력 사용량을 자동으로 조절합니다.", available, activeScheme, recommendedKey),
            CreateProfile("performance", "고성능", HighPerformanceScheme, "전원 연결 상태의 장시간 작업과 벤치마크에 적합합니다.", available, activeScheme, recommendedKey),
            CreateProfile("saver", "절전", PowerSaverScheme, "배터리 지속 시간과 낮은 발열을 우선합니다.", available, activeScheme, recommendedKey)
        };

        return new PowerProfileAnalysis(DateTimeOffset.Now, activeScheme, GetSchemeName(activeScheme), powerStatus.BatteryPresent, powerStatus.OnAcPower,
            powerStatus.BatteryPercent, minAc, maxAc, minDc, maxDc, boostAc, boostDc, recommendedKey, recommendation, reason, profiles);
    }

    private string SaveBackup(PowerProfileAnalysis analysis)
    {
        Directory.CreateDirectory(BackupDirectory);
        var backup = new PowerSettingsBackup(1, DateTimeOffset.Now, analysis.ActiveSchemeId, analysis.ActiveSchemeName);
        var json = JsonSerializer.Serialize(backup, JsonOptions);
        var archivePath = Path.Combine(BackupDirectory, $"power-{DateTime.Now:yyyyMMdd-HHmmss-fff}.json");
        var tempPath = LatestBackupPath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, LatestBackupPath, true);
        File.WriteAllText(archivePath, json);
        return archivePath;
    }

    private static PowerProfileOption CreateProfile(string key, string name, Guid id, string description, HashSet<Guid> available, Guid active, string recommended) =>
        new(key, name, id, description, available.Contains(id), active == id, string.Equals(key, recommended, StringComparison.Ordinal));

    private static string BuildRecommendationReason(PowerStatus status, uint maximumAc, uint boostModeAc)
    {
        if (status.BatteryPresent && !status.OnAcPower) return "현재 배터리로 동작 중이므로 발열과 소비 전력을 낮추는 편이 유리합니다.";
        if (maximumAc < 90) return $"현재 AC CPU 최대 상태가 {maximumAc}%로 제한되어 있습니다. 균형 조정 적용 후 설정을 다시 확인하세요.";
        if (boostModeAc == 0) return "현재 AC CPU 부스트가 비활성화되어 있습니다. 저소음 목적이 아니라면 균형 조정을 권장합니다.";
        return "CPU 최대 상태와 부스트가 정상 범위이며, 균형 조정이 대부분의 작업에서 가장 안정적입니다.";
    }

    private static HashSet<Guid> GetAvailableSchemes()
    {
        var schemes = new HashSet<Guid>();
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = "/list",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            if (process is null) return schemes;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(output, "[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}"))
                if (Guid.TryParse(match.Value, out var id)) schemes.Add(id);
        }
        catch { }
        return schemes;
    }

    private static Guid GetActiveScheme()
    {
        var result = PowerGetActiveScheme(IntPtr.Zero, out var pointer);
        if (result != 0 || pointer == IntPtr.Zero) throw new InvalidOperationException($"활성 전원 계획을 읽을 수 없습니다. 오류 코드: {result}");
        try { return Marshal.PtrToStructure<Guid>(pointer); }
        finally { _ = LocalFree(pointer); }
    }

    private static uint ReadValue(Guid scheme, Guid setting, bool ac, uint fallback)
    {
        var subgroup = ProcessorSubgroup;
        var result = ac
            ? PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref subgroup, ref setting, out var value)
            : PowerReadDCValueIndex(IntPtr.Zero, ref scheme, ref subgroup, ref setting, out value);
        return result == 0 ? value : fallback;
    }

    private static void SetActiveScheme(Guid scheme)
    {
        var result = PowerSetActiveScheme(IntPtr.Zero, ref scheme);
        if (result != 0) throw new InvalidOperationException($"전원 계획을 적용하지 못했습니다. Windows 오류 코드: {result}");
    }

    private static string GetSchemeName(Guid id)
    {
        if (id == BalancedScheme) return "균형 조정";
        if (id == HighPerformanceScheme) return "고성능";
        if (id == PowerSaverScheme) return "절전";
        return "사용자 지정";
    }

    private static PowerStatus GetPowerStatus()
    {
        if (!GetSystemPowerStatus(out var status)) return new PowerStatus(false, true, null);
        var batteryPresent = status.BatteryFlag != 128 && status.BatteryFlag != 255;
        var batteryPercent = batteryPresent && status.BatteryLifePercent <= 100 ? status.BatteryLifePercent : (int?)null;
        return new PowerStatus(batteryPresent, status.ACLineStatus != 0, batteryPercent);
    }

    private sealed record PowerStatus(bool BatteryPresent, bool OnAcPower, int? BatteryPercent);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadACValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subgroupGuid, ref Guid settingGuid, out uint valueIndex);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadDCValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subgroupGuid, ref Guid settingGuid, out uint valueIndex);

    [DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveScheme(IntPtr userRootPowerKey, ref Guid schemeGuid);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus systemPowerStatus);
}
