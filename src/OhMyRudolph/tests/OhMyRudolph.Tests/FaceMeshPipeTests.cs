using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using OhMyRudolph.Core.Clients;
using OhMyRudolph.Abstractions.FaceMesh;
using OpenCvSharp;
using Xunit;

namespace OhMyRudolph.Tests;

public class FaceMeshPipeTests
{
    private static FaceMeshFrameMetadata Metadata(long id = 1) => new("session", "camera", id, 8, 8, false, 0);
    private static Mat Image() => new(8, 8, MatType.CV_8UC3, Scalar.Black);
    private static string Name() => "ohmyrudolph-test-" + Guid.NewGuid().ToString("N");
    private static FaceMeshClient Client(string name, int timeoutMs = 1000) =>
        new(NullLogger<FaceMeshClient>.Instance, name, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(timeoutMs));
    private static NamedPipeServerStream Server(string name) => new(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

    private static async Task<JsonElement> ReadRequest(Stream stream)
    {
        byte[] header = new byte[4];
        await stream.ReadExactlyAsync(header);
        int size = BinaryPrimitives.ReadInt32LittleEndian(header);
        byte[] data = new byte[size];
        await stream.ReadExactlyAsync(data);
        int metadataSize = BinaryPrimitives.ReadInt32LittleEndian(data);
        using JsonDocument document = JsonDocument.Parse(data.AsMemory(4, metadataSize));
        Assert.True(data.Length > 4 + metadataSize);
        return document.RootElement.Clone();
    }

    private static byte[] Response(JsonElement metadata, string status = "no_face", string landmarks = "[]")
    {
        string text = "{\"metadata\":" + metadata.GetRawText() + ",\"status\":\"" + status + "\",\"landmarks\":" + landmarks + "}";
        byte[] body = Encoding.UTF8.GetBytes(text);
        byte[] result = new byte[body.Length + 4];
        BinaryPrimitives.WriteInt32LittleEndian(result, body.Length);
        body.CopyTo(result, 4);
        return result;
    }

    [Fact]
    public async Task Missing_server_connect_times_out()
    {
        await using var client = new FaceMeshClient(NullLogger<FaceMeshClient>.Instance, Name(),
            TimeSpan.FromMilliseconds(150), TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<TimeoutException>(() => client.ConnectAsync());
    }

    [Fact]
    public async Task Connecting_to_missing_server_can_be_cancelled()
    {
        await using var client = Client(Name());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ConnectAsync(cancellation.Token));
    }

    [Fact]
    public async Task Delayed_server_and_fragmented_response_are_supported()
    {
        string name = Name();
        await using var client = Client(name);
        var connecting = client.ConnectAsync();
        await Task.Delay(100);
        await using var server = Server(name);
        await server.WaitForConnectionAsync();
        await connecting;
        var serve = Task.Run(async () => {
            var request = await ReadRequest(server);
            foreach (byte b in Response(request, "ok", "[{\"x\":2,\"y\":3}]"))
                await server.WriteAsync(new byte[] { b });
        });
        using var image = Image();
        var result = await client.RequestAsync(image, Metadata());
        Assert.Equal(FaceMeshStatus.Ok, result.Status);
        Assert.Equal((2, 3), Assert.Single(result.Landmarks));
        await serve;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    public async Task Truncated_response_breaks_connection(int bytes)
    {
        string name = Name();
        await using var client = Client(name);
        var server = Server(name);
        var accepted = server.WaitForConnectionAsync();
        await client.ConnectAsync();
        await accepted;
        var serve = Task.Run(async () => {
            var request = await ReadRequest(server);
            byte[] response = Response(request);
            await server.WriteAsync(response.AsMemory(0, bytes));
            server.Dispose();
        });
        using var image = Image();
        await Assert.ThrowsAnyAsync<IOException>(() => client.RequestAsync(image, Metadata()));
        await serve;
        await Assert.ThrowsAnyAsync<IOException>(() => client.RequestAsync(image, Metadata()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65537)]
    public async Task Invalid_response_length_is_rejected(int length)
    {
        string name = Name();
        await using var client = Client(name);
        await using var server = Server(name);
        var accepted = server.WaitForConnectionAsync();
        await client.ConnectAsync();
        await accepted;
        var serve = Task.Run(async () => {
            await ReadRequest(server);
            byte[] header = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(header, length);
            await server.WriteAsync(header);
        });
        using var image = Image();
        await Assert.ThrowsAnyAsync<IOException>(() => client.RequestAsync(image, Metadata()));
        await serve;
    }

    [Fact]
    public async Task No_face_is_not_an_error_but_server_error_is()
    {
        string name = Name();
        await using var client = Client(name);
        await using var server = Server(name);
        var accepted = server.WaitForConnectionAsync();
        await client.ConnectAsync();
        await accepted;
        var serve = Task.Run(async () => {
            var first = await ReadRequest(server);
            await server.WriteAsync(Response(first));
            var second = await ReadRequest(server);
            await server.WriteAsync(Response(second, "error"));
        });
        using var image = Image();
        Assert.Equal(FaceMeshStatus.NoFace, (await client.RequestAsync(image, Metadata())).Status);
        await Assert.ThrowsAsync<FaceMeshServerException>(() => client.RequestAsync(image, Metadata(2)));
        await serve;
    }

    [Fact]
    public async Task Timeout_invalidates_connection_and_reconnect_succeeds()
    {
        string name = Name();
        await using var client = Client(name, 150);
        var server = Server(name);
        var accepted = server.WaitForConnectionAsync();
        await client.ConnectAsync();
        await accepted;
        using var image = Image();
        var read = ReadRequest(server);
        var request = client.RequestAsync(image, Metadata());
        await read;
        await Assert.ThrowsAsync<TimeoutException>(() => request);
        server.Dispose();
        await using var next = Server(name);
        var nextAccepted = next.WaitForConnectionAsync();
        await client.ConnectAsync();
        await nextAccepted;
        var serve = Task.Run(async () => await next.WriteAsync(Response(await ReadRequest(next))));
        Assert.Equal(FaceMeshStatus.NoFace, (await client.RequestAsync(image, Metadata(2))).Status);
        await serve;
    }

    [Fact]
    public async Task Cancellation_interrupts_response_and_breaks_connection()
    {
        string name = Name();
        await using var client = Client(name);
        await using var server = Server(name);
        var accepted = server.WaitForConnectionAsync();
        await client.ConnectAsync();
        await accepted;
        using var image = Image();
        using var cts = new CancellationTokenSource();
        var request = client.RequestAsync(image, Metadata(), cts.Token);
        await ReadRequest(server);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        await Assert.ThrowsAnyAsync<IOException>(() => client.RequestAsync(image, Metadata()));
    }

    [Fact]
    public async Task Disconnect_interrupts_an_inflight_request()
    {
        string name = Name();
        await using var client = Client(name);
        await using var server = Server(name);
        var accepted = server.WaitForConnectionAsync();
        await client.ConnectAsync();
        await accepted;
        using var image = Image();
        var request = client.RequestAsync(image, Metadata());
        await ReadRequest(server);
        var disconnect = client.DisconnectAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        await disconnect;
        await Assert.ThrowsAnyAsync<IOException>(() => client.RequestAsync(image, Metadata()));
    }

    [Fact]
    public async Task Dispose_rejects_a_connect_already_waiting_for_an_inflight_request()
    {
        string name = Name();
        var client = Client(name);
        await using var server = Server(name);
        var accepted = server.WaitForConnectionAsync();
        await client.ConnectAsync();
        await accepted;
        using var image = Image();
        var request = client.RequestAsync(image, Metadata());
        await ReadRequest(server);
        var queuedConnect = client.ConnectAsync();
        var dispose = client.DisposeAsync().AsTask();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => queuedConnect);
        }
        finally
        {
            await dispose;
        }
    }

    [Fact]
    public async Task Concurrent_disposals_all_finish_before_future_operations_are_rejected()
    {
        string name = Name();
        var client = Client(name);
        await using var server = Server(name);
        var accepted = server.WaitForConnectionAsync();
        await client.ConnectAsync();
        await accepted;
        using var image = Image();
        var request = client.RequestAsync(image, Metadata());
        await ReadRequest(server);
        var first = client.DisposeAsync().AsTask();
        var second = client.DisposeAsync().AsTask();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        await Task.WhenAll(first, second);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.ConnectAsync());
    }

