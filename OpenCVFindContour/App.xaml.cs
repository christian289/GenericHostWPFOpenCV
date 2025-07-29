using OpenCVFindContour.Interfaces;
using OpenCVFindContour.Services;
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
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices((context, service) =>
            {
                for (int i = 0; i < 10; i++)
                {
                    var videoCapture = new VideoCapture(i, VideoCaptureAPIs.DSHOW);

                    if (videoCapture.IsOpened())
                        service.AddSingleton<IActivatedCameraHandleService>(new ActivatedCameraHandleService(i, videoCapture, 60));
                }
                service.AddTransient<MainWindowViewModel>();
                service.AddTransient<CannyViewModel>();
                service.AddTransient<FindContour_MinAreaRectViewModel>();
                service.AddTransient<FindContour_ApproxPolyDPViewModel>();
            })
            .Build();
        ServiceProvider = host.Services;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await host.StartAsync();
        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        using (host)
        {
            await host.StopAsync(TimeSpan.FromSeconds(5));
        }

        base.OnExit(e);
    }
}
