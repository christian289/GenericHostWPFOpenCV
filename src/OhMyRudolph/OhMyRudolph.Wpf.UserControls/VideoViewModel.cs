using OhMyRudolph.Core.Managers;

namespace OhMyRudolph.Wpf.UserControls;

public partial class VideoViewModel : ObservableRecipient, IRecipient<PropertyChangedMessage<bool>>
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

        _effectAvailable = false;
        IsActive = true;
    }

    public CameraManager CameraManager { get; init; }

    public DetectingNoseViewModel DetectingNoseViewModel { get; init; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DrawingRudolphEffectCommand))]
    [NotifyCanExecuteChangedFor(nameof(OverlayDeadpoolEffectCommand))]
    bool _effectAvailable;

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

    [RelayCommand(CanExecute = nameof(CanDrawingRudolphEffect))]
    public void DrawingRudolphEffect()
    {
        ApplyingDrawingRudolphEffect = true;
        ApplyingOverlayDeadpoolEffect = false;
    }

    public bool CanDrawingRudolphEffect() => EffectAvailable;

    [RelayCommand(CanExecute = nameof(CanOverlayDeadpoolEffect))]
    public void OverlayDeadpoolEffect()
    {
        ApplyingDrawingRudolphEffect = false;
        ApplyingOverlayDeadpoolEffect = true;
    }

    public bool CanOverlayDeadpoolEffect() => EffectAvailable;

    public void Receive(PropertyChangedMessage<bool> message)
    {
        if (message.Sender is DetectingNoseViewModel &&
            message.PropertyName == nameof(DetectingNoseViewModel.EffectApplyAvailable))
        {
            EffectAvailable = message.NewValue;
        }
    }
}
