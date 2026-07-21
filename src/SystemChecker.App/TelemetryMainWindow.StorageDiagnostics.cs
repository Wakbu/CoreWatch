using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SystemChecker.Services;

namespace SystemChecker;

public partial class TelemetryMainWindow
{
    private readonly StorageDiagnosticsService _storageDiagnosticsService = new();
    private readonly ObservableCollection<StorageVolumeDiagnostic> _storageDiagnostics = [];
    private TextBlock? _storageDiagnosticsStatus;

    private FrameworkElement CreateStorageDiagnosticsPanel()
    {
        var surface = Surface(); surface.Margin = new Thickness(12, 0, 10, 12); surface.Padding = new Thickness(20, 0, 20, 18); var body = new StackPanel(); var header = new Grid { Height = 76 }; header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; title.Children.Add(new TextBlock { Text = "저장장치 상태", FontSize = 16, FontWeight = FontWeights.SemiBold }); title.Children.Add(new TextBlock { Text = "TRIM, 볼륨 여유 공간과 파일 시스템 dirty bit를 읽기 전용으로 진단합니다.", Foreground = Muted(), FontSize = 10, Margin = new Thickness(0, 5, 0, 0) }); header.Children.Add(title); var refresh = Action("저장장치 새로고침", async (_, _) => await RefreshStorageDiagnosticsAsync(), "#F1F2F4", "#24262B"); refresh.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(refresh, 1); header.Children.Add(refresh); body.Children.Add(header);
        _storageDiagnosticsStatus = new TextBlock { Text = "저장장치 분석 전", Foreground = Muted(), Margin = new Thickness(0, 0, 0, 10) }; body.Children.Add(_storageDiagnosticsStatus); var grid = BaseGrid(_storageDiagnostics, 48); grid.Height = 260; grid.Columns.Add(CreateCenteredTextColumn("볼륨", nameof(StorageVolumeDiagnostic.Volume), nameof(StorageVolumeDiagnostic.Volume), 75)); grid.Columns.Add(CreateCenteredTextColumn("파일 시스템", nameof(StorageVolumeDiagnostic.FileSystem), nameof(StorageVolumeDiagnostic.FileSystem), 100)); grid.Columns.Add(CreateCenteredTextColumn("여유 공간", nameof(StorageVolumeDiagnostic.FreeSpace), nameof(StorageVolumeDiagnostic.FreePercent), 120)); grid.Columns.Add(CreateCenteredTextColumn("파일 시스템 상태", nameof(StorageVolumeDiagnostic.FileSystemStatus), nameof(StorageVolumeDiagnostic.FileSystemStatus), 135)); grid.Columns.Add(CreateCenteredTextColumn("TRIM", nameof(StorageVolumeDiagnostic.TrimStatus), nameof(StorageVolumeDiagnostic.TrimStatus), 90)); grid.Columns.Add(CreateCenteredTextColumn("판정", nameof(StorageVolumeDiagnostic.Assessment), nameof(StorageVolumeDiagnostic.Assessment), 100)); grid.Columns.Add(CreateCenteredTextColumn("권장 사항", nameof(StorageVolumeDiagnostic.Recommendation), nameof(StorageVolumeDiagnostic.Recommendation), new DataGridLength(2, DataGridLengthUnitType.Star))); body.Children.Add(grid); surface.Child = body; return surface;
    }

    private async Task<StorageDiagnosticsResult> CaptureStorageDiagnosticsAsync(CancellationToken token) { var result = await _storageDiagnosticsService.CaptureAsync(token); RenderStorageDiagnostics(result); return result; }
    private async Task RefreshStorageDiagnosticsAsync() { if (_optimizationBusy) return; _optimizationBusy = true; try { if (_optimizationStatus is not null) _optimizationStatus.Text = "저장장치 상태를 분석하는 중…"; var result = await CaptureStorageDiagnosticsAsync(_monitorCancellation.Token); if (_optimizationStatus is not null) _optimizationStatus.Text = result.Summary; } catch (OperationCanceledException) { } finally { _optimizationBusy = false; } }
    private void RenderStorageDiagnostics(StorageDiagnosticsResult result) { _storageDiagnostics.Clear(); foreach (var item in result.Volumes) _storageDiagnostics.Add(item); if (_storageDiagnosticsStatus is not null) _storageDiagnosticsStatus.Text = result.Summary; }
}