    [Fact]
    public async Task Concurrent_requests_are_serialized_and_keep_metadata()
    {
        string name = Name();
        await using var client = Client(name);
        await using var server = Server(name);
        var accepted = server.WaitForConnectionAsync();
        await client.ConnectAsync();
        await accepted;
        var serve = Task.Run(async () => {
            for (int i = 0; i < 2; i++) {
                var metadata = await ReadRequest(server);
                await Task.Delay(30);
                await server.WriteAsync(Response(metadata));
            }
        });
        using var a = Image();
        using var b = Image();
        var results = await Task.WhenAll(client.RequestAsync(a, Metadata(1)), client.RequestAsync(b, Metadata(2)));
        Assert.Equal(1, results[0].Metadata.FrameId);
        Assert.Equal(2, results[1].Metadata.FrameId);
        await serve;
    }

    [Fact]
    public async Task Mismatched_response_metadata_is_not_accepted()
    {
        string name = Name();
        await using var client = Client(name);
        await using var server = Server(name);
        var accepted = server.WaitForConnectionAsync();
        await client.ConnectAsync();
        await accepted;
        var serve = Task.Run(async () => {
            await ReadRequest(server);
            using var wrong = JsonDocument.Parse("{\"sessionId\":\"other\",\"cameraId\":\"camera\",\"frameId\":1,\"width\":8,\"height\":8,\"mirrored\":false,\"rotationDegrees\":0}");
            await server.WriteAsync(Response(wrong.RootElement));
        });
        using var image = Image();
        await Assert.ThrowsAnyAsync<IOException>(() => client.RequestAsync(image, Metadata()));
        await serve;
    }
}
