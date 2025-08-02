using OhMyRudolph.Core.Managers;

namespace OhMyRudolph.Wpf.UserControls;

public partial class VideoViewModel : ObservableRecipient
{
    private readonly ILogger<VideoViewModel> logger;

    public VideoViewModel(
        ILogger<VideoViewModel> logger,
        CameraManager cameraManager,
        DetectingNoseViewModel detectingNoseViewModel)
    {
        this.logger = logger;
        CameraManager = cameraManager;
        DetectingNoseViewModel = detectingNoseViewModel;

        IsActive = true;
    }

    public CameraManager CameraManager { get; init; }

    public DetectingNoseViewModel DetectingNoseViewModel { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedRecipients]
    bool _applyingDrawingRudolphEffect;

    [ObservableProperty]
    [NotifyPropertyChangedRecipients]
    bool _applyingOverlayDeadpoolEffect;

    public async Task CameraStartAsync()
    {
        await CameraManager.CameraStartAsync();
        await DetectingNoseViewModel.RefreshSubscription();
    }

    [RelayCommand]
    public void NoEffect()
    {
        ApplyingDrawingRudolphEffect = false;
        ApplyingOverlayDeadpoolEffect = false;
    }

    [RelayCommand]
    public void DrawingRudolphEffect()
    {
        ApplyingDrawingRudolphEffect = true;
        ApplyingOverlayDeadpoolEffect = false;
    }

    [RelayCommand]
    public void OverlayDeadpoolEffect()
    {
        ApplyingDrawingRudolphEffect = false;
        ApplyingOverlayDeadpoolEffect = true;
    }
}
