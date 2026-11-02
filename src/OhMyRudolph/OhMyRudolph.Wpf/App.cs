using OhMyRudolph.Core.Clients;
using OhMyRudolph.Core.Containers;
using OhMyRudolph.Core.Effects;
using OhMyRudolph.Core.Managers;
using OhMyRudolph.Wpf.UserControls;

namespace OhMyRudolph.Wpf;

internal sealed class App : Application
{
    private IHost? _host;
    private MainWindowViewModel? _mainViewModel;
    private bool _closing;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            _host = BootStrapper();
            await _host.StartAsync();
            MainWindow = _host.Services.GetRequiredService<MainWindow>();
            _mainViewModel = _host.Services.GetRequiredService<MainWindowViewModel>();
            MainWindow.DataContext = _mainViewModel;
            MainWindow.Closed += OnMainWindowClosed;
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"앱 시작 실패: {ex.GetType().Name}");
            await CloseHostAsync();
            Shutdown(1);
        }
    }

    private async void OnMainWindowClosed(object? sender, EventArgs e)
    {
        if (_closing)
            return;
        _closing = true;
        try
        {
            await CloseHostAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"앱 종료 정리 실패: {ex.GetType().Name}");
            Environment.ExitCode = 1;
        }
        finally
        {
            Shutdown(Environment.ExitCode);
        }
    }

    private async Task CloseHostAsync()
    {
        var host = _host;
        if (host is null)
            return;
        _host = null;
        try
        {
            var viewModel = _mainViewModel;
            _mainViewModel = null;
            if (viewModel is not null)
            {
                await viewModel.PhotoViewModel.DisposeAsync();
                await viewModel.VideoViewModel.DisposeAsync();
                await viewModel.VideoViewModel.DetectingNoseViewModel.DisposeAsync();
                await viewModel.VideoViewModel.CameraManager.DisposeAsync();
            }
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await host.StopAsync(timeout.Token);
        }
        finally
        {
            if (host is IAsyncDisposable asynchronousHost)
                await asynchronousHost.DisposeAsync();
            else
                host.Dispose();
        }
    }

    private static IHost BootStrapper()
    {
        IHost host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddZLoggerConsole();
                logging.SetMinimumLevel(LogLevel.Information);
                logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<CameraManager>();
                services.AddSingleton<IProcessingRuntime, PodmanProcessingRuntime>();
                services.AddSingleton<ContainerFaceMeshClient>();
                services.AddSingleton<FinalPageViewModel>();
                services.AddSingleton<DetectingNoseViewModel>();
                services.AddSingleton<ReadyScreenViewModel>();
                services.AddSingleton<VideoViewModel>();
                services.AddSingleton<PhotoViewModel>();
                services.AddSingleton<SelectModeViewModel>();
                services.AddSingleton<MainPageViewModel>();
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();

                services.AddSingleton<DrawingRudolphEffect>();
                services.AddSingleton<OverlayDeadpoolEffect>();

            })
            .Build();

        return host;
    }
}
