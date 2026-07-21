using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using SystemChecker.Converters;
using SystemChecker.Models;
using SystemChecker.Services;

namespace SystemChecker;

public partial class TelemetryMainWindow
{
    private void ApplyNextSessionFixes()
    {
        ConfigureNavigationNumbers();
        ConfigureProcessPresentation();
        ConfigureHardwarePresentation();
        ConfigureIncrementalProcessRefresh();
    }

    private void ConfigureNavigationNumbers()
    {
        if (_processNav?.Parent is not StackPanel navigation) return;
        var index = 1;
        foreach (var button in navigation.Children.OfType<Button>())
        {
            if (button.Content is StackPanel panel && panel.Children.OfType<TextBlock>().FirstOrDefault() is { } number)
                number.Text = index.ToString("00");
            index++;
        }
    }

    private void ConfigureProcessPresentation()
    {
        if (_processGrid is null) return;
        _processGrid.EnableRowVirtualization = true;
        _processGrid.SetValue(VirtualizingPanel.IsVirtualizingProperty, true);
        _processGrid.SetValue(VirtualizingPanel.VirtualizationModeProperty, VirtualizationMode.Recycling);
        _processGrid.Columns.Clear();
        _processGrid.Columns.Add(CreateProcessNameColumn());
        _processGrid.Columns.Add(CreateCenteredTextColumn("상태", "Classification", "Classification", 130));
        _processGrid.Columns.Add(UsageColumn("CPU", "CpuPercent", "{0:0.0}%", 105, "cpu"));
        _processGrid.Columns.Add(UsageColumn("메모리", "MemoryMb", "{0:N0} MB", 135, "memory"));
        _processGrid.Columns.Add(CreateCenteredTextColumn("PID", "Id", "Id", 90));

        if (CollectionViewSource.GetDefaultView(_processes) is ICollectionViewLiveShaping live && live.CanChangeLiveSorting)
        {
            live.LiveSortingProperties.Clear();
            live.LiveSortingProperties.Add(nameof(ProcessItem.CpuPercent));
            live.LiveSortingProperties.Add(nameof(ProcessItem.MemoryMb));
            live.IsLiveSorting = true;
        }
    }

    private static DataGridTemplateColumn CreateProcessNameColumn()
    {
        var root = new FrameworkElementFactory(typeof(Grid));
        root.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Stretch);

