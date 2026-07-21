using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SystemChecker.Services;

public sealed class ProcessItem : INotifyPropertyChanged
{
    private double _cpuPercent;
    private double _memoryMb;

    public ProcessItem(int id, string name, double cpuPercent, double memoryMb, string kind, string classification, string detail, bool canTerminate, ImageSource? icon)
    {
        Id = id;
        Name = name;
        _cpuPercent = cpuPercent;
        _memoryMb = memoryMb;
        Kind = kind;
        Classification = classification;
        Detail = detail;
        CanTerminate = canTerminate;
        Icon = icon;
    }

    public int Id { get; }
    public string Name { get; }
    public double CpuPercent { get => _cpuPercent; private set => SetField(ref _cpuPercent, value); }
    public double MemoryMb { get => _memoryMb; private set => SetField(ref _memoryMb, value); }
    public string Kind { get; }
    public string Classification { get; }
    public string Detail { get; }
    public bool CanTerminate { get; }
    public ImageSource? Icon { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void UpdateUsage(ProcessItem latest)
    {
        if (latest.Id != Id) throw new ArgumentException("PID가 다른 프로세스로 갱신할 수 없습니다.", nameof(latest));
        CpuPercent = latest.CpuPercent;
        MemoryMb = latest.MemoryMb;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class ProcessMonitorService
{
    private readonly Dictionary<int, (TimeSpan Cpu, DateTimeOffset At)> _previous = [];
    private readonly Dictionary<string, ImageSource?> _iconCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Critical = new(StringComparer.OrdinalIgnoreCase) { "system", "registry", "smss", "csrss", "wininit", "winlogon", "services", "lsass", "svchost", "fontdrvhost", "sihost", "dwm" };

    public Task<IReadOnlyList<ProcessItem>> CaptureAsync(CancellationToken token = default) => Task.Run(() => Capture(token), token);

    private IReadOnlyList<ProcessItem> Capture(CancellationToken token)
    {
        var now = DateTimeOffset.Now;
        var current = new Dictionary<int, (TimeSpan, DateTimeOffset)>();
        var list = new List<ProcessItem>();
        foreach (var process in Process.GetProcesses())
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var cpu = process.TotalProcessorTime;
                current[process.Id] = (cpu, now);
                var percent = _previous.TryGetValue(process.Id, out var old)
                    ? Math.Clamp((cpu - old.Cpu).TotalMilliseconds / Math.Max(1, (now - old.At).TotalMilliseconds) / Environment.ProcessorCount * 100, 0, 100)
                    : 0;
                var path = TryPath(process);
                var classification = Classify(process.Id, process.ProcessName, path);
                var kind = classification is "보호된 시스템" or "Windows 구성요소" ? "Windows 프로세스" : process.MainWindowHandle != IntPtr.Zero ? "앱" : "백그라운드 프로세스";
                var canTerminate = classification == "일반 프로그램" && process.Id != Environment.ProcessId;
                list.Add(new ProcessItem(process.Id, process.ProcessName, percent, process.WorkingSet64 / 1048576d, kind, classification, string.IsNullOrWhiteSpace(path) ? "실행 경로 확인 불가" : path, canTerminate, GetIcon(path)));
            }
            catch { }
            finally { process.Dispose(); }
        }
        _previous.Clear();
        foreach (var pair in current) _previous[pair.Key] = pair.Value;
        return list.OrderBy(item => KindOrder(item.Kind)).ThenByDescending(item => item.CpuPercent).ThenByDescending(item => item.MemoryMb).ToList();
    }

    public async Task TerminateAsync(ProcessItem item, CancellationToken token = default)
    {
        if (!item.CanTerminate) throw new InvalidOperationException("CoreWatch는 보호된 시스템 구성요소나 경로를 확인할 수 없는 프로세스를 종료하지 않습니다.");
        using var process = Process.GetProcessById(item.Id);
        if (process.HasExited) return;
        if (process.CloseMainWindow())
        {
            try { await process.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(2), token); return; }
            catch (TimeoutException) { }
        }
        process.Kill(true);
        await process.WaitForExitAsync(token);
    }

    private static int KindOrder(string kind) => kind == "앱" ? 0 : kind == "백그라운드 프로세스" ? 1 : 2;
    private static string TryPath(Process process) { try { return process.MainModule?.FileName ?? string.Empty; } catch { return string.Empty; } }

    private ImageSource? GetIcon(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (_iconCache.TryGetValue(path, out var cached)) return cached;
        ImageSource? image = null;
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is not null)
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(24, 24));
                source.Freeze();
                image = source;
            }
        }
        catch { }
        _iconCache[path] = image;
        return image;
    }

    private static string Classify(int id, string name, string path)
    {
        if (id <= 4 || Critical.Contains(name)) return "보호된 시스템";
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!string.IsNullOrWhiteSpace(path) && path.StartsWith(windows, StringComparison.OrdinalIgnoreCase)) return "Windows 구성요소";
        if (string.IsNullOrWhiteSpace(path)) return "확인 필요";
        return "일반 프로그램";
    }
}
