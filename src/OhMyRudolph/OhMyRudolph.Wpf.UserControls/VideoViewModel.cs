namespace OhMyRudolph.Wpf.UserControls;

public partial class VideoViewModel : ObservableRecipient
{
    private readonly ILogger<VideoViewModel> logger;

    public VideoViewModel(
        ILogger<VideoViewModel> logger,
        DetectingNoseViewModel detectingNoseViewModel)
    {
        this.logger = logger;
        DetectingNoseViewModel = detectingNoseViewModel;

        IsActive = true;
    }

    public DetectingNoseViewModel DetectingNoseViewModel { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedRecipients]
    bool _applyingDrawingRudolphEffect;

    [ObservableProperty]
    [NotifyPropertyChangedRecipients]
    bool _applyingOverlayDeadpoolEffect;

    public async Task CameraStartAsync()
    {
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
