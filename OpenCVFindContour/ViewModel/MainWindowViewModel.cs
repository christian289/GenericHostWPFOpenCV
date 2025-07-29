namespace OpenCVFindContour.ViewModel;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly VideoCapture opencvVideo;
    CancellationTokenSource tokenSource;

    [ObservableProperty]
    bool _cameraStartButtonEnabled = true;

    [ObservableProperty]
    bool _cameraStopButtonEnabled = false;

    public MainWindowViewModel()
    {
        this.opencvVideo = new VideoCapture(1, VideoCaptureAPIs.DSHOW);
    }

    [RelayCommand]
    public void CameraStart()
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
                    WeakReferenceMessenger.Default.Send(frame);
                }
            }
        }, tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        CameraStartButtonEnabled = false;
        CameraStopButtonEnabled = true;
    }

    [RelayCommand]
    public virtual void CameraStop()
    {
        tokenSource.Cancel();
        opencvVideo.Release();

        CameraStartButtonEnabled = true;
        CameraStopButtonEnabled = false;
    }
}
