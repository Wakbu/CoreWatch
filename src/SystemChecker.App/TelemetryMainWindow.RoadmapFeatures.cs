using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SystemChecker.Services;

namespace SystemChecker;

public partial class TelemetryMainWindow
{
    private readonly AutomaticUpdateService _updateService = new();
    private readonly StorageMaintenanceService _storageMaintenanceService = new();
    private readonly NetworkDiagnosticsService _networkDiagnosticsService = new();
    private readonly ObservableCollection<NetworkAdapterDiagnostic> _networkAdapters = [];
    private readonly ObservableCollection<LatencyDiagnostic> _networkLatencies = [];
    private readonly Dictionary<string, Button> _recommendationPurposeButtons = [];
    private IReadOnlyList<NetworkAdapterDiagnostic> _allNetworkAdapters = [];
    private UpdateRelease? _latestUpdate;
    private string? _downloadedUpdatePath;
    private TextBlock? _storageSenseValue;
    private TextBlock? _reservedStorageValue;
    private TextBlock? _maintenanceDriveValue;
    private TextBlock? _maintenanceRecommendation;
    private TextBlock? _networkStatus;
    private TextBox? _networkTarget;
    private Button? _networkAdapterToggle;
    private TextBlock? _recommendationScore;
    private TextBlock? _recommendationItems;
    private string _recommendationPurpose = "일반";
    private int _networkWarningCount;
    private bool _updateCheckStarted;
    private bool _updateBusy;
    private bool _networkAdaptersExpanded;

