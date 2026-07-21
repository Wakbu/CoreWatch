using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32;

namespace SystemChecker.Services;

public sealed class ManagedStartupEntry : INotifyPropertyChanged
{
    private bool _isSelected;

    internal ManagedStartupEntry(string name, string publisher, string source, string command, string recommendation, string impact,
        bool isEnabled, bool canChange, string restriction, RegistryHive hive, RegistryView view, string approvalPath, string approvalValueName)
    {
        Name = name;
        Publisher = publisher;
        Source = source;
        Command = command;
        Recommendation = recommendation;
        Impact = impact;
        IsEnabled = isEnabled;
        CanChange = canChange;
        Restriction = restriction;
        Hive = hive;
        View = view;
        ApprovalPath = approvalPath;
        ApprovalValueName = approvalValueName;
    }

    public string Name { get; }
    public string Publisher { get; }
    public string Source { get; }
    public string Command { get; }
    public string Recommendation { get; }
    public string Impact { get; }
    public bool IsEnabled { get; }
    public string Status => IsEnabled ? "활성" : "비활성";
    public bool CanChange { get; }
    public string Restriction { get; }
    public bool IsSelected { get => _isSelected; set { if (_isSelected == value) return; _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); } }
    internal RegistryHive Hive { get; }
    internal RegistryView View { get; }
    internal string ApprovalPath { get; }
    internal string ApprovalValueName { get; }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed record StartupChangeResult(int ChangedCount, int EnabledCount, int DisabledCount, string BackupPath, string Message);

internal sealed record StartupApprovalBackupItem(RegistryHive Hive, RegistryView View, string ApprovalPath, string ValueName, bool HadValue, byte[]? PreviousValue);
internal sealed record StartupApprovalBackup(int FormatVersion, DateTimeOffset CapturedAt, IReadOnlyList<StartupApprovalBackupItem> Items);

