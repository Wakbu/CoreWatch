using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SystemChecker;

public partial class TelemetryMainWindow
{
    private Icon? _coreWatchTrayIcon;
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);Title="CoreWatch";foreach(var text in Descendants<TextBlock>(this)){if(text.Text=="SYSTEM CHECKER")text.Text="COREWATCH";else if(text.Text=="SC")text.Text="CW";}
        var iconPath=Path.Combine(AppContext.BaseDirectory,"Assets","CoreWatch.ico");if(File.Exists(iconPath)){_coreWatchTrayIcon=new Icon(iconPath);_tray.Icon=_coreWatchTrayIcon;}_tray.Text="CoreWatch · Local Telemetry";Closed+=(_,_)=>_coreWatchTrayIcon?.Dispose();
        ImproveThermalLayout();ImproveHardwareGrid();WrapReportInScroller();EnhanceBenchmarkReference();EnhanceReportContents();InitializeProcessPage();
    }
    private void ImproveThermalLayout()
    {
        var temperature=Descendants<TextBlock>(this).FirstOrDefault(t=>BindingOperations.GetBinding(t,TextBlock.TextProperty)?.Path.Path=="TemperatureValue");if(temperature?.Parent is StackPanel stack&&stack.Parent is Grid grid&&grid.ColumnDefinitions.Count>=3){grid.ColumnDefinitions[0].Width=new GridLength(245);temperature.FontSize=15;var existing=grid.Children.OfType<Button>().FirstOrDefault(b=>Equals(b.Content,"센서 권한 재시작"));if(existing is not null){grid.Children.Remove(existing);var actions=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(12,0,0,0)};Grid.SetColumn(actions,2);existing.Margin=new Thickness(0,0,8,0);actions.Children.Add(existing);var pawn=Action("PawnIO 센서 드라이버",PawnIo_Click,"#E4E7EB","#303640");pawn.ToolTip="Windows 메모리 무결성과 호환되는 공식 저수준 센서 드라이버 설치 페이지";actions.Children.Add(pawn);grid.Children.Add(actions);}}
    }
    private void ImproveHardwareGrid(){var grid=Descendants<DataGrid>(HardwarePage).FirstOrDefault();if(grid is null)return;grid.Margin=new Thickness(6,0,6,0);var cell=new Style(typeof(DataGridCell));cell.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(22,0,16,0)));cell.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty,VerticalAlignment.Center));cell.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(0)));grid.CellStyle=cell;var header=new Style(typeof(DataGridColumnHeader));header.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(22,0,16,0)));header.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty,VerticalAlignment.Center));header.Setters.Add(new Setter(Control.BackgroundProperty,new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(242,244,246))));header.Setters.Add(new Setter(Control.HeightProperty,44d));grid.ColumnHeaderStyle=header;}
    private void WrapReportInScroller(){if(ReportPage.Parent is not Grid host)return;host.Children.Remove(ReportPage);ReportPage.HorizontalAlignment=HorizontalAlignment.Stretch;ReportPage.MaxWidth=double.PositiveInfinity;var scroll=new ScrollViewer{Content=ReportPage,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};scroll.SetBinding(VisibilityProperty,new Binding("Visibility"){Source=ReportPage,Mode=BindingMode.OneWay});host.Children.Add(scroll);}
    private static void PawnIo_Click(object sender,RoutedEventArgs e)=>Process.Start(new ProcessStartInfo("https://github.com/namazso/PawnIO.Setup/releases/latest"){UseShellExecute=true});
}

