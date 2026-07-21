using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using SystemChecker.Services;

namespace SystemChecker;

public partial class TelemetryMainWindow
{
    private readonly StartupManagementService _startupManagementService = new();
    private Button? _startupToggleButton;
    private Button? _startupRestoreButton;

    private static DataGridTemplateColumn CreateStartupSelectionColumn()
    {
        var checkBox = new FrameworkElementFactory(typeof(CheckBox));
        checkBox.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(ManagedStartupEntry.IsSelected)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        checkBox.SetBinding(UIElement.IsEnabledProperty, new Binding(nameof(ManagedStartupEntry.CanChange)));
        checkBox.SetBinding(FrameworkElement.ToolTipProperty, new Binding(nameof(ManagedStartupEntry.Restriction)));
        checkBox.SetValue(FrameworkElement.WidthProperty, 22d);
        checkBox.SetValue(FrameworkElement.HeightProperty, 22d);
        checkBox.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        checkBox.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        checkBox.SetValue(UIElement.FocusableProperty, false);
        checkBox.SetValue(Control.TemplateProperty, CreateCleanupCheckBoxTemplate());
        return new DataGridTemplateColumn { Header = "선택", CellTemplate = new DataTemplate { VisualTree = checkBox }, Width = 74 };
    }

    private static Style CreateStartupRowStyle()
    {
        var basedOn = Application.Current.TryFindResource(typeof(DataGridRow)) as Style;
        var style = new Style(typeof(DataGridRow), basedOn);
        var selected = new DataTrigger { Binding = new Binding(nameof(ManagedStartupEntry.IsSelected)), Value = true };
        selected.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(242, 247, 252))));
        style.Triggers.Add(selected);
        var disabled = new DataTrigger { Binding = new Binding(nameof(ManagedStartupEntry.IsEnabled)), Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, .62));
        style.Triggers.Add(disabled);
        return style;
    }

    private async void ToggleSelectedStartup_Click(object sender, RoutedEventArgs e)
    {
        if (_optimizationBusy) return;
        var selected = _startupEntries.Where(item => item.IsSelected).ToList();
        var changeable = selected.Where(item => item.CanChange).ToList();
        if (changeable.Count == 0)
        {
            var reason = selected.FirstOrDefault()?.Restriction ?? "항목을 먼저 선택하세요.";
            MessageBox.Show(this, reason, "시작 프로그램", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var enable = changeable.Count(item => !item.IsEnabled);
        var disable = changeable.Count - enable;
        var message = $"선택한 시작 프로그램 {changeable.Count}개의 상태를 전환합니다.\n활성화 {enable}개 · 비활성화 {disable}개\n\n현재 상태는 자동 백업됩니다. 계속할까요?";
        if (MessageBox.Show(this, message, "시작 프로그램 변경 확인", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _optimizationBusy = true;
        try
        {
            if (_optimizationStatus is not null) _optimizationStatus.Text = "시작 프로그램 상태를 변경하는 중…";
            var result = await _startupManagementService.ToggleAsync(changeable, _monitorCancellation.Token);
            await ReloadStartupEntriesAsync();
            if (_optimizationStatus is not null) _optimizationStatus.Text = result.Message;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "시작 프로그램 변경 실패", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { _optimizationBusy = false; }
    }

    private async void RestoreStartupChanges_Click(object sender, RoutedEventArgs e)
    {
        if (_optimizationBusy || !_startupManagementService.CanRestore) return;
        if (MessageBox.Show(this, "마지막 시작 프로그램 변경을 이전 상태로 복원할까요?", "시작 프로그램 복원", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _optimizationBusy = true;
        try
        {
            if (_optimizationStatus is not null) _optimizationStatus.Text = "시작 프로그램 변경을 복원하는 중…";
            var result = await _startupManagementService.RestoreLatestAsync(_monitorCancellation.Token);
            await ReloadStartupEntriesAsync();
            if (_optimizationStatus is not null) _optimizationStatus.Text = result.Message;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "시작 프로그램 복원 실패", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { _optimizationBusy = false; }
    }

    private async Task ReloadStartupEntriesAsync()
    {
        var entries = await _startupManagementService.AnalyzeAsync(_monitorCancellation.Token);
        _startupEntries.Clear();
        foreach (var item in entries) _startupEntries.Add(item);
        UpdateStartupSummary();
        if (_startupRestoreButton is not null) _startupRestoreButton.IsEnabled = _startupManagementService.CanRestore;
    }

    private void UpdateStartupSummary()
    {
        if (_startupSummary is null) return;
        _startupSummary.Text = $"활성 {_startupEntries.Count(item => item.IsEnabled):N0}개 · 비활성 {_startupEntries.Count(item => !item.IsEnabled):N0}개 · 변경 가능 {_startupEntries.Count(item => item.CanChange):N0}개";
    }
}

