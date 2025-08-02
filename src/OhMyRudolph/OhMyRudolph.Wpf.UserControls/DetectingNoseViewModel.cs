using OhMyRudolph.Core.Clients;
using OhMyRudolph.Core.Effects;
using OhMyRudolph.Core.Managers;
using OhMyRudolph.Core.Services;

namespace OhMyRudolph.Wpf.UserControls;

public partial class DetectingNoseViewModel : ObservableRecipient, IRecipient<PropertyChangedMessage<CameraService>>, IRecipient<PropertyChangedMessage<bool>>
{
    private readonly ILogger<DetectingNoseViewModel> logger;
    private readonly FaceMeshClient faceMeshClient;
    private readonly DrawingRudolphEffect drawingRudolphEffect;
    private readonly OverlayDeadpoolEffect overlayDeadpoolEffect;
    IDisposable? currentSubscription;
    CameraService? currentCameraService;
    bool _applyingDrawingRudolphEffect;
    bool _applyingOverlayDeadpoolEffect;

    public DetectingNoseViewModel(
        ILogger<DetectingNoseViewModel> logger,
        FaceMeshClient faceMeshClient,
        DrawingRudolphEffect drawingRudolphEffect,
        OverlayDeadpoolEffect overlayDeadpoolEffect)
    {
        this.logger = logger;
        this.faceMeshClient = faceMeshClient;
        this.drawingRudolphEffect = drawingRudolphEffect;
        this.overlayDeadpoolEffect = overlayDeadpoolEffect;
        _applyingDrawingRudolphEffect = false;
        _applyingOverlayDeadpoolEffect = false;
        IsActive = true;
    }

    [ObservableProperty]
    Mat? _printMat;

    public void Receive(PropertyChangedMessage<CameraService> message)
    {
        if (message.PropertyName == nameof(CameraManager.SelectedCamera) && message.NewValue is not null)
        {
            currentSubscription?.Dispose();
            currentCameraService = message.NewValue;
            MakeSubscription(currentCameraService);
        }
    }

    public void Receive(PropertyChangedMessage<bool> message)
    {
        if (message.Sender is VideoViewModel &&
            message.PropertyName == nameof(VideoViewModel.ApplyingDrawingRudolphEffect))
        {
            _applyingDrawingRudolphEffect = message.NewValue;
        }
        else if (message.Sender is VideoViewModel &&
            message.PropertyName == nameof(VideoViewModel.ApplyingOverlayDeadpoolEffect))
        {
            _applyingOverlayDeadpoolEffect = message.NewValue;
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

        if (points is not null)
        {
            if (_applyingDrawingRudolphEffect)
            {
                foreach (var (X, Y) in points)
                    mat = drawingRudolphEffect.ProcesingImage(mat, X, Y);
            }
            else if (_applyingOverlayDeadpoolEffect)
            {
                foreach (var (X, Y) in points)
                    mat = overlayDeadpoolEffect.ProcesingImage(mat, X, Y);
            }
        }

        PrintMat = mat;
    }
}
