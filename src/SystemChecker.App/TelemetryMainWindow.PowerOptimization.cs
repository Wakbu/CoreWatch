using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SystemChecker.Services;

namespace SystemChecker;

public partial class TelemetryMainWindow
{
    private readonly PowerSettingsService _powerSettingsService = new();
    private readonly Dictionary<string, Button> _powerProfileButtons = [];
    private TextBlock? _powerPlanValue;
    private TextBlock? _powerSourceValue;
    private TextBlock? _powerCpuValue;
    private TextBlock? _powerBoostValue;
    private TextBlock? _powerRecommendation;
    private TextBlock? _powerRecommendationReason;
    private Button? _powerRestoreButton;
    private PowerProfileAnalysis? _powerAnalysis;

    private FrameworkElement CreatePowerAnalysisPanel()
    {
        var surface = Surface();
        surface.Margin = new Thickness(12, 0, 10, 12);
        surface.Padding = new Thickness(20, 0, 20, 18);
        var body = new StackPanel();

        var header = new StackPanel { Height = 72, Margin = new Thickness(0, 16, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        header.Children.Add(new TextBlock { Text = "전원·성능 설정", FontSize = 16, FontWeight = FontWeights.SemiBold });
        header.Children.Add(new TextBlock { Text = "현재 전원 계획과 CPU 제한을 진단합니다. 설정 변경은 확인 후 실행되며 이전 계획으로 복원할 수 있습니다.", Foreground = Muted(), FontSize = 10, Margin = new Thickness(0, 5, 0, 0) });
        body.Children.Add(header);

        var metrics = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        for (var i = 0; i < 4; i++) metrics.ColumnDefinitions.Add(new ColumnDefinition());
        metrics.Children.Add(PowerMetric("현재 계획", out _powerPlanValue, 0));
        metrics.Children.Add(PowerMetric("전원 상태", out _powerSourceValue, 1));
        metrics.Children.Add(PowerMetric("CPU 최소–최대", out _powerCpuValue, 2));
        metrics.Children.Add(PowerMetric("CPU 부스트", out _powerBoostValue, 3));
        body.Children.Add(metrics);

        var recommendation = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(244, 248, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(217, 228, 239)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(16, 13, 16, 13)
        };
        var recommendationBody = new StackPanel();
        _powerRecommendation = new TextBlock { Text = "전원 설정 분석 전", FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(34, 78, 120)) };
        _powerRecommendationReason = new TextBlock { Text = "분석을 시작하면 용도에 맞는 안전한 프로필을 추천합니다.", Foreground = Muted(), FontSize = 10, Margin = new Thickness(0, 5, 0, 0), TextWrapping = TextWrapping.Wrap };
        recommendationBody.Children.Add(_powerRecommendation);
        recommendationBody.Children.Add(_powerRecommendationReason);
        recommendation.Child = recommendationBody;
        body.Children.Add(recommendation);

        var actions = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        actions.ColumnDefinitions.Add(new ColumnDefinition());
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var profiles = new StackPanel { Orientation = Orientation.Horizontal };
        profiles.Children.Add(CreatePowerProfileButton("balanced", "균형 조정"));
        profiles.Children.Add(CreatePowerProfileButton("performance", "고성능"));
        profiles.Children.Add(CreatePowerProfileButton("saver", "절전"));
        actions.Children.Add(profiles);
        _powerRestoreButton = Action("이전 설정 복원", async (_, _) => await RestorePowerSettingsAsync(), "#F1F2F4", "#24262B");
        _powerRestoreButton.IsEnabled = _powerSettingsService.CanRestore;
        Grid.SetColumn(_powerRestoreButton, 1);
        actions.Children.Add(_powerRestoreButton);
        body.Children.Add(actions);

        surface.Child = body;
        return surface;
    }

    private static Border PowerMetric(string title, out TextBlock value, int column)
    {
        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(249, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(229, 232, 237)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(column == 0 ? 0 : 5, 0, column == 3 ? 0 : 5, 0),
            Padding = new Thickness(14, 12, 14, 12)
        };
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = title, Foreground = Muted(), FontSize = 10 });
        value = new TextBlock { Text = "분석 전", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 7, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = "분석 전" };
        stack.Children.Add(value);
        card.Child = stack;
        Grid.SetColumn(card, column);
        return card;
    }

    private Button CreatePowerProfileButton(string key, string label)
    {
        var button = Action(label, async (_, _) => await ApplyPowerProfileAsync(key), "#E7EEF7", "#234D7A");
        button.IsEnabled = false;
        _powerProfileButtons[key] = button;
        return button;
    }

    private void RenderPowerAnalysis(PowerProfileAnalysis analysis)
    {
        _powerAnalysis = analysis;
        SetPowerMetric(_powerPlanValue, analysis.ActiveSchemeName);
        SetPowerMetric(_powerSourceValue, analysis.PowerSourceText);
        SetPowerMetric(_powerCpuValue, analysis.CpuLimitsText);
        SetPowerMetric(_powerBoostValue, analysis.BoostText);
        if (_powerRecommendation is not null) _powerRecommendation.Text = analysis.Recommendation;
        if (_powerRecommendationReason is not null) _powerRecommendationReason.Text = analysis.RecommendationReason + " GPU 전력 제한은 제조사 드라이버 정책에 따라 달라지므로 CoreWatch가 임의로 변경하지 않습니다.";

        foreach (var profile in analysis.Profiles)
        {
            if (!_powerProfileButtons.TryGetValue(profile.Key, out var button)) continue;
            button.IsEnabled = profile.IsAvailable && !profile.IsActive;
            button.Content = profile.IsActive ? $"{profile.Name} · 사용 중" : profile.IsRecommended ? $"{profile.Name} · 권장" : profile.Name;
            button.ToolTip = profile.IsAvailable ? profile.Description : "이 PC에 해당 Windows 전원 계획이 없습니다.";
        }
        if (_powerRestoreButton is not null) _powerRestoreButton.IsEnabled = _powerSettingsService.CanRestore;
    }

    private static void SetPowerMetric(TextBlock? target, string value)
    {
        if (target is null) return;
        target.Text = value;
        target.ToolTip = value;
    }

    private async Task ApplyPowerProfileAsync(string key)
    {
        if (_optimizationBusy || _powerAnalysis is null) return;
        var profile = _powerAnalysis.Profiles.FirstOrDefault(item => item.Key == key);
        if (profile is null || !profile.IsAvailable || profile.IsActive) return;
        var warning = $"'{profile.Name}' 전원 계획을 적용합니다.\n현재 계획은 자동으로 백업되며 '이전 설정 복원'으로 되돌릴 수 있습니다. 계속할까요?";
        if (MessageBox.Show(this, warning, "전원 계획 변경 확인", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        _optimizationBusy = true;
        try
        {
            if (_optimizationStatus is not null) _optimizationStatus.Text = $"{profile.Name} 전원 계획을 적용하는 중…";
            var result = await _powerSettingsService.ApplyProfileAsync(key, _monitorCancellation.Token);
            RenderPowerAnalysis(result.Analysis);
            if (_optimizationStatus is not null) _optimizationStatus.Text = result.Message;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "전원 설정 변경 실패", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { _optimizationBusy = false; }
    }

    private async Task RestorePowerSettingsAsync()
    {
        if (_optimizationBusy || !_powerSettingsService.CanRestore) return;
        if (MessageBox.Show(this, "마지막 전원 설정 백업으로 복원할까요?", "전원 설정 복원", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _optimizationBusy = true;
        try
        {
            if (_optimizationStatus is not null) _optimizationStatus.Text = "이전 전원 계획을 복원하는 중…";
            var result = await _powerSettingsService.RestoreLatestAsync(_monitorCancellation.Token);
            RenderPowerAnalysis(result.Analysis);
            if (_optimizationStatus is not null) _optimizationStatus.Text = result.Message;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "전원 설정 복원 실패", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { _optimizationBusy = false; }
    }
}

