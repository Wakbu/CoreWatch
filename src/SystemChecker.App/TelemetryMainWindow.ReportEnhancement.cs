using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace SystemChecker;

public partial class TelemetryMainWindow
{
    private TextBlock? _reportAssessment;
    private TextBlock? _reportCoverage;
    private TextBlock? _reportGeneratedAt;
    private TextBlock? _reportReason;

    private void InitializeReportEnhancement()
    {
        var surface = Panel();
        surface.Tag = "report-summary";
        surface.Margin = new Thickness(0, 12, 0, 0);
        var body = new StackPanel();
        body.Children.Add(Heading("REPORT SUMMARY / READINESS"));
        body.Children.Add(new TextBlock
        {
            Text = "현재 수집된 로컬 데이터의 포함 범위와 확인 필요 항목을 내보내기 전에 점검합니다.",
            Foreground = new SolidColorBrush(Color.FromRgb(104, 112, 125)),
            Margin = new Thickness(0, 6, 0, 12)
        });

        var metrics = new Grid();
        for (var index = 0; index < 3; index++) metrics.ColumnDefinitions.Add(new ColumnDefinition());
        metrics.Children.Add(CreateReportMetric("종합 판정", out _reportAssessment, 0));
        metrics.Children.Add(CreateReportMetric("포함 범위", out _reportCoverage, 1));
        metrics.Children.Add(CreateReportMetric("기준 시각", out _reportGeneratedAt, 2));
        body.Children.Add(metrics);

        _reportReason = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(82, 90, 102)),
            Margin = new Thickness(0, 12, 0, 0)
        };
        body.Children.Add(_reportReason);
        body.Children.Add(new TextBlock
        {
            Text = "개인정보 안내 · 보고서는 이 PC에서만 생성되며 자동 업로드하거나 외부 서버로 전송하지 않습니다.",
            Foreground = new SolidColorBrush(Color.FromRgb(35, 112, 86)),
            FontSize = 10,
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
        surface.Child = body;
        ReportPage.Children.Insert(0, surface);
        AutomationProperties.SetName(surface, "Report summary");
        ReportNav.Click += (_, _) => RefreshReportSummary();
        Loaded += (_, _) => RefreshReportSummary();
    }

    private static Border CreateReportMetric(string title, out TextBlock value, int column)
    {
        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 249, 251)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(222, 226, 232)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Margin = new Thickness(column == 0 ? 0 : 5, 0, column == 2 ? 0 : 5, 0)
        };
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = title, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(104, 112, 125)) });
        value = new TextBlock { Text = "데이터 준비 중", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 7, 0, 0), TextWrapping = TextWrapping.Wrap };
        stack.Children.Add(value);
        card.Child = stack;
        Grid.SetColumn(card, column);
        return card;
    }

    private void RefreshReportSummary()
    {
        if (_reportAssessment is null || _reportCoverage is null || _reportGeneratedAt is null || _reportReason is null) return;
        var report = _viewModel.BuildReportSnapshot();
        _reportAssessment.Text = report.Assessment;
        _reportCoverage.Text = report.Coverage;
        _reportGeneratedAt.Text = report.GeneratedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        _reportReason.Text = report.AssessmentReason;
    }
}
