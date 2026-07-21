using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SystemChecker.Services;

namespace SystemChecker;

public partial class TelemetryMainWindow
{
    private readonly MemoryAnalysisService _memoryAnalysisService = new();
    private readonly ObservableCollection<MemoryProcessSnapshot> _memoryProcesses = [];
    private TextBlock? _memoryPhysicalValue;
    private TextBlock? _memoryAvailableValue;
    private TextBlock? _memoryCommitValue;
    private TextBlock? _memoryCacheValue;
    private TextBlock? _memoryDetailStatus;
    private TextBlock? _memoryDetailExplanation;

    private FrameworkElement CreateMemoryAnalysisPanel()
    {
        var surface = Surface();
        surface.Margin = new Thickness(12, 0, 10, 12);
        surface.Padding = new Thickness(20, 0, 20, 18);
        var body = new StackPanel();
        var header = new Grid { Height = 76 };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(new TextBlock { Text = "메모리 상세 분석", FontSize = 16, FontWeight = FontWeights.SemiBold });
        title.Children.Add(new TextBlock { Text = "물리 메모리, 커밋 한계, 캐시, 커널 풀, 페이지 파일과 프로세스별 전용 커밋을 분석합니다.", Foreground = Muted(), FontSize = 10, Margin = new Thickness(0, 5, 0, 0) });
        header.Children.Add(title);
        var refresh = Action("메모리 새로고침", async (_, _) => await RefreshMemoryAnalysisAsync(), "#F1F2F4", "#24262B");
        refresh.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(refresh, 1);
        header.Children.Add(refresh);
        body.Children.Add(header);

        var metrics = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        for (var index = 0; index < 4; index++) metrics.ColumnDefinitions.Add(new ColumnDefinition());
        metrics.Children.Add(MemoryMetric("물리 메모리 사용", out _memoryPhysicalValue, 0));
        metrics.Children.Add(MemoryMetric("사용 가능", out _memoryAvailableValue, 1));
        metrics.Children.Add(MemoryMetric("커밋 사용/한계", out _memoryCommitValue, 2));
        metrics.Children.Add(MemoryMetric("시스템 캐시", out _memoryCacheValue, 3));
        body.Children.Add(metrics);

        var detail = new Border { Background = new SolidColorBrush(Color.FromRgb(247, 249, 252)), BorderBrush = new SolidColorBrush(Color.FromRgb(224, 229, 235)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(0, 0, 0, 12) };
        var detailBody = new StackPanel();
        _memoryDetailStatus = new TextBlock { Text = "메모리 분석 전", FontWeight = FontWeights.SemiBold };
        _memoryDetailExplanation = new TextBlock { Text = "분석 후 페이지 파일, 압축과 커널 메모리 상태를 표시합니다.", Foreground = Muted(), FontSize = 10, Margin = new Thickness(0, 5, 0, 0), TextWrapping = TextWrapping.Wrap };
        detailBody.Children.Add(_memoryDetailStatus);
        detailBody.Children.Add(_memoryDetailExplanation);
        detail.Child = detailBody;
        body.Children.Add(detail);

        var grid = BaseGrid(_memoryProcesses, 46);
        grid.Height = 278;
        grid.Columns.Add(CreateCenteredTextColumn("프로세스", nameof(MemoryProcessSnapshot.ProcessText), nameof(MemoryProcessSnapshot.Name), new DataGridLength(1.35, DataGridLengthUnitType.Star)));
        grid.Columns.Add(CreateCenteredTextColumn("구분", nameof(MemoryProcessSnapshot.Kind), nameof(MemoryProcessSnapshot.Kind), 105));
        grid.Columns.Add(CreateCenteredTextColumn("작업 집합", nameof(MemoryProcessSnapshot.WorkingSetText), nameof(MemoryProcessSnapshot.WorkingSetBytes), 120));
        grid.Columns.Add(CreateCenteredTextColumn("전용 커밋", nameof(MemoryProcessSnapshot.PrivateCommitText), nameof(MemoryProcessSnapshot.PrivateCommitBytes), 120));
        grid.Columns.Add(CreateCenteredTextColumn("최대 작업 집합", nameof(MemoryProcessSnapshot.PeakWorkingSetText), nameof(MemoryProcessSnapshot.PeakWorkingSetBytes), 130));
        grid.Columns.Add(CreateCenteredTextColumn("스레드", nameof(MemoryProcessSnapshot.ThreadCount), nameof(MemoryProcessSnapshot.ThreadCount), 80));
        grid.Columns.Add(CreateCenteredTextColumn("판정", nameof(MemoryProcessSnapshot.Assessment), nameof(MemoryProcessSnapshot.Assessment), 125));
        body.Children.Add(grid);
        surface.Child = body;
        return surface;
    }

    private static Border MemoryMetric(string title, out TextBlock value, int column)
    {
        var card = new Border { Background = new SolidColorBrush(Color.FromRgb(249, 250, 252)), BorderBrush = new SolidColorBrush(Color.FromRgb(229, 232, 237)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Margin = new Thickness(column == 0 ? 0 : 5, 0, column == 3 ? 0 : 5, 0), Padding = new Thickness(14, 12, 14, 12) };
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = title, Foreground = Muted(), FontSize = 10 });
        value = new TextBlock { Text = "분석 전", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 7, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
        stack.Children.Add(value);
        card.Child = stack;
        Grid.SetColumn(card, column);
        return card;
    }

    private async Task RefreshMemoryAnalysisAsync()
    {
        if (_optimizationBusy) return;
        _optimizationBusy = true;
        try
        {
            if (_optimizationStatus is not null) _optimizationStatus.Text = "메모리 상세 정보를 분석하는 중…";
            var result = await _memoryAnalysisService.CaptureAsync(_monitorCancellation.Token);
            RenderMemoryAnalysis(result);
            if (_optimizationStatus is not null) _optimizationStatus.Text = $"메모리 분석 완료 · {result.Assessment} · 커밋 {result.CommitUsagePercent:0.0}%";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (_optimizationStatus is not null) _optimizationStatus.Text = $"메모리 분석 실패 · {ex.Message}"; }
        finally { _optimizationBusy = false; }
    }

    private void RenderMemoryAnalysis(MemoryAnalysisResult result)
    {
        SetMemoryMetric(_memoryPhysicalValue, result.PhysicalText);
        SetMemoryMetric(_memoryAvailableValue, result.AvailableText);
        SetMemoryMetric(_memoryCommitValue, result.CommitText);
        SetMemoryMetric(_memoryCacheValue, result.CacheText);
        if (_memoryDetailStatus is not null) _memoryDetailStatus.Text = $"{result.Assessment} · 페이지 파일 {result.PageFile.Status} · 메모리 압축 {result.CompressionText}";
        if (_memoryDetailExplanation is not null) _memoryDetailExplanation.Text = $"{result.Recommendation} 커널: {result.KernelText} · 커밋 최고 {OptimizationService.FormatBytes((long)result.CommitPeakBytes)} · 페이지 파일 현재 {OptimizationService.FormatBytes((long)result.PageFile.CurrentUsageBytes)}";
        _memoryProcesses.Clear();
        foreach (var item in result.Processes) _memoryProcesses.Add(item);
    }

    private static void SetMemoryMetric(TextBlock? target, string value) { if (target is null) return; target.Text = value; target.ToolTip = value; }
}
