using OhMyRudolph.Core.Managers;
using OhMyRudolph.Core.Sessions;
using OhMyRudolph.Domain.Sessions;
using System.Collections.ObjectModel;

namespace OhMyRudolph.Wpf.UserControls;

public sealed partial class BoothShotItem : ObservableObject
{
    public BoothShotItem(int index) => Index = index;
    public int Index { get; }
    public string Label => $"{Index + 1}번째 사진";
    public string AutomationId => $"Booth.Shot.{Index + 1}";
    [ObservableProperty] private object? _image;
}

public partial class VideoViewModel : ObservableRecipient, IRecipient<PropertyChangedMessage<bool>>, IAsyncDisposable
{
    private static readonly string[] StateProperties =
    [
        nameof(Stage), nameof(IsFrameSelection), nameof(IsLive), nameof(IsReview), nameof(IsSaved),
        nameof(HasComposite), nameof(IsLightFrame), nameof(IsDarkFrame), nameof(StepLabel),
        nameof(ShotProgress), nameof(FrameLabel), nameof(CaptureLabel)
    ];

    private readonly ILogger<VideoViewModel> _logger;
    private readonly BoothSession _session = new();
    private readonly Mat?[] _photos = new Mat?[BoothSession.ShotCount];
    private readonly SemaphoreSlim _viewLifecycle = new(1, 1);
    private CancellationTokenSource? _operationCancellation;
    private TaskCompletionSource? _operationCompletion;
    private Mat? _composite;
    private bool _viewActive;
    private bool _disposed;

    public VideoViewModel(ILogger<VideoViewModel> logger, CameraManager cameraManager, DetectingNoseViewModel detectingNoseViewModel)
    {
        _logger = logger;
        CameraManager = cameraManager;
        DetectingNoseViewModel = detectingNoseViewModel;
        Shots = new ObservableCollection<BoothShotItem>(Enumerable.Range(0, 4).Select(i => new BoothShotItem(i)));
        _selectedShot = Shots[0];
        LightFramePreview = BoothImageService.CreateFramePreview(BoothFrame.Light);
        DarkFramePreview = BoothImageService.CreateFramePreview(BoothFrame.Dark);
        IsActive = true;
    }

    public CameraManager CameraManager { get; }
    public DetectingNoseViewModel DetectingNoseViewModel { get; }
    public ObservableCollection<BoothShotItem> Shots { get; }
    public object LightFramePreview { get; }
    public object DarkFramePreview { get; }
    public BoothStage Stage => _session.Stage;
    public bool IsFrameSelection => Stage == BoothStage.FrameSelection;
    public bool IsLive => Stage is BoothStage.Preview or BoothStage.Countdown;
    public bool IsReview => Stage == BoothStage.Review;
    public bool IsSaved => Stage == BoothStage.Saved;
    public bool HasComposite => Stage is BoothStage.Review or BoothStage.Saving or BoothStage.Saved;
    public bool IsLightFrame => _session.Frame == BoothFrame.Light;
    public bool IsDarkFrame => _session.Frame == BoothFrame.Dark;
    public string StepLabel => Stage switch
    {
        BoothStage.FrameSelection => "01 프레임 선택  /  02 촬영  /  03 검토·저장",
        BoothStage.Preview or BoothStage.Countdown => "01 프레임 선택  /  02 촬영 중  /  03 검토·저장",
        _ => "01 프레임 선택  /  02 촬영  /  03 검토·저장"
    };
    public string ShotProgress => $"{_session.ConfirmedCount} / 4 컷";
    public string FrameLabel => IsLightFrame ? "라이트 페이퍼" : "다크 페이퍼";
    public string CaptureLabel => _session.ConfirmedCount == 0 ? "4컷 촬영 시작" : "남은 사진 촬영";

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _effectAvailable;
    [ObservableProperty] private int _countdown;
    [ObservableProperty] private string _statusMessage = "직접 만든 프레임을 선택하세요. 카메라는 미리보기 시작을 눌러야 켜집니다.";
    [ObservableProperty] private object? _compositePreview;
    [ObservableProperty] private BoothShotItem? _selectedShot;

    [NotifyPropertyChangedRecipients]
    [ObservableProperty] private bool _applyingDrawingRudolphEffect;

    partial void OnIsBusyChanged(bool value) => RefreshCommands();
    partial void OnSelectedShotChanged(BoothShotItem? value) => RetakeCommand.NotifyCanExecuteChanged();

    private bool CanChooseFrame() => !IsBusy && IsFrameSelection;
    private bool CanStartPreview() => !IsBusy && IsFrameSelection;
    private bool CanCapture() => !IsBusy && Stage == BoothStage.Preview;
    private bool CanRetake() => !IsBusy && IsReview && SelectedShot is not null;
    private bool CanSave() => !IsBusy && IsReview;
    private bool CanReset() => !IsBusy;
    private bool CanChangeEffect() => !IsBusy && IsLive;
    private bool CanDrawingRudolphEffect() => CanChangeEffect();
    private bool CanCancel() => IsBusy && Stage == BoothStage.Countdown;

