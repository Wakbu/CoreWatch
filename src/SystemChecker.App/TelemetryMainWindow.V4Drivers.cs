using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace SystemChecker;

public partial class TelemetryMainWindow
{
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        if (ReportPage.Children.OfType<Border>().Any(x => Equals(x.Tag, "driver-diagnostics"))) return;
        var border = new Border { Tag = "driver-diagnostics", Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(222, 226, 232)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Padding = new Thickness(18), Margin = new Thickness(0, 12, 0, 0) };
        var stack = new StackPanel(); border.Child = stack;
        stack.Children.Add(new TextBlock { Text = "DRIVER / UNKNOWN DEVICE DIAGNOSTICS", FontSize = 11, FontWeight = FontWeights.SemiBold });
        stack.Children.Add(new TextBlock { Text = "장치 관리자 오류 코드가 있는 PnP 장치를 표시합니다. 목록이 비어 있으면 감지된 오류가 없습니다.", Foreground = new SolidColorBrush(Color.FromRgb(104, 112, 125)), FontSize = 10, Margin = new Thickness(0, 5, 0, 10) });
        var grid = new DataGrid { ItemsSource = _viewModel.DriverDiagnostics, AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, Height = 180 };
        grid.Columns.Add(new DataGridTextColumn { Header = "상태", Binding = new Binding("Severity"), Width = 75 });
        grid.Columns.Add(new DataGridTextColumn { Header = "장치", Binding = new Binding("Title"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "진단", Binding = new Binding("Detail"), Width = new DataGridLength(3, DataGridLengthUnitType.Star) });
        stack.Children.Add(grid); ReportPage.Children.Add(border);
    }
}

