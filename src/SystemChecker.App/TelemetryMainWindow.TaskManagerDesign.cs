using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using SystemChecker.Converters;
using SystemChecker.Models;

namespace SystemChecker;

public partial class TelemetryMainWindow
{
    private TextBlock? _processUpdateText;
    private ListBox? _hardwareCategories;
    private ICollectionView? _hardwareView;
    private TextBlock? _hardwareTitle;

    private void ApplyTaskManagerDesign()
    {
        ReorderNavigation();
        RedesignProcessPage();
        RedesignHardwarePage();
    }

    private void ReorderNavigation()
    {
        if (_processNav?.Parent is not StackPanel navigation) return;
        navigation.Children.Remove(_processNav);
        navigation.Children.Insert(Math.Min(1, navigation.Children.Count), _processNav);
    }

    private void RedesignProcessPage()
    {
        if (_processPage is not Grid page) return;
        page.Children.Clear();
        page.RowDefinitions.Clear();
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition());
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = PageHeader("프로세스", "실행 중인 앱과 백그라운드 프로세스의 리소스 사용량", out var headerRight);
        headerRight.Children.Add(Summary("전체 CPU", nameof(ViewModels.TelemetryV4ViewModel.CpuValue)));
        headerRight.Children.Add(Summary("전체 메모리", nameof(ViewModels.TelemetryV4ViewModel.MemoryDetail)));
        page.Children.Add(header);

