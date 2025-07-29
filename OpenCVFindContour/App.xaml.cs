using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenCvSharp;
using System.Configuration;
using System.Data;
using System.Windows;

namespace OpenCvFindContour;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private readonly IHost host;
    public static IServiceProvider ServiceProvider { get; private set; }

    public App()
    {
        host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, service) =>
                {
                    //service.AddTransient<VideoCapture>(c => new VideoCapture(1, VideoCaptureAPIs.DSHOW));
                    service.AddTransient<VideoCapture>();
                    service.AddTransient<MainWindowViewModel>();
                    service.AddTransient<CannyViewModel>();
                    service.AddTransient<FindContour_MinAreaRectViewModel>();
                    service.AddTransient<FindContour_ApproxPolyDPViewModel>();
                    service.AddTransient<MainWindow>();
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
