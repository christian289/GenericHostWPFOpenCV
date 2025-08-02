using OhMyRudolph.Core.Clients;
using OhMyRudolph.Core.Effects;
using OhMyRudolph.Core.Managers;
using OhMyRudolph.Core.Services;

namespace OhMyRudolph.Wpf.UserControls;

public sealed partial class DetectingNoseViewModel : ObservableRecipient, IRecipient<PropertyChangedMessage<CameraService>>, IRecipient<PropertyChangedMessage<bool>>
{
    private readonly ILogger<DetectingNoseViewModel> logger;
    private readonly FaceMeshClient faceMeshClient;
    private readonly RudolphEffect rudolphEffect;
    IDisposable? currentSubscription;
    CameraService? currentCameraService;
    bool _applyingEffect;

    public DetectingNoseViewModel(
        ILogger<DetectingNoseViewModel> logger,
        FaceMeshClient faceMeshClient,
        RudolphEffect rudolphEffect)
    {
        IsActive = true;
        this.logger = logger;
        this.faceMeshClient = faceMeshClient;
        this.rudolphEffect = rudolphEffect;

        _applyingEffect = false;
    }

    [ObservableProperty]
    Mat? _printMat;

    public void Receive(PropertyChangedMessage<CameraService> message)
    {
        if (message.PropertyName == nameof(CameraManager.SelectedCameraHandleService) && message.NewValue is not null)
        {
            currentSubscription?.Dispose();
            currentCameraService = message.NewValue;
            MakeSubscription(currentCameraService);
        }
    }

    public void Receive(PropertyChangedMessage<bool> message)
    {
        //if (message.Sender is MainWindowViewModel &&
        //    message.PropertyName == nameof(MainWindowViewModel.ApplyingEffect))
        //{
        //    _applyingEffect = message.NewValue;
        //}
    }

    public async Task RefreshSubscription()
    {
        if (currentCameraService is null)
            return;
        faceMeshClient.StartPythonProcess();
        await faceMeshClient.ConnectPipe();
        MakeSubscription(currentCameraService);
    }

    private void MakeSubscription(CameraService service)
    {
        if (currentCameraService is null)
            return;

        currentSubscription?.Dispose();
        currentCameraService = service;
        currentSubscription = currentCameraService.ImageStream
            .ObserveOn(SynchronizationContext.Current!) // Application.Dispatcher.Invoke 와 동일한 효과
            .Where(mat => mat is not null && !mat.Empty())
            .Subscribe(
                onNext: async (mat) =>
                {
                    await ProcessImage(mat);
                },
                onError: (ex) =>
                {
                    logger.ZLogError(ex, $"에러 발생. 구독 종료: {ex.Message}");
                },
                onCompleted: () =>
                {
                    logger.ZLogInformation($"구독 종료. 파이프 제거.");
                    faceMeshClient.DisconnectPipe();
                });
    }

    private async Task ProcessImage(Mat mat)
    {
        IReadOnlyCollection<(int X, int Y)>? points = await faceMeshClient.SendImageAndGetNoseAsync(mat);

        if (_applyingEffect && points is not null)
        {
            foreach (var (X, Y) in points)
                mat = rudolphEffect.ProcesingImage(mat, X, Y);
        }

        PrintMat = mat;
    }
}
