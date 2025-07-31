using System.Reactive;

namespace OpenCVFindContour.Services;

public sealed class ActivatedCameraHandleService : IDisposable
{
    private readonly ILogger logger;
    private readonly VideoCapture _videoCapture;
    private readonly Subject<Mat> _imageSubject;
    private IDisposable _captureSubscription;
    private readonly SemaphoreSlim _captureSemaphore;
    private CancellationTokenSource _cancellationTokenSource;
    private volatile bool _isCapturing;
    private volatile bool _isDisposed;

    public ActivatedCameraHandleService(
        ILogger logger,
        int cameraIndex,
        VideoCapture videoCapture)
    {
        this.logger = logger;
        CameraIndex = cameraIndex;
        _videoCapture = videoCapture ?? throw new ArgumentNullException(nameof(videoCapture));

        _imageSubject = new Subject<Mat>();
        _captureSemaphore = new SemaphoreSlim(1, 1);
    }

    public string DisplayName => $"Camera {CameraIndex}";
    public int CameraIndex { get; }
    public bool IsCapturing => _isCapturing && !_isDisposed;
    public IObservable<Mat> ImageStream => _imageSubject.AsObservable();

    public void InitializeCamera(int renderWidth = 1920, int renderHeight = 1080, int fps = 30)
    {
        try
        {
            _videoCapture.Set(VideoCaptureProperties.FrameWidth, renderWidth);
            _videoCapture.Set(VideoCaptureProperties.FrameHeight, renderHeight);
            _videoCapture.Set(VideoCaptureProperties.Fps, fps); // FPS 설정
            _videoCapture.Set(VideoCaptureProperties.ConvertRgb, 0); // 불필요한 색상 변환을 방지
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"카메라 초기화 실패: {ex.Message}");
            throw new InvalidOperationException($"카메라 초기화 실패: {ex.Message}", ex);
        }
    }

    private async Task<Mat?> CaptureFrameAsync()
    {
        if (_isDisposed || !_isCapturing)
            return null;

        await _captureSemaphore.WaitAsync(_cancellationTokenSource.Token);
        try
        {
            return await Task.Run(() =>
            {
                var mat = new Mat();
                if (_videoCapture.Read(mat) && !mat.Empty())
                    return mat;

                mat.Dispose();
                return null;
            }, _cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"프레임 캡처 실패: {ex.Message}");
            return null;
        }
        finally
        {
            _captureSemaphore.Release();
        }
    }

    private void HandleCaptureError(Exception ex)
    {
        if (ex is TaskCanceledException)
        {
            logger.ZLogInformation($"카메라 종료");
            return;
        }

        logger.ZLogError(ex, $"캡처 스트림 에러: {ex.Message}");

        // 자동 복구 로직 (더 안전하게 개선)
        _ = Task.Run(async () =>
        {
            await Task.Delay(1000, _cancellationTokenSource.Token);
            if (_isCapturing && !_isDisposed)
            {
                await StartCaptureAsync();
            }
        }, _cancellationTokenSource.Token);
    }

    public async Task<bool> StartCaptureAsync()
    {
        if (_isDisposed)
            return false;

        try
        {
            var isConnected = await IsConnectedAsync();
            if (!isConnected)
                return false;

            _isCapturing = true;
            _cancellationTokenSource = new CancellationTokenSource();
            _captureSubscription?.Dispose();
            _captureSubscription = Observable
                .While(() => !_cancellationTokenSource.Token.IsCancellationRequested,
                    Observable.FromAsync(CaptureFrameAsync)
                        .Where(mat => mat is not null && !mat.Empty())
                )
                .Subscribe(
                    onNext: mat =>
                    {
                        if (!_isDisposed && mat is not null)
                            _imageSubject.OnNext(mat);
                    },
                    onError: (ex) =>
                    {
                        _imageSubject.OnCompleted();
                        HandleCaptureError(ex);
                    },
                    onCompleted: () =>
                    {
                        _imageSubject.OnNext(null);
                        _imageSubject.OnCompleted();
                    });

            return true;
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"캡처 시작 실패: {ex.Message}");
            return false;
        }
    }

    public async Task StopCaptureAsync()
    {
        _isCapturing = false;
        _cancellationTokenSource.Cancel();
        _captureSubscription?.Dispose();
        // 현재 진행 중인 캡처 작업이 완료될 때까지 대기
        await _captureSemaphore.WaitAsync();
        _captureSemaphore.Release();
    }

    public async Task<bool> IsConnectedAsync()
    {
        if (_isDisposed)
            return false;

        return await Task.Run(() =>
        {
            try
            {
                return _videoCapture.IsOpened();
            }
            catch
            {
                return false;
            }
        });
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _isCapturing = false;

        _cancellationTokenSource.Cancel();

        try
        {
            _captureSubscription?.Dispose();
            _imageSubject?.OnCompleted();
            _imageSubject?.Dispose();
            _captureSemaphore?.Dispose();
            _cancellationTokenSource?.Dispose();
            _videoCapture?.Dispose();
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"Dispose 중 오류: {ex.Message}");
        }
    }
}