using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using SystemChecker.Services;

namespace SystemChecker;

public partial class TelemetryMainWindow
{
    private readonly OptimizationService _optimizationService = new();
    private readonly ObservableCollection<StartupEntry> _startupEntries = [];
    private readonly ObservableCollection<CleanupGroup> _cleanupGroups = [];
    private Button? _optimizationNav;
    private ScrollViewer? _optimizationPage;
    private TextBlock? _optimizationStatus;
    private TextBlock? _startupSummary;
    private TextBlock? _cleanupSummary;
    private TextBlock? _comparisonSummary;
    private OptimizationSnapshot? _optimizationBaseline;
    private bool _optimizationLoaded;
    private bool _optimizationBusy;

    private void InitializeOptimizationPage()
    {
        if (OverviewPage.Parent is not Grid host || ReportNav.Parent is not StackPanel navigation) return;

        _optimizationNav = new Button { Style = (Style)FindResource("Nav"), CommandParameter = "Optimization" };
        _optimizationNav.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                new TextBlock { Text = "00", FontFamily = new FontFamily("Consolas"), FontSize = 10, Width = 28 },
                new TextBlock { Text = "OPTIMIZE", FontSize = 11, FontWeight = FontWeights.SemiBold }
            }
        };
        _optimizationNav.Click += OptimizationNav_Click;
        navigation.Children.Insert(Math.Max(0, navigation.Children.IndexOf(ReportNav)), _optimizationNav);

        foreach (var button in new[] { OverviewNav, HardwareNav, BenchmarkNav, ReportNav })
            button.Click += (_, _) => { if (_optimizationPage is not null) _optimizationPage.Visibility = Visibility.Collapsed; if (_optimizationNav is not null) _optimizationNav.Tag = null; };
        if (_processNav is not null)
            _processNav.Click += (_, _) => { if (_optimizationPage is not null) _optimizationPage.Visibility = Visibility.Collapsed; if (_optimizationNav is not null) _optimizationNav.Tag = null; };

        var root = new StackPanel { Margin = new Thickness(8, 6, 10, 18) };
        root.Children.Add(PageHeader("시스템 최적화", "시작 프로그램과 정리 가능 공간을 분석하고 변경 전후 효과를 수치로 비교합니다.", out _));
        root.Children.Add(CreateOptimizationToolbar());
        root.Children.Add(CreateOptimizationSummary());
        root.Children.Add(CreateStartupAnalysisPanel());
        root.Children.Add(CreateCleanupPanel());

        _optimizationPage = new ScrollViewer
        {
            Content = root,
            Visibility = Visibility.Collapsed,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            PanningMode = PanningMode.VerticalOnly
        };
        host.Children.Add(_optimizationPage);
        ConfigureNavigationNumbers();
    }

    private FrameworkElement CreateOptimizationToolbar()
    {
        var surface = Surface();
        surface.Margin = new Thickness(12, 0, 10, 12);
        surface.Padding = new Thickness(18, 14, 18, 14);
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _optimizationStatus = new TextBlock { Text = "분석을 시작하면 시스템을 변경하지 않고 안전하게 진단합니다.", Foreground = Muted(), VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(_optimizationStatus);
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(Action("다시 분석", async (_, _) => await LoadOptimizationAsync(true), "#F1F2F4", "#24262B"));
        actions.Children.Add(Action("기준 기록", CaptureOptimizationBaseline_Click, "#E7EEF7", "#234D7A"));
        actions.Children.Add(Action("선택 항목 정리", CleanupSelected_Click, "#24262B", "#FFFFFF"));
        Grid.SetColumn(actions, 1);
        row.Children.Add(actions);
        surface.Child = row;
        return surface;
    }

    private FrameworkElement CreateOptimizationSummary()
    {
        var grid = new Grid { Margin = new Thickness(12, 0, 10, 12) };
        for (var i = 0; i < 3; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(SummaryCard("시작 프로그램", out _startupSummary, 0));
        grid.Children.Add(SummaryCard("정리 가능 공간", out _cleanupSummary, 1));
        grid.Children.Add(SummaryCard("최적화 전후 비교", out _comparisonSummary, 2));
        _startupSummary.Text = "분석 전";
        _cleanupSummary.Text = "분석 전";
        _comparisonSummary.Text = "기준 기록 필요";
        return grid;
    }

    private static Border SummaryCard(string title, out TextBlock value, int column)
    {
        var card = Surface();
        card.Padding = new Thickness(18);
        card.Margin = new Thickness(column == 0 ? 0 : 6, 0, column == 2 ? 0 : 6, 0);
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = title, Foreground = Muted(), FontSize = 10, FontWeight = FontWeights.SemiBold });
        value = new TextBlock { FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 9, 0, 0), TextWrapping = TextWrapping.Wrap };
        stack.Children.Add(value);
        card.Child = stack;
        Grid.SetColumn(card, column);
        return card;
    }

    private FrameworkElement CreateStartupAnalysisPanel()
    {
        var surface = Surface();
        surface.Margin = new Thickness(12, 0, 10, 12);
        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(62) });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(280) });
        var header = new StackPanel { Margin = new Thickness(20, 0, 20, 0), VerticalAlignment = VerticalAlignment.Center };
        header.Children.Add(new TextBlock { Text = "시작 프로그램 분석", FontSize = 16, FontWeight = FontWeights.SemiBold });
        header.Children.Add(new TextBlock { Text = "게시자와 등록 위치를 확인해 부팅 시 꼭 필요한 항목인지 판단합니다. 이 화면에서는 자동으로 비활성화하지 않습니다.", Foreground = Muted(), FontSize = 10, Margin = new Thickness(0, 5, 0, 0) });
        body.Children.Add(header);
        var grid = BaseGrid(_startupEntries, 48);
        grid.Columns.Add(CreateCenteredTextColumn("이름", nameof(StartupEntry.Name), nameof(StartupEntry.Name), new DataGridLength(1.2, DataGridLengthUnitType.Star)));
        grid.Columns.Add(CreateCenteredTextColumn("게시자", nameof(StartupEntry.Publisher), nameof(StartupEntry.Publisher), new DataGridLength(1.2, DataGridLengthUnitType.Star)));
        grid.Columns.Add(CreateCenteredTextColumn("등록 위치", nameof(StartupEntry.Source), nameof(StartupEntry.Source), new DataGridLength(1.3, DataGridLengthUnitType.Star)));
        grid.Columns.Add(CreateCenteredTextColumn("권장 사항", nameof(StartupEntry.Recommendation), nameof(StartupEntry.Recommendation), new DataGridLength(1.5, DataGridLengthUnitType.Star)));
        Grid.SetRow(grid, 1);
        body.Children.Add(grid);
        surface.Child = body;
        return surface;
    }

    private FrameworkElement CreateCleanupPanel()
    {
        var surface = Surface();
        surface.Margin = new Thickness(12, 0, 10, 0);
        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(70) });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(250) });
        var header = new StackPanel { Margin = new Thickness(20, 0, 20, 0), VerticalAlignment = VerticalAlignment.Center };
        header.Children.Add(new TextBlock { Text = "저장 공간 정리 미리보기", FontSize = 16, FontWeight = FontWeights.SemiBold });
        header.Children.Add(new TextBlock { Text = "24시간 이상 된 임시 파일만 표시합니다. 항목을 선택하고 확인한 경우에만 삭제합니다.", Foreground = Muted(), FontSize = 10, Margin = new Thickness(0, 5, 0, 0) });
        body.Children.Add(header);
        var grid = BaseGrid(_cleanupGroups, 52);
        grid.Columns.Add(new DataGridCheckBoxColumn { Header = "선택", Binding = new Binding(nameof(CleanupGroup.IsSelected)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = 70 });
        grid.Columns.Add(CreateCenteredTextColumn("항목", nameof(CleanupGroup.Category), nameof(CleanupGroup.Category), new DataGridLength(1.2, DataGridLengthUnitType.Star)));
        grid.Columns.Add(CreateCenteredTextColumn("예상 확보", nameof(CleanupGroup.SizeText), nameof(CleanupGroup.SizeBytes), 110));
        grid.Columns.Add(CreateCenteredTextColumn("파일", nameof(CleanupGroup.FileCount), nameof(CleanupGroup.FileCount), 80));
        grid.Columns.Add(CreateCenteredTextColumn("안전 기준", nameof(CleanupGroup.SafetyNote), nameof(CleanupGroup.SafetyNote), new DataGridLength(2.4, DataGridLengthUnitType.Star)));
        Grid.SetRow(grid, 1);
        body.Children.Add(grid);
        surface.Child = body;
        return surface;
    }

    private async void OptimizationNav_Click(object sender, RoutedEventArgs e)
    {
        OverviewPage.Visibility = HardwarePage.Visibility = BenchmarkPage.Visibility = ReportPage.Visibility = Visibility.Collapsed;
        if (_processPage is not null) _processPage.Visibility = Visibility.Collapsed;
        if (_optimizationPage is not null) _optimizationPage.Visibility = Visibility.Visible;
        OverviewNav.Tag = HardwareNav.Tag = BenchmarkNav.Tag = ReportNav.Tag = null;
        if (_processNav is not null) _processNav.Tag = null;
        if (_optimizationNav is not null) _optimizationNav.Tag = "Active";
        PageTitle.Text = "OPTIMIZE";
        if (!_optimizationLoaded) await LoadOptimizationAsync(false);
    }

    private async Task LoadOptimizationAsync(bool compareWithBaseline)
    {
        if (_optimizationBusy) return;
        _optimizationBusy = true;
        if (_optimizationStatus is not null) _optimizationStatus.Text = "시작 프로그램과 임시 파일을 분석하는 중…";
        try
        {
            var startupTask = _optimizationService.AnalyzeStartupAsync(_monitorCancellation.Token);
            var cleanupTask = _optimizationService.ScanCleanupAsync(_monitorCancellation.Token);
            var snapshotTask = _optimizationService.CaptureSnapshotAsync(_monitorCancellation.Token);
            await Task.WhenAll(startupTask, cleanupTask, snapshotTask);

            _startupEntries.Clear();
            foreach (var item in await startupTask) _startupEntries.Add(item);
            _cleanupGroups.Clear();
            foreach (var item in await cleanupTask) _cleanupGroups.Add(item);
            var snapshot = await snapshotTask;
            if (_startupSummary is not null) _startupSummary.Text = $"{_startupEntries.Count:N0}개 · 검토 권장 {_startupEntries.Count(item => item.Recommendation.Contains("검토")):N0}개";
            if (_cleanupSummary is not null) _cleanupSummary.Text = $"{OptimizationService.FormatBytes(_cleanupGroups.Sum(item => item.SizeBytes))} · {_cleanupGroups.Sum(item => item.FileCount):N0}개 파일";
            if (compareWithBaseline && _optimizationBaseline is not null) ShowOptimizationComparison(_optimizationBaseline, snapshot);
            if (_optimizationStatus is not null) _optimizationStatus.Text = $"분석 완료 · {snapshot.Summary}";
            _optimizationLoaded = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (_optimizationStatus is not null) _optimizationStatus.Text = $"분석 실패 · {ex.Message}";
        }
        finally { _optimizationBusy = false; }
    }

    private async void CaptureOptimizationBaseline_Click(object sender, RoutedEventArgs e)
    {
        if (_optimizationBusy) return;
        _optimizationBusy = true;
        try
        {
            if (_optimizationStatus is not null) _optimizationStatus.Text = "비교 기준을 측정하는 중…";
            _optimizationBaseline = await _optimizationService.CaptureSnapshotAsync(_monitorCancellation.Token);
            if (_comparisonSummary is not null) _comparisonSummary.Text = $"기준 기록 · {_optimizationBaseline.CapturedAt:HH:mm:ss}";
            if (_optimizationStatus is not null) _optimizationStatus.Text = _optimizationBaseline.Summary;
        }
        catch (OperationCanceledException) { }
        finally { _optimizationBusy = false; }
    }

    private async void CleanupSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_optimizationBusy) return;
        var selected = _cleanupGroups.Where(item => item.IsSelected && item.FileCount > 0).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show(this, "정리할 항목을 먼저 선택하세요.", "CoreWatch", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var expected = selected.Sum(item => item.SizeBytes);
        var files = selected.Sum(item => item.FileCount);
        var message = $"선택한 {files:N0}개 임시 파일({OptimizationService.FormatBytes(expected)})을 삭제합니다.\n사용 중인 파일과 24시간 이내 파일은 건너뜁니다. 계속할까요?";
        if (MessageBox.Show(this, message, "저장 공간 정리 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        _optimizationBusy = true;
        try
        {
            if (_optimizationStatus is not null) _optimizationStatus.Text = "선택한 임시 파일을 정리하는 중…";
            var result = await _optimizationService.CleanAsync(selected, _monitorCancellation.Token);
            MessageBox.Show(this, $"{result.DeletedFiles:N0}개 파일을 정리해 {OptimizationService.FormatBytes(result.ReclaimedBytes)}를 확보했습니다.\n건너뛰거나 실패한 파일: {result.FailedFiles:N0}개", "정리 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            _optimizationBusy = false;
            await LoadOptimizationAsync(_optimizationBaseline is not null);
        }
        catch (OperationCanceledException) { }
        finally { _optimizationBusy = false; }
    }

    private void ShowOptimizationComparison(OptimizationSnapshot before, OptimizationSnapshot after)
    {
        if (_comparisonSummary is null) return;
        _comparisonSummary.Text = $"CPU {after.CpuPercent - before.CpuPercent:+0.0;-0.0;0.0}%p · 메모리 {after.MemoryPercent - before.MemoryPercent:+0.0;-0.0;0.0}%p · 프로세스 {after.ProcessCount - before.ProcessCount:+#;-#;0}개";
    }
}
