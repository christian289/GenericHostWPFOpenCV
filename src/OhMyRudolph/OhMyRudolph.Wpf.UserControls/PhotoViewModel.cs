using OhMyRudolph.Abstractions.FaceMesh;
using OhMyRudolph.Core.Clients;
using OhMyRudolph.Core.Effects;
using OhMyRudolph.Core.Helpers;

namespace OhMyRudolph.Wpf.UserControls;

public partial class PhotoViewModel : ObservableRecipient, IAsyncDisposable
{
    private readonly ILogger<PhotoViewModel> _logger;
    private readonly ContainerFaceMeshClient _client;
    private CancellationTokenSource? _operationCancellation;
    private TaskCompletionSource? _operationCompletion;
    private TaskCompletionSource? _navigationCompletion;
    private Task? _disposeTask;
    private string _sessionId = Guid.NewGuid().ToString("N");
    private long _frameId;
    private bool _disposed;
    private bool _leaving;
    private bool _usesProcessing;

    public PhotoViewModel(ILogger<PhotoViewModel> logger, ContainerFaceMeshClient client)
    {
        _logger = logger;
        _client = client;
        IsActive = true;
    }

    [ObservableProperty] private Mat? _originalImage;
    [ObservableProperty] private Mat? _processedImage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "이미지를 선택하세요. 얼굴 효과는 로컬 처리 컨테이너를 사용합니다.";

    partial void OnOriginalImageChanged(Mat? value) => RefreshCommands();
    partial void OnProcessedImageChanged(Mat? value) => RefreshCommands();
    partial void OnIsBusyChanged(bool value) => RefreshCommands();
    private bool CanInteract() => !_disposed && !_leaving && !IsBusy;
    private bool CanProcess() => CanInteract() && OriginalImage is not null;
    private bool CanSave() => CanInteract() && ProcessedImage is not null;

    [RelayCommand(CanExecute = nameof(CanInteract))]
    public void LoadImage()
    {
        if (!CanInteract()) return;
        try
        {
            var dialog = new CommonOpenFileDialog { Title = "이미지 파일 선택", RestoreDirectory = true };
            dialog.Filters.Add(new CommonFileDialogFilter("이미지 파일", "*.jpg;*.jpeg;*.png;*.bmp"));
            if (dialog.ShowDialog() != CommonFileDialogResult.Ok) return;
            Mat? image = Cv2.ImRead(dialog.FileName!, ImreadModes.Color);
            try
            {
                if (image.Empty()) { StatusMessage = "이미지를 읽지 못했습니다. 기존 사진은 보관됩니다."; return; }
                Mat? previous = OriginalImage;
                OriginalImage = image;
                image = null;
                previous?.Dispose();
                ReplaceProcessed(null);
                _sessionId = Guid.NewGuid().ToString("N");
                _frameId = 0;
                StatusMessage = "이미지를 불러왔습니다. 적용할 효과를 선택하세요.";
            }
            finally { image?.Dispose(); }
        }
        catch (Exception error) { ReportFailure(error, "이미지를 읽지 못했습니다. 기존 사진은 보관됩니다."); }
    }

    [RelayCommand(CanExecute = nameof(CanProcess))]
    public void NoEffect()
    {
        if (!CanProcess()) return;
        try
        {
            ReplaceProcessed(OriginalImage!.Clone());
            StatusMessage = "효과 없이 원본 이미지를 표시합니다.";
        }
        catch (Exception error) { ReportFailure(error, "원본 이미지를 표시하지 못했습니다."); }
    }

    [RelayCommand(CanExecute = nameof(CanProcess))]
    public Task ApplyDrawingRudolphEffect() => ApplyEffectAsync(true);

    [RelayCommand(CanExecute = nameof(CanProcess))]
    public Task ApplyOverlayDeadpoolEffect() => ApplyEffectAsync(false);

