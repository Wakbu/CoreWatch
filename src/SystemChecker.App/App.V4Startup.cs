using System.Windows;

namespace SystemChecker;

public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        StartupUri = new Uri("TelemetryMainWindow.xaml", UriKind.Relative);
    }
}

