using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace SystemChecker;

public partial class TelemetryMainWindow
{
    private readonly Dictionary<Button, StackPanel> _featureTrees = [];
    private ScrollViewer? _reportFeatureScroll;

    private void InitializeFeatureTreeNavigation()
    {
        if (ReportNav.Parent is not StackPanel navigation || _processNav is null || _optimizationNav is null) return;
        EnsureReportScrollHost();
        AutomationProperties.SetName(OverviewPage, "Overview content");
        AutomationProperties.SetName(BenchmarkPage, "Benchmark content");
        if (_optimizationPage is not null) AutomationProperties.SetName(_optimizationPage, "Optimization content");
        if (_reportFeatureScroll is not null) AutomationProperties.SetName(_reportFeatureScroll, "Report content");
        AddFeatureTree(navigation, OverviewNav, [("실시간 요약", () => ScrollToText(OverviewPage, "CPU LOAD")), ("성능 그래프", () => ScrollToText(OverviewPage, "CPU · 60 SEC")), ("온도·센서", () => ScrollToText(OverviewPage, "THERMALS"))]);
        AddFeatureTree(navigation, _processNav, [("전체 프로세스", () => SelectProcessTree("모두")), ("앱", () => SelectProcessTree("앱")), ("백그라운드", () => SelectProcessTree("백그라운드 프로세스")), ("Windows", () => SelectProcessTree("Windows 프로세스"))]);
        AddFeatureTree(navigation, HardwareNav, [("CPU", () => ScrollHardware("CPU")), ("GPU", () => ScrollHardware("GPU")), ("메모리", () => ScrollHardware("메모리")), ("저장장치", () => ScrollHardware("저장장치"))]);
        AddFeatureTree(navigation, BenchmarkNav, [("종합 점수", () => ScrollToText(BenchmarkPage, "SYSTEM SCORE")), ("테스트 실행", () => ScrollToText(BenchmarkPage, "TEST CONTROL")), ("디스크·GPU", () => ScrollToText(BenchmarkPage, "STORAGE / GPU")), ("이전 결과", () => ScrollToText(BenchmarkPage, "PREVIOUS RESULTS")), ("내 기록 비교", () => ScrollToText(BenchmarkPage, "LOCAL BENCHMARK")), ("성능 기준", () => ScrollToText(BenchmarkPage, "PERFORMANCE REFERENCE"))]);
        AddFeatureTree(navigation, _optimizationNav, [("자동 업데이트", () => ScrollToText(_optimizationPage, "자동 업데이트")), ("Windows 저장소", () => ScrollToText(_optimizationPage, "Windows 저장소 설정")), ("네트워크 진단", () => ScrollToText(_optimizationPage, "네트워크 진단")), ("개인화 추천", () => ScrollToText(_optimizationPage, "개인화 추천")), ("전원 설정", () => ScrollToText(_optimizationPage, "전원·성능 설정")), ("백그라운드 부하", () => ScrollToText(_optimizationPage, "백그라운드")), ("메모리 분석", () => ScrollToText(_optimizationPage, "메모리 상세")), ("저장장치 상태", () => ScrollToText(_optimizationPage, "저장장치 상태")), ("시작 프로그램", () => ScrollToText(_optimizationPage, "시작 프로그램 관리")), ("공간 정리", () => ScrollToText(_optimizationPage, "저장 공간 정리"))]);
        AddFeatureTree(navigation, ReportNav, [("진단 요약", () => ScrollToText(ReportPage, "REPORT SUMMARY")), ("내보내기", () => ScrollToText(ReportPage, "REPORT EXPORT")), ("보고서 데이터", () => ScrollToText(ReportPage, "REPORT DATASET")), ("SMART 상태", () => ScrollToText(ReportPage, "SMART / STORAGE")), ("벤치마크 이력", () => ScrollToText(ReportPage, "SQLITE BENCHMARK"))]);
        foreach (var pair in _featureTrees) pair.Key.Click += (_, _) => ExpandFeatureTree(pair.Key);
        ExpandFeatureTree(OverviewNav);
    }

    private void AddFeatureTree(StackPanel navigation, Button owner, IReadOnlyList<(string Label, Action Action)> items)
    {
        AutomationProperties.SetName(owner, owner.CommandParameter?.ToString() ?? "Navigation");
        var tree = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 1, 0, 5) };
        foreach (var item in items)
        {
            var content = new Grid { Width = 150, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var arrow = new TextBlock { Text = "›", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left, FontSize = 12 };
            var label = new TextBlock { Text = item.Label, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 };
            Grid.SetColumn(label, 1); content.Children.Add(arrow); content.Children.Add(label);
            var button = new Button { Content = content, Height = 30, Padding = new Thickness(28, 0, 8, 0), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, Background = Brushes.Transparent, Foreground = new SolidColorBrush(Color.FromRgb(112, 123, 139)), BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
            AutomationProperties.SetName(button, $"기능: {item.Label}");
            button.Click += (_, e) => { e.Handled = true; item.Action(); };
            tree.Children.Add(button);
        }
        navigation.Children.Insert(navigation.Children.IndexOf(owner) + 1, tree);
        _featureTrees[owner] = tree;
    }

    private void ExpandFeatureTree(Button owner) { foreach (var pair in _featureTrees) pair.Value.Visibility = pair.Key == owner ? Visibility.Visible : Visibility.Collapsed; }
    private void SelectProcessTree(string kind) { if (_processTabs.TryGetValue(kind, out var button)) button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
    private void ScrollHardware(string category) { var grid = Descendants<DataGrid>(HardwarePage).FirstOrDefault(); var item = _viewModel.HardwareItems.FirstOrDefault(value => value.Category == category); if (grid is not null && item is not null) { grid.ScrollIntoView(item); grid.SelectedItem = item; } }
    private void EnsureReportScrollHost()
    {
        if (_reportFeatureScroll is not null || ReportPage.Parent is not Panel host) return;
        var index = host.Children.IndexOf(ReportPage); host.Children.Remove(ReportPage);
        _reportFeatureScroll = new ScrollViewer { Content = ReportPage, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _reportFeatureScroll.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(Visibility)) { Source = ReportPage, Mode = BindingMode.OneWay });
        host.Children.Insert(index, _reportFeatureScroll);
    }

    private static void ScrollToText(DependencyObject? root, string text)
    {
        if (root is null) return;
        var target = Descendants<TextBlock>(root).FirstOrDefault(item => item.Text.Contains(text, StringComparison.OrdinalIgnoreCase));
        if (target is null) return;
        ScrollViewer? scroll = null;
        for (DependencyObject? current = target; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is ScrollViewer candidate) { scroll = candidate; break; }
        if (scroll?.Content is not UIElement content) { target.BringIntoView(); return; }
        scroll.UpdateLayout();
        try
        {
            var position = target.TranslatePoint(new Point(0, 0), content);
            scroll.ScrollToVerticalOffset(Math.Max(0, position.Y - 8));
        }
        catch (InvalidOperationException) { target.BringIntoView(); }
    }
}
