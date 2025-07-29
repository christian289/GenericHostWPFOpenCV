using OpenCVFindContour.Services;

namespace OpenCVFindContour.ViewModel;

public partial class MainWindowViewModel : ObservableRecipient
{
    readonly ILogger<MainWindowViewModel> logger;

    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger,
        CannyViewModel cannyViewModel,
        FindContour_ApproxPolyDPViewModel findContour_ApproxPolyDPViewModel,
        FindContour_MinAreaRectViewModel findContour_MinAreaRectViewModel)
    {
        IsActive = true;

        this.logger = logger;
        CannyViewModel = cannyViewModel;
        FindContour_ApproxPolyDPViewModel = findContour_ApproxPolyDPViewModel;
        FindContour_MinAreaRectViewModel = findContour_MinAreaRectViewModel;

        int cameraCount = 10;
        List<ActivatedCameraHandleService> cameraHandleServices = new(cameraCount);
        for (int i = 0; i < cameraCount; i++)
        {
            var videoCapture = new VideoCapture(i, VideoCaptureAPIs.DSHOW);

            if (videoCapture.IsOpened())
                cameraHandleServices.Add(new ActivatedCameraHandleService(logger, i, videoCapture, 60));
        }

        ActivatedCameraHandleCollection = new ObservableCollection<ActivatedCameraHandleService>(cameraHandleServices);

        if (ActivatedCameraHandleCollection.Count == 0)
        {
            logger.ZLogCritical($"No camera devices found.");
            return;
        }

        SelectedCameraHandleService = ActivatedCameraHandleCollection.First();

        IsCameraStartButtonEnabled = true;
        IsCameraStopButtonEnabled = false;
    }

    public CannyViewModel CannyViewModel { get; init; }
    public FindContour_ApproxPolyDPViewModel FindContour_ApproxPolyDPViewModel { get; init; }
    public FindContour_MinAreaRectViewModel FindContour_MinAreaRectViewModel { get; init; }

    [ObservableProperty]
    ObservableCollection<ActivatedCameraHandleService> _activatedCameraHandleCollection;

    [ObservableProperty]
    [NotifyPropertyChangedRecipients]
    ActivatedCameraHandleService _selectedCameraHandleService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CameraStopCommand))]
    [NotifyCanExecuteChangedFor(nameof(CameraStartCommand))]
    bool _isCameraStartButtonEnabled;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CameraStartCommand))]
    [NotifyCanExecuteChangedFor(nameof(CameraStopCommand))]
    bool _isCameraStopButtonEnabled;

    [RelayCommand(CanExecute = nameof(CanCameraStart))]
    public async Task CameraStart()
    {
        foreach (var cameraHandleService in ActivatedCameraHandleCollection)
            await cameraHandleService.StartCaptureAsync();

        CannyViewModel.RefreshSubscription();
        FindContour_ApproxPolyDPViewModel.RefreshSubscription();
        FindContour_MinAreaRectViewModel.RefreshSubscription();

        IsCameraStopButtonEnabled = true;
        IsCameraStartButtonEnabled = false;
    }

    public bool CanCameraStart() => !IsCameraStopButtonEnabled && IsCameraStartButtonEnabled;

    [RelayCommand(CanExecute = nameof(CanCameraStop))]
    public async Task CameraStop()
    {
        foreach (var cameraHandleService in ActivatedCameraHandleCollection)
            await cameraHandleService.StopCaptureAsync();

        IsCameraStopButtonEnabled = false;
        IsCameraStartButtonEnabled = true;
    }

    public bool CanCameraStop() => IsCameraStopButtonEnabled && !IsCameraStartButtonEnabled;
}
