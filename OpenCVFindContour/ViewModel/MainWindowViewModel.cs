using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using OpenCvSharp;
using System.Threading;
using System.Threading.Tasks;

namespace OpenCVFindContour.ViewModel
{
    [POCOViewModel]
    public class MainWindowViewModel
    {
        private readonly VideoCapture opencvVideo;
        CancellationTokenSource tokenSource;

        public virtual bool CameraStartButtonEnabled { get; set; } = true;
        public virtual bool CameraStopButtonEnabled { get; set; } = false;

        public MainWindowViewModel(VideoCapture opencvVideo)
        {
            this.opencvVideo = opencvVideo;
        }

        public virtual void CameraStart()
        {
            tokenSource = new CancellationTokenSource();
            Task cameraTask = Task.Factory.StartNew(() =>
            {
                Mat frame = new();
                opencvVideo.Open(1, VideoCaptureAPIs.DSHOW);

                if (opencvVideo.IsOpened())
                {
                    while (!tokenSource.IsCancellationRequested)
                    {
                        opencvVideo.Read(frame);
                        Messenger.Default.Send(frame);
                    }
                }
            }, tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            CameraStartButtonEnabled = false;
            CameraStopButtonEnabled = true;
        }

        public virtual void CameraStop()
        {
            tokenSource.Cancel();
            opencvVideo.Release();

            CameraStartButtonEnabled = true;
            CameraStopButtonEnabled = false;
        }
    }
}
