namespace OhMyRudolph.Core.Helpers;

public readonly record struct FrameStamp(long Generation, string CameraId);

/// <summary>Serializes publication with invalidation so a late result cannot enter a newer camera session.</summary>
public sealed class FrameGeneration
{
    private readonly object _gate = new();
    private long _generation;
    private string? _cameraId;

    public FrameStamp Begin(string cameraId)
    {
        ArgumentException.ThrowIfNullOrEmpty(cameraId);
        lock (_gate)
        {
            _cameraId = cameraId;
            return new FrameStamp(++_generation, cameraId);
        }
    }

    public void Invalidate()
    {
        lock (_gate)
        {
            ++_generation;
            _cameraId = null;
        }
    }

    public bool IsCurrent(FrameStamp stamp)
    {
        lock (_gate)
            return Matches(stamp);
    }

    public bool TryRunCurrent(FrameStamp stamp, Action publish)
    {
        ArgumentNullException.ThrowIfNull(publish);
        lock (_gate)
        {
            if (!Matches(stamp))
                return false;
            publish();
            return true;
        }
    }

    private bool Matches(FrameStamp stamp) => _cameraId is not null &&
        stamp.Generation == _generation && string.Equals(stamp.CameraId, _cameraId, StringComparison.Ordinal);
}
