using OhMyRudolph.Abstractions.FaceMesh;
using OhMyRudolph.Core.Helpers;

namespace OhMyRudolph.Core.Services;

public enum CameraCaptureState { Stopped, Running, ReadFailed, Disposed }

public sealed class CameraService : IAsyncDisposable
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(3);
    private readonly ILogger _logger;
    private readonly VideoCapture _videoCapture;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly object _nativeGate = new();
    private readonly object _consumerGate = new();
    private readonly FrameGeneration _generation = new();
    private Action<CameraFrame>? _consumer;
    private CancellationTokenSource? _captureCancellation;
    private Task _captureLoop = Task.CompletedTask;
    private Task? _releaseTask;
    private volatile bool _isCapturing;
    private volatile bool _isDisposed;
    private volatile CameraCaptureState _state;

    public CameraService(ILogger logger, int cameraIndex, VideoCapture videoCapture)
    {
        _logger = logger;
        CameraIndex = cameraIndex;
        CameraId = cameraIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _videoCapture = videoCapture ?? throw new ArgumentNullException(nameof(videoCapture));
    }

    public string DisplayName => $"Camera {CameraIndex}";
    public string CameraId { get; }
    public int CameraIndex { get; }
    public bool IsCapturing => _isCapturing && !_isDisposed;
    public CameraCaptureState State => _state;
    public string SessionId { get; private set; } = string.Empty;
    public event EventHandler? StateChanged;

    /// <summary>The only subscriber receives exclusive frame ownership. Its callback must not await.</summary>
    public IDisposable SubscribeFrames(Action<CameraFrame> consumer)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        lock (_consumerGate)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (_consumer is not null)
                throw new InvalidOperationException("카메라 프레임 소비자는 하나만 등록할 수 있습니다.");
            _consumer = consumer;
            return new FrameSubscription(this, consumer);
        }
    }

    public void InitializeCamera(int renderWidth = 1920, int renderHeight = 1080, int fps = 30)
    {
        lock (_nativeGate)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _videoCapture.Set(VideoCaptureProperties.FrameWidth, renderWidth);
            _videoCapture.Set(VideoCaptureProperties.FrameHeight, renderHeight);
            _videoCapture.Set(VideoCaptureProperties.Fps, fps);
            // Keep backend BGR conversion enabled; the presentation/encoding path expects decoded pixels.
        }
    }

    public async Task<bool> StartCaptureAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (_isCapturing)
                return true;
            if (!_captureLoop.IsCompleted)
                throw new InvalidOperationException("이전 카메라 읽기가 아직 종료되지 않았습니다.");
            if (!await IsConnectedCoreAsync(cancellationToken).ConfigureAwait(false))
                return false;
            cancellationToken.ThrowIfCancellationRequested();

            _captureCancellation?.Dispose();
            _captureCancellation = new CancellationTokenSource();
            SessionId = Guid.NewGuid().ToString("N");
            FrameStamp stamp = _generation.Begin(CameraId);
            _isCapturing = true;
            SetState(CameraCaptureState.Running);
            string sessionId = SessionId;
            CancellationToken token = _captureCancellation.Token;
            _captureLoop = Task.Run(() => CaptureLoopAsync(sessionId, stamp, token), CancellationToken.None);
            return true;
        }
        finally { _lifecycle.Release(); }
    }

    private async Task CaptureLoopAsync(string sessionId, FrameStamp stamp, CancellationToken token)
    {
        long frameId = 0;
        try
        {
            while (!token.IsCancellationRequested)
            {
                Mat? image = new();
                try
                {
                    bool read;
                    lock (_nativeGate)
                        read = _videoCapture.Read(image);
                    long capturedTimestamp = Stopwatch.GetTimestamp();
                    if (token.IsCancellationRequested || !_generation.IsCurrent(stamp))
                        return;
                    if (!read || image.Empty())
                    {
                        _generation.TryRunCurrent(stamp, () => SetState(CameraCaptureState.ReadFailed));
                        await Task.Delay(100, token).ConfigureAwait(false);
                        continue;
                    }

                    _generation.TryRunCurrent(stamp, () => SetState(CameraCaptureState.Running));
                    var metadata = new FaceMeshFrameMetadata(sessionId, CameraId, ++frameId,
                        image.Width, image.Height, false, 0);
                    var frame = new CameraFrame(image, metadata, stamp, capturedTimestamp, token);
                    image = null;
                    lock (_consumerGate)
                    {
                        if (_consumer is null || token.IsCancellationRequested)
                            frame.Dispose();
                        else
                        {
                            try { _consumer(frame); }
                            catch
                            {
                                frame.Dispose();
                                throw;
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                catch (Exception ex)
                {
                    if (token.IsCancellationRequested)
                        return;
                    if (State != CameraCaptureState.ReadFailed)
                        _logger.LogWarning("카메라 프레임을 읽지 못했습니다. 오류 유형: {ErrorType}", ex.GetType().Name);
                    _generation.TryRunCurrent(stamp, () => SetState(CameraCaptureState.ReadFailed));
                    await Task.Delay(100, token).ConfigureAwait(false);
                }
                finally { image?.Dispose(); }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    /// <summary>Publication and stop invalidation share a lock; never publishes a stopped session.</summary>
    public bool TryPublish(CameraFrame frame, Action publish)
    {
        bool published = false;
        _generation.TryRunCurrent(frame.Stamp, () =>
        {
            if (IsCapturing && !frame.CancellationToken.IsCancellationRequested)
            {
                publish();
                published = true;
            }
        });
        return published;
    }

    public async Task StopCaptureAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await StopCoreAsync().ConfigureAwait(false); }
        finally { _lifecycle.Release(); }
    }

    private async Task StopCoreAsync()
    {
        _isCapturing = false;
        _generation.Invalidate();
        _captureCancellation?.Cancel();
        SetState(_isDisposed ? CameraCaptureState.Disposed : CameraCaptureState.Stopped);
        // Native DirectShow Read cannot be safely aborted. Bound the wait, never dispose its handle concurrently.
        await _captureLoop.WaitAsync(StopTimeout).ConfigureAwait(false);
        _captureCancellation?.Dispose();
        _captureCancellation = null;
    }

    public async Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return !_isDisposed && await IsConnectedCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { _lifecycle.Release(); }
    }

    private Task<bool> IsConnectedCoreAsync(CancellationToken token) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        lock (_nativeGate)
            return _videoCapture.IsOpened();
    }, token);

    private void SetState(CameraCaptureState state)
    {
        if (_state == state)
            return;
        _state = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_isDisposed)
                return;
            _isDisposed = true;
            lock (_consumerGate) { _consumer = null; }
            try { await StopCoreAsync().ConfigureAwait(false); }
            catch (TimeoutException)
            {
                _logger.LogWarning("카메라 드라이버 읽기 종료가 지연되어 핸들 해제를 읽기 완료 후로 연기합니다.");
                _releaseTask = ReleaseAfterCaptureAsync();
                return;
            }
            lock (_nativeGate) { _videoCapture.Dispose(); }
        }
        finally { _lifecycle.Release(); }
    }

    private async Task ReleaseAfterCaptureAsync()
    {
        try
        {
            await _captureLoop.ConfigureAwait(false);
            lock (_nativeGate) { _videoCapture.Dispose(); }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("카메라 지연 해제 오류 유형: {ErrorType}", ex.GetType().Name);
        }
        finally
        {
            _captureCancellation?.Dispose();
            _captureCancellation = null;
        }
    }

    private sealed class FrameSubscription(CameraService owner, Action<CameraFrame> consumer) : IDisposable
    {
        private CameraService? _owner = owner;
        public void Dispose()
        {
            var service = Interlocked.Exchange(ref _owner, null);
            if (service is null) return;
            lock (service._consumerGate)
                if (service._consumer == consumer)
                    service._consumer = null;
        }
    }
}
