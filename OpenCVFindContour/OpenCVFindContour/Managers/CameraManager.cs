using OpenCVFindContour.Services;

namespace OpenCVFindContour.Managers;

public sealed partial class CameraManager : ObservableRecipient
{
    private readonly ILogger<CameraManager> logger;

    public CameraManager(ILogger<CameraManager> logger)
    {
        this.logger = logger;

        IsActive = true;

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

        this.logger.ZLogInformation($"이용 가능한 카메라 장치 개수: {cameraHandleServices.Count}");

        ActivatedCameraHandleCollection = new ObservableCollection<ActivatedCameraHandleService>(cameraHandleServices);

        if (ActivatedCameraHandleCollection.Count == 0)
        {
            this.logger.ZLogCritical($"No camera devices found.");
            SelectedCameraHandleService = null;
        }
    }

    [ObservableProperty]
    ObservableCollection<ActivatedCameraHandleService>? _activatedCameraHandleCollection;

    [ObservableProperty]
    [NotifyPropertyChangedRecipients]
    ActivatedCameraHandleService? _selectedCameraHandleService;

    public async Task CameraStartAsync()
    {
        foreach (var cameraHandleService in ActivatedCameraHandleCollection!)
        {
            // 아래 옵션은 하드웨어 특징이 명확하지 않은 시점에서는 적용할 경우 resizing 등 오히려 성능 저하를 일으킨다.
            //cameraHandleService.InitializeCamera();

            await cameraHandleService.StartCaptureAsync();
        }
    }

    public async Task CameraStopAsync()
    {
        foreach (var cameraHandleService in ActivatedCameraHandleCollection!)
            await cameraHandleService.StopCaptureAsync();
    }
}
