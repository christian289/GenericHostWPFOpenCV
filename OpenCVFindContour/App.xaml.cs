using OpenCVFindContour.View;
using OpenCVFindContour.ViewModel;

namespace OpenCVFindContour;

public partial class App : Application
{
    private readonly IHost host;
    public static IServiceProvider ServiceProvider { get; private set; }

    public App()
    {
        host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, service) =>
            {
                service.AddKeyedTransient<VideoCapture>("VideoCapture1", (sp) =>
                {
                    var videoCapture = new VideoCapture(1, VideoCaptureAPIs.DSHOW);
                    if (!videoCapture.IsOpened())
                    {
                        throw new InvalidOperationException("Failed to open video capture device.");
                    }
                    return videoCapture;
                });
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
