using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace SystemChecker;

public partial class TelemetryMainWindow
{
    private readonly HashSet<DataGrid> _coordinatedGrids = [];
    private readonly HashSet<DataGrid> _expandableNestedGrids = [];

    private void InitializeNestedScrollCoordination()
    {
        ConfigureNestedTables(FindOuterScroll(OverviewPage));
        ConfigureNestedTables(FindOuterScroll(BenchmarkPage));
        ConfigureNestedTables(FindOuterScroll(ReportPage));
        ConfigureNestedTables(_optimizationPage);
    }

    private void ConfigureNestedTables(ScrollViewer? outer)
    {
        if (outer is null) return;
        foreach (var grid in Descendants<DataGrid>(outer).ToList())
        {
            if (!_coordinatedGrids.Add(grid)) continue;
            if (!Equals(grid.Tag, "CustomExpandable")) ConfigureExpandableNestedGrid(grid);
            grid.PreviewMouseWheel += (_, e) => ForwardTableWheel(outer, e);
        }
    }

    private void ConfigureExpandableNestedGrid(DataGrid grid)
    {
        if (!_expandableNestedGrids.Add(grid) || grid.Parent is not Panel parent) return;
        var rowHeight = grid.RowHeight > 0 && !double.IsNaN(grid.RowHeight) ? grid.RowHeight : 46;
        var headerHeight = grid.ColumnHeaderHeight > 0 && !double.IsNaN(grid.ColumnHeaderHeight) ? grid.ColumnHeaderHeight : 46;
        var originalHeight = grid.Height;
        var collapsedHeight = !double.IsNaN(originalHeight) && originalHeight > headerHeight
            ? originalHeight
            : headerHeight + rowHeight * 6;
        var visibleRows = Math.Max(1, (int)Math.Floor((collapsedHeight - headerHeight) / rowHeight));
        var expanded = false;

        var wrapper = new StackPanel { Margin = grid.Margin };
        grid.Margin = new Thickness(0);
        grid.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        Button toggle = null!;
        toggle = Action("모두 보기", (_, _) =>
        {
            expanded = !expanded;
            UpdateState();
        }, "#F1F2F4", "#24262B");
        toggle.HorizontalAlignment = HorizontalAlignment.Left;
        toggle.Margin = new Thickness(14, 8, 0, 0);
        toggle.Padding = new Thickness(12, 6, 12, 6);
        var index = parent.Children.IndexOf(grid);
        var row = Grid.GetRow(grid);
        var column = Grid.GetColumn(grid);
        var rowSpan = Grid.GetRowSpan(grid);
        var columnSpan = Grid.GetColumnSpan(grid);
        parent.Children.RemoveAt(index);
        wrapper.Children.Add(grid);
        wrapper.Children.Add(toggle);
        parent.Children.Insert(index, wrapper);
        Grid.SetRow(wrapper, row);
        Grid.SetColumn(wrapper, column);
        Grid.SetRowSpan(wrapper, rowSpan);
        Grid.SetColumnSpan(wrapper, columnSpan);
        if (parent is Grid parentGrid && row >= 0 && row < parentGrid.RowDefinitions.Count && parentGrid.RowDefinitions[row].Height.IsAbsolute)
            parentGrid.RowDefinitions[row].Height = GridLength.Auto;
        void UpdateState()
        {
            var count = grid.Items.Count;
            var needsToggle = count > visibleRows;
            if (!needsToggle) expanded = false;
            grid.Height = needsToggle && !expanded ? collapsedHeight : double.NaN;
            toggle.Visibility = needsToggle ? Visibility.Visible : Visibility.Collapsed;
            toggle.Content = expanded ? "접기" : $"모두 보기 · {count}개";
        }

        if (grid.Items is INotifyCollectionChanged changed)
            changed.CollectionChanged += (_, _) => Dispatcher.BeginInvoke(UpdateState);
        grid.Loaded += (_, _) => UpdateState();
        UpdateState();
    }

    private static void ForwardTableWheel(ScrollViewer outer, MouseWheelEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindAncestor<ScrollBar>(source) is not null) return;
        if (outer.ScrollableHeight <= 0) return;
        e.Handled = true;
        outer.ScrollToVerticalOffset(CalculateOuterScrollOffset(outer.VerticalOffset, outer.ScrollableHeight, e.Delta));
    }

    internal static double CalculateOuterScrollOffset(double current, double scrollableHeight, int wheelDelta) => Math.Clamp(current - wheelDelta, 0, Math.Max(0, scrollableHeight));

    private static ScrollViewer? FindOuterScroll(DependencyObject element)
    {
        for (DependencyObject? current = element; current is not null; current = VisualTreeHelper.GetParent(current)) if (current is ScrollViewer scroll) return scroll;
        return element as ScrollViewer;
    }

    private static T? FindAncestor<T>(DependencyObject source) where T : DependencyObject
    {
        for (DependencyObject? current = source; current is not null; current = VisualTreeHelper.GetParent(current)) if (current is T match) return match;
        return null;
    }
}
