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
    private readonly ObservableCollection<ManagedStartupEntry> _startupEntries = [];
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
        root.Children.Add(PageHeader("시스템 최적화", "전원 설정, 시작 프로그램과 정리 가능 공간을 분석하고 변경 전후 효과를 수치로 비교합니다.", out _));
        root.Children.Add(CreateOptimizationToolbar());
        root.Children.Add(CreateOptimizationSummary());

        root.Children.Add(CreateStorageMaintenancePanel());
        root.Children.Add(CreateNetworkDiagnosticsPanel());
        root.Children.Add(CreatePersonalizedRecommendationPanel());
        root.Children.Add(CreatePowerAnalysisPanel());
        root.Children.Add(CreateBackgroundLoadPanel());
        root.Children.Add(CreateMemoryAnalysisPanel());
        root.Children.Add(CreateStorageDiagnosticsPanel());
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
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(76) });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(300) });
        var header = new Grid { Margin = new Thickness(20, 0, 20, 0) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(new TextBlock { Text = "시작 프로그램 관리", FontSize = 16, FontWeight = FontWeights.SemiBold });
        title.Children.Add(new TextBlock { Text = "Windows StartupApproved 상태를 사용하며, 변경 전 상태를 백업합니다. Windows·보안 항목은 보호됩니다.", Foreground = Muted(), FontSize = 10, Margin = new Thickness(0, 5, 0, 0) });
        header.Children.Add(title);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _startupRestoreButton = Action("마지막 변경 복원", RestoreStartupChanges_Click, "#F1F2F4", "#24262B");
        _startupRestoreButton.IsEnabled = _startupManagementService.CanRestore;
        actions.Children.Add(_startupRestoreButton);
        _startupToggleButton = Action("선택 상태 전환", ToggleSelectedStartup_Click, "#24262B", "#FFFFFF");
        actions.Children.Add(_startupToggleButton);
        Grid.SetColumn(actions, 1);
        header.Children.Add(actions);
        body.Children.Add(header);
        var grid = BaseGrid(_startupEntries, 48);
        grid.RowStyle = CreateStartupRowStyle();
        grid.Columns.Add(CreateStartupSelectionColumn());
        grid.Columns.Add(CreateCenteredTextColumn("이름", nameof(ManagedStartupEntry.Name), nameof(ManagedStartupEntry.Name), new DataGridLength(1.2, DataGridLengthUnitType.Star)));
        grid.Columns.Add(CreateCenteredTextColumn("상태", nameof(ManagedStartupEntry.Status), nameof(ManagedStartupEntry.IsEnabled), 85));
        grid.Columns.Add(CreateCenteredTextColumn("게시자", nameof(ManagedStartupEntry.Publisher), nameof(ManagedStartupEntry.Publisher), new DataGridLength(1.1, DataGridLengthUnitType.Star)));
        grid.Columns.Add(CreateCenteredTextColumn("등록 위치", nameof(ManagedStartupEntry.Source), nameof(ManagedStartupEntry.Source), new DataGridLength(1.25, DataGridLengthUnitType.Star)));
        grid.Columns.Add(CreateCenteredTextColumn("권장 사항", nameof(ManagedStartupEntry.Recommendation), nameof(ManagedStartupEntry.Recommendation), new DataGridLength(1.5, DataGridLengthUnitType.Star)));
        grid.Columns.Add(CreateCenteredTextColumn("제한·영향", nameof(ManagedStartupEntry.Restriction), nameof(ManagedStartupEntry.Restriction), new DataGridLength(1.45, DataGridLengthUnitType.Star)));
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
        grid.RowStyle = CreateCleanupRowStyle();
        grid.Columns.Add(CreateCleanupSelectionColumn());
        grid.Columns.Add(CreateCenteredTextColumn("항목", nameof(CleanupGroup.Category), nameof(CleanupGroup.Category), new DataGridLength(1.2, DataGridLengthUnitType.Star)));
        grid.Columns.Add(CreateCenteredTextColumn("예상 확보", nameof(CleanupGroup.SizeText), nameof(CleanupGroup.SizeBytes), 110));
        grid.Columns.Add(CreateCenteredTextColumn("파일", nameof(CleanupGroup.FileCount), nameof(CleanupGroup.FileCount), 80));
        grid.Columns.Add(CreateCenteredTextColumn("안전 기준", nameof(CleanupGroup.SafetyNote), nameof(CleanupGroup.SafetyNote), new DataGridLength(2.4, DataGridLengthUnitType.Star)));
        Grid.SetRow(grid, 1);
        body.Children.Add(grid);
        surface.Child = body;
        return surface;
    }

    private static DataGridTemplateColumn CreateCleanupSelectionColumn()
    {
        var checkBox = new FrameworkElementFactory(typeof(CheckBox));
        checkBox.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(CleanupGroup.IsSelected)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        checkBox.SetValue(FrameworkElement.WidthProperty, 22d);
        checkBox.SetValue(FrameworkElement.HeightProperty, 22d);
        checkBox.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        checkBox.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        checkBox.SetValue(UIElement.FocusableProperty, false);
        checkBox.SetValue(Control.TemplateProperty, CreateCleanupCheckBoxTemplate());
        return new DataGridTemplateColumn
        {
            Header = "정리",
            CellTemplate = new DataTemplate { VisualTree = checkBox },
            Width = 78
        };
    }

    private static ControlTemplate CreateCleanupCheckBoxTemplate()
    {
        var shell = new FrameworkElementFactory(typeof(Border));
        shell.Name = "Shell";
        shell.SetValue(FrameworkElement.WidthProperty, 20d);
        shell.SetValue(FrameworkElement.HeightProperty, 20d);
        shell.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
        shell.SetValue(Border.BackgroundProperty, Brushes.White);
        shell.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(184, 189, 198)));
        shell.SetValue(Border.BorderThicknessProperty, new Thickness(1));

        var mark = new FrameworkElementFactory(typeof(TextBlock));
        mark.Name = "Mark";
        mark.SetValue(TextBlock.TextProperty, "✓");
        mark.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe UI"));
        mark.SetValue(TextBlock.FontSizeProperty, 13d);
        mark.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        mark.SetValue(TextBlock.ForegroundProperty, Brushes.White);
        mark.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        mark.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        mark.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
        shell.AppendChild(mark);

        var template = new ControlTemplate(typeof(CheckBox)) { VisualTree = shell };
        var checkedTrigger = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
        checkedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(34, 105, 170)), "Shell"));
        checkedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(34, 105, 170)), "Shell"));
        checkedTrigger.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "Mark"));
        template.Triggers.Add(checkedTrigger);
        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(72, 125, 180)), "Shell"));
        template.Triggers.Add(hoverTrigger);
        var disabledTrigger = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabledTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, .45, "Shell"));
        template.Triggers.Add(disabledTrigger);
        return template;
    }

    private static Style CreateCleanupRowStyle()
    {
        var basedOn = Application.Current.TryFindResource(typeof(DataGridRow)) as Style;
        var style = new Style(typeof(DataGridRow), basedOn);
        var selected = new DataTrigger { Binding = new Binding(nameof(CleanupGroup.IsSelected)), Value = true };
        selected.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(242, 247, 252))));
        style.Triggers.Add(selected);
        return style;
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
            var startupTask = _startupManagementService.AnalyzeAsync(_monitorCancellation.Token);
            var cleanupTask = _optimizationService.ScanCleanupAsync(_monitorCancellation.Token);
            var snapshotTask = _optimizationService.CaptureSnapshotAsync(_monitorCancellation.Token);
            var powerTask = _powerSettingsService.AnalyzeAsync(_monitorCancellation.Token);
            var memoryTask = _memoryAnalysisService.CaptureAsync(_monitorCancellation.Token);
            var storageTask = _storageDiagnosticsService.CaptureAsync(_monitorCancellation.Token);
            await Task.WhenAll(startupTask, cleanupTask, snapshotTask, powerTask, memoryTask, storageTask);

            _startupEntries.Clear();
            foreach (var item in await startupTask) _startupEntries.Add(item);
            _cleanupGroups.Clear();
            foreach (var item in await cleanupTask) _cleanupGroups.Add(item);
            var snapshot = await snapshotTask;
            RenderPowerAnalysis(await powerTask);
            RenderMemoryAnalysis(await memoryTask);
            RenderStorageDiagnostics(await storageTask);
            UpdateStartupSummary();
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





