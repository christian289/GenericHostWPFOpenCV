

namespace OpenCVFindContour.Interfaces;

public interface IActivatedCameraHandleService : IDisposable
{
    int CameraIndex { get; init; }
    int Fps { get; init; }
    IObservable<Mat?>? ImageStream { get; }

    void StartCapture();
    void StopCapture();
}
