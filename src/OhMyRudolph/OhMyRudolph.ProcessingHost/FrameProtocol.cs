using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using OhMyRudolph.Abstractions.FaceMesh;
using OhMyRudolph.Interop;

namespace OhMyRudolph.ProcessingHost;

public readonly record struct ParsedFrame(FaceMeshFrameMetadata Metadata, int JpegOffset);

public static class FrameProtocol
{
    public const int MaxBodyBytes = 8 * 1024 * 1024;
    public const int MaxMetadataBytes = 4096;
    public const int MaxDimension = 4096;

    public static bool TryParse(ReadOnlySpan<byte> body, out ParsedFrame frame)
    {
        frame = default;
        if (body.Length < 5 || body.Length > MaxBodyBytes) return false;
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(body);
        if (length == 0 || length > MaxMetadataBytes || length >= body.Length - 4) return false;
        try
        {
            var reader = new Utf8JsonReader(body.Slice(4, (int)length));
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            if (reader.Read()) return false;
            JsonElement json = document.RootElement;
            if (json.ValueKind != JsonValueKind.Object ||
                !json.TryGetProperty("sessionId", out var sessionElement) || sessionElement.ValueKind != JsonValueKind.String ||
                !json.TryGetProperty("cameraId", out var cameraElement) || cameraElement.ValueKind != JsonValueKind.String ||
                !json.TryGetProperty("frameId", out var frameElement) || !frameElement.TryGetInt64(out long frameId) ||
                !json.TryGetProperty("width", out var widthElement) || !widthElement.TryGetInt32(out int width) ||
                !json.TryGetProperty("height", out var heightElement) || !heightElement.TryGetInt32(out int height) ||
                !json.TryGetProperty("mirrored", out var mirrorElement) || mirrorElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                !json.TryGetProperty("rotationDegrees", out var rotationElement) || !rotationElement.TryGetInt32(out int rotation)) return false;
            string? session = sessionElement.GetString();
            string? camera = cameraElement.GetString();
            if (string.IsNullOrWhiteSpace(session) || string.IsNullOrWhiteSpace(camera) ||
                width is < 1 or > MaxDimension || height is < 1 or > MaxDimension ||
                rotation is not (0 or 90 or 180 or 270)) return false;
            frame = new ParsedFrame(new FaceMeshFrameMetadata(session, camera, frameId, width, height,
                mirrorElement.GetBoolean(), rotation), 4 + (int)length);
            return true;
        }
        catch (JsonException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (FormatException) { return false; }
    }

    public static int StatusCode(NativeStatus status) => status == NativeStatus.InvalidInput ? 400 : 500;
}

public sealed class BearerToken
{
    private readonly byte[] _token;

    public BearerToken(string token)
    {
        if (token is null || token.Length != 64 || token.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("A 64-character hexadecimal token is required.", nameof(token));
        _token = System.Text.Encoding.ASCII.GetBytes(token);
    }

    public bool IsAuthorized(string? authorization)
    {
        const string prefix = "Bearer ";
        if (authorization is null || authorization.Length != prefix.Length + 64 ||
            !authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        Span<byte> candidate = stackalloc byte[64];
        for (int i = 0; i < candidate.Length; i++)
        {
            char c = authorization[prefix.Length + i];
            if (c > 127) return false;
            candidate[i] = (byte)c;
        }
        return CryptographicOperations.FixedTimeEquals(candidate, _token);
    }
}
