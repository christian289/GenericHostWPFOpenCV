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
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices((context, service) =>
            {
                service.AddSingleton<MainWindowViewModel>();
                service.AddSingleton<NormalViewModel>();
                service.AddSingleton<CannyViewModel>();
                service.AddSingleton<FindContour_MinAreaRectViewModel>();
                service.AddSingleton<FindContour_ApproxPolyDPViewModel>();
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
