using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SystemChecker.Services;

namespace SystemChecker;

public partial class TelemetryMainWindow
{
    private readonly BackgroundLoadMonitorService _backgroundLoadService = new();
    private readonly ObservableCollection<BackgroundLoadRow> _backgroundLoadRows = [];
    private readonly Dictionary<int, Button> _loadDurationButtons = [];
    private TextBlock? _backgroundLoadStatus;
    private ProgressBar? _backgroundLoadProgress;
    private Button? _backgroundLoadStartButton;
    private CancellationTokenSource? _backgroundLoadCancellation;
    private int _backgroundLoadDurationSeconds = 30;
    private bool _backgroundLoadRunning;

    private FrameworkElement CreateBackgroundLoadPanel()
    {
        var surface = Surface();
        surface.Margin = new Thickness(12, 0, 10, 12);
        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(82) });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(310) });

        var header = new Grid { Margin = new Thickness(20, 0, 20, 0) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(new TextBlock { Text = "장시간 백그라운드 부하", FontSize = 16, FontWeight = FontWeights.SemiBold });
        title.Children.Add(new TextBlock { Text = "순간 사용량이 아니라 일정 시간의 CPU·메모리·디스크와 활성 네트워크 연결을 표본화합니다.", Foreground = Muted(), FontSize = 10, Margin = new Thickness(0, 5, 0, 0) });
        header.Children.Add(title);

        var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        controls.Children.Add(new TextBlock { Text = "측정 시간", Foreground = Muted(), FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) });
        foreach (var seconds in new[] { 15, 30, 60 }) controls.Children.Add(CreateLoadDurationButton(seconds));
        _backgroundLoadStartButton = Action("측정 시작", BackgroundLoadStart_Click, "#24262B", "#FFFFFF");
        controls.Children.Add(_backgroundLoadStartButton);
        Grid.SetColumn(controls, 1);
        header.Children.Add(controls);
        body.Children.Add(header);

        var progressRow = new Grid { Margin = new Thickness(20, 0, 20, 10) };
        progressRow.ColumnDefinitions.Add(new ColumnDefinition());
        progressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        _backgroundLoadStatus = new TextBlock { Text = "측정을 시작하면 지속 부하가 큰 프로세스를 순위로 표시합니다.", Foreground = Muted(), FontSize = 10, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        progressRow.Children.Add(_backgroundLoadStatus);
        _backgroundLoadProgress = new ProgressBar { Minimum = 0, Maximum = 100, Height = 5, Value = 0, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(42, 111, 176)), Background = new SolidColorBrush(Color.FromRgb(229, 233, 239)), BorderThickness = new Thickness(0) };
        Grid.SetColumn(_backgroundLoadProgress, 1);
        progressRow.Children.Add(_backgroundLoadProgress);
        Grid.SetRow(progressRow, 1);
        body.Children.Add(progressRow);

        var grid = BaseGrid(_backgroundLoadRows, 46);
        grid.Columns.Add(CreateCenteredTextColumn("순위", nameof(BackgroundLoadRow.Rank), nameof(BackgroundLoadRow.Rank), 60));
        grid.Columns.Add(CreateCenteredTextColumn("프로세스", nameof(BackgroundLoadRow.Name), nameof(BackgroundLoadRow.Name), new DataGridLength(1.2, DataGridLengthUnitType.Star)));
        grid.Columns.Add(CreateCenteredTextColumn("구분", nameof(BackgroundLoadRow.Kind), nameof(BackgroundLoadRow.Kind), 105));
        grid.Columns.Add(CreateCenteredTextColumn("판정", nameof(BackgroundLoadRow.Assessment), nameof(BackgroundLoadRow.Assessment), 120));
        grid.Columns.Add(CreateCenteredTextColumn("CPU 평균", nameof(BackgroundLoadRow.AverageCpuText), nameof(BackgroundLoadRow.AverageCpuPercent), 90));
        grid.Columns.Add(CreateCenteredTextColumn("CPU 최대", nameof(BackgroundLoadRow.MaximumCpuText), nameof(BackgroundLoadRow.MaximumCpuPercent), 90));
        grid.Columns.Add(CreateCenteredTextColumn("메모리 평균/최대", nameof(BackgroundLoadRow.MemoryText), nameof(BackgroundLoadRow.AverageMemoryMb), 145));
        grid.Columns.Add(CreateCenteredTextColumn("디스크 누적·평균", nameof(BackgroundLoadRow.DiskText), nameof(BackgroundLoadRow.DiskBytes), 165));
        grid.Columns.Add(CreateCenteredTextColumn("네트워크 연결", nameof(BackgroundLoadRow.NetworkText), nameof(BackgroundLoadRow.AverageNetworkConnections), 145));
        Grid.SetRow(grid, 2);
        body.Children.Add(grid);
        surface.Child = body;
        return surface;
    }

    private Button CreateLoadDurationButton(int seconds)
    {
        var selected = seconds == _backgroundLoadDurationSeconds;
        var button = Action($"{seconds}초", (_, _) => SelectLoadDuration(seconds), selected ? "#DDEAF6" : "#F1F2F4", selected ? "#1F5F99" : "#4F5661");
        button.Padding = new Thickness(12, 7, 12, 7);
        _loadDurationButtons[seconds] = button;
        return button;
    }

    private void SelectLoadDuration(int seconds)
    {
        if (_backgroundLoadRunning) return;
        _backgroundLoadDurationSeconds = seconds;
        foreach (var pair in _loadDurationButtons)
        {
            var selected = pair.Key == seconds;
            pair.Value.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(selected ? "#DDEAF6" : "#F1F2F4"));
            pair.Value.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(selected ? "#1F5F99" : "#4F5661"));
        }
    }

    private async void BackgroundLoadStart_Click(object sender, RoutedEventArgs e)
    {
        if (_backgroundLoadRunning)
        {
            _backgroundLoadCancellation?.Cancel();
            return;
        }

        _backgroundLoadRunning = true;
        _backgroundLoadRows.Clear();
        _backgroundLoadCancellation = CancellationTokenSource.CreateLinkedTokenSource(_monitorCancellation.Token);
        if (_backgroundLoadStartButton is not null) _backgroundLoadStartButton.Content = "측정 중지";
        foreach (var button in _loadDurationButtons.Values) button.IsEnabled = false;
        if (_backgroundLoadProgress is not null) _backgroundLoadProgress.Value = 0;
        var duration = TimeSpan.FromSeconds(_backgroundLoadDurationSeconds);
        var progress = new Progress<BackgroundLoadProgress>(UpdateBackgroundLoadProgress);
        try
        {
            if (_backgroundLoadStatus is not null) _backgroundLoadStatus.Text = $"{_backgroundLoadDurationSeconds}초 측정을 시작합니다…";
            var results = await _backgroundLoadService.MonitorAsync(duration, progress, _backgroundLoadCancellation.Token);
            UpdateBackgroundLoadRows(results);
            if (_backgroundLoadProgress is not null) _backgroundLoadProgress.Value = 100;
            var high = results.Count(item => item.Assessment == "지속 부하 높음");
            var review = results.Count(item => item.Assessment == "검토 권장");
            if (_backgroundLoadStatus is not null) _backgroundLoadStatus.Text = $"측정 완료 · 지속 부하 높음 {high}개 · 검토 권장 {review}개 · 결과 {results.Count}개";
        }
        catch (OperationCanceledException)
        {
            if (_backgroundLoadStatus is not null) _backgroundLoadStatus.Text = _monitorCancellation.IsCancellationRequested ? "앱 종료로 측정이 중단되었습니다." : "사용자가 측정을 중단했습니다. 현재까지의 결과는 유지합니다.";
        }
        catch (Exception ex)
        {
            if (_backgroundLoadStatus is not null) _backgroundLoadStatus.Text = $"부하 측정 실패 · {ex.Message}";
        }
        finally
        {
            _backgroundLoadRunning = false;
            _backgroundLoadCancellation?.Dispose();
            _backgroundLoadCancellation = null;
            if (_backgroundLoadStartButton is not null) _backgroundLoadStartButton.Content = "측정 시작";
            foreach (var button in _loadDurationButtons.Values) button.IsEnabled = true;
        }
    }

    private void UpdateBackgroundLoadProgress(BackgroundLoadProgress progress)
    {
        if (_backgroundLoadProgress is not null) _backgroundLoadProgress.Value = progress.Percent;
        if (_backgroundLoadStatus is not null) _backgroundLoadStatus.Text = $"측정 중 · {progress.Elapsed.TotalSeconds:0}/{progress.Duration.TotalSeconds:0}초 · 프로세스 {progress.Readings.Count}개 분석";
        UpdateBackgroundLoadRows(progress.Readings);
    }

    private void UpdateBackgroundLoadRows(IReadOnlyList<BackgroundLoadReading> readings)
    {
        var retained = readings.Select(item => item.ProcessId).ToHashSet();
        for (var index = _backgroundLoadRows.Count - 1; index >= 0; index--)
            if (!retained.Contains(_backgroundLoadRows[index].ProcessId)) _backgroundLoadRows.RemoveAt(index);

        for (var targetIndex = 0; targetIndex < readings.Count; targetIndex++)
        {
            var reading = readings[targetIndex];
            var existing = _backgroundLoadRows.FirstOrDefault(item => item.ProcessId == reading.ProcessId);
            if (existing is null)
            {
                existing = new BackgroundLoadRow(reading);
                _backgroundLoadRows.Insert(Math.Min(targetIndex, _backgroundLoadRows.Count), existing);
            }
            else existing.Update(reading);
            var currentIndex = _backgroundLoadRows.IndexOf(existing);
            if (currentIndex != targetIndex && targetIndex < _backgroundLoadRows.Count) _backgroundLoadRows.Move(currentIndex, targetIndex);
        }
    }
}