    [RelayCommand(CanExecute = nameof(CanChooseFrame))]
    private void SelectLightFrame() => SelectFrame(BoothFrame.Light);

    [RelayCommand(CanExecute = nameof(CanChooseFrame))]
    private void SelectDarkFrame() => SelectFrame(BoothFrame.Dark);

    private void SelectFrame(BoothFrame frame)
    {
        _session.SelectFrame(frame);
        StatusMessage = $"{FrameLabel} 프레임을 선택했습니다. 미리보기를 시작해 주세요.";
        RefreshState();
    }

    [RelayCommand(CanExecute = nameof(CanStartPreview))]
    private Task StartPreviewAsync() => RunOperationAsync(async token =>
    {
        await CameraStartAsync(token);
        token.ThrowIfCancellationRequested();
        _session.BeginPreview();
        StatusMessage = "효과를 선택하고 촬영을 시작하세요. 사진마다 3초를 셉니다.";
    });

    public async Task CameraStartAsync(CancellationToken cancellationToken = default)
    {
        if (!await CameraManager.CameraStartAsync(cancellationToken))
            throw new InvalidOperationException("Camera is unavailable.");
        await DetectingNoseViewModel.RefreshSubscription(cancellationToken);
    }

    public async Task CameraStopAsync(CancellationToken cancellationToken = default)
    {
        await CameraManager.CameraStopAsync(cancellationToken);
        await DetectingNoseViewModel.StopPreviewAsync(cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(CanChangeEffect))]
    public void NoEffect() => ApplyingDrawingRudolphEffect = false;

    [RelayCommand(CanExecute = nameof(CanDrawingRudolphEffect))]
    public void DrawingRudolphEffect() => ApplyingDrawingRudolphEffect = true;

    [RelayCommand(CanExecute = nameof(CanCapture))]
    private Task CaptureAsync() => RunOperationAsync(async token =>
    {
        await CameraStartAsync(token);
        while (_session.ConfirmedCount < BoothSession.ShotCount)
            await CaptureOneAsync(null, token);
        await PrepareReviewAsync();
    });

    [RelayCommand(CanExecute = nameof(CanRetake))]
    private Task RetakeAsync() => RunOperationAsync(async token =>
    {
        var index = SelectedShot!.Index;
        await CameraStartAsync(token);
        await CaptureOneAsync(index, token);
        await PrepareReviewAsync();
    });

    private async Task CaptureOneAsync(int? retakeSlot, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        _session.BeginCountdown(retakeSlot);
        RefreshState();
        for (var second = 3; second > 0; second--)
        {
            Countdown = second;
            StatusMessage = $"{_session.PendingSlot + 1}번째 사진 · {second}초 후 촬영";
            await Task.Delay(TimeSpan.FromSeconds(1), token);
        }
        token.ThrowIfCancellationRequested();
        // Snapshot clones under the preview lock. Confirmed photos never enter the droppable preview queue.
        Mat? snapshot = DetectingNoseViewModel.CaptureSnapshot();
        try
        {
            var thumbnail = BoothImageService.CreateThumbnail(snapshot);
            var index = _session.ConfirmShot();
            var previous = _photos[index];
            _photos[index] = snapshot;
            snapshot = null;
            Shots[index].Image = thumbnail;
            previous?.Dispose();
            SelectedShot = Shots[index];
            ClearComposite();
            Countdown = 0;
            RefreshState();
        }
        finally
        {
            snapshot?.Dispose();
        }
    }

