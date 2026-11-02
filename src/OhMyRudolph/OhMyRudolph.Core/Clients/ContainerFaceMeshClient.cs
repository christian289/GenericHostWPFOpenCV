using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using OhMyRudolph.Abstractions.FaceMesh;
using OhMyRudolph.Core.Containers;
using OpenCvSharp;

namespace OhMyRudolph.Core.Clients;

public sealed class ContainerFaceMeshClient : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IProcessingRuntime _runtime;
    private readonly HttpClient _http = new(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _sync = new();
    private CancellationTokenSource _lifetime = new();
    private ProcessingSession? _session;
    private bool _disposed, _disconnecting;
    private Task? _startupTask, _disconnectTask, _disposeTask;

    public ContainerFaceMeshClient(IProcessingRuntime runtime) => _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Task startup;
        CancellationTokenSource linked;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_disconnecting) throw new InvalidOperationException("Processing runtime is disconnecting.");
            linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
            if (_startupTask is null || _startupTask.IsCompleted)
            {
                _startupTask = StartCoreAsync(_lifetime.Token);
                // The caller may cancel its wait before startup eventually faults.
                _ = _startupTask.ContinueWith(static task => { _ = task.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
            startup = _startupTask;
        }
        using (linked) await startup.WaitAsync(linked.Token).ConfigureAwait(false);
    }

    private async Task StartCoreAsync(CancellationToken lifetime)
    {
        await Task.Yield();
        await _gate.WaitAsync(lifetime).ConfigureAwait(false);
        try
        {
            lifetime.ThrowIfCancellationRequested();
            if (_session is null)
            {
                ProcessingSession session = await _runtime.StartAsync(lifetime).ConfigureAwait(false);
                if (session.Address.Scheme != Uri.UriSchemeHttp || session.Address.Host != "127.0.0.1" || session.Address.Port is < 1 or > 65535 || session.Token.Length != 64 || !session.Token.All(Uri.IsHexDigit))
                    throw new InvalidDataException("Processing runtime returned an invalid loopback session.");
                lock (_sync)
                {
                    lifetime.ThrowIfCancellationRequested();
                    _session = session;
                }
            }
            _runtime.ThrowIfUnhealthy();
        }
        finally { _gate.Release(); }
    }

    public async Task<FaceMeshResponse> RequestAsync(Mat frame, FaceMeshFrameMetadata metadata, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(metadata);
        CancellationToken lifetime;
        lock (_sync) { ObjectDisposedException.ThrowIf(_disposed, this); if (_disconnecting) throw new InvalidOperationException("Processing runtime is disconnecting."); lifetime = _lifetime.Token; }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var session = _session ?? throw new InvalidOperationException("Processing runtime is not started.");
            _runtime.ThrowIfUnhealthy();
            if (frame.Empty() || metadata.Width != frame.Width || metadata.Height != frame.Height ||
                metadata.Width is < 1 or > 4096 || metadata.Height is < 1 or > 4096 ||
                string.IsNullOrWhiteSpace(metadata.SessionId) || string.IsNullOrWhiteSpace(metadata.CameraId) || metadata.RotationDegrees is not (0 or 90 or 180 or 270))
                throw new ArgumentException("Frame metadata does not describe the image.", nameof(metadata));
            byte[] metadataBytes = JsonSerializer.SerializeToUtf8Bytes(metadata, JsonOptions);
            if (metadataBytes.Length is 0 or > 4096) throw new ArgumentException("Frame metadata exceeds the limit.", nameof(metadata));
            if (!Cv2.ImEncode(".jpg", frame, out byte[] jpeg) || jpeg.Length == 0) throw new IOException("Unable to encode the frame.");
            try
            {
                if (4L + metadataBytes.Length + jpeg.Length > 8 * 1024 * 1024) throw new IOException("Processing request exceeds the size limit.");
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(3));
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(session.Address, "/v1/frames/detect"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
                    request.Content = new FrameContent(metadataBytes, jpeg);
                    using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                    byte[] body = await PodmanProcessingRuntime.ReadBoundedAsync(response.Content, 65536, timeout.Token).ConfigureAwait(false);
                    try
                    {
                        if (!response.IsSuccessStatusCode) throw new ProcessingHttpException(response.StatusCode, ReadErrorCode(body));
                        var result = ParseResponse(body, metadata);
                        timeout.Token.ThrowIfCancellationRequested();
                        _runtime.ThrowIfUnhealthy();
                        return result;
                    }
                    finally { CryptographicOperations.ZeroMemory(body); }
                }
                catch (OperationCanceledException) when (!linked.IsCancellationRequested) { throw new TimeoutException("Processing request timed out."); }
                catch (HttpRequestException ex) { throw new IOException("Processing container is unreachable or exited.", ex); }
            }
            finally { CryptographicOperations.ZeroMemory(jpeg); CryptographicOperations.ZeroMemory(metadataBytes); }
        }
        finally { _gate.Release(); }
    }

    private static string ReadErrorCode(byte[] body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            string? code = json.RootElement.GetProperty("code").GetString();
            return code is "unauthorized" or "invalid_request" or "request_too_large" or "request_timeout" or "busy" or "invalid_image" or "processing_failed" ? code : "http_error";
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException) { return "http_error"; }
    }

    private static FaceMeshResponse ParseResponse(byte[] payload, FaceMeshFrameMetadata expected)
    {
        try
        {
            using var json = JsonDocument.Parse(payload);
            var root = json.RootElement;
            var m = root.GetProperty("metadata");
            var metadata = new FaceMeshFrameMetadata(m.GetProperty("sessionId").GetString()!, m.GetProperty("cameraId").GetString()!, m.GetProperty("frameId").GetInt64(), m.GetProperty("width").GetInt32(), m.GetProperty("height").GetInt32(), m.GetProperty("mirrored").GetBoolean(), m.GetProperty("rotationDegrees").GetInt32());
            if (metadata != expected) throw new IOException("Processing response metadata mismatch.");
            int status = root.GetProperty("status").GetInt32();
            var landmarks = root.GetProperty("landmarks");
            if (status is not (0 or 1) || landmarks.ValueKind != JsonValueKind.Array) throw new IOException("Invalid processing response status.");
            int count = landmarks.GetArrayLength();
            if (count > 5 || (status == 0 && count == 0) || (status == 1 && count != 0)) throw new IOException("Processing response landmark count is invalid.");
            var points = new FaceMeshPoint[count];
            int index = 0;
            foreach (var point in landmarks.EnumerateArray())
            {
                int x = point.GetProperty("x").GetInt32(), y = point.GetProperty("y").GetInt32();
                if (x < 0 || x >= expected.Width || y < 0 || y >= expected.Height) throw new IOException("Processing response landmark is out of bounds.");
                points[index++] = new FaceMeshPoint(x, y);
            }
            return new FaceMeshResponse(metadata, (FaceMeshStatus)status, points);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { throw new IOException("Invalid processing response.", ex); }
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Task disconnect;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_disconnecting)
            {
                _disconnecting = true;
                CancellationTokenSource old = _lifetime;
                old.Cancel();
                _lifetime = new CancellationTokenSource();
                _disconnectTask = DisconnectCoreAsync(_startupTask, old);
                _ = _disconnectTask.ContinueWith(static task => { _ = task.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
            disconnect = _disconnectTask!;
        }
        return disconnect.WaitAsync(cancellationToken);
    }

    private async Task DisconnectCoreAsync(Task? startup, CancellationTokenSource old)
    {
        await Task.Yield();
        try
        {
            if (startup is not null)
            {
                try { await startup.ConfigureAwait(false); }
                catch { /* Stop still has to resolve any resource left by failed startup. */ }
            }
            await _gate.WaitAsync().ConfigureAwait(false);
            try { _session = null; await _runtime.StopAsync().ConfigureAwait(false); }
            finally { _gate.Release(); }
            lock (_sync) { _startupTask = null; _disconnectTask = null; _disconnecting = false; }
        }
        finally { old.Dispose(); }
    }

    public ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (_disposeTask is not null) return new ValueTask(_disposeTask);
            _disposed = true;
            _lifetime.Cancel();
            _disposeTask = DisposeCoreAsync(_startupTask, _disconnectTask);
            return new ValueTask(_disposeTask);
        }
    }
    private async Task DisposeCoreAsync(Task? startup, Task? disconnect)
    {
        await Task.Yield();
        try
        {
            try
            {
                if (startup is not null)
                {
                    try { await startup.ConfigureAwait(false); }
                    catch { /* Disposal must still resolve any owned resource. */ }
                }
                if (disconnect is not null) await disconnect.ConfigureAwait(false);
            }
            finally
            {
                await _gate.WaitAsync().ConfigureAwait(false);
                try { _session = null; await _runtime.DisposeAsync().ConfigureAwait(false); }
                finally { _gate.Release(); }
            }
        }
        finally { _http.Dispose(); _lifetime.Dispose(); }
    }
    private sealed class FrameContent : HttpContent
    {
        private readonly byte[] _metadata;
        private readonly byte[] _jpeg;
        public FrameContent(byte[] metadata, byte[] jpeg)
        {
            _metadata = metadata;
            _jpeg = jpeg;
            Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        }
        protected override bool TryComputeLength(out long length) { length = 4L + _metadata.Length + _jpeg.Length; return true; }
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context) => await SerializeToStreamAsync(stream, context, CancellationToken.None).ConfigureAwait(false);
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            byte[] header = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(header, _metadata.Length);
            await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(_metadata, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(_jpeg, cancellationToken).ConfigureAwait(false);
        }
    }
}
