using System.Collections;
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
    private TextBlock? _processUpdateText;
    private TextBlock? _processCountText;
    private ListBox? _hardwareCategories;
    private ICollectionView? _hardwareView;
    private TextBlock? _hardwareTitle;
    private string _processKindFilter = "모두";
    private string _processSearch = string.Empty;
    private readonly Dictionary<string, Button> _processTabs = [];

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
        page.Margin = new Thickness(8, 6, 2, 0);
        page.Children.Clear();
        page.RowDefinitions.Clear();
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition());
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = PageHeader("프로세스", "앱, 백그라운드 작업과 Windows 구성 요소의 실시간 리소스 사용량", out var headerRight);
        headerRight.Children.Add(Summary("CPU", nameof(ViewModels.TelemetryV4ViewModel.CpuValue), 96));
        headerRight.Children.Add(Summary("메모리", nameof(ViewModels.TelemetryV4ViewModel.MemoryDetail), 220));
        page.Children.Add(header);

        var surface = Surface();
        surface.Margin = new Thickness(12, 0, 10, 0);
        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(62) });
        body.RowDefinitions.Add(new RowDefinition());
        body.Children.Add(CreateProcessTabs());

        var toolbar = new Grid { Background = new SolidColorBrush(Color.FromRgb(250, 250, 251)), Margin = new Thickness(0, 0, 0, 1) };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition());
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var live = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 0, 20, 0), VerticalAlignment = VerticalAlignment.Center };
        live.Children.Add(new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(Color.FromRgb(40, 166, 102)), Margin = new Thickness(0, 0, 9, 0) });
        _processUpdateText = new TextBlock { Text = "실시간 데이터 준비 중", Foreground = Muted(), FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        live.Children.Add(_processUpdateText);
        toolbar.Children.Add(live);

        var tools = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
        tools.Children.Add(CreateSearchBox());
        var refresh = Action("새로 고침", async (_, _) => { await RefreshProcessesAsync(); RefreshProcessTabs(); }, "#F1F2F4", "#24262B");
        refresh.Margin = new Thickness(8, 0, 8, 0);
        refresh.Padding = new Thickness(14, 7, 14, 7);
        tools.Children.Add(refresh);
        var end = Action("작업 끝내기", TerminateProcess_Click, "#24262B", "#FFFFFF");
        end.Margin = new Thickness(0);
        end.Padding = new Thickness(14, 7, 14, 7);
        tools.Children.Add(end);
        Grid.SetColumn(tools, 1);
        toolbar.Children.Add(tools);
        Grid.SetRow(toolbar, 1);
        body.Children.Add(toolbar);

        _processGrid = CreateProcessGrid();
        var view = CollectionViewSource.GetDefaultView(_processes);
        view.Filter = ProcessFilter;
        Grid.SetRow(_processGrid, 2);
        body.Children.Add(_processGrid);
        surface.Child = body;
        Grid.SetRow(surface, 1);
        page.Children.Add(surface);

        var footer = new Grid { Margin = new Thickness(18, 12, 14, 6) };
        footer.ColumnDefinitions.Add(new ColumnDefinition());
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(new TextBlock { Text = "보호된 시스템 및 경로를 확인할 수 없는 프로세스는 종료할 수 없습니다.", Foreground = Muted(), FontSize = 10 });
        _processCountText = new TextBlock { Foreground = Muted(), FontSize = 10 };
        Grid.SetColumn(_processCountText, 1);
        footer.Children.Add(_processCountText);
        Grid.SetRow(footer, 2);
        page.Children.Add(footer);

        _processTimer?.Stop();
        _processTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _processTimer.Tick += async (_, _) =>
        {
            if (_processPage?.Visibility != Visibility.Visible) return;
            await RefreshProcessesAsync();
            if (_processUpdateText is not null) _processUpdateText.Text = $"실시간 · {DateTime.Now:HH:mm:ss} 갱신";
            RefreshProcessTabs();
        };
        _processTimer.Start();
        RefreshProcessTabs();
    }

    private Grid CreateProcessTabs()
    {
        var bar = new Grid { Background = Brushes.White, Margin = new Thickness(12, 8, 12, 5) };
        bar.ColumnDefinitions.Add(new ColumnDefinition());
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var kind in new[] { "모두", "앱", "백그라운드 프로세스", "Windows 프로세스" })
        {
            var button = new Button { Tag = kind, Padding = new Thickness(14, 7, 14, 7), MinHeight = 34, BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 4, 0), FontSize = 11 };
            button.Click += ProcessTab_Click;
            _processTabs[kind] = button;
            tabs.Children.Add(button);
        }
        bar.Children.Add(tabs);
        var hint = new TextBlock { Text = "열 머리글을 눌러 정렬", Foreground = Muted(), FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        Grid.SetColumn(hint, 1);
        bar.Children.Add(hint);
        return bar;
    }

    private FrameworkElement CreateSearchBox()
    {
        var grid = new Grid { Width = 300, Height = 38 };
        var search = new TextBox { Padding = new Thickness(37, 7, 12, 7), ToolTip = "이름, PID, 유형, 보호 상태 또는 실행 경로 검색" };
        var icon = new TextBlock { Text = "\uE721", FontFamily = new FontFamily("Segoe MDL2 Assets"), Foreground = Muted(), FontSize = 13, Margin = new Thickness(13, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        var placeholder = new TextBlock { Text = "이름 또는 PID 검색", Foreground = new SolidColorBrush(Color.FromRgb(142, 145, 152)), Margin = new Thickness(37, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        search.TextChanged += (_, _) => { _processSearch = search.Text; placeholder.Visibility = string.IsNullOrEmpty(search.Text) ? Visibility.Visible : Visibility.Collapsed; CollectionViewSource.GetDefaultView(_processes).Refresh(); UpdateVisibleProcessCount(); };
        grid.Children.Add(search);
        grid.Children.Add(icon);
        grid.Children.Add(placeholder);
        return grid;
    }

    private void ProcessTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string kind }) return;
        _processKindFilter = kind;
        CollectionViewSource.GetDefaultView(_processes).Refresh();
        RefreshProcessTabs();
    }

    private bool ProcessFilter(object value)
    {
        if (value is not ProcessItem item) return false;
        if (_processKindFilter != "모두" && item.Kind != _processKindFilter) return false;
        return string.IsNullOrWhiteSpace(_processSearch) || $"{item.Name} {item.Id} {item.Kind} {item.Classification} {item.Detail}".Contains(_processSearch, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshProcessTabs()
    {
        foreach (var pair in _processTabs)
        {
            var count = pair.Key == "모두" ? _processes.Count : _processes.Count(item => item.Kind == pair.Key);
            pair.Value.Content = $"{pair.Key}  {count:N0}";
            var active = pair.Key == _processKindFilter;
            pair.Value.Background = new SolidColorBrush(active ? Color.FromRgb(231, 237, 245) : Colors.Transparent);
            pair.Value.Foreground = new SolidColorBrush(active ? Color.FromRgb(26, 72, 124) : Color.FromRgb(72, 74, 80));
            pair.Value.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
        }
        CollectionViewSource.GetDefaultView(_processes).Refresh();
        UpdateVisibleProcessCount();
    }

    private void UpdateVisibleProcessCount()
    {
        if (_processCountText is not null) _processCountText.Text = $"표시 {CollectionViewSource.GetDefaultView(_processes).Cast<object>().Count():N0} / 전체 {_processes.Count:N0}";
    }

    private DataGrid CreateProcessGrid()
    {
        var grid = BaseGrid(_processes, 56);
        grid.SelectionMode = DataGridSelectionMode.Single;
        grid.Columns.Add(ProcessNameColumn());
        grid.Columns.Add(new DataGridTextColumn { Header = "상태", Binding = new Binding("Classification"), SortMemberPath = "Classification", Width = 130 });
        grid.Columns.Add(UsageColumn("CPU", "CpuPercent", "{0:0.0}%", 105, "cpu"));
        grid.Columns.Add(UsageColumn("메모리", "MemoryMb", "{0:N0} MB", 135, "memory"));
        grid.Columns.Add(new DataGridTextColumn { Header = "PID", Binding = new Binding("Id"), SortMemberPath = "Id", Width = 90 });
        return grid;
    }

    private static DataGridTemplateColumn ProcessNameColumn()
    {
        var root = new FrameworkElementFactory(typeof(Grid));
        var icon = new FrameworkElementFactory(typeof(Border));
        icon.SetValue(Border.WidthProperty, 30d); icon.SetValue(Border.HeightProperty, 30d); icon.SetValue(Border.CornerRadiusProperty, new CornerRadius(7)); icon.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(235, 238, 243))); icon.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Left); icon.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Center);
        var glyph = new FrameworkElementFactory(typeof(TextBlock)); glyph.SetValue(TextBlock.TextProperty, "\uECAA"); glyph.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe MDL2 Assets")); glyph.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(76, 82, 92))); glyph.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center); glyph.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center); icon.AppendChild(glyph); root.AppendChild(icon);
        var stack = new FrameworkElementFactory(typeof(StackPanel)); stack.SetValue(StackPanel.MarginProperty, new Thickness(43, 0, 8, 0)); stack.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
        var name = new FrameworkElementFactory(typeof(TextBlock)); name.SetBinding(TextBlock.TextProperty, new Binding("Name")); name.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold); name.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis); stack.AppendChild(name);
        var kind = new FrameworkElementFactory(typeof(TextBlock)); kind.SetBinding(TextBlock.TextProperty, new Binding("Kind")); kind.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(113, 116, 123))); kind.SetValue(TextBlock.FontSizeProperty, 10d); kind.SetValue(TextBlock.MarginProperty, new Thickness(0, 3, 0, 0)); stack.AppendChild(kind); root.AppendChild(stack);
        return new DataGridTemplateColumn { Header = "이름", CellTemplate = new DataTemplate { VisualTree = root }, SortMemberPath = "Name", Width = new DataGridLength(1, DataGridLengthUnitType.Star) };
    }

    private static DataGridTemplateColumn UsageColumn(string header, string path, string format, double width, string parameter)
    {
        var cell = new FrameworkElementFactory(typeof(Border));
        cell.SetValue(Border.PaddingProperty, new Thickness(12, 0, 16, 0));
        cell.SetValue(Border.BackgroundProperty, new Binding(path) { Converter = new UsageHeatBrushConverter(), ConverterParameter = parameter });
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right);
        text.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        text.SetBinding(TextBlock.TextProperty, new Binding(path) { StringFormat = format });
        cell.AppendChild(text);
        return new DataGridTemplateColumn { Header = header, CellTemplate = new DataTemplate { VisualTree = cell }, SortMemberPath = path, Width = width };
    }

    private void RedesignHardwarePage()
    {
        HardwarePage.Margin = new Thickness(8, 6, 2, 0);
        HardwarePage.Children.Clear();
        HardwarePage.RowDefinitions.Clear();
        HardwarePage.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        HardwarePage.RowDefinitions.Add(new RowDefinition());
        HardwarePage.Children.Add(PageHeader("하드웨어", "장치 유형별로 정리한 현재 PC의 구성과 세부 사양", out _));

        var content = new Grid { Margin = new Thickness(12, 0, 10, 12) };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(255) });
        content.ColumnDefinitions.Add(new ColumnDefinition());
        var categories = Surface();
        categories.Background = new SolidColorBrush(Color.FromRgb(250, 250, 251));
        categories.Padding = new Thickness(4, 10, 4, 10);
        categories.Margin = new Thickness(0, 0, 12, 0);
        _hardwareCategories = new ListBox { BorderThickness = new Thickness(0), Background = Brushes.Transparent, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        if (Application.Current.TryFindResource(typeof(ListBoxItem)) is Style itemStyle) _hardwareCategories.ItemContainerStyle = itemStyle;
        _hardwareCategories.SelectionChanged += HardwareCategoryChanged;
        categories.Child = _hardwareCategories;
        content.Children.Add(categories);

        var detailsSurface = Surface();
        Grid.SetColumn(detailsSurface, 1);
        var detail = new Grid();
        detail.RowDefinitions.Add(new RowDefinition { Height = new GridLength(78) });
        detail.RowDefinitions.Add(new RowDefinition());
        var detailHeader = new Grid { Margin = new Thickness(24, 0, 22, 0) };
        detailHeader.ColumnDefinitions.Add(new ColumnDefinition());
        detailHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _hardwareTitle = new TextBlock { Text = "장치", FontSize = 20, FontWeight = FontWeights.SemiBold };
        titleStack.Children.Add(_hardwareTitle);
        titleStack.Children.Add(new TextBlock { Text = "선택한 유형의 감지된 장치", Foreground = Muted(), FontSize = 10, Margin = new Thickness(0, 4, 0, 0) });
        detailHeader.Children.Add(titleStack);
        var source = new Border { Background = new SolidColorBrush(Color.FromRgb(243, 244, 246)), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 6, 10, 6), VerticalAlignment = VerticalAlignment.Center };
        source.Child = new TextBlock { Text = "CIM · LOCAL API", FontFamily = new FontFamily("Consolas"), FontSize = 9, Foreground = Muted() };
        Grid.SetColumn(source, 1);
        detailHeader.Children.Add(source);
        detail.Children.Add(detailHeader);
        var details = BaseGrid(null, 54);
        details.Columns.Add(new DataGridTextColumn { Header = "장치", Binding = new Binding("Name"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        details.Columns.Add(new DataGridTextColumn { Header = "세부 정보", Binding = new Binding("Value"), Width = new DataGridLength(3, DataGridLengthUnitType.Star) });
        Grid.SetRow(details, 1);
        detail.Children.Add(details);
        detailsSurface.Child = detail;
        content.Children.Add(detailsSurface);
        Grid.SetRow(content, 1);
        HardwarePage.Children.Add(content);

        _hardwareView = new ListCollectionView((IList)_viewModel.HardwareItems);
        details.ItemsSource = _hardwareView;
        _viewModel.HardwareItems.CollectionChanged += (_, _) => UpdateHardwareCategories();
        UpdateHardwareCategories();
    }

    private static Grid PageHeader(string title, string subtitle, out StackPanel right)
    {
        var header = new Grid { Margin = new Thickness(14, 12, 18, 22) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new StackPanel();
        text.Children.Add(new TextBlock { Text = title, FontSize = 26, FontWeight = FontWeights.SemiBold });
        text.Children.Add(new TextBlock { Text = subtitle, Foreground = Muted(), Margin = new Thickness(1, 7, 0, 0) });
        header.Children.Add(text);
        right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
        Grid.SetColumn(right, 1);
        header.Children.Add(right);
        return header;
    }

    private FrameworkElement Summary(string label, string binding, double width)
    {
        var stack = new StackPanel { Margin = new Thickness(22, 0, 2, 0), Width = width };
        stack.Children.Add(new TextBlock { Text = label, Foreground = Muted(), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Right });
        var value = new TextBlock { FontSize = 18, FontWeight = FontWeights.SemiBold, FontFamily = new FontFamily("Consolas"), HorizontalAlignment = HorizontalAlignment.Stretch, TextAlignment = TextAlignment.Right, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 5, 0, 0) };
        value.SetBinding(TextBlock.TextProperty, new Binding(binding));
        stack.Children.Add(value);
        return stack;
    }

    private static Border Surface() => new() { Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(228, 229, 233)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), ClipToBounds = true };

    private static DataGrid BaseGrid(IEnumerable? source, double rowHeight)
    {
        var grid = new DataGrid { ItemsSource = source, AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, CanUserSortColumns = true, RowHeight = rowHeight, ColumnHeaderHeight = 46, HeadersVisibility = DataGridHeadersVisibility.Column, GridLinesVisibility = DataGridGridLinesVisibility.None, Background = Brushes.White, BorderThickness = new Thickness(0), RowHeaderWidth = 0 };
        if (Application.Current.TryFindResource(typeof(DataGridRow)) is Style rowStyle) grid.RowStyle = rowStyle;
        if (Application.Current.TryFindResource(typeof(DataGridCell)) is Style cellStyle) grid.CellStyle = cellStyle;
        if (Application.Current.TryFindResource(typeof(DataGridColumnHeader)) is Style headerStyle) grid.ColumnHeaderStyle = headerStyle;
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
        control.Resources[SystemColors.HighlightBrushKey] = new SolidColorBrush(Color.FromRgb(234, 236, 240));
        control.Resources[SystemColors.HighlightTextBrushKey] = new SolidColorBrush(Color.FromRgb(27, 27, 31));
        control.Resources[SystemColors.InactiveSelectionHighlightBrushKey] = new SolidColorBrush(Color.FromRgb(240, 241, 243));
        control.Resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = new SolidColorBrush(Color.FromRgb(27, 27, 31));
    }

    private static Brush Muted() => new SolidColorBrush(Color.FromRgb(104, 106, 112));
}
