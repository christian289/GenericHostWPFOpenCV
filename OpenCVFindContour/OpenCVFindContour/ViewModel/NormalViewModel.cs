using Microsoft.Extensions.Logging;
using OpenCVFindContour.Clients;
using OpenCVFindContour.Services;

namespace OpenCVFindContour.ViewModel;

public partial class NormalViewModel : ObservableRecipient, IRecipient<PropertyChangedMessage<ActivatedCameraHandleService>>
{
    private readonly ILogger<NormalViewModel> logger;
    private readonly FaceMeshClient faceMeshClient;
    IDisposable? currentSubscription;
    ActivatedCameraHandleService? currentCameraService;

    public NormalViewModel(
        ILogger<NormalViewModel> logger,
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
            .Subscribe(async mat =>
            {
                if (mat.Empty())
                {
                    PrintMat = null;
                    return;
                }
                await ProcessImage(mat);
            });
    }

    private async Task ProcessImage(Mat mat)
    {
        (int X, int Y)? point = await faceMeshClient.SendImageAndGetNoseAsync(mat);
        logger.ZLogInformation($"[Noise Received point] X:{point?.X}, Y: {point?.Y}");
        PrintMat = mat;
    }
}
