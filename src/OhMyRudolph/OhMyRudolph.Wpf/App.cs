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
        window.DataContext = serviceProvider.GetRequiredService<MainWindowViewModel>();
        window.ShowDialog();
    }

    private static IServiceProvider BootStrapper()
    {
        IHost host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddZLoggerConsole();
                //logging.SetMinimumLevel(LogLevel.Information);
                logging.SetMinimumLevel(LogLevel.Debug);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<CameraManager>();
                services.AddSingleton<FastApiFaceMeshClient>();
                services.AddSingleton<FaceMeshClient>();
                services.AddSingleton<FinalPageViewModel>();
                services.AddSingleton<DetectingNoseViewModel>();
                services.AddSingleton<ReadyScreenViewModel>();
                services.AddSingleton<VideoViewModel>();
                services.AddSingleton<PhotoViewModel>();
                services.AddSingleton<SelectModeViewModel>();
                services.AddSingleton<MainPageViewModel>();
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();

                // 나중에 Effect는 DLL로 빼서 Plugin 형태면 좋을듯.
                services.AddSingleton<DrawingRudolphEffect>();
                services.AddSingleton<OverlayDeadpoolEffect>();

                services.AddHttpClient<FastApiFaceMeshClient>();
            })
            .Build();

        return host.Services;
    }
}
