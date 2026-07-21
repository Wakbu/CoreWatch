using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace SystemChecker;

public partial class TelemetryMainWindow
{
    private Icon? _coreWatchTrayIcon;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Title = "CoreWatch";
        foreach (var text in Descendants<TextBlock>(this))
        {
            if (text.Text == "SYSTEM CHECKER") text.Text = "COREWATCH";
            else if (text.Text == "SC") text.Text = "CW";
        }
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "CoreWatch.ico");
        if (File.Exists(iconPath)) { _coreWatchTrayIcon = new Icon(iconPath); _tray.Icon = _coreWatchTrayIcon; }
        _tray.Text = "CoreWatch · Local Telemetry";
        Closed += (_, _) => _coreWatchTrayIcon?.Dispose();
        IncreaseWorkspaceSpacing();
        ImproveThermalLayout();
        WrapReportInScroller();
        EnhanceBenchmarkReference();
        EnhanceReportContents();
        InitializeProcessPage();
        ApplyTaskManagerDesign();
        InitializeOptimizationPage();
        ApplyNextSessionFixes();
        ApplyGridAlignment();
    }

    private void IncreaseWorkspaceSpacing()
    {
        if (OverviewPage.Parent is Grid workspace)
            workspace.Margin = new Thickness(28, 22, 22, 20);
        foreach (var scroll in Descendants<ScrollViewer>(this))
        {
            scroll.PanningMode = PanningMode.VerticalOnly;
            scroll.SetValue(ScrollViewer.IsDeferredScrollingEnabledProperty, false);
        }
    }

    private void ApplyGridAlignment()
    {
        var cellStyle = Application.Current.TryFindResource(typeof(DataGridCell)) as Style;
        var headerStyle = Application.Current.TryFindResource(typeof(DataGridColumnHeader)) as Style;
        var rowStyle = Application.Current.TryFindResource(typeof(DataGridRow)) as Style;
        foreach (var grid in Descendants<DataGrid>(this))
        {
            if (cellStyle is not null) grid.CellStyle = cellStyle;
            if (headerStyle is not null) grid.ColumnHeaderStyle = headerStyle;
            if (rowStyle is not null) grid.RowStyle = rowStyle;
            grid.VerticalContentAlignment = VerticalAlignment.Center;
        }
    }

    private void ImproveThermalLayout()
    {
        var temperature = Descendants<TextBlock>(this).FirstOrDefault(text => BindingOperations.GetBinding(text, TextBlock.TextProperty)?.Path.Path == "TemperatureValue");
        if (temperature?.Parent is not StackPanel stack || stack.Parent is not Grid grid || grid.ColumnDefinitions.Count < 3) return;
        grid.ColumnDefinitions[0].Width = new GridLength(245);
        temperature.FontSize = 15;
        var restart = grid.Children.OfType<Button>().FirstOrDefault(button => Equals(button.Content, "센서 권한 재시작"));
        if (restart is not null) grid.Children.Remove(restart);
        var badge = new Border { Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 244, 247)), CornerRadius = new CornerRadius(7), Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        badge.Child = new TextBlock { Text = "PawnIO · 설치 시 자동 구성", Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(75, 80, 88)), FontSize = 10 };
        Grid.SetColumn(badge, 2);
        grid.Children.Add(badge);
    }

    private void WrapReportInScroller()
    {
        if (ReportPage.Parent is not Grid host) return;
        host.Children.Remove(ReportPage);
        ReportPage.HorizontalAlignment = HorizontalAlignment.Stretch;
        ReportPage.MaxWidth = double.PositiveInfinity;
        ReportPage.Margin = new Thickness(8, 8, 8, 18);
        var scroll = new ScrollViewer { Content = ReportPage, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, PanningMode = PanningMode.VerticalOnly };
        scroll.SetBinding(VisibilityProperty, new Binding("Visibility") { Source = ReportPage, Mode = BindingMode.OneWay });
        host.Children.Add(scroll);
    }
}
