using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace SystemChecker;

public partial class TelemetryMainWindow
{
    private readonly HashSet<DataGrid> _coordinatedGrids = [];

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
        foreach (var grid in Descendants<DataGrid>(outer))
        {
            if (!_coordinatedGrids.Add(grid)) continue;
            grid.PreviewMouseWheel += (_, e) => ForwardTableWheel(outer, e);
        }
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
