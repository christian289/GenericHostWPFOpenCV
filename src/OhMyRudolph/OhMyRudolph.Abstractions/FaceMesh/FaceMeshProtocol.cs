namespace OhMyRudolph.Abstractions.FaceMesh;

public sealed record FaceMeshFrameMetadata(
    string SessionId, string CameraId, long FrameId, int Width, int Height,
    bool Mirrored, int RotationDegrees);

public enum FaceMeshStatus { Ok, NoFace }

public sealed record FaceMeshResponse(
    FaceMeshFrameMetadata Metadata, FaceMeshStatus Status,
    IReadOnlyList<(int X, int Y)> Landmarks);

public sealed class FaceMeshServerException(string message) : IOException(message) { }
