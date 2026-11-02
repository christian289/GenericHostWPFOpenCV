using System.Net;
using System.Net.Sockets;
using System.Text;
using OhMyRudolph.Abstractions.FaceMesh;
using OhMyRudolph.Core.Clients;
using OhMyRudolph.Core.Containers;
using OpenCvSharp;

namespace OhMyRudolph.Tests;

public class ContainerHttpTests
{
    private static readonly FaceMeshFrameMetadata Metadata = new("session", "camera", 1, 8, 8, false, 0);

    [Theory]
    [InlineData("{\"metadata\":{\"sessionId\":\"other\",\"cameraId\":\"camera\",\"frameId\":1,\"width\":8,\"height\":8,\"mirrored\":false,\"rotationDegrees\":0},\"status\":1,\"landmarks\":[]}")]
    [InlineData("{\"metadata\":{\"sessionId\":\"session\",\"cameraId\":\"camera\",\"frameId\":1,\"width\":8,\"height\":8,\"mirrored\":false,\"rotationDegrees\":0},\"status\":0,\"landmarks\":[{\"x\":8,\"y\":1}]}")]
    [InlineData("{\"metadata\":{\"sessionId\":\"session\",\"cameraId\":\"camera\",\"frameId\":1,\"width\":8,\"height\":8,\"mirrored\":false,\"rotationDegrees\":0},\"status\":1,\"landmarks\":[{\"x\":1,\"y\":1}]}")]
    [InlineData("{\"metadata\":{\"sessionId\":\"session\",\"cameraId\":\"camera\",\"frameId\":1,\"width\":8,\"height\":8,\"mirrored\":false,\"rotationDegrees\":0},\"status\":0,\"landmarks\":[]}")]
    [InlineData("{\"metadata\":{\"sessionId\":\"session\",\"cameraId\":\"camera\",\"frameId\":1,\"width\":8,\"height\":8,\"mirrored\":false,\"rotationDegrees\":0},\"status\":2,\"landmarks\":[]}")]
    public async Task Invalid_response_is_rejected(string response)
    {
        await using var server = new LocalServer(response, 200);
        await using var client = new ContainerFaceMeshClient(new FakeRuntime(server.Address));
        await client.StartAsync();
        using var image = new Mat(8, 8, MatType.CV_8UC3, Scalar.Black);
        await Assert.ThrowsAsync<IOException>(() => client.RequestAsync(image, Metadata));
        await server.Completion;
    }

    [Fact]
    public async Task No_face_is_a_valid_result()
    {
        const string response = "{\"metadata\":{\"sessionId\":\"session\",\"cameraId\":\"camera\",\"frameId\":1,\"width\":8,\"height\":8,\"mirrored\":false,\"rotationDegrees\":0},\"status\":1,\"landmarks\":[]}";
        await using var server = new LocalServer(response, 200);
        await using var client = new ContainerFaceMeshClient(new FakeRuntime(server.Address));
        await client.StartAsync();
        using var image = new Mat(8, 8, MatType.CV_8UC3, Scalar.Black);
        var result = await client.RequestAsync(image, Metadata);
        Assert.Equal(FaceMeshStatus.NoFace, result.Status);
        Assert.Empty(result.Landmarks);
        await server.Completion;
    }

    [Fact]
    public async Task Authentication_failure_is_not_empty_success()
    {
        await using var server = new LocalServer("{\"code\":\"unauthorized\"}", 401);
        await using var client = new ContainerFaceMeshClient(new FakeRuntime(server.Address));
        await client.StartAsync();
        using var image = new Mat(8, 8, MatType.CV_8UC3, Scalar.Black);
        var error = await Assert.ThrowsAsync<ProcessingHttpException>(() => client.RequestAsync(image, Metadata));
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        await server.Completion;
    }

    [Fact]
    public async Task Native_failure_is_reported_separately_from_no_face()
    {
        await using var server = new LocalServer("{\"code\":\"processing_failed\"}", 500);
        await using var client = new ContainerFaceMeshClient(new FakeRuntime(server.Address));
        await client.StartAsync();
        using var image = new Mat(8, 8, MatType.CV_8UC3, Scalar.Black);
        var error = await Assert.ThrowsAsync<ProcessingHttpException>(() => client.RequestAsync(image, Metadata));
        Assert.Equal(HttpStatusCode.InternalServerError, error.StatusCode);
        Assert.Equal("processing_failed", error.Code);
        await server.Completion;
    }

