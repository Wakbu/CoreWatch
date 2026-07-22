using System.Runtime.CompilerServices;
using SystemChecker;
using SystemChecker.Services;

namespace CoreWatch.Verification;

internal static class RoadmapFeatureVerification
{
    [ModuleInitializer]
    internal static void Run()
    {
        Check(AutomaticUpdateService.IsNewerVersion("6.0.0", "v6.1.1") && !AutomaticUpdateService.IsNewerVersion("6.1.1", "v6.1.1"), "automatic update version policy");
        Check(AutomaticUpdateService.ParseSha256Digest("sha256:" + new string('a', 64)).Length == 64, "automatic update digest policy");
        Check(NetworkDiagnosticsService.ValidateTarget("1.1.1.1") == "1.1.1.1", "network target validation");
        Check(PersonalizedRecommendationService.Calculate(new RecommendationInput("게임", 16, 32, 25, 0, 0)).Score >= 85, "personalized recommendation scoring");
        Check(TelemetryMainWindow.CalculateOuterScrollOffset(300, 1000, 120) == 180 && TelemetryMainWindow.CalculateOuterScrollOffset(20, 1000, 120) == 0, "nested wheel outer scroll policy");
        var diagnosticGrid = new System.Windows.Controls.DataGrid(); TelemetryMainWindow.ConfigureDiagnosticGrid(diagnosticGrid);
        Check(!diagnosticGrid.CanUserResizeColumns && !diagnosticGrid.CanUserResizeRows && diagnosticGrid.VerticalScrollBarVisibility == System.Windows.Controls.ScrollBarVisibility.Visible, "fixed diagnostic grid policy");
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException($"FAILED: {name}");
        Console.WriteLine($"PASS {name}");
    }
}
