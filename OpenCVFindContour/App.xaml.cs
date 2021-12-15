using DevExpress.Mvvm;
using DevExpress.Mvvm.POCO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenCVFindContour.View;
using OpenCVFindContour.ViewModel;
using OpenCvSharp;
using System;
using System.Windows;

namespace OpenCVFindContour
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly IHost host;

        public static IServiceProvider ServiceProvider { get; private set; }

        public App()
        {
            Messenger.Default = new Messenger(isMultiThreadSafe: true, actionReferenceType: ActionReferenceType.WeakReference);
            host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, service) =>
                {
                    //service.AddTransient<VideoCapture>(c => new VideoCapture(1, VideoCaptureAPIs.DSHOW));
                    service.AddTransient<VideoCapture>();
                    service.AddTransient(ViewModelSource.GetPOCOType(typeof(MainWindowViewModel)));
                    service.AddTransient(ViewModelSource.GetPOCOType(typeof(CannyViewModel)));
                    service.AddTransient(ViewModelSource.GetPOCOType(typeof(FindContour_MinAreaRectViewModel)));
                    service.AddTransient(ViewModelSource.GetPOCOType(typeof(FindContour_ApproxPolyDPViewModel)));
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
}
