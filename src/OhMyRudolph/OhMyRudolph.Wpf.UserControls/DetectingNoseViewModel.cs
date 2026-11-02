using OhMyRudolph.Abstractions.FaceMesh;
using OhMyRudolph.Core.Clients;
using OhMyRudolph.Core.Effects;
using OhMyRudolph.Core.Helpers;
using OhMyRudolph.Core.Managers;
using OhMyRudolph.Core.Services;
using OpenCvSharp.WpfExtensions;
using System.Windows.Media.Imaging;

namespace OhMyRudolph.Wpf.UserControls;

public enum PreviewInferenceStatus { Stopped, Disabled, Connecting, Ready, NoFace, ConnectionFailed, InferenceFailed, CameraReadFailed, FrameFailed }

public sealed partial class DetectingNoseViewModel : ObservableRecipient,
    IRecipient<PropertyChangedMessage<CameraService>>, IRecipient<PropertyChangedMessage<bool>>, IAsyncDisposable
{
    private readonly ILogger<DetectingNoseViewModel> _logger;
    private readonly ContainerFaceMeshClient _faceMeshClient;
    private readonly object _sessionGate = new();
    private readonly FrameGeneration _generation = new();
    private readonly LatestFrameProcessor<PendingFrame> _processor;
    private readonly SynchronizationContext? _uiContext;
    private IDisposable? _subscription;
    private CameraService? _camera;
    private CancellationTokenSource? _requestCancellation;
    private Task _disconnectTask = Task.CompletedTask;
    private Task? _disposeTask;
    private FrameStamp _publishedStamp;
    private long _publishedCapturedTimestamp;
    private string _publishedCameraSessionId = string.Empty;
    private Mat? _printMat;
    private volatile bool _applyingDrawingRudolphEffect;
    private bool _disposed;
    private int _retryVersion;
    // Only the single worker accesses transport/retry state below.
    private long _transportGeneration = -1;
    private int _transportRetryVersion = -1;
    private bool _connected;
    private bool _inferenceBlocked;
    private PreviewInferenceStatus _blockedStatus;
    private string _blockedMessage = string.Empty;

    public DetectingNoseViewModel(ILogger<DetectingNoseViewModel> logger, ContainerFaceMeshClient faceMeshClient)
    {
        _logger = logger;
        _faceMeshClient = faceMeshClient;
        _uiContext = SynchronizationContext.Current;
        _processor = new LatestFrameProcessor<PendingFrame>(ProcessFrameAsync, OnProcessingError);
        IsActive = true;
    }

    /// <summary>Borrowed presentation Mat. Never dispose/mutate it; capture with CaptureSnapshot instead.</summary>
    public Mat? PrintMat => _printMat;

    [ObservableProperty] private BitmapSource? _previewImage;
    [ObservableProperty]
    [NotifyPropertyChangedRecipients]
    private bool _effectApplyAvailable;
    [ObservableProperty] private PreviewInferenceStatus _inferenceStatus = PreviewInferenceStatus.Stopped;
    [ObservableProperty] private string _statusMessage = "카메라가 꺼져 있습니다.";

    /// <summary>One confirmed pixel copy. The caller owns and must dispose the returned Mat.</summary>
    public Mat CaptureSnapshot()
    {
        lock (_sessionGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_printMat is null || _camera is null || !CaptureFreshness.CanCapture(
                _camera.IsCapturing && _camera.State == CameraCaptureState.Running,
                _generation.IsCurrent(_publishedStamp) &&
                    string.Equals(_camera.SessionId, _publishedCameraSessionId, StringComparison.Ordinal),
                _publishedCapturedTimestamp, Stopwatch.GetTimestamp()))
                throw new InvalidOperationException("최근 1초 이내의 카메라 프레임이 없습니다. 연결을 확인한 뒤 다시 촬영해주세요.");
            return _printMat.Clone();
        }
    }

    public void Receive(PropertyChangedMessage<CameraService> message)
    {
        if (message.Sender is CameraManager && message.PropertyName == nameof(CameraManager.SelectedCamera))
            SwitchCamera(message.NewValue);
    }

    public void Receive(PropertyChangedMessage<bool> message)
    {
        if (message.Sender is not VideoViewModel || message.PropertyName != nameof(VideoViewModel.ApplyingDrawingRudolphEffect))
            return;
        lock (_sessionGate)
        {
            _applyingDrawingRudolphEffect = message.NewValue;
            // A pending effect must not keep no-effect preview waiting on a failed server.
            _requestCancellation?.Cancel();
        }
    }

    /// <summary>Allows an explicit inference retry; camera subscription remains single and idempotent.</summary>
    public Task RefreshSubscription(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sessionGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Interlocked.Increment(ref _retryVersion);
            _requestCancellation?.Cancel();
        }
        return Task.CompletedTask;
    }

    /// <summary>Invalidates presentation immediately and awaits all owned transport/container cleanup.</summary>
    public Task StopPreviewAsync(CancellationToken cancellationToken = default)
    {
        lock (_sessionGate)
        {
            SwitchCamera(null);
            return _disconnectTask.WaitAsync(cancellationToken);
        }
    }

    private void SwitchCamera(CameraService? camera)
    {
        lock (_sessionGate)
        {
            if (_disposed || ReferenceEquals(_camera, camera))
                return;
            _generation.Invalidate();
            _requestCancellation?.Cancel();
            _subscription?.Dispose();
            _subscription = null;
            if (_camera is not null)
                _camera.StateChanged -= OnCameraStateChanged;
            _camera = camera;
            // Every cleanup joins this chain before mode ownership can pass to another consumer.
            _disconnectTask = DisconnectTransportAsync(_disconnectTask);
            ClearPresentation();
            EffectApplyAvailable = false;
            InferenceStatus = camera is null ? PreviewInferenceStatus.Stopped : PreviewInferenceStatus.Disabled;
            StatusMessage = camera is null ? "카메라가 꺼져 있습니다." : "효과 없이 미리보기 중입니다.";
            if (camera is null)
                return;

            FrameStamp stamp = _generation.Begin(camera.CameraId);
            camera.StateChanged += OnCameraStateChanged;
            _subscription = camera.SubscribeFrames(frame => _processor.TryPost(new PendingFrame(camera, frame, stamp)));
        }
    }

    private async Task DisconnectTransportAsync(Task previous)
    {
        try
        {
            try { await previous.ConfigureAwait(false); }
            finally { await _faceMeshClient.DisconnectAsync().ConfigureAwait(false); }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("얼굴 감지 연결 종료 오류 유형: {ErrorType}", ex.GetType().Name);
            throw;
        }
    }

    private async Task ProcessFrameAsync(PendingFrame pending, CancellationToken workerToken)
    {
        CameraFrame frame = pending.Frame;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(workerToken, frame.CancellationToken);
        Task disconnected;
        bool applyEffect;
        int retryVersion;
        lock (_sessionGate)
        {
            if (_disposed || request.IsCancellationRequested || !_generation.IsCurrent(pending.Stamp))
                return;
            _requestCancellation = request;
            disconnected = _disconnectTask;
            applyEffect = _applyingDrawingRudolphEffect;
            retryVersion = _retryVersion;
        }

        try
        {
            if (_transportGeneration != pending.Stamp.Generation || _transportRetryVersion != retryVersion)
            {
                _transportGeneration = pending.Stamp.Generation;
                _transportRetryVersion = retryVersion;
                _connected = false;
                _inferenceBlocked = false;
            }
            PreviewInferenceStatus status = PreviewInferenceStatus.Disabled;
            string message = "효과 없이 미리보기 중입니다.";
            bool available = false;

            if (applyEffect && !_inferenceBlocked)
            {
                bool requesting = false;
                try
                {
                    await disconnected.WaitAsync(request.Token).ConfigureAwait(false);
                    if (!_connected)
                    {
                        await PublishStateAsync(pending, PreviewInferenceStatus.Connecting,
                            "로컬 처리 컨테이너 준비 중입니다. 최초 이미지 가져오기는 최대 5분입니다. 효과 없음을 선택하면 미리보기를 계속할 수 있습니다.", request.Token).ConfigureAwait(false);
                        Task startup;
                        lock (_sessionGate)
                        {
                            if (_disposed || request.IsCancellationRequested || !_generation.IsCurrent(pending.Stamp))
                                return;
                            startup = _faceMeshClient.StartAsync(request.Token);
                        }
                        await startup.ConfigureAwait(false);
                        _connected = true;
                    }
                    Task<FaceMeshResponse> inference;
                    lock (_sessionGate)
                    {
                        if (_disposed || request.IsCancellationRequested || !_generation.IsCurrent(pending.Stamp))
                            return;
                        requesting = true;
                        inference = _faceMeshClient.RequestAsync(frame.Image, frame.Metadata, request.Token);
                    }
                    // The worker must keep borrowing this Mat until the request itself has settled.
                    FaceMeshResponse response = await inference.ConfigureAwait(false);
                    request.Token.ThrowIfCancellationRequested();
                    if (response.Status == FaceMeshStatus.Ok)
                    {
                        foreach (var (x, y) in response.Landmarks)
                            DrawingRudolphEffect.ProcessImage(frame.Image, x, y);
                        status = PreviewInferenceStatus.Ready;
                        message = "루돌프 효과가 적용되었습니다.";
                        available = true;
                    }
                    else
                    {
                        status = PreviewInferenceStatus.NoFace;
                        message = "얼굴을 찾지 못했습니다. 카메라 정면을 바라봐주세요. 원본 미리보기는 계속됩니다.";
                    }
                }
                catch (OperationCanceledException) when (request.IsCancellationRequested)
                {
                    _connected = false;
                    return;
                }
                catch (Exception ex)
                {
                    lock (_sessionGate)
                    {
                        if (_disposed || request.IsCancellationRequested || !_generation.IsCurrent(pending.Stamp))
                            return;
                        _inferenceBlocked = true;
                        _connected = false;
                        _blockedStatus = requesting ? PreviewInferenceStatus.InferenceFailed : PreviewInferenceStatus.ConnectionFailed;
                        _blockedMessage = requesting
                            ? "얼굴 감지에 실패했습니다. 원본 미리보기로 촬영할 수 있으며 다시 시작하면 연결을 재시도합니다."
                            : "얼굴 감지 서버에 연결하지 못했습니다. 원본 미리보기로 촬영할 수 있으며 다시 시작하면 연결을 재시도합니다.";
                        // Do not hold the frame worker on cleanup: no-effect preview must keep flowing.
                        _disconnectTask = DisconnectTransportAsync(_disconnectTask);
                    }
                    _logger.LogWarning("얼굴 감지 오류 유형: {ErrorType}", ex.GetType().Name);
                }
            }

            if (applyEffect && _inferenceBlocked)
            {
                status = _blockedStatus;
                message = _blockedMessage;
            }
            request.Token.ThrowIfCancellationRequested();
            // ToBitmapSource copies all pixels; the frozen image never points at a disposed native Mat.
            BitmapSource bitmap = frame.Image.ToBitmapSource();
            bitmap.Freeze();
            await RunOnUiAsync(() =>
            {
                lock (_sessionGate)
                {
                    if (_disposed || applyEffect != _applyingDrawingRudolphEffect || request.IsCancellationRequested)
                        return;
                    _generation.TryRunCurrent(pending.Stamp, () => pending.Camera.TryPublish(frame, () =>
                    {
                        Mat? previous = _printMat;
                        _printMat = frame.TakeImage();
                        _publishedStamp = pending.Stamp;
                        _publishedCapturedTimestamp = frame.CapturedTimestamp;
                        _publishedCameraSessionId = frame.Metadata.SessionId;
                        try
                        {
                            PreviewImage = bitmap;
                            OnPropertyChanged(nameof(PrintMat));
                            EffectApplyAvailable = available;
                            InferenceStatus = status;
                            StatusMessage = message;
                        }
                        finally { previous?.Dispose(); }
                    }));
                }
            }, request.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
            _connected = false;
        }
        catch (Exception ex) when (!request.IsCancellationRequested)
        {
            _logger.LogWarning("미리보기 프레임 처리 오류 유형: {ErrorType}", ex.GetType().Name);
            await PublishStateAsync(pending, PreviewInferenceStatus.FrameFailed,
                "미리보기 프레임을 처리하지 못했습니다. 다음 프레임에서 다시 시도합니다.", request.Token).ConfigureAwait(false);
        }
        finally
        {
            lock (_sessionGate)
                if (ReferenceEquals(_requestCancellation, request))
                    _requestCancellation = null;
        }
    }

    private Task PublishStateAsync(PendingFrame pending, PreviewInferenceStatus status, string message, CancellationToken token) =>
        RunOnUiAsync(() =>
        {
            lock (_sessionGate)
            {
                if (_disposed || token.IsCancellationRequested)
                    return;
                _generation.TryRunCurrent(pending.Stamp, () => pending.Camera.TryPublish(pending.Frame, () =>
                {
                    EffectApplyAvailable = false;
                    InferenceStatus = status;
                    StatusMessage = message;
                }));
            }
        }, token);

    private void OnCameraStateChanged(object? sender, EventArgs e)
    {
        if (sender is not CameraService camera || camera.State != CameraCaptureState.ReadFailed)
            return;
        // State transitions only, not a dispatch operation for every captured frame.
        _uiContext?.Post(_ =>
        {
            lock (_sessionGate)
            {
                if (!_disposed && ReferenceEquals(_camera, camera) && camera.State == CameraCaptureState.ReadFailed)
                {
                    EffectApplyAvailable = false;
                    InferenceStatus = PreviewInferenceStatus.CameraReadFailed;
                    StatusMessage = "카메라 프레임을 읽지 못했습니다. 장치 연결을 확인해주세요.";
                }
            }
        }, null);
    }

    private Task RunOnUiAsync(Action action, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_uiContext is null || ReferenceEquals(SynchronizationContext.Current, _uiContext))
        {
            action();
            return Task.CompletedTask;
        }
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _uiContext.Post(_ =>
        {
            try
            {
                if (!token.IsCancellationRequested)
                    action();
                completion.TrySetResult();
            }
            catch (Exception ex) { completion.TrySetException(ex); }
        }, null);
        return completion.Task.WaitAsync(token);
    }

    private void OnProcessingError(Exception ex)
    {
        // The next camera frame remains processable; no reusable stream is completed by one failure.
        _logger.LogWarning("미리보기 프레임 처리 오류 유형: {ErrorType}", ex.GetType().Name);
    }

    private void ClearPresentation()
    {
        Mat? previous = _printMat;
        _printMat = null;
        try
        {
            PreviewImage = null;
            OnPropertyChanged(nameof(PrintMat));
        }
        finally { previous?.Dispose(); }
    }

    public ValueTask DisposeAsync()
    {
        lock (_sessionGate)
        {
            if (_disposeTask is not null)
                return new ValueTask(_disposeTask);
            SwitchCamera(null);
            _disposed = true;
            _generation.Invalidate();
            IsActive = false;
            _disposeTask = FinishDisposeAsync();
            return new ValueTask(_disposeTask);
        }
    }

    private async Task FinishDisposeAsync()
    {
        try { await _processor.DisposeAsync().ConfigureAwait(false); }
        finally { await _disconnectTask.ConfigureAwait(false); }
    }

    private sealed class PendingFrame(CameraService camera, CameraFrame frame, FrameStamp stamp) : IDisposable
    {
        public CameraService Camera { get; } = camera;
        public CameraFrame Frame { get; } = frame;
        public FrameStamp Stamp { get; } = stamp;
        public void Dispose() => Frame.Dispose();
    }
}
