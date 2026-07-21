using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Win32;
using SystemChecker.Models;

namespace SystemChecker.Services;

public sealed record StartupEntry(string Name, string Publisher, string Source, string Command, string Recommendation, string Impact);

public sealed class CleanupGroup : INotifyPropertyChanged
{
    private bool _isSelected;

    public CleanupGroup(string category, string path, long sizeBytes, IReadOnlyList<string> files, string safetyNote)
    {
        Category = category;
        Path = path;
        SizeBytes = sizeBytes;
        Files = files;
        SafetyNote = safetyNote;
    }

    public string Category { get; }
    public string Path { get; }
    public long SizeBytes { get; }
    public string SizeText => OptimizationService.FormatBytes(SizeBytes);
    public int FileCount => Files.Count;
    public string SafetyNote { get; }
    public IReadOnlyList<string> Files { get; }
    public bool IsSelected { get => _isSelected; set { if (_isSelected == value) return; _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed record OptimizationSnapshot(DateTimeOffset CapturedAt, double CpuPercent, double MemoryPercent, ulong UsedMemoryBytes, int ProcessCount, TimeSpan Uptime, double LowestDiskFreePercent)
{
    public string Summary => $"CPU {CpuPercent:0.0}% · 메모리 {MemoryPercent:0.0}% ({OptimizationService.FormatBytes((long)UsedMemoryBytes)}) · 프로세스 {ProcessCount:N0}개 · 최소 여유 공간 {LowestDiskFreePercent:0.0}%";
}

public sealed record CleanupResult(long ReclaimedBytes, int DeletedFiles, int FailedFiles);

public sealed class OptimizationService
{
    private const int MaximumFilesPerGroup = 50_000;
    private static readonly TimeSpan MinimumFileAge = TimeSpan.FromHours(24);

    public Task<IReadOnlyList<StartupEntry>> AnalyzeStartupAsync(CancellationToken token = default) => Task.Run(() => AnalyzeStartup(token), token);
    public Task<IReadOnlyList<CleanupGroup>> ScanCleanupAsync(CancellationToken token = default) => Task.Run(() => ScanCleanup(token), token);

    public async Task<OptimizationSnapshot> CaptureSnapshotAsync(CancellationToken token = default)
    {
        using var metrics = new SystemMetricsService();
        _ = metrics.Capture();
        await Task.Delay(650, token);
        var snapshot = metrics.Capture();
        return new OptimizationSnapshot(DateTimeOffset.Now, snapshot.CpuUsagePercent, snapshot.MemoryUsagePercent, snapshot.UsedMemoryBytes, Process.GetProcesses().Length, TimeSpan.FromMilliseconds(Environment.TickCount64), snapshot.LowestDiskFreePercent);
    }

    public Task<CleanupResult> CleanAsync(IEnumerable<CleanupGroup> groups, CancellationToken token = default) => Task.Run(() =>
    {
        long reclaimed = 0;
        var deleted = 0;
        var failed = 0;
        var approvedRoots = CleanupRoots().Select(item => NormalizeRoot(item.Path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var group in groups.Where(item => item.IsSelected))
        {
            foreach (var file in group.Files)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    var fullPath = Path.GetFullPath(file);
                    if (!approvedRoots.Any(root => fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))) { failed++; continue; }
                    var info = new FileInfo(fullPath);
                    if (!info.Exists || DateTime.UtcNow - info.LastWriteTimeUtc < MinimumFileAge) continue;
                    var length = info.Length;
                    info.Delete();
                    reclaimed += length;
                    deleted++;
                }
                catch { failed++; }
            }
        }
        return new CleanupResult(reclaimed, deleted, failed);
    }, token);

    private static IReadOnlyList<StartupEntry> AnalyzeStartup(CancellationToken token)
    {
        var entries = new List<StartupEntry>();
        ReadRunKey(entries, RegistryHive.CurrentUser, RegistryView.Default, @"Software\Microsoft\Windows\CurrentVersion\Run", "현재 사용자 레지스트리", token);
        ReadRunKey(entries, RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\Run", "전체 사용자 레지스트리", token);
        ReadRunKey(entries, RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Microsoft\Windows\CurrentVersion\Run", "전체 사용자 레지스트리 (32비트)", token);
        ReadStartupFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.Startup), "현재 사용자 시작 폴더", token);
        ReadStartupFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "전체 사용자 시작 폴더", token);
        return entries.GroupBy(item => $"{item.Source}\0{item.Name}\0{item.Command}", StringComparer.OrdinalIgnoreCase).Select(group => group.First()).OrderBy(item => item.Recommendation).ThenBy(item => item.Name).ToList();
    }