public sealed class StartupManagementService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private const string StartupApprovedRoot = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

    public string BackupDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CoreWatch", "StartupBackups");
    public string LatestBackupPath => Path.Combine(BackupDirectory, "latest.json");
    public bool CanRestore => File.Exists(LatestBackupPath);

    public Task<IReadOnlyList<ManagedStartupEntry>> AnalyzeAsync(CancellationToken token = default) => Task.Run(() => Analyze(token), token);

    public Task<StartupChangeResult> ToggleAsync(IEnumerable<ManagedStartupEntry> entries, CancellationToken token = default) => Task.Run(() =>
    {
        var selected = entries.Where(item => item.IsSelected && item.CanChange).ToList();
        if (selected.Count == 0) throw new InvalidOperationException("변경할 수 있는 시작 프로그램을 선택하세요.");
        token.ThrowIfCancellationRequested();
        var backupItems = selected.Select(CreateBackupItem).ToList();
        var backupPath = SaveBackup(backupItems);
        var applied = new List<StartupApprovalBackupItem>();
        try
        {
            for (var index = 0; index < selected.Count; index++)
            {
                token.ThrowIfCancellationRequested();
                var entry = selected[index];
                WriteApproval(entry.Hive, entry.View, entry.ApprovalPath, entry.ApprovalValueName, CreateApprovalValue(!entry.IsEnabled));
                applied.Add(backupItems[index]);
            }
        }
        catch
        {
            foreach (var item in applied.AsEnumerable().Reverse()) TryRestore(item);
            throw;
        }
        var enabled = selected.Count(item => !item.IsEnabled);
        var disabled = selected.Count - enabled;
        return new StartupChangeResult(selected.Count, enabled, disabled, backupPath, $"시작 프로그램 {selected.Count}개의 상태를 변경했습니다. 활성화 {enabled}개 · 비활성화 {disabled}개");
    }, token);

    public async Task<StartupChangeResult> RestoreLatestAsync(CancellationToken token = default)
    {
        if (!File.Exists(LatestBackupPath)) throw new InvalidOperationException("복원할 시작 프로그램 변경 백업이 없습니다.");
        var json = await File.ReadAllTextAsync(LatestBackupPath, token);
        var backup = JsonSerializer.Deserialize<StartupApprovalBackup>(json, JsonOptions)
            ?? throw new InvalidOperationException("시작 프로그램 백업을 읽을 수 없습니다.");
        if (backup.FormatVersion != 1 || backup.Items.Count == 0) throw new InvalidOperationException("지원하지 않는 시작 프로그램 백업입니다.");
        foreach (var item in backup.Items)
        {
            token.ThrowIfCancellationRequested();
            Restore(item);
        }
        return new StartupChangeResult(backup.Items.Count, 0, 0, LatestBackupPath, $"마지막 변경 {backup.Items.Count}개를 이전 상태로 복원했습니다.");
    }

    public static bool IsApprovalEnabled(byte[]? value) => value is null || value.Length == 0 || value[0] != 3;

    public static byte[] CreateApprovalValue(bool enabled)
    {
        var value = new byte[12];
        BitConverter.GetBytes(enabled ? 2 : 3).CopyTo(value, 0);
        if (!enabled) BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(value, 4);
        return value;
    }

    private static IReadOnlyList<ManagedStartupEntry> Analyze(CancellationToken token)
    {
        var entries = new List<ManagedStartupEntry>();
        ReadRunKey(entries, RegistryHive.CurrentUser, RegistryView.Default, @"Software\Microsoft\Windows\CurrentVersion\Run", "현재 사용자 레지스트리", token);
        ReadRunKey(entries, RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\Run", "전체 사용자 레지스트리", token);
        ReadRunKey(entries, RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Microsoft\Windows\CurrentVersion\Run", "전체 사용자 레지스트리 (32비트)", token);
        ReadStartupFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.Startup), RegistryHive.CurrentUser, RegistryView.Default, "현재 사용자 시작 폴더", token);
        ReadStartupFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), RegistryHive.LocalMachine, RegistryView.Registry64, "전체 사용자 시작 폴더", token);
        return entries.GroupBy(item => $"{item.Source}\0{item.Name}\0{item.Command}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First()).OrderBy(item => item.IsEnabled).ThenBy(item => item.Recommendation).ThenBy(item => item.Name).ToList();
    }

    private static void ReadRunKey(List<ManagedStartupEntry> entries, RegistryHive hive, RegistryView view, string keyPath, string source, CancellationToken token)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(keyPath, false);
            if (key is null) return;
            var approvalPath = $@"{StartupApprovedRoot}\Run";
            foreach (var name in key.GetValueNames())
            {
                token.ThrowIfCancellationRequested();
                var command = key.GetValue(name)?.ToString() ?? string.Empty;
                entries.Add(CreateEntry(name, command, source, hive, view, approvalPath, name));
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (System.Security.SecurityException) { }
    }

    private static void ReadStartupFolder(List<ManagedStartupEntry> entries, string folder, RegistryHive hive, RegistryView view, string source, CancellationToken token)
    {
        try
        {
            if (!Directory.Exists(folder)) return;
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                token.ThrowIfCancellationRequested();
                entries.Add(CreateEntry(Path.GetFileNameWithoutExtension(file), file, source, hive, view, $@"{StartupApprovedRoot}\StartupFolder", Path.GetFileName(file)));
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }

    private static ManagedStartupEntry CreateEntry(string name, string command, string source, RegistryHive hive, RegistryView view, string approvalPath, string approvalValueName)
    {
        var executable = ExtractExecutablePath(command);
        var publisher = "확인 불가";
        try { if (File.Exists(executable)) publisher = FileVersionInfo.GetVersionInfo(executable).CompanyName ?? "확인 불가"; } catch { }
        var enabled = IsApprovalEnabled(ReadApproval(hive, view, approvalPath, approvalValueName));
        var protectedEntry = IsProtectedEntry(name, executable);
        var requiresAdministrator = hive == RegistryHive.LocalMachine && !IsAdministrator();
        var canChange = !protectedEntry && !requiresAdministrator;
        var restriction = protectedEntry ? "Windows·보안 구성요소는 변경할 수 없습니다." : requiresAdministrator ? "전체 사용자 항목은 관리자 권한이 필요합니다." : "변경 가능";
        var recommendation = protectedEntry ? "유지 권장" : !enabled ? "현재 비활성화" : "사용하지 않으면 비활성화 검토";
        var impact = protectedEntry ? "시스템·보안 연관" : enabled ? "로그인 후 자동 실행" : "자동 실행 중지됨";
        return new ManagedStartupEntry(name, publisher, source, command, recommendation, impact, enabled, canChange, restriction, hive, view, approvalPath, approvalValueName);
    }

    private StartupApprovalBackupItem CreateBackupItem(ManagedStartupEntry entry)
    {
        var previous = ReadApproval(entry.Hive, entry.View, entry.ApprovalPath, entry.ApprovalValueName);
        return new StartupApprovalBackupItem(entry.Hive, entry.View, entry.ApprovalPath, entry.ApprovalValueName, previous is not null, previous);
    }

    private string SaveBackup(IReadOnlyList<StartupApprovalBackupItem> items)
    {
        Directory.CreateDirectory(BackupDirectory);
        var backup = new StartupApprovalBackup(1, DateTimeOffset.Now, items);
        var json = JsonSerializer.Serialize(backup, JsonOptions);
        var archive = Path.Combine(BackupDirectory, $"startup-{DateTime.Now:yyyyMMdd-HHmmss-fff}.json");
        var temporary = LatestBackupPath + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, LatestBackupPath, true);
        File.WriteAllText(archive, json);
        return archive;
    }

    private static byte[]? ReadApproval(RegistryHive hive, RegistryView view, string path, string valueName)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(path, false);
            return key?.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as byte[];
        }
        catch { return null; }
    }

    private static void WriteApproval(RegistryHive hive, RegistryView view, string path, string valueName, byte[] value)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
        using var key = baseKey.CreateSubKey(path, true) ?? throw new InvalidOperationException("StartupApproved 레지스트리를 열 수 없습니다.");
        key.SetValue(valueName, value, RegistryValueKind.Binary);
    }

    private static void Restore(StartupApprovalBackupItem item)
    {
        using var baseKey = RegistryKey.OpenBaseKey(item.Hive, item.View);
        using var key = baseKey.CreateSubKey(item.ApprovalPath, true) ?? throw new InvalidOperationException("시작 프로그램 백업을 복원할 수 없습니다.");
        if (item.HadValue && item.PreviousValue is not null) key.SetValue(item.ValueName, item.PreviousValue, RegistryValueKind.Binary);
        else key.DeleteValue(item.ValueName, false);
    }

    private static void TryRestore(StartupApprovalBackupItem item) { try { Restore(item); } catch { } }

    private static bool IsProtectedEntry(string name, string executable)
    {
        if (name.Contains("Security", StringComparison.OrdinalIgnoreCase) || name.Contains("Defender", StringComparison.OrdinalIgnoreCase) || name.Contains("WindowsHealth", StringComparison.OrdinalIgnoreCase)) return true;
        var system32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32") + Path.DirectorySeparatorChar;
        try { return Path.GetFullPath(executable).StartsWith(system32, StringComparison.OrdinalIgnoreCase); } catch { return false; }
    }

    private static bool IsAdministrator()
    {
        try { using var identity = WindowsIdentity.GetCurrent(); return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator); }
        catch { return false; }
    }

    private static string ExtractExecutablePath(string command)
    {
        var expanded = Environment.ExpandEnvironmentVariables(command).Trim();
        if (expanded.StartsWith('"')) { var end = expanded.IndexOf('"', 1); return end > 1 ? expanded[1..end] : expanded.Trim('"'); }
        var exe = expanded.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exe >= 0 ? expanded[..(exe + 4)] : expanded;
    }
}
