using OpenCVFindContour.Managers;
using OpenCVFindContour.Services;

namespace OpenCVFindContour.ViewModel;

public partial class NormalViewModel : ObservableRecipient, IRecipient<PropertyChangedMessage<ActivatedCameraHandleService>>
{
    private readonly ILogger<NormalViewModel> logger;
    IDisposable? currentSubscription;
    ActivatedCameraHandleService? currentCameraService;

    public NormalViewModel(ILogger<NormalViewModel> logger)
    {
        IsActive = true;
        this.logger = logger;
    }

    [ObservableProperty]
    Mat? _printMat;

    public void Receive(PropertyChangedMessage<ActivatedCameraHandleService> message)
    {
        if (message.PropertyName == nameof(CameraManager.SelectedCameraHandleService) && message.NewValue is not null)
        {
            currentSubscription?.Dispose();
            currentCameraService = message.NewValue;
            MakeSubscription(currentCameraService);
        }
    }

    public void RefreshSubscription()
    {
        if (currentCameraService is null)
            return;
        MakeSubscription(currentCameraService);
    }

    private void MakeSubscription(ActivatedCameraHandleService service)
    {
        if (currentCameraService is null)
            return;

        currentSubscription?.Dispose();
        currentCameraService = service;
        currentSubscription = currentCameraService.ImageStream
            .ObserveOn(SynchronizationContext.Current!) // Application.Dispatcher.Invoke 와 동일한 효과
            .Subscribe(mat =>
            {
                if (mat.Empty())
                {
                    PrintMat = null;
                    return;
                }
                ProcessImage(mat);
            });
    }

    private void ProcessImage(Mat mat)
    {
        PrintMat = mat;
    }
}