    private static void ReadRunKey(List<StartupEntry> entries, RegistryHive hive, RegistryView view, string keyPath, string source, CancellationToken token)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(keyPath, false);
            if (key is null) return;
            foreach (var name in key.GetValueNames())
            {
                token.ThrowIfCancellationRequested();
                var command = key.GetValue(name)?.ToString() ?? string.Empty;
                entries.Add(CreateStartupEntry(name, command, source));
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (System.Security.SecurityException) { }
    }

    private static void ReadStartupFolder(List<StartupEntry> entries, string folder, string source, CancellationToken token)
    {
        try
        {
            if (!Directory.Exists(folder)) return;
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                token.ThrowIfCancellationRequested();
                entries.Add(CreateStartupEntry(Path.GetFileNameWithoutExtension(file), file, source));
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }

    private static StartupEntry CreateStartupEntry(string name, string command, string source)
    {
        var executable = ExtractExecutablePath(command);
        var publisher = "확인 불가";
        try
        {
            if (File.Exists(executable)) publisher = FileVersionInfo.GetVersionInfo(executable).CompanyName ?? "확인 불가";
        }
        catch { }
        var trusted = publisher.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) || name.Contains("Security", StringComparison.OrdinalIgnoreCase) || name.Contains("Windows", StringComparison.OrdinalIgnoreCase);
        var recommendation = trusted ? "유지 권장" : "사용하지 않으면 비활성화 검토";
        var impact = trusted ? "시스템/보안 연관 가능" : "부팅 후 백그라운드 실행";
        return new StartupEntry(name, publisher, source, command, recommendation, impact);
    }

    private static string ExtractExecutablePath(string command)
    {
        var expanded = Environment.ExpandEnvironmentVariables(command).Trim();
        if (expanded.StartsWith('"'))
        {
            var end = expanded.IndexOf('"', 1);
            return end > 1 ? expanded[1..end] : expanded.Trim('"');
        }
        var exe = expanded.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exe >= 0 ? expanded[..(exe + 4)] : expanded;
    }

    private static IReadOnlyList<CleanupGroup> ScanCleanup(CancellationToken token)
    {
        var result = new List<CleanupGroup>();
        foreach (var root in CleanupRoots().DistinctBy(item => NormalizeRoot(item.Path), StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            if (!Directory.Exists(root.Path)) continue;
            var files = EnumerateOldFiles(root.Path, token);
            long size = 0;
            foreach (var file in files) { try { size += new FileInfo(file).Length; } catch { } }
            result.Add(new CleanupGroup(root.Category, root.Path, size, files, root.Note));
        }
        return result.OrderByDescending(item => item.SizeBytes).ToList();
    }

    private static List<string> EnumerateOldFiles(string root, CancellationToken token)
    {
        var files = new List<string>();
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0 && files.Count < MaximumFilesPerGroup)
        {
            token.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            try
            {
                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    if (files.Count >= MaximumFilesPerGroup) break;
                    try { if (DateTime.UtcNow - File.GetLastWriteTimeUtc(file) >= MinimumFileAge) files.Add(file); } catch { }
                }
                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    try
                    {
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) pending.Push(child);
                    }
                    catch { }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }
        return files;
    }

    private static IEnumerable<(string Category, string Path, string Note)> CleanupRoots()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return ("사용자 임시 파일", Path.GetTempPath(), "24시간 이상 사용되지 않은 임시 파일만 선택합니다.");
        yield return ("충돌 덤프", Path.Combine(local, "CrashDumps"), "프로그램 오류 분석용 덤프입니다. 문제 분석 중이면 유지하세요.");
        yield return ("Windows 웹 캐시", Path.Combine(local, "Microsoft", "Windows", "INetCache"), "로그인 정보가 아닌 재생성 가능한 캐시 파일을 대상으로 합니다.");
        yield return ("Windows 임시 파일", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"), "접근 가능한 오래된 파일만 삭제하며 사용 중인 파일은 건너뜁니다.");
    }

    private static string NormalizeRoot(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = Math.Max(0, bytes);
        var index = 0;
        double display = value;
        while (display >= 1024 && index < units.Length - 1) { display /= 1024; index++; }
        return $"{display:0.##} {units[index]}";
    }
}


