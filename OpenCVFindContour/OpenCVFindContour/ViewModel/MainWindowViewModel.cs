using OpenCVFindContour.Services;

namespace OpenCVFindContour.ViewModel;

public partial class MainWindowViewModel : ObservableRecipient
{
    readonly ILogger<MainWindowViewModel> logger;

    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger,
        NormalViewModel normalViewModel)
    {
        IsActive = true;

        this.logger = logger;
        NormalViewModel = normalViewModel;

        Initialize();
    }

    //public MainWindowViewModel(
    //    ILogger<MainWindowViewModel> logger,
    //    NormalViewModel normalViewModel,
    //    CannyViewModel cannyViewModel,
    //    FindContour_ApproxPolyDPViewModel findContour_ApproxPolyDPViewModel,
    //    FindContour_MinAreaRectViewModel findContour_MinAreaRectViewModel)
    //{
    //    IsActive = true;

    //    this.logger = logger;
    //    NormalViewModel = normalViewModel;
    //    CannyViewModel = cannyViewModel;
    //    FindContour_ApproxPolyDPViewModel = findContour_ApproxPolyDPViewModel;
    //    FindContour_MinAreaRectViewModel = findContour_MinAreaRectViewModel;

    //    Initialize();
    //}

    public NormalViewModel NormalViewModel { get; init; }
    public CannyViewModel CannyViewModel { get; init; }
    public FindContour_ApproxPolyDPViewModel FindContour_ApproxPolyDPViewModel { get; init; }
    public FindContour_MinAreaRectViewModel FindContour_MinAreaRectViewModel { get; init; }

    [ObservableProperty]
    ObservableCollection<ActivatedCameraHandleService> _activatedCameraHandleCollection;

    [ObservableProperty]
    [NotifyPropertyChangedRecipients]
    ActivatedCameraHandleService _selectedCameraHandleService;

    [ObservableProperty]
    double _normalViewActualHeight;

    [ObservableProperty]
    double _normalViewActualWidth;

    [ObservableProperty]
    double _cannyViewActualHeight;

    [ObservableProperty]
    double _cannyViewActualWidth;

    [ObservableProperty]
    double _approxPolyDPViewActualHeight;

    [ObservableProperty]
    double _approxPolyDPViewActualWidth;

    [ObservableProperty]
    double _minAreaRectActualHeight;

    [ObservableProperty]
    double _minAreaRectActualWidth;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CameraStopCommand))]
    [NotifyCanExecuteChangedFor(nameof(CameraStartCommand))]
    bool _isCameraStartButtonEnabled;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CameraStartCommand))]
    [NotifyCanExecuteChangedFor(nameof(CameraStopCommand))]
    bool _isCameraStopButtonEnabled;

    private void Initialize()
    {
        int cameraCount = 10;
        List<ActivatedCameraHandleService> cameraHandleServices = new(cameraCount);
        for (int i = 0; i < cameraCount; i++)
        {
            #region Web Camera
            var videoCapture = new VideoCapture(i, VideoCaptureAPIs.DSHOW);

            if (videoCapture.IsOpened())
                cameraHandleServices.Add(new ActivatedCameraHandleService(logger, i, videoCapture));
            #endregion

            #region Surface Pro 9
            //var videoCapture = new VideoCapture(i, VideoCaptureAPIs.MSMF);

            ////if (videoCapture.IsOpened())
            //    cameraHandleServices.Add(new ActivatedCameraHandleService(logger, i, videoCapture));
            #endregion
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

    [RelayCommand(CanExecute = nameof(CanCameraStart))]
    public async Task CameraStart()
    {
        foreach (var cameraHandleService in ActivatedCameraHandleCollection)
        {
            // 아래 옵션은 하드웨어 특징이 명확하지 않은 시점에서는 적용할 경우 resizing 등 오히려 성능 저하를 일으킨다.
            //cameraHandleService.InitializeCamera();

            await cameraHandleService.StartCaptureAsync();
        }   

        NormalViewModel.RefreshSubscription();
        //CannyViewModel.RefreshSubscription();
        //FindContour_ApproxPolyDPViewModel.RefreshSubscription();
        //FindContour_MinAreaRectViewModel.RefreshSubscription();

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
