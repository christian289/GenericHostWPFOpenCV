namespace OhMyRudolph.Abstractions.FaceMesh;

public sealed record FaceMeshFrameMetadata(
    string SessionId, string CameraId, long FrameId, int Width, int Height,
    bool Mirrored, int RotationDegrees);

public enum FaceMeshStatus { Ok, NoFace }

public readonly record struct FaceMeshPoint(int X, int Y);

public sealed record FaceMeshResponse(
    FaceMeshFrameMetadata Metadata, FaceMeshStatus Status,
    IReadOnlyList<FaceMeshPoint> Landmarks);

