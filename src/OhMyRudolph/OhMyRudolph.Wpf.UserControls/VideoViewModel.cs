namespace OhMyRudolph.Wpf.UserControls;

public sealed partial class VideoViewModel : ObservableRecipient
{
    private readonly ILogger<VideoViewModel> logger;
    private readonly DetectingNoseViewModel detectingNoseViewModel;

    public VideoViewModel(
        ILogger<VideoViewModel> logger,
        DetectingNoseViewModel detectingNoseViewModel)
    {
        IsActive = true;
        this.logger = logger;
        this.detectingNoseViewModel = detectingNoseViewModel;
    }

    public async Task CameraStartAsync()
    {
        await detectingNoseViewModel.RefreshSubscription();
    }
}