        var surface = new Border { Background = Brushes.White, BorderBrush = Line(), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Margin = new Thickness(20, 0, 16, 0) };
        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(58) });
        body.RowDefinitions.Add(new RowDefinition());
        var toolbar = new Grid { Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)) };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition());
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var live = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(18, 0, 18, 0), VerticalAlignment = VerticalAlignment.Center };
        live.Children.Add(new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(Color.FromRgb(32, 163, 93)), Margin = new Thickness(0, 0, 8, 0) });
        _processUpdateText = new TextBlock { Text = "실시간 · 1초마다 갱신", Foreground = Muted(), VerticalAlignment = VerticalAlignment.Center };
        live.Children.Add(_processUpdateText);
        toolbar.Children.Add(live);
        var tools = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        var search = new TextBox { Width = 270, Height = 34, Padding = new Thickness(11, 6, 11, 6), Margin = new Thickness(0, 0, 8, 0), ToolTip = "프로세스명, PID, 분류 또는 경로 검색" };
        tools.Children.Add(search);
        tools.Children.Add(Action("새로 고침", async (_, _) => await RefreshProcessesAsync(), "#E7EAEE", "#20252B"));
        tools.Children.Add(Action("작업 끝내기", TerminateProcess_Click, "#20252B", "#FFFFFF"));
        Grid.SetColumn(tools, 1);
        toolbar.Children.Add(tools);
        body.Children.Add(toolbar);

        _processGrid = CreateProcessGrid();
        var view = CollectionViewSource.GetDefaultView(_processes);
        view.Filter = value => value is Services.ProcessItem item && (string.IsNullOrWhiteSpace(search.Text) || $"{item.Name} {item.Id} {item.Classification} {item.Detail}".Contains(search.Text, StringComparison.OrdinalIgnoreCase));
        search.TextChanged += (_, _) => view.Refresh();
        Grid.SetRow(_processGrid, 1);
        body.Children.Add(_processGrid);
        surface.Child = body;
        Grid.SetRow(surface, 1);
        page.Children.Add(surface);

        var footer = new TextBlock { Text = "Windows 핵심·Windows 폴더·경로 미확인 프로세스는 CoreWatch에서 종료할 수 없습니다.", Foreground = Muted(), FontSize = 10, Margin = new Thickness(24, 12, 18, 8) };
        Grid.SetRow(footer, 2);
        page.Children.Add(footer);
        _processTimer?.Stop();
        _processTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _processTimer.Tick += async (_, _) =>
        {
            if (_processPage?.Visibility != Visibility.Visible) return;
            if (_processUpdateText is not null) _processUpdateText.Text = $"실시간 · {DateTime.Now:HH:mm:ss} 갱신";
            await RefreshProcessesAsync();
        };
        _processTimer.Start();
    }

    private DataGrid CreateProcessGrid()
    {
        var grid = BaseGrid(_processes, 43);
        grid.SelectionMode = DataGridSelectionMode.Single;
        grid.Columns.Add(new DataGridTextColumn { Header = "이름", Binding = new Binding("Name"), SortMemberPath = "Name", Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "PID", Binding = new Binding("Id"), SortMemberPath = "Id", Width = 75 });
        grid.Columns.Add(UsageColumn("CPU", "CpuPercent", "0.0' %'", 95, "cpu"));
        grid.Columns.Add(UsageColumn("메모리", "MemoryMb", "N0' MB'", 120, "memory"));
        grid.Columns.Add(new DataGridTextColumn { Header = "상태", Binding = new Binding("Classification"), SortMemberPath = "Classification", Width = 135 });
        grid.Columns.Add(new DataGridTextColumn { Header = "실행 경로 / 보호 판단", Binding = new Binding("Detail"), SortMemberPath = "Detail", Width = new DataGridLength(3, DataGridLengthUnitType.Star) });
        return grid;
    }

    private static DataGridTemplateColumn UsageColumn(string header, string path, string format, double width, string parameter)
    {
        var cell = new FrameworkElementFactory(typeof(Border));
        cell.SetValue(Border.PaddingProperty, new Thickness(10, 0, 12, 0));
        cell.SetValue(Border.BackgroundProperty, new Binding(path) { Converter = new UsageHeatBrushConverter(), ConverterParameter = parameter });
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right);
        text.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        text.SetBinding(TextBlock.TextProperty, new Binding(path) { StringFormat = format });
        cell.AppendChild(text);
        return new DataGridTemplateColumn { Header = header, CellTemplate = new DataTemplate { VisualTree = cell }, SortMemberPath = path, Width = width };
    }

    private void ProcessCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            if (_processUpdateText is not null) _processUpdateText.Text = $"실시간 · {DateTime.Now:HH:mm:ss} 갱신";
            CollectionViewSource.GetDefaultView(_processes).Refresh();
        });
    }

    private void RedesignHardwarePage()
    {
        HardwarePage.Children.Clear();
        HardwarePage.RowDefinitions.Clear();
        HardwarePage.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        HardwarePage.RowDefinitions.Add(new RowDefinition());
        HardwarePage.Children.Add(PageHeader("하드웨어", "현재 시스템의 장치 구성과 세부 사양", out _));

        var content = new Grid { Margin = new Thickness(20, 0, 16, 14) };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(235) });
        content.ColumnDefinitions.Add(new ColumnDefinition());
        var categorySurface = new Border { Background = Brushes.White, BorderBrush = Line(), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Padding = new Thickness(0, 10, 0, 10), Margin = new Thickness(0, 0, 10, 0) };
        _hardwareCategories = new ListBox { BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(7), HorizontalContentAlignment = HorizontalAlignment.Stretch };
        NeutralSelection(_hardwareCategories);
        _hardwareCategories.SelectionChanged += HardwareCategoryChanged;
        categorySurface.Child = _hardwareCategories;
        content.Children.Add(categorySurface);

        var detailSurface = new Border { Background = Brushes.White, BorderBrush = Line(), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5) };
        Grid.SetColumn(detailSurface, 1);
        var detail = new Grid();
        detail.RowDefinitions.Add(new RowDefinition { Height = new GridLength(72) });
        detail.RowDefinitions.Add(new RowDefinition());
        var detailHeader = new Grid { Margin = new Thickness(22, 0, 18, 0) };
        detailHeader.ColumnDefinitions.Add(new ColumnDefinition());
        detailHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _hardwareTitle = new TextBlock { Text = "장치", FontSize = 19, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        detailHeader.Children.Add(_hardwareTitle);
        var source = new TextBlock { Text = "CIM · LOCAL API", FontFamily = new FontFamily("Consolas"), FontSize = 9, Foreground = Muted(), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(source, 1);
        detailHeader.Children.Add(source);
        detail.Children.Add(detailHeader);
        var details = BaseGrid(null, 50);
        details.BorderThickness = new Thickness(0, 1, 0, 0);
        details.Columns.Add(new DataGridTextColumn { Header = "장치", Binding = new Binding("Name"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        details.Columns.Add(new DataGridTextColumn { Header = "세부 정보", Binding = new Binding("Value"), Width = new DataGridLength(3, DataGridLengthUnitType.Star) });
        Grid.SetRow(details, 1);
        detail.Children.Add(details);
        detailSurface.Child = detail;
        content.Children.Add(detailSurface);
        Grid.SetRow(content, 1);
        HardwarePage.Children.Add(content);

        _hardwareView = new ListCollectionView((IList)_viewModel.HardwareItems);
        details.ItemsSource = _hardwareView;
        _viewModel.HardwareItems.CollectionChanged += (_, _) => UpdateHardwareCategories();
        UpdateHardwareCategories();
    }

    private static Grid PageHeader(string title, string subtitle, out StackPanel right)
    {
        var header = new Grid { Margin = new Thickness(24, 18, 20, 16) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new StackPanel();
        text.Children.Add(new TextBlock { Text = title, FontSize = 24, FontWeight = FontWeights.SemiBold });
        text.Children.Add(new TextBlock { Text = subtitle, Foreground = Muted(), Margin = new Thickness(0, 5, 0, 0) });
        header.Children.Add(text);
        right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
        Grid.SetColumn(right, 1);
        header.Children.Add(right);
        return header;
    }

    private FrameworkElement Summary(string label, string binding)
    {
        var stack = new StackPanel { Margin = new Thickness(16, 0, 16, 0), MinWidth = 125 };
        stack.Children.Add(new TextBlock { Text = label, Foreground = Muted(), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Right });
        var value = new TextBlock { FontFamily = new FontFamily("Consolas"), FontSize = 18, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 4, 0, 0) };
        value.SetBinding(TextBlock.TextProperty, new Binding(binding));
        stack.Children.Add(value);
        return stack;
    }

    private static DataGrid BaseGrid(IEnumerable? source, double rowHeight)
    {
        var grid = new DataGrid { ItemsSource = source, AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, CanUserSortColumns = true, RowHeight = rowHeight, HeadersVisibility = DataGridHeadersVisibility.Column, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(235, 237, 240)), BorderThickness = new Thickness(0) };
        NeutralSelection(grid);
        return grid;
    }

    private void UpdateHardwareCategories()
    {
        if (_hardwareCategories is null) return;
        var selected = _hardwareCategories.SelectedItem?.ToString();
        var categories = _viewModel.HardwareItems.Select(item => item.Category).Distinct().ToList();
        _hardwareCategories.ItemsSource = categories;
        if (categories.Count > 0) _hardwareCategories.SelectedItem = selected is not null && categories.Contains(selected) ? selected : categories[0];
    }

    private void HardwareCategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        var category = _hardwareCategories?.SelectedItem?.ToString();
        if (_hardwareView is null) return;
        _hardwareView.Filter = value => value is HardwareItem item && item.Category == category;
        _hardwareView.Refresh();
        if (_hardwareTitle is not null) _hardwareTitle.Text = category ?? "장치";
    }

    private static void NeutralSelection(ItemsControl control)
    {
        control.Resources[SystemColors.HighlightBrushKey] = new SolidColorBrush(Color.FromRgb(231, 235, 239));
        control.Resources[SystemColors.HighlightTextBrushKey] = new SolidColorBrush(Color.FromRgb(24, 27, 32));
        control.Resources[SystemColors.InactiveSelectionHighlightBrushKey] = new SolidColorBrush(Color.FromRgb(238, 240, 243));
        control.Resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = new SolidColorBrush(Color.FromRgb(24, 27, 32));
    }

    private static Brush Muted() => new SolidColorBrush(Color.FromRgb(104, 112, 125));
    private static Brush Line() => new SolidColorBrush(Color.FromRgb(222, 226, 232));
}