    private void InitializeUpdateNotification()
    {
        UpdateBadgeButton.Visibility = Visibility.Collapsed;
        Loaded += async (_, _) =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), _monitorCancellation.Token);
                await CheckUpdateNotificationAsync();
            }
            catch (OperationCanceledException) { }
        };
    }

    private async Task CheckUpdateNotificationAsync()
    {
        if (_updateCheckStarted) return;
        _updateCheckStarted = true;
        try
        {
            _latestUpdate = await _updateService.CheckAsync(_monitorCancellation.Token);
            var setup = _latestUpdate.Assets.FirstOrDefault(item => item.Name.EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase));
            if (!_latestUpdate.IsNewer || setup is null || string.IsNullOrWhiteSpace(setup.Digest)) return;
            UpdateBadgeButton.Content = $"업데이트 v{_latestUpdate.Version}";
            UpdateBadgeButton.ToolTip = "SHA-256 검증 후 CoreWatch 설치 파일 다운로드";
            UpdateBadgeButton.Visibility = Visibility.Visible;
        }
        catch
        {
            UpdateBadgeButton.Visibility = Visibility.Collapsed;
        }
    }

    private async void UpdateBadge_Click(object sender, RoutedEventArgs e) => await DownloadOrOpenUpdateAsync();

    private FrameworkElement CreateStorageMaintenancePanel()
    {
        var surface = FeatureSurface("Windows 저장소 설정", "저장소 센스와 예약 저장소 상태를 읽기 전용으로 확인하고 Windows 설정 화면을 엽니다.", out var body);
        var metrics = new Grid();
        for (var i = 0; i < 3; i++) metrics.ColumnDefinitions.Add(new ColumnDefinition());
        metrics.Children.Add(RoadmapMetric("저장소 센스", out _storageSenseValue, 0, 3));
        metrics.Children.Add(RoadmapMetric("예약 저장소", out _reservedStorageValue, 1, 3));
        metrics.Children.Add(RoadmapMetric("시스템 드라이브", out _maintenanceDriveValue, 2, 3));
        body.Children.Add(metrics);
        _maintenanceRecommendation = new TextBlock { Text = "상태 확인 전", Foreground = Muted(), Margin = new Thickness(0, 12, 0, 0), TextWrapping = TextWrapping.Wrap };
        body.Children.Add(_maintenanceRecommendation);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 13, 0, 0) };
        actions.Children.Add(Action("상태 새로고침", async (_, _) => await RefreshStorageMaintenanceAsync(), "#E7EEF7", "#234D7A"));
        actions.Children.Add(Action("Windows 저장소 설정", (_, _) => Process.Start(new ProcessStartInfo("ms-settings:storagesense") { UseShellExecute = true }), "#F1F2F4", "#24262B"));
        body.Children.Add(actions);
        return surface;
    }

    private FrameworkElement CreateNetworkDiagnosticsPanel()
    {
        var surface = FeatureSurface("네트워크 진단", "버튼을 누를 때만 로컬 게이트웨이와 입력 대상에 ICMP 진단을 보내고 어댑터 오류 카운터를 확인합니다.", out var body);
        var controls = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        controls.ColumnDefinitions.Add(new ColumnDefinition()); controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) }); controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _networkStatus = new TextBlock { Text = "진단 전 · 외부 지연 진단은 버튼을 누를 때만 실행", Foreground = Muted(), VerticalAlignment = VerticalAlignment.Center };
        controls.Children.Add(_networkStatus);
        _networkTarget = new TextBox { Text = "1.1.1.1", Margin = new Thickness(8, 0, 8, 0), ToolTip = "선택적 외부 지연 진단 대상" };
        Grid.SetColumn(_networkTarget, 1); controls.Children.Add(_networkTarget);
        var run = Action("네트워크 진단 실행", async (_, _) => await RefreshNetworkDiagnosticsAsync(), "#24262B", "#FFFFFF"); Grid.SetColumn(run, 2); controls.Children.Add(run);
        body.Children.Add(controls);

        var adapters = BaseGrid(_networkAdapters, 42);
        ConfigureDiagnosticGrid(adapters);
        adapters.Columns.Add(CreateCenteredTextColumn("어댑터", nameof(NetworkAdapterDiagnostic.Name), nameof(NetworkAdapterDiagnostic.Name), new DataGridLength(1, DataGridLengthUnitType.Star)));
        adapters.Columns.Add(CreateCenteredTextColumn("링크 속도", nameof(NetworkAdapterDiagnostic.Speed), nameof(NetworkAdapterDiagnostic.Speed), 100));
        adapters.Columns.Add(CreateCenteredTextColumn("수신 오류", nameof(NetworkAdapterDiagnostic.ReceiveErrors), nameof(NetworkAdapterDiagnostic.ReceiveErrors), 85));
        adapters.Columns.Add(CreateCenteredTextColumn("송신 오류", nameof(NetworkAdapterDiagnostic.SendErrors), nameof(NetworkAdapterDiagnostic.SendErrors), 85));
        adapters.Columns.Add(CreateCenteredTextColumn("폐기", nameof(NetworkAdapterDiagnostic.Discards), nameof(NetworkAdapterDiagnostic.Discards), 75));
        adapters.Columns.Add(CreateCenteredTextColumn("판정", nameof(NetworkAdapterDiagnostic.Assessment), nameof(NetworkAdapterDiagnostic.Assessment), 105));
        body.Children.Add(adapters);
        _networkAdapterToggle = Action("모두 보기", (_, _) =>
        {
            _networkAdaptersExpanded = !_networkAdaptersExpanded;
            RenderNetworkAdapters();
        }, "#F1F2F4", "#24262B");
        _networkAdapterToggle.Visibility = Visibility.Collapsed;
        _networkAdapterToggle.HorizontalAlignment = HorizontalAlignment.Left;
        _networkAdapterToggle.Margin = new Thickness(0, 8, 0, 0);
        _networkAdapterToggle.Padding = new Thickness(12, 6, 12, 6);
        body.Children.Add(_networkAdapterToggle);

        var latency = BaseGrid(_networkLatencies, 42);
        ConfigureDiagnosticGrid(latency);
        latency.Height = 130;
        latency.Margin = new Thickness(0, 10, 0, 0);
        latency.Columns.Add(CreateCenteredTextColumn("대상", nameof(LatencyDiagnostic.Target), nameof(LatencyDiagnostic.Target), new DataGridLength(1, DataGridLengthUnitType.Star)));
        latency.Columns.Add(CreateCenteredTextColumn("송신", nameof(LatencyDiagnostic.Sent), nameof(LatencyDiagnostic.Sent), 70));
        latency.Columns.Add(CreateCenteredTextColumn("응답", nameof(LatencyDiagnostic.Received), nameof(LatencyDiagnostic.Received), 70));
        latency.Columns.Add(CreateCenteredTextColumn("손실률", nameof(LatencyDiagnostic.LossPercent), nameof(LatencyDiagnostic.LossPercent), 85));
        latency.Columns.Add(CreateCenteredTextColumn("평균 ms", nameof(LatencyDiagnostic.AverageMs), nameof(LatencyDiagnostic.AverageMs), 85));
        latency.Columns.Add(CreateCenteredTextColumn("판정", nameof(LatencyDiagnostic.Assessment), nameof(LatencyDiagnostic.Assessment), 105));
        body.Children.Add(latency);
        return surface;
    }

    internal static void ConfigureDiagnosticGrid(DataGrid grid)
    {
        grid.Tag = "CustomExpandable";
        grid.CanUserResizeColumns = false;
        grid.CanUserResizeRows = false;
        grid.CanUserReorderColumns = false;
        grid.CanUserAddRows = false;
        grid.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        grid.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
    }

    private FrameworkElement CreatePersonalizedRecommendationPanel()
    {
        var surface = FeatureSurface("개인화 추천", "PC 구성과 선택한 사용 목적, 로컬 진단 결과만 조합해 적합도와 우선 개선 항목을 계산합니다.", out var body);
        var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var purposeShell = new Border { Background = new SolidColorBrush(Color.FromRgb(244, 245, 247)), BorderBrush = new SolidColorBrush(Color.FromRgb(226, 228, 232)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(3) };
        var purposes = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var purpose in new[] { "일반", "게임", "콘텐츠 제작", "저전력" })
        {
            var button = new Button { Content = purpose, Height = 32, MinHeight = 32, Padding = new Thickness(13, 0, 13, 0), Margin = new Thickness(1, 0, 1, 0), BorderThickness = new Thickness(0), FontSize = 10, Cursor = System.Windows.Input.Cursors.Hand };
            button.Click += (_, _) => SelectRecommendationPurpose(purpose);
            _recommendationPurposeButtons[purpose] = button;
            purposes.Children.Add(button);
        }
        purposeShell.Child = purposes;
        row.Children.Add(purposeShell);
        _recommendationScore = new TextBlock { Text = "추천 계산 전", FontWeight = FontWeights.SemiBold, Margin = new Thickness(16, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(_recommendationScore, 1); row.Children.Add(_recommendationScore);
        var calculate = Action("추천 계산", async (_, _) => await CalculateRecommendationAsync(), "#24262B", "#FFFFFF"); Grid.SetColumn(calculate, 2); row.Children.Add(calculate);
        body.Children.Add(row);
        _recommendationItems = new TextBlock { Text = "용도를 선택하고 추천 계산을 누르세요.", Foreground = Muted(), Margin = new Thickness(0, 14, 0, 0), TextWrapping = TextWrapping.Wrap, LineHeight = 21 };
        body.Children.Add(_recommendationItems);
        SelectRecommendationPurpose("일반");
        return surface;
    }

    private void SelectRecommendationPurpose(string purpose)
    {
        _recommendationPurpose = purpose;
        foreach (var pair in _recommendationPurposeButtons)
        {
            var selected = pair.Key == purpose;
            pair.Value.Background = selected ? new SolidColorBrush(Color.FromRgb(36, 38, 43)) : Brushes.Transparent;
            pair.Value.Foreground = selected ? Brushes.White : new SolidColorBrush(Color.FromRgb(77, 81, 89));
            pair.Value.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    private static Border FeatureSurface(string title, string description, out StackPanel body)
    {
        var surface = Surface(); surface.Margin = new Thickness(12, 0, 10, 12); surface.Padding = new Thickness(20, 17, 20, 18);
        body = new StackPanel(); body.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold }); body.Children.Add(new TextBlock { Text = description, Foreground = Muted(), FontSize = 10, Margin = new Thickness(0, 5, 0, 14), TextWrapping = TextWrapping.Wrap }); surface.Child = body; return surface;
    }

    private static Border RoadmapMetric(string title, out TextBlock value, int column, int count)
    {
        var card = new Border { Background = new SolidColorBrush(Color.FromRgb(249, 250, 252)), BorderBrush = new SolidColorBrush(Color.FromRgb(229, 232, 237)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Margin = new Thickness(column == 0 ? 0 : 5, 0, column == count - 1 ? 0 : 5, 0), Padding = new Thickness(14, 12, 14, 12) };
        var stack = new StackPanel(); stack.Children.Add(new TextBlock { Text = title, Foreground = Muted(), FontSize = 10 }); value = new TextBlock { Text = "확인 전", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 7, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis }; stack.Children.Add(value); card.Child = stack; Grid.SetColumn(card, column); return card;
    }

    private async Task DownloadOrOpenUpdateAsync()
    {
        if (!string.IsNullOrWhiteSpace(_downloadedUpdatePath) && File.Exists(_downloadedUpdatePath))
        {
            if (MessageBox.Show(this, "검증된 설치 프로그램을 실행할까요? 실행 후 설치 화면에서 다시 확인할 수 있습니다.", "CoreWatch 업데이트", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) Process.Start(new ProcessStartInfo(_downloadedUpdatePath) { UseShellExecute = true });
            return;
        }
        var asset = _latestUpdate?.Assets.FirstOrDefault(item => item.Name.EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase));
        if (_updateBusy || _latestUpdate is null || asset is null) return;
        if (MessageBox.Show(this, $"v{_latestUpdate.Version} 설치 파일을 다운로드하고 GitHub SHA-256으로 검증할까요?\n다운로드만 수행하며 자동 실행하지 않습니다.", "업데이트 다운로드", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _updateBusy = true;
        UpdateBadgeButton.IsEnabled = false;
        try
        {
            var progress = new Progress<double>(value => UpdateBadgeButton.Content = $"다운로드 {value:P0}");
            _downloadedUpdatePath = await _updateService.DownloadVerifiedAsync(_latestUpdate, asset, progress, _monitorCancellation.Token);
            UpdateBadgeButton.Content = "설치 준비 완료";
            UpdateBadgeButton.ToolTip = _downloadedUpdatePath;
        }
        catch (Exception ex)
        {
            UpdateBadgeButton.Content = $"업데이트 v{_latestUpdate.Version}";
            MessageBox.Show(this, ex.Message, "업데이트 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { _updateBusy = false; UpdateBadgeButton.IsEnabled = true; }
    }

    private async Task RefreshStorageMaintenanceAsync()
    {
        var result = await _storageMaintenanceService.CaptureAsync(_monitorCancellation.Token);
        if (_storageSenseValue is not null) _storageSenseValue.Text = result.StorageSense;
        if (_reservedStorageValue is not null) _reservedStorageValue.Text = result.ReservedStorage;
        if (_maintenanceDriveValue is not null) _maintenanceDriveValue.Text = result.SystemDrive;
        if (_maintenanceRecommendation is not null) _maintenanceRecommendation.Text = result.Recommendation;
    }

    private void RenderNetworkAdapters()
    {
        const int collapsedCount = 8;
        _networkAdapters.Clear();
        foreach (var item in (_networkAdaptersExpanded ? _allNetworkAdapters : _allNetworkAdapters.Take(collapsedCount))) _networkAdapters.Add(item);
        if (_networkAdapterToggle is null) return;
        _networkAdapterToggle.Visibility = _allNetworkAdapters.Count > collapsedCount ? Visibility.Visible : Visibility.Collapsed;
        _networkAdapterToggle.Content = _networkAdaptersExpanded ? "접기" : $"모두 보기 · {_allNetworkAdapters.Count}개";
    }
    private async Task RefreshNetworkDiagnosticsAsync()
    {
        if (_optimizationBusy) return; _optimizationBusy = true;
        try
        {
            if (_networkStatus is not null) _networkStatus.Text = "네트워크 진단 중…";
            var result = await _networkDiagnosticsService.CaptureAsync(_networkTarget?.Text, _monitorCancellation.Token);
            _allNetworkAdapters = result.Adapters; _networkAdaptersExpanded = false; RenderNetworkAdapters();
            _networkLatencies.Clear(); foreach (var item in result.Latencies) _networkLatencies.Add(item);
            _networkWarningCount = result.Adapters.Count(item => item.Assessment != "정상") + result.Latencies.Count(item => item.Assessment != "정상");
            if (_networkStatus is not null) _networkStatus.Text = result.Summary;
        }
        catch (Exception ex) { if (_networkStatus is not null) _networkStatus.Text = $"진단 실패 · {ex.Message}"; }
        finally { _optimizationBusy = false; }
    }

    private async Task CalculateRecommendationAsync()
    {
        var memory = await _memoryAnalysisService.CaptureAsync(_monitorCancellation.Token);
        var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\");
        var free = drive.TotalSize > 0 ? drive.AvailableFreeSpace * 100d / drive.TotalSize : 0;
        var storageWarnings = _storageDiagnostics.Count(item => item.Assessment != "정상");
        var input = new RecommendationInput(_recommendationPurpose, Environment.ProcessorCount, memory.PhysicalTotalBytes / 1024d / 1024 / 1024, free, _networkWarningCount, storageWarnings);
        var result = PersonalizedRecommendationService.Calculate(input);
        if (_recommendationScore is not null) _recommendationScore.Text = result.Summary;
        if (_recommendationItems is not null) _recommendationItems.Text = string.Join("\n", result.Items.Select(item => $"› {item}"));
    }
}