public sealed class BackgroundLoadRow : INotifyPropertyChanged
{
    private BackgroundLoadReading _reading;
    public BackgroundLoadRow(BackgroundLoadReading reading) => _reading = reading;
    public int Rank => _reading.Rank;
    public int ProcessId => _reading.ProcessId;
    public string Name => $"{_reading.Name}  ·  PID {_reading.ProcessId}";
    public string Kind => _reading.Kind;
    public string Assessment => _reading.Assessment;
    public double AverageCpuPercent => _reading.AverageCpuPercent;
    public string AverageCpuText => _reading.AverageCpuText;
    public double MaximumCpuPercent => _reading.MaximumCpuPercent;
    public string MaximumCpuText => _reading.MaximumCpuText;
    public double AverageMemoryMb => _reading.AverageMemoryMb;
    public string MemoryText => _reading.MemoryText;
    public long DiskBytes => _reading.DiskBytes;
    public string DiskText => _reading.DiskText;
    public double AverageNetworkConnections => _reading.AverageNetworkConnections;
    public string NetworkText => _reading.NetworkText;
    public string Recommendation => _reading.Recommendation;
    public void Update(BackgroundLoadReading reading) { _reading = reading; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty)); }
    public event PropertyChangedEventHandler? PropertyChanged;
}