    [Fact]
    public async Task Valid_points_are_returned()
    {
        const string response = "{\"metadata\":{\"sessionId\":\"session\",\"cameraId\":\"camera\",\"frameId\":1,\"width\":8,\"height\":8,\"mirrored\":false,\"rotationDegrees\":0},\"status\":0,\"landmarks\":[{\"x\":2,\"y\":3}]}";
        await using var server = new LocalServer(response, 200);
        await using var client = new ContainerFaceMeshClient(new FakeRuntime(server.Address));
        await client.StartAsync();
        using var image = new Mat(8, 8, MatType.CV_8UC3, Scalar.Black);
        var result = await client.RequestAsync(image, Metadata);
        Assert.Equal(FaceMeshStatus.Ok, result.Status);
        Assert.Equal(new FaceMeshPoint(2, 3), Assert.Single(result.Landmarks));
        await server.Completion;
    }

    [Fact]
    public async Task Cancelled_start_waiter_does_not_release_owned_startup_before_disconnect()
    {
        var runtime = new DelayedRuntime();
        await using var client = new ContainerFaceMeshClient(runtime);
        using var cancellation = new CancellationTokenSource();
        Task start = client.StartAsync(cancellation.Token);
        await runtime.CreateEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start.WaitAsync(TimeSpan.FromSeconds(5)));
            Task disconnect = client.DisconnectAsync();
            Assert.False(disconnect.IsCompleted);
            runtime.ReleaseCreate.TrySetResult();
            await runtime.StopEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(runtime.ResourceOwned);
            Assert.False(disconnect.IsCompleted);
            runtime.ReleaseStop.TrySetResult();
            await disconnect.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(runtime.ResourceOwned);
            using var image = new Mat(8, 8, MatType.CV_8UC3, Scalar.Black);
            await Assert.ThrowsAsync<InvalidOperationException>(() => client.RequestAsync(image, Metadata));
        }
        finally { runtime.ReleaseCreate.TrySetResult(); runtime.ReleaseStop.TrySetResult(); }
    }

    [Fact]
    public async Task Concurrent_disconnects_join_cleanup_and_allow_restart()
    {
        var runtime = new DelayedRuntime();
        await using var client = new ContainerFaceMeshClient(runtime);
        Task start = client.StartAsync();
        await runtime.CreateEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            Task first = client.DisconnectAsync();
            Task second = client.DisconnectAsync();
            Assert.False(first.IsCompleted);
            Assert.False(second.IsCompleted);
            runtime.ReleaseCreate.TrySetResult();
            await runtime.StopEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(first.IsCompleted);
            Assert.False(second.IsCompleted);
            runtime.ReleaseStop.TrySetResult();
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start);
            Assert.False(runtime.ResourceOwned);
            await client.StartAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(runtime.ResourceOwned);
        }
        finally { runtime.ReleaseCreate.TrySetResult(); runtime.ReleaseStop.TrySetResult(); }
    }

    [Fact]
    public async Task Dispose_during_startup_drains_creation_and_owned_resource()
    {
        var runtime = new DelayedRuntime();
        var client = new ContainerFaceMeshClient(runtime);
        Task start = client.StartAsync();
        await runtime.CreateEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            Task dispose = client.DisposeAsync().AsTask();
            Assert.False(dispose.IsCompleted);
            runtime.ReleaseCreate.TrySetResult();
            await runtime.StopEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(runtime.ResourceOwned);
            Assert.False(dispose.IsCompleted);
            runtime.ReleaseStop.TrySetResult();
            await dispose.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start);
            Assert.False(runtime.ResourceOwned);
        }
        finally { runtime.ReleaseCreate.TrySetResult(); runtime.ReleaseStop.TrySetResult(); await client.DisposeAsync(); }
    }

    [Fact]
    public async Task Cancelled_disconnect_waiter_does_not_abandon_cleanup()
    {
        var runtime = new DelayedRuntime();
        await using var client = new ContainerFaceMeshClient(runtime);
        using var cancellation = new CancellationTokenSource();
        runtime.ReleaseCreate.TrySetResult();
        await client.StartAsync().WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            Task first = client.DisconnectAsync(cancellation.Token);
            await runtime.StopEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WaitAsync(TimeSpan.FromSeconds(5)));
            Task joined = client.DisconnectAsync();
            Assert.False(joined.IsCompleted);
            await Assert.ThrowsAsync<InvalidOperationException>(() => client.StartAsync());
            runtime.ReleaseStop.TrySetResult();
            await joined.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(runtime.ResourceOwned);
        }
        finally { runtime.ReleaseStop.TrySetResult(); }
    }

    [Fact]
    public async Task Failed_disconnect_is_reported_and_prevents_restart()
    {
        var runtime = new DelayedRuntime { FailNextStop = true };
        var client = new ContainerFaceMeshClient(runtime);
        runtime.ReleaseCreate.TrySetResult();
        runtime.ReleaseStop.TrySetResult();
        await client.StartAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<IOException>(() => client.DisconnectAsync());
        Assert.True(runtime.ResourceOwned);
        await Assert.ThrowsAsync<IOException>(() => client.DisconnectAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.StartAsync());
        await Assert.ThrowsAsync<IOException>(() => client.DisposeAsync().AsTask());
        Assert.False(runtime.ResourceOwned);
    }

    private sealed class DelayedRuntime : IProcessingRuntime
    {
        public TaskCompletionSource CreateEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseCreate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource StopEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseStop { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool ResourceOwned { get; private set; }
        public bool FailNextStop { get; set; }

        public async Task<ProcessingSession> StartAsync(CancellationToken cancellationToken = default)
        {
            CreateEntered.TrySetResult();
            await ReleaseCreate.Task;
            ResourceOwned = true;
            return new ProcessingSession(new Uri("http://127.0.0.1:1234/"), new string('a', 64));
        }
        public void ThrowIfUnhealthy() { }
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopEntered.TrySetResult();
            await ReleaseStop.Task;
            if (FailNextStop) { FailNextStop = false; throw new IOException("Owned resource could not be removed."); }
            ResourceOwned = false;
        }
        public async ValueTask DisposeAsync() => await StopAsync();
    }

    private sealed class FakeRuntime(Uri address) : IProcessingRuntime
    {
        public Task<ProcessingSession> StartAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ProcessingSession(address, new string('a', 64)));
        public void ThrowIfUnhealthy() { }
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class LocalServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        public Uri Address { get; }
        public Task Completion { get; }
        public LocalServer(string response, int status)
        {
            _listener.Start();
            Address = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/");
            Completion = Serve(response, status);
        }
        private async Task Serve(string response, int status)
        {
            using var socket = await _listener.AcceptTcpClientAsync();
            await using var stream = socket.GetStream();
            var buffer = new byte[4096];
            using var header = new MemoryStream();
            while (true)
            {
                int n = await stream.ReadAsync(buffer.AsMemory(0, 1));
                if (n == 0) throw new IOException("Request truncated");
                header.WriteByte(buffer[0]);
                var span = header.GetBuffer().AsSpan(0, (int)header.Length);
                if (span.EndsWith("\r\n\r\n"u8)) break;
            }
            string text = Encoding.ASCII.GetString(header.ToArray());
            Assert.Contains("Authorization: Bearer ", text, StringComparison.OrdinalIgnoreCase);
            int length = int.Parse(text.Split("\r\n").Single(x => x.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).Split(':')[1].Trim());
            while (length > 0) { int n = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, length))); if (n == 0) throw new IOException("Body truncated"); length -= n; }
            byte[] body = Encoding.UTF8.GetBytes(response);
            byte[] headers = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Test\r\nContent-Length: {body.Length}\r\nConnection: close\r\nContent-Type: application/json\r\n\r\n");
            await stream.WriteAsync(headers);
            await stream.WriteAsync(body);
        }
        public async ValueTask DisposeAsync() { _listener.Stop(); try { await Completion; } catch (SocketException) { } }
    }
}
