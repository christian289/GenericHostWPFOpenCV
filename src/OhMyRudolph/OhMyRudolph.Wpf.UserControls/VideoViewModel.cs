using OhMyRudolph.Core.Helpers;
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

        PhotoShotSlot = new();
        _effectAvailable = false;
        IsActive = true;
    }

    public CameraManager CameraManager { get; init; }

    public DetectingNoseViewModel DetectingNoseViewModel { get; init; }

    public Queue<Mat> PhotoShotSlot { get; init; }

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

    public async Task CameraStopAsync()
    {
        await CameraManager.CameraStopAsync();
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

    [RelayCommand]
    public async Task PhotoShot()
    {
        if (DetectingNoseViewModel.PrintMat is null)
            return;

        await CameraStopAsync();
        Mat saveTarget = new();
        DetectingNoseViewModel.PrintMat.CopyTo(saveTarget);
        PhotoShotSlot.Enqueue(saveTarget); // 나중에 여러장 선택 저장할 때 사용

        CommonSaveFileDialog saveFileDialog = new()
        {
            Title = "이미지 저장할 위치 선택",
            DefaultExtension = ".png"
        };
        saveFileDialog.Filters.Add(new CommonFileDialogFilter("PNG 파일", "*.png"));
        saveFileDialog.Filters.Add(new CommonFileDialogFilter("JPEG 파일", "*.jpg"));
        saveFileDialog.Filters.Add(new CommonFileDialogFilter("BMP 파일", "*.bmp"));

        if (saveFileDialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            var ext = Path.GetExtension(saveFileDialog.FileName!);
            ImageFormat imageFormat = ext.ToLower() switch
            {
                ".png" => ImageFormat.Png,
                ".jpg" => ImageFormat.Jpeg,
                ".bmp" => ImageFormat.Bmp,
                _ => ImageFormat.Jpeg
            };
            MatToBitmapSaver.SaveMatAsBitmap(saveTarget, saveFileDialog.FileName!, imageFormat);

            logger.ZLogInformation($"이미지 저장 완료: {saveFileDialog.FileName}");
        }

        await Task.Delay(TimeSpan.FromSeconds(1));
        Messenger.Send(new ValueChangedMessage<string>(nameof(FinalPageViewModel)));
        await Task.Delay(TimeSpan.FromSeconds(4));
        Messenger.Send(new ValueChangedMessage<string>(nameof(MainPageViewModel)));
    }

    public void Receive(PropertyChangedMessage<bool> message)
    {
        if (message.Sender is DetectingNoseViewModel &&
            message.PropertyName == nameof(DetectingNoseViewModel.EffectApplyAvailable))
        {
            EffectAvailable = message.NewValue;
        }
    }
}
