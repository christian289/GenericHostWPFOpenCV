using OpenCVFindContour.Clients;
using OpenCVFindContour.Effects;
using OpenCVFindContour.Managers;
using OpenCVFindContour.View;
using OpenCVFindContour.ViewModel;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace OpenCVFindContour;

public partial class App : Application
{
    private readonly IHost host;
    public static IServiceProvider ServiceProvider { get; private set; }

    public App()
    {
        host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddZLoggerConsole();
                //logging.AddDebug();
                //logging.SetMinimumLevel(LogLevel.Information);
                logging.SetMinimumLevel(LogLevel.Debug);
            })
            .ConfigureServices((context, service) =>
            {
                service.AddSingleton<CameraManager>();
                service.AddSingleton<MainWindowViewModel>();
                service.AddSingleton<NormalViewModel>();
                service.AddSingleton<DetectingNoseViewModel>();
                service.AddSingleton<CannyViewModel>();
                service.AddSingleton<FindContour_MinAreaRectViewModel>();
                service.AddSingleton<FindContour_ApproxPolyDPViewModel>();
                service.AddSingleton<VideoViewModel>();
                service.AddSingleton<PhotoViewModel>();
                service.AddSingleton<FaceMeshClient>();
                service.AddSingleton<RudolphEffect>(); // 나중에 Effect는 DLL로 빼서 Plugin 형태면 좋을듯.
            })
            .Build();

        ServiceProvider = host.Services;
    }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        host.Start();

        MainWindow mainWindow = new()
        {
            DataContext = ServiceProvider.GetRequiredService<MainWindowViewModel>()
        };
        mainWindow.Show();
    }
}
