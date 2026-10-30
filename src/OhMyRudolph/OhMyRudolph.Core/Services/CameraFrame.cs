using OhMyRudolph.Abstractions.FaceMesh;
using OhMyRudolph.Core.Helpers;

namespace OhMyRudolph.Core.Services;

/// <summary>Exclusive ownership of one captured Mat. A subscriber must dispose or transfer this frame.</summary>
public sealed class CameraFrame(Mat image, FaceMeshFrameMetadata metadata, FrameStamp stamp,
    long capturedTimestamp, CancellationToken cancellationToken) : IDisposable
{
    private Mat? _image = image ?? throw new ArgumentNullException(nameof(image));
    public Mat Image => _image ?? throw new ObjectDisposedException(nameof(CameraFrame));
    public FaceMeshFrameMetadata Metadata { get; } = metadata;
    public FrameStamp Stamp { get; } = stamp;
    /// <summary>Monotonic Stopwatch timestamp immediately after the successful native read.</summary>
    public long CapturedTimestamp { get; } = capturedTimestamp;
    public CancellationToken CancellationToken { get; } = cancellationToken;

    /// <summary>Moves the Mat to the presentation owner without a pixel copy.</summary>
    public Mat TakeImage() => Interlocked.Exchange(ref _image, null) ?? throw new ObjectDisposedException(nameof(CameraFrame));

    public void Dispose() => Interlocked.Exchange(ref _image, null)?.Dispose();
}
