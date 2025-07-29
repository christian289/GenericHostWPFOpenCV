using OpenCVFindContour.Interfaces;

namespace OpenCVFindContour.Services;

public sealed class ActivatedCameraHandleService : IActivatedCameraHandleService
{
    private readonly VideoCapture _videoCapture;
    private readonly Subject<Mat?>? _imageSubject;
    private readonly IDisposable _captureSubscription;
    private volatile bool _isCapturing;

    public ActivatedCameraHandleService(int cameraIndex, VideoCapture videoCapture, int fps = 30)
    {
        CameraIndex = cameraIndex;
        _videoCapture = videoCapture;
        Fps = fps;

        _videoCapture.Set(VideoCaptureProperties.FrameWidth, 1920);
        _videoCapture.Set(VideoCaptureProperties.FrameHeight, 1080);
        _videoCapture.Set(VideoCaptureProperties.Fps, fps);

        var interval = TimeSpan.FromMilliseconds(1000.0 / fps);

        _captureSubscription = Observable
            .Interval(interval)
            .ObserveOn(TaskPoolScheduler.Default)
            .Where(_ => _isCapturing)
            .Select(_ => CaptureFrame())
            .Where(mat => mat != null && !mat.Empty())
            .Subscribe(
                onNext: mat => _imageSubject?.OnNext(mat),
                onError: ex => HandleCaptureError(ex)
            );
    }

    public int CameraIndex { get; init; }
    public int Fps { get; init; }
    public IObservable<Mat?>? ImageStream => _imageSubject?.AsObservable();

    private Mat? CaptureFrame()
    {
        try
        {
            var mat = new Mat();
            
            if (_videoCapture.Read(mat) && !mat.Empty())
                return mat;

            mat.Dispose();
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"프레임 캡처 실패: {ex.Message}");
            return null;
        }
    }

    private void HandleCaptureError(Exception ex)
    {
        Console.WriteLine($"캡처 스트림 에러: {ex.Message}");
        // 자동 복구 로직
        Task.Delay(1000).ContinueWith(_ =>
        {
            if (_isCapturing) StartCapture();
        });
    }

    public void StartCapture() => _isCapturing = true;
    public void StopCapture() => _isCapturing = false;

    public void Dispose()
    {
        _isCapturing = false;
        _captureSubscription?.Dispose();
        _imageSubject?.OnCompleted();
        _imageSubject?.Dispose();
        _videoCapture?.Dispose();
    }
}
