using OhMyRudolph.Core.Services;

namespace OhMyRudolph.Core.Managers;

public sealed partial class CameraManager : ObservableRecipient
{
    private readonly ILogger<CameraManager> logger;

    public CameraManager(ILogger<CameraManager> logger)
    {
        this.logger = logger;

        IsActive = true;
    }

    [ObservableProperty]
    ObservableCollection<CameraService>? _activatedCameras;

    [ObservableProperty]
    [NotifyPropertyChangedRecipients]
    CameraService? _selectedCamera;

    public async Task<bool> CameraStartAsync()
    {
        bool result = false;

        int cameraCount = 10;
        List<CameraService> cameraHandleServices = new(cameraCount);

        for (int i = 0; i < cameraCount; i++)
        {
            #region Web Camera
            var videoCapture = new VideoCapture(i, VideoCaptureAPIs.DSHOW);

            if (videoCapture.IsOpened())
                cameraHandleServices.Add(new CameraService(logger, i, videoCapture));
            #endregion

            #region Surface Pro 9
            //var videoCapture = new VideoCapture(i, VideoCaptureAPIs.MSMF);

            ////if (videoCapture.IsOpened())
            //    cameraHandleServices.Add(new ActivatedCameraHandleService(logger, i, videoCapture));
            #endregion
        }

        this.logger.ZLogInformation($"이용 가능한 카메라 장치 개수: {cameraHandleServices.Count}");

        ActivatedCameras = new ObservableCollection<CameraService>(cameraHandleServices);

        if (ActivatedCameras.Count == 0)
        {
            this.logger.ZLogCritical($"No camera devices found.");
            SelectedCamera = null;
            return false;
        }

        SelectedCamera = ActivatedCameras[0];

        foreach (var cameraHandleService in ActivatedCameras!)
        {
            // 아래 옵션은 하드웨어 특징이 명확하지 않은 시점에서는 적용할 경우 resizing 등 오히려 성능 저하를 일으킨다.
            //cameraHandleService.InitializeCamera();

            result = await cameraHandleService.StartCaptureAsync();
        }

        return result;
    }

    public async Task CameraStopAsync()
    {
        foreach (var cameraHandleService in ActivatedCameras!)
            await cameraHandleService.StopCaptureAsync();
    }
}