    private async Task PrepareReviewAsync()
    {
        // Camera stops before composition; confirmed Mats remain exclusively session-owned.
        await CameraStopAsync();
        _composite = await Task.Run(() => BoothCompositor.Compose(_photos.Select(photo => photo!).ToArray(), _session.Frame));
        CompositePreview = BoothImageService.FreezeImage(_composite);
        StatusMessage = "4컷 완성! 사진을 선택해 다시 찍거나 합성 사진을 저장하세요.";
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void CancelCapture() => _operationCancellation?.Cancel();

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => RunOperationAsync(async _ =>
    {
        if (_composite is null)
            await PrepareReviewAsync();
        _session.BeginSave();
        RefreshState();
        try
        {
            var saved = await BoothImageService.SaveAsync(_composite!);
            _session.FinishSave(saved ? BoothSaveResult.Succeeded : BoothSaveResult.Cancelled);
            StatusMessage = saved ? "사진을 저장했습니다. 새 세션을 시작하면 이전 사진이 지워집니다." : "저장을 취소했습니다. 사진은 그대로 보관되어 있습니다.";
        }
        catch
        {
            _session.FinishSave(BoothSaveResult.Failed);
            throw;
        }
    });

    [RelayCommand(CanExecute = nameof(CanReset))]
    private Task NewSessionAsync() => RunOperationAsync(async _ =>
    {
        await CameraStopAsync();
        ReleasePhotos();
        _session.Reset();
        ApplyingDrawingRudolphEffect = false;
        StatusMessage = "새 세션입니다. 프레임을 선택하세요. 이전 사진은 삭제되었습니다.";
    });

    [RelayCommand(CanExecute = nameof(CanReset))]
    private async Task BackAsync()
    {
        await RunOperationAsync(async _ => await CameraStopAsync());
        if (!_disposed && !IsBusy)
            Messenger.Send(new ValueChangedMessage<string>(nameof(SelectModeViewModel)));
    }

    /// <summary>View Loaded hook. Frame selection never starts the camera.</summary>
    public async Task ActivateAsync()
    {
        if (_disposed) return;
        _viewActive = true;
        await _viewLifecycle.WaitAsync();
        try
        {
            if (_viewActive && !_disposed && Stage == BoothStage.Preview)
                await RunOperationAsync(CameraStartAsync);
        }
        finally { _viewLifecycle.Release(); }
    }

    /// <summary>View Unloaded hook: cancel countdown/start, finish owned work, then stop camera. Keep confirmed shots.</summary>
    public async Task DeactivateAsync()
    {
        _viewActive = false;
        _operationCancellation?.Cancel();
        var pending = _operationCompletion?.Task;
        if (pending is not null) await pending;
        await _viewLifecycle.WaitAsync();
        try { await CameraStopAsync(); }
        catch (Exception ex) { ReportFailure(ex); }
        finally { _viewLifecycle.Release(); }
    }

    private async Task RunOperationAsync(Func<CancellationToken, Task> action)
    {
        if (_disposed || IsBusy) return;
        using var cancellation = new CancellationTokenSource();
        _operationCancellation = cancellation;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _operationCompletion = completion;
        IsBusy = true;
        try { await action(cancellation.Token); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            await RecoverInterruptedOperationAsync();
            StatusMessage = "촬영을 취소했습니다. 이미 찍은 사진은 보관됩니다.";
        }
        catch (Exception ex)
        {
            await RecoverInterruptedOperationAsync();
            ReportFailure(ex);
        }
        finally
        {
            Countdown = 0;
            _operationCancellation = null;
            _operationCompletion = null;
            IsBusy = false;
            RefreshState();
            completion.TrySetResult();
        }
    }

    private async Task RecoverInterruptedOperationAsync()
    {
        if (Stage == BoothStage.Countdown) _session.CancelCountdown();
        if (Stage == BoothStage.Preview) return;
        try { await CameraStopAsync(); }
        catch (Exception exception)
        {
            _logger.LogWarning("Photo booth camera cleanup failed ({ExceptionType}).", exception.GetType().Name);
        }
    }

    private void ReportFailure(Exception exception)
    {
        // Exception messages can include full user paths; log only the category.
        _logger.LogWarning("Photo booth operation failed ({ExceptionType}).", exception.GetType().Name);
        StatusMessage = "작업을 완료하지 못했습니다. 카메라 또는 저장 위치를 확인한 뒤 다시 시도하세요. 촬영한 사진은 보관됩니다.";
    }

    private void RefreshState()
    {
        foreach (var property in StateProperties)
            OnPropertyChanged(property);
        RefreshCommands();
    }

    private void RefreshCommands()
    {
        SelectLightFrameCommand.NotifyCanExecuteChanged();
        SelectDarkFrameCommand.NotifyCanExecuteChanged();
        StartPreviewCommand.NotifyCanExecuteChanged();
        CaptureCommand.NotifyCanExecuteChanged();
        RetakeCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        NewSessionCommand.NotifyCanExecuteChanged();
        BackCommand.NotifyCanExecuteChanged();
        NoEffectCommand.NotifyCanExecuteChanged();
        DrawingRudolphEffectCommand.NotifyCanExecuteChanged();
        CancelCaptureCommand.NotifyCanExecuteChanged();
    }

    private void ClearComposite()
    {
        CompositePreview = null;
        _composite?.Dispose();
        _composite = null;
    }

    private void ReleasePhotos()
    {
        ClearComposite();
        for (var i = 0; i < _photos.Length; i++)
        {
            Shots[i].Image = null;
            _photos[i]?.Dispose();
            _photos[i] = null;
        }
        SelectedShot = Shots[0];
    }

    public void Receive(PropertyChangedMessage<bool> message)
    {
        if (message.Sender is DetectingNoseViewModel && message.PropertyName == nameof(DetectingNoseViewModel.EffectApplyAvailable))
            EffectAvailable = message.NewValue;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await DeactivateAsync();
        ReleasePhotos();
        IsActive = false;
        GC.SuppressFinalize(this);
    }
}
