using OhMyRudolph.Core.Clients;
using OhMyRudolph.Core.Effects;
using OhMyRudolph.Core.Managers;
using OhMyRudolph.Wpf.UserControls;

namespace OhMyRudolph.Wpf;

internal sealed class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var serviceProvider = BootStrapper();
        MainWindow window = serviceProvider.GetRequiredService<MainWindow>();
        window.ShowDialog();
    }

    private static IServiceProvider BootStrapper()
    {
        IHost host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddZLoggerConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<CameraManager>();
                services.AddSingleton<FaceMeshClient>();
                services.AddSingleton<VideoViewModel>();
                services.AddSingleton<PhotoViewModel>();
                services.AddSingleton<RudolphEffect>(); // 나중에 Effect는 DLL로 빼서 Plugin 형태면 좋을듯.

            })
            .Build();

        return host.Services;
    }
}
