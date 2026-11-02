using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using OhMyRudolph.ProcessingHost;
using OhMyRudolph.Interop;
using Xunit;

namespace OhMyRudolph.ProcessingHost.Tests;

public class ProtocolTests
{
    private const string Metadata = "{\"sessionId\":\"s\",\"cameraId\":\"c\",\"frameId\":1,\"width\":8,\"height\":8,\"mirrored\":false,\"rotationDegrees\":0}";

    private static byte[] Envelope(string metadata, byte[]? jpeg = null)
    {
        byte[] json = Encoding.UTF8.GetBytes(metadata);
        jpeg ??= [0xff, 0xd8, 0xff, 0xd9];
        byte[] result = new byte[4 + json.Length + jpeg.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(result, (uint)json.Length);
        json.CopyTo(result, 4);
        jpeg.CopyTo(result, 4 + json.Length);
        return result;
    }

    [Fact]
    public void ParsesMetadataAndRetainsJpegOffset()
    {
        byte[] bytes = Envelope(Metadata);
        Assert.True(FrameProtocol.TryParse(bytes, out var frame));
        Assert.Equal("s", frame.Metadata.SessionId);
        Assert.Equal(8, frame.Metadata.Width);
        Assert.Equal(new byte[] { 0xff, 0xd8, 0xff, 0xd9 }, bytes.AsSpan(frame.JpegOffset).ToArray());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"sessionId\":\"s\",\"cameraId\":\"c\",\"frameId\":1,\"width\":4097,\"height\":8,\"mirrored\":false,\"rotationDegrees\":0}")]
    [InlineData("{\"sessionId\":\"s\",\"cameraId\":\"c\",\"frameId\":1,\"width\":8,\"height\":8,\"mirrored\":false,\"rotationDegrees\":45}")]
    [InlineData("{\"sessionId\":\"s\",\"cameraId\":\"c\",\"frameId\":1,\"width\":\"8\",\"height\":8,\"mirrored\":false,\"rotationDegrees\":0}")]
    [InlineData("{\"sessionId\":\"s\",\"cameraId\":\"c\",\"frameId\":1,\"width\":8,\"height\":8,\"mirrored\":false,\"rotationDegrees\":0} {}")]
    [InlineData("not json")]
    public void RejectsInvalidMetadata(string metadata) => Assert.False(FrameProtocol.TryParse(Envelope(metadata), out _));

    [Fact]
    public void RejectsTruncatedOversizedAndEmptyJpeg()
    {
        Assert.False(FrameProtocol.TryParse([1, 2, 3], out _));
        Assert.False(FrameProtocol.TryParse(Envelope(new string(' ', 4097)), out _));
        Assert.False(FrameProtocol.TryParse(Envelope(Metadata, []), out _));
        byte[] truncated = Envelope(Metadata);
        BinaryPrimitives.WriteUInt32LittleEndian(truncated, uint.MaxValue);
        Assert.False(FrameProtocol.TryParse(truncated, out _));
    }

    [Fact]
    public void TotalBodyLimitIsInclusive()
    {
        byte[] withinLimit = new byte[FrameProtocol.MaxBodyBytes];
        byte[] prefix = Envelope(Metadata);
        prefix.CopyTo(withinLimit, 0);
        Assert.True(FrameProtocol.TryParse(withinLimit, out _));
        byte[] tooLarge = new byte[FrameProtocol.MaxBodyBytes + 1];
        prefix.CopyTo(tooLarge, 0);
        Assert.False(FrameProtocol.TryParse(tooLarge, out _));
    }

    [Fact]
    public void BearerRequiresExactValidToken()
    {
        string token = new('a', 64);
        var auth = new BearerToken(token);
        Assert.True(auth.IsAuthorized("Bearer " + token));
        Assert.False(auth.IsAuthorized("Bearer " + new string('b', 64)));
        Assert.False(auth.IsAuthorized("Basic " + token));
        Assert.False(auth.IsAuthorized("Bearer " + token + "0"));
        Assert.Throws<ArgumentException>(() => new BearerToken(new string('z', 64)));
    }

    [Theory]
    [InlineData(NativeStatus.InvalidInput, 400)]
    [InlineData(NativeStatus.ResourceUnavailable, 500)]
    [InlineData(NativeStatus.InferenceFailed, 500)]
    [InlineData(NativeStatus.OutputBufferTooSmall, 500)]
    public void NativeFailuresMapToHttpErrors(NativeStatus native, int expected) =>
        Assert.Equal(expected, FrameProtocol.StatusCode(native));

    [Fact]
    public void JsonPointContractUsesObjectCoordinatesAndNumericStatus()
    {
        var response = new OhMyRudolph.Abstractions.FaceMesh.FaceMeshResponse(
            new("s", "c", 1, 8, 8, false, 0), OhMyRudolph.Abstractions.FaceMesh.FaceMeshStatus.Ok,
            [new(2, 3)]);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(0, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("landmarks")[0].GetProperty("x").GetInt32());
        Assert.Equal(3, json.RootElement.GetProperty("landmarks")[0].GetProperty("y").GetInt32());
    }
}
