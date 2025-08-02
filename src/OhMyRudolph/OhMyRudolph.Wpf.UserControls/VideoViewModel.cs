namespace OhMyRudolph.Wpf.UserControls;

public partial class VideoViewModel : ObservableRecipient
{
    private readonly ILogger<VideoViewModel> logger;
    private readonly DetectingNoseViewModel detectingNoseViewModel;

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
    bool _applyingDrawingRudolphEffect;

    [ObservableProperty]
    bool _applyingOverlayDeadpoolEffect;

    public async Task CameraStartAsync()
    {
        await DetectingNoseViewModel.RefreshSubscription();
    }
}
