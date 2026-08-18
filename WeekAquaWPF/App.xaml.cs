using System.Configuration;
using System.Data;
using System.Windows;
using WeekAquaWPF.CLI;

namespace WeekAquaWPF;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length > 0)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            int exitCode = await CliRunner.RunAsync(e.Args);
            Shutdown(exitCode);
        }
        else
        {
            WeekAquaWPF.Protocol.ProtocolTests.RunVerification();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}

