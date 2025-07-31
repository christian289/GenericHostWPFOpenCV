using Microsoft.Extensions.Logging;
using OpenCVFindContour.Clients;
using OpenCVFindContour.Services;

namespace OpenCVFindContour.ViewModel;

public partial class DetectingNoseViewModel : ObservableRecipient, IRecipient<PropertyChangedMessage<ActivatedCameraHandleService>>
{
    private readonly ILogger<DetectingNoseViewModel> logger;
    private readonly FaceMeshClient faceMeshClient;
    IDisposable? currentSubscription;
    ActivatedCameraHandleService? currentCameraService;

    public DetectingNoseViewModel(
        ILogger<DetectingNoseViewModel> logger,
        FaceMeshClient faceMeshClient)
    {
        IsActive = true;
        this.logger = logger;
        this.faceMeshClient = faceMeshClient;
    }

    [ObservableProperty]
    Mat? _printMat;

    public void Receive(PropertyChangedMessage<ActivatedCameraHandleService> message)
    {
        if (message.PropertyName == nameof(MainWindowViewModel.SelectedCameraHandleService) && message.NewValue is not null)
        {
            currentSubscription?.Dispose();
            currentCameraService = message.NewValue;
            MakeSubscription(currentCameraService);
        }
    }

    public async Task RefreshSubscription()
    {
        if (currentCameraService is null)
            return;
        faceMeshClient.StartPythonProcess();
        await faceMeshClient.ConnectPipe();
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
            .Subscribe(
            onNext: async mat =>
            {
                if (mat.Empty())
                {
                    PrintMat = null;
                    return;
                }
                await ProcessImage(mat);
            },
            onError: (ex) => logger.ZLogError(ex, $"에러 발생. 구독 종료: {ex.Message}"),
            onCompleted: () =>
            {
                logger.ZLogInformation($"구독 종료. 파이프 제거.");
                faceMeshClient.DisconnectPipe();
            });
    }

    private async Task ProcessImage(Mat mat)
    {
        (int X, int Y)? point = await faceMeshClient.SendImageAndGetNoseAsync(mat);
        PrintMat = mat;
    }
}