        var iconShell = new FrameworkElementFactory(typeof(Border));
        iconShell.SetValue(FrameworkElement.WidthProperty, 32d);
        iconShell.SetValue(FrameworkElement.HeightProperty, 32d);
        iconShell.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
        iconShell.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(239, 241, 244)));
        iconShell.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        iconShell.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

        var iconContent = new FrameworkElementFactory(typeof(Grid));
        var fallback = new FrameworkElementFactory(typeof(TextBlock));
        fallback.SetValue(TextBlock.TextProperty, "\uECAA");
        fallback.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe MDL2 Assets"));
        fallback.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(94, 99, 108)));
        fallback.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        fallback.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        iconContent.AppendChild(fallback);

        var image = new FrameworkElementFactory(typeof(Image));
        image.SetBinding(Image.SourceProperty, new Binding(nameof(ProcessItem.Icon)));
        image.SetValue(FrameworkElement.WidthProperty, 24d);
        image.SetValue(FrameworkElement.HeightProperty, 24d);
        image.SetValue(Image.StretchProperty, Stretch.Uniform);
        image.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        image.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        iconContent.AppendChild(image);
        iconShell.AppendChild(iconContent);
        root.AppendChild(iconShell);

        var text = new FrameworkElementFactory(typeof(StackPanel));
        text.SetValue(FrameworkElement.MarginProperty, new Thickness(44, 0, 8, 0));
        text.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        var name = new FrameworkElementFactory(typeof(TextBlock));
        name.SetBinding(TextBlock.TextProperty, new Binding(nameof(ProcessItem.Name)));
        name.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        name.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        text.AppendChild(name);
        var kind = new FrameworkElementFactory(typeof(TextBlock));
        kind.SetBinding(TextBlock.TextProperty, new Binding(nameof(ProcessItem.Kind)));
        kind.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(113, 116, 123)));
        kind.SetValue(TextBlock.FontSizeProperty, 10d);
        kind.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 3, 0, 0));
        text.AppendChild(kind);
        root.AppendChild(text);

        return new DataGridTemplateColumn
        {
            Header = "이름",
            CellTemplate = new DataTemplate { VisualTree = root },
            SortMemberPath = nameof(ProcessItem.Name),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        };
    }

    private static DataGridTemplateColumn CreateCenteredTextColumn(string header, string path, string sortPath, DataGridLength width)
    {
        var root = new FrameworkElementFactory(typeof(Grid));
        root.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Stretch);
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding(path));
        text.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        text.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        root.AppendChild(text);
        return new DataGridTemplateColumn { Header = header, CellTemplate = new DataTemplate { VisualTree = root }, SortMemberPath = sortPath, Width = width };
    }

    private void ConfigureHardwarePresentation()
    {
        var grid = Descendants<DataGrid>(HardwarePage).FirstOrDefault(candidate =>
            candidate.Columns.Any(column => Equals(column.Header, "장치")) &&
            candidate.Columns.Any(column => Equals(column.Header, "세부 정보")));
        if (grid is null) return;
        grid.Columns.Clear();
        grid.Columns.Add(CreateCenteredTextColumn("장치", nameof(HardwareItem.Name), nameof(HardwareItem.Name), new DataGridLength(2, DataGridLengthUnitType.Star)));
        grid.Columns.Add(CreateCenteredTextColumn("세부 정보", nameof(HardwareItem.Value), nameof(HardwareItem.Value), new DataGridLength(3, DataGridLengthUnitType.Star)));
    }

    private void ConfigureIncrementalProcessRefresh()
    {
        _processTimer?.Stop();
        _processTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _processTimer.Tick += async (_, _) =>
        {
            if (_processPage?.Visibility != Visibility.Visible) return;
            await RefreshProcessesIncrementallyAsync();
        };
        _processTimer.Start();

        var oldRefresh = _processPage is null
            ? null
            : Descendants<Button>(_processPage).FirstOrDefault(button => Equals(button.Content, "새로 고침"));
        if (oldRefresh?.Parent is StackPanel parent)
        {
            var index = parent.Children.IndexOf(oldRefresh);
            var replacement = Action("새로 고침", async (_, _) => await RefreshProcessesIncrementallyAsync(), "#F1F2F4", "#24262B");
            replacement.Margin = oldRefresh.Margin;
            replacement.Padding = oldRefresh.Padding;
            parent.Children.RemoveAt(index);
            parent.Children.Insert(index, replacement);
        }
    }

    private async Task RefreshProcessesIncrementallyAsync()
    {
        if (_processRefreshing) return;
        _processRefreshing = true;
        try
        {
            var latest = await _processService.CaptureAsync(_monitorCancellation.Token);
            var latestById = latest.ToDictionary(item => item.Id);

            for (var index = _processes.Count - 1; index >= 0; index--)
            {
                var current = _processes[index];
                if (!latestById.TryGetValue(current.Id, out var item))
                {
                    _processes.RemoveAt(index);
                    continue;
                }

                if (CanUpdateInPlace(current, item)) current.UpdateUsage(item);
                else _processes[index] = item;
                latestById.Remove(current.Id);
            }

            foreach (var item in latest.Where(item => latestById.ContainsKey(item.Id)))
                _processes.Add(item);

            UpdateProcessSummaryWithoutRefresh();
        }
        catch (OperationCanceledException) { }
        finally { _processRefreshing = false; }
    }

    private static bool CanUpdateInPlace(ProcessItem current, ProcessItem latest) =>
        current.Name == latest.Name &&
        current.Kind == latest.Kind &&
        current.Classification == latest.Classification &&
        current.Detail == latest.Detail &&
        current.CanTerminate == latest.CanTerminate;

    private void UpdateProcessSummaryWithoutRefresh()
    {
        foreach (var pair in _processTabs)
        {
            var count = pair.Key == "모두" ? _processes.Count : _processes.Count(item => item.Kind == pair.Key);
            pair.Value.Content = $"{pair.Key}  {count:N0}";
        }
        if (_processUpdateText is not null) _processUpdateText.Text = $"실시간 · {DateTime.Now:HH:mm:ss} 갱신";
        UpdateVisibleProcessCount();
    }
}


