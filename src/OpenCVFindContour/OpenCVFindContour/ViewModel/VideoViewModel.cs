namespace OpenCVFindContour.ViewModel;

public partial class VideoViewModel : ObservableRecipient
{
    private readonly ILogger<VideoViewModel> logger;

    public VideoViewModel(
        ILogger<VideoViewModel> logger,
        NormalViewModel normalViewModel,
        DetectingNoseViewModel detectingNoseViewModel,
        CannyViewModel cannyViewModel,
        FindContour_ApproxPolyDPViewModel findContour_ApproxPolyDPViewModel,
        FindContour_MinAreaRectViewModel findContour_MinAreaRectViewModel)
    {
        IsActive = true;
        this.logger = logger;
        NormalViewModel = normalViewModel;
        DetectingNoseViewModel = detectingNoseViewModel;
        CannyViewModel = cannyViewModel;
        FindContour_ApproxPolyDPViewModel = findContour_ApproxPolyDPViewModel;
        FindContour_MinAreaRectViewModel = findContour_MinAreaRectViewModel;
    }

    public NormalViewModel NormalViewModel { get; init; }
    public DetectingNoseViewModel DetectingNoseViewModel { get; init; }
    public CannyViewModel CannyViewModel { get; init; }
    public FindContour_ApproxPolyDPViewModel FindContour_ApproxPolyDPViewModel { get; init; }
    public FindContour_MinAreaRectViewModel FindContour_MinAreaRectViewModel { get; init; }

    public async Task CameraStartAsync()
    {
        NormalViewModel.RefreshSubscription();
        await DetectingNoseViewModel.RefreshSubscription();
        CannyViewModel.RefreshSubscription();
        FindContour_ApproxPolyDPViewModel.RefreshSubscription();
        FindContour_MinAreaRectViewModel.RefreshSubscription();
    }
}