    private async Task ApplyEffectAsync(bool drawingRudolph)
    {
        if (!CanProcess()) return;
        // Load/no-effect/save are disabled until this operation ends. Back and shutdown
        // cancel and await it before releasing the original Mat, so no extra input clone is needed.
        Mat original = OriginalImage!;
        using var cancellation = new CancellationTokenSource();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _operationCancellation = cancellation;
        _operationCompletion = completion;
        IsBusy = true;
        try
        {
            StatusMessage = "로컬 처리 컨테이너 준비 중입니다. 최초 이미지 가져오기는 최대 5분이 걸릴 수 있습니다.";
            _usesProcessing = true;
            await _client.StartAsync(cancellation.Token);
            var metadata = new FaceMeshFrameMetadata(_sessionId, "static-photo", ++_frameId,
                original.Width, original.Height, false, 0);
            var response = await _client.RequestAsync(original, metadata, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (_disposed) return;
            if (response.Status == FaceMeshStatus.NoFace)
            {
                ReplaceProcessed(null);
                StatusMessage = "얼굴을 찾지 못했습니다. 원본 사진은 보관됩니다.";
                return;
            }
            Mat? result = original.Clone();
            try
            {
                using var overlay = drawingRudolph ? null : new OverlayDeadpoolEffect();
                foreach (var point in response.Landmarks)
                {
                    if (drawingRudolph) DrawingRudolphEffect.ProcessImage(result, point.X, point.Y);
                    else overlay!.ProcessImage(result, point.X, point.Y);
                }
                cancellation.Token.ThrowIfCancellationRequested();
                ReplaceProcessed(result);
                result = null;
                StatusMessage = "얼굴 효과를 적용했습니다.";
            }
            finally { result?.Dispose(); }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (!_disposed) StatusMessage = "처리를 취소했습니다. 원본 사진은 보관됩니다.";
        }
        catch (Exception error)
        {
            ReportFailure(error, "처리하지 못했습니다. Podman·머신·동봉 이미지를 확인하세요. 기존 사진은 보관됩니다.");
            try { await _client.DisconnectAsync(); _usesProcessing = false; }
            catch (Exception cleanup) { _logger.LogWarning("사진 처리 정리 오류 유형: {ErrorType}", cleanup.GetType().Name); }
        }
        finally
        {
            _operationCancellation = null;
            _operationCompletion = null;
            IsBusy = false;
            completion.TrySetResult();
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    public void SaveImage()
    {
        if (!CanSave()) return;
        try
        {
            var dialog = new CommonSaveFileDialog { Title = "이미지 저장", DefaultExtension = ".png" };
            dialog.Filters.Add(new CommonFileDialogFilter("PNG 파일", "*.png"));
            dialog.Filters.Add(new CommonFileDialogFilter("JPEG 파일", "*.jpg;*.jpeg"));
            dialog.Filters.Add(new CommonFileDialogFilter("BMP 파일", "*.bmp"));
            if (dialog.ShowDialog() != CommonFileDialogResult.Ok) { StatusMessage = "저장을 취소했습니다. 처리 결과는 보관됩니다."; return; }
            ImageFormat format = Path.GetExtension(dialog.FileName!).ToLowerInvariant() switch
            {
                ".png" => ImageFormat.Png,
                ".jpg" or ".jpeg" => ImageFormat.Jpeg,
                ".bmp" => ImageFormat.Bmp,
                _ => throw new IOException("지원하지 않는 이미지 확장자입니다.")
            };
            StatusMessage = MatToBitmapSaver.SaveMatAsBitmap(ProcessedImage!, dialog.FileName!, format)
                ? "이미지를 저장했습니다." : "저장하지 못했습니다. 처리 결과는 보관됩니다.";
        }
        catch (Exception error) { ReportFailure(error, "저장하지 못했습니다. 처리 결과는 보관됩니다."); }
    }

    [RelayCommand]
    public async Task NavigateSelectMode()
    {
        if (_disposed || _leaving) return;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _navigationCompletion = completion;
        _leaving = true;
        RefreshCommands();
        try
        {
            await CancelOperationAsync();
            await _client.DisconnectAsync();
            _usesProcessing = false;
            ReleaseImages();
            if (!_disposed) Messenger.Send(new ValueChangedMessage<string>(nameof(SelectModeViewModel)));
        }
        catch (Exception error) { ReportFailure(error, "처리 환경 정리를 확인하지 못했습니다. 다시 시도해 주세요."); }
        finally
        {
            _leaving = false;
            _navigationCompletion = null;
            RefreshCommands();
            completion.TrySetResult();
        }
    }

    private async Task CancelOperationAsync()
    {
        Task? pending = _operationCompletion?.Task;
        _operationCancellation?.Cancel();
        if (pending is not null) await pending;
    }

    private void ReplaceProcessed(Mat? image)
    {
        Mat? previous = ProcessedImage;
        ProcessedImage = image;
        previous?.Dispose();
    }

    private void ReleaseImages()
    {
        ReplaceProcessed(null);
        Mat? previous = OriginalImage;
        OriginalImage = null;
        previous?.Dispose();
    }

    private void ReportFailure(Exception error, string message)
    {
        _logger.LogWarning("정적 사진 작업 오류 유형: {ErrorType}", error.GetType().Name);
        if (!_disposed) StatusMessage = message;
    }

    private void RefreshCommands()
    {
        LoadImageCommand.NotifyCanExecuteChanged();
        NoEffectCommand.NotifyCanExecuteChanged();
        ApplyDrawingRudolphEffectCommand.NotifyCanExecuteChanged();
        ApplyOverlayDeadpoolEffectCommand.NotifyCanExecuteChanged();
        SaveImageCommand.NotifyCanExecuteChanged();
    }

    public ValueTask DisposeAsync() => new(_disposeTask ??= DisposeCoreAsync());
    private async Task DisposeCoreAsync()
    {
        _disposed = true;
        await CancelOperationAsync();
        if (_navigationCompletion is { } navigation) await navigation.Task;
        try
        {
            if (_usesProcessing)
            {
                await _client.DisconnectAsync();
                _usesProcessing = false;
            }
        }
        finally { ReleaseImages(); IsActive = false; }
    }
}
