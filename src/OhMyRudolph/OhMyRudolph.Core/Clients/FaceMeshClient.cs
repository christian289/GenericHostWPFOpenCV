using System.Buffers.Binary;
using OhMyRudolph.Abstractions.FaceMesh;

namespace OhMyRudolph.Core.Clients;

public sealed class FaceMeshClient : IAsyncDisposable
{
    private const int MaxRequest = 8 * 1024 * 1024;
    private const int MaxResponse = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _lifetimeLock = new();
    private string _pipeName;
    private readonly TimeSpan _connectTimeout;
    private readonly TimeSpan _responseTimeout;
    private NamedPipeClientStream? _pipe;
    private Process? _process;
    private int _ownedServerProcessId;
    private string? _ownedServerPipeName;
    private CancellationTokenSource _connectionLifetime = new();
    private volatile bool _disposed;
    private Task? _disposeTask;

    public FaceMeshClient(ILogger<FaceMeshClient> logger, string? pipeName = null,
        TimeSpan? connectTimeout = null, TimeSpan? responseTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _pipeName = pipeName ?? "OhMyRudolph-" + Guid.NewGuid().ToString("N");
        _connectTimeout = connectTimeout ?? TimeSpan.FromSeconds(10);
        _responseTimeout = responseTimeout ?? TimeSpan.FromSeconds(3);
    }

    public int? OwnedServerProcessId
    {
        get
        {
            int id = Volatile.Read(ref _ownedServerProcessId);
            return id == 0 ? null : id;
        }
    }

    public string? OwnedServerPipeName => Volatile.Read(ref _ownedServerPipeName);

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_process is { HasExited: false }) return;
            BreakConnection();
            _process?.Dispose();
            _process = null;
            Volatile.Write(ref _ownedServerProcessId, 0);
            Volatile.Write(ref _ownedServerPipeName, null);
            string script = Path.Combine(AppContext.BaseDirectory, "NamedPipeFaceMeshServer.py");
            if (!File.Exists(script)) throw new FileNotFoundException("Face mesh server script was not found.");
            var start = new ProcessStartInfo(PythonRuntime.ResolveExecutable())
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory
            };
            string newPipeName = "OhMyRudolph-" + Guid.NewGuid().ToString("N");
            start.ArgumentList.Add(script);
            start.ArgumentList.Add("--pipe-name");
            start.ArgumentList.Add(newPipeName);
            start.Environment["PYTHONIOENCODING"] = "utf-8";
            start.Environment["PYTHONUNBUFFERED"] = "1";
            var process = new Process { StartInfo = start };
            try
            {
                if (!process.Start()) throw new IOException("Unable to start face mesh server.");
                _process = process;
                _pipeName = newPipeName;
                Volatile.Write(ref _ownedServerPipeName, newPipeName);
                Volatile.Write(ref _ownedServerProcessId, process.Id);
            }
            catch { process.Dispose(); throw; }
        }
        finally { _gate.Release(); }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_pipe is { IsConnected: true }) return;
            _pipe?.Dispose();
            _pipe = null;
            CancellationTokenSource lifetime;
            lock (_lifetimeLock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _connectionLifetime.Dispose();
                lifetime = _connectionLifetime = new CancellationTokenSource();
            }
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            timeout.CancelAfter(_connectTimeout);
            var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
                _pipe = pipe;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !lifetime.IsCancellationRequested)
            {
                pipe.Dispose();
                throw new TimeoutException("Timed out connecting to the face mesh server.");
            }
            catch { pipe.Dispose(); throw; }
        }
        finally { _gate.Release(); }
    }

    public async Task<FaceMeshResponse> RequestAsync(Mat frame, FaceMeshFrameMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(metadata);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var pipe = _pipe;
            if (pipe is null || !pipe.IsConnected) throw new IOException("Face mesh pipe is not connected.");
            if (frame.Empty() || metadata.Width != frame.Width || metadata.Height != frame.Height ||
                string.IsNullOrWhiteSpace(metadata.SessionId) || string.IsNullOrWhiteSpace(metadata.CameraId) ||
                metadata.RotationDegrees is not (0 or 90 or 180 or 270))
                throw new ArgumentException("Frame metadata does not describe the image.", nameof(metadata));
            if (!Cv2.ImEncode(".jpg", frame, out byte[] jpeg) || jpeg.Length == 0)
                throw new IOException("Unable to encode face mesh frame.");
            byte[] metadataBytes = JsonSerializer.SerializeToUtf8Bytes(metadata, JsonOptions);
            long length = 4L + metadataBytes.Length + jpeg.Length;
            if (length > MaxRequest) throw new IOException("Face mesh request exceeds the maximum size.");
            byte[] header = new byte[8];
            BinaryPrimitives.WriteInt32LittleEndian(header, (int)length);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), metadataBytes.Length);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _connectionLifetime.Token);
            timeout.CancelAfter(_responseTimeout);
            try
            {
                await pipe.WriteAsync(header, timeout.Token).ConfigureAwait(false);
                await pipe.WriteAsync(metadataBytes, timeout.Token).ConfigureAwait(false);
                await pipe.WriteAsync(jpeg, timeout.Token).ConfigureAwait(false);
                await pipe.FlushAsync(timeout.Token).ConfigureAwait(false);
                await pipe.ReadExactlyAsync(header.AsMemory(0, 4), timeout.Token).ConfigureAwait(false);
                int responseLength = BinaryPrimitives.ReadInt32LittleEndian(header);
                if (responseLength <= 0 || responseLength > MaxResponse)
                    throw new IOException("Invalid face mesh response length.");
                byte[] payload = new byte[responseLength];
                await pipe.ReadExactlyAsync(payload, timeout.Token).ConfigureAwait(false);
                return ParseResponse(payload, metadata);
            }
            catch (FaceMeshServerException) { throw; }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !_connectionLifetime.IsCancellationRequested)
            {
                BreakConnection();
                throw new TimeoutException("Timed out waiting for the face mesh response.");
            }
            catch
            {
                BreakConnection();
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    private static FaceMeshResponse ParseResponse(byte[] payload, FaceMeshFrameMetadata expected)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(payload);
            JsonElement root = document.RootElement;
            JsonElement frame = root.GetProperty("metadata");
            var metadata = new FaceMeshFrameMetadata(
                frame.GetProperty("sessionId").GetString() ?? throw new IOException("Invalid session metadata."),
                frame.GetProperty("cameraId").GetString() ?? throw new IOException("Invalid camera metadata."),
                frame.GetProperty("frameId").GetInt64(), frame.GetProperty("width").GetInt32(),
                frame.GetProperty("height").GetInt32(), frame.GetProperty("mirrored").GetBoolean(),
                frame.GetProperty("rotationDegrees").GetInt32());
            if (metadata != expected) throw new IOException("Face mesh response metadata mismatch.");
            string? status = root.GetProperty("status").GetString();
            if (status == "error") throw new FaceMeshServerException("Face mesh server could not process the frame.");
            if (status is not ("ok" or "no_face")) throw new IOException("Invalid face mesh response status.");
            JsonElement landmarks = root.GetProperty("landmarks");
            if (landmarks.ValueKind != JsonValueKind.Array) throw new IOException("Invalid face mesh landmarks.");
            var points = new List<(int X, int Y)>(landmarks.GetArrayLength());
            foreach (JsonElement item in landmarks.EnumerateArray())
                points.Add((item.GetProperty("x").GetInt32(), item.GetProperty("y").GetInt32()));
            if ((status == "no_face" && points.Count != 0) || (status == "ok" && points.Count == 0))
                throw new IOException("Face mesh status and landmarks disagree.");
            return new FaceMeshResponse(metadata, status == "ok" ? FaceMeshStatus.Ok : FaceMeshStatus.NoFace, points);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        {
            throw new IOException("Invalid face mesh response.", ex);
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Interrupt active I/O before waiting for the single-flight gate.
        lock (_lifetimeLock)
        {
            try { _connectionLifetime.Cancel(); } catch (ObjectDisposedException) { }
        }
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            BreakConnection();
            if (_process is not null)
            {
                try
                {
                    if (!_process.HasExited) _process.Kill(entireProcessTree: true);
                    await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    Volatile.Write(ref _ownedServerProcessId, 0);
                    Volatile.Write(ref _ownedServerPipeName, null);
                    _process.Dispose();
                    _process = null;
                }
            }
        }
        finally { _gate.Release(); }
    }

    private void BreakConnection()
    {
        _pipe?.Dispose();
        _pipe = null;
    }

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource<object?> completion;
        lock (_lifetimeLock)
        {
            if (_disposeTask is not null) return new ValueTask(_disposeTask);
            _disposed = true;
            completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposeTask = completion.Task;
        }
        _ = DisposeCoreAsync(completion);
        return new ValueTask(completion.Task);
    }

    private async Task DisposeCoreAsync(TaskCompletionSource<object?> completion)
    {
        Exception? failure = null;
        try { await DisconnectAsync().ConfigureAwait(false); }
        catch (Exception exception) { failure = exception; }
        finally { lock (_lifetimeLock) _connectionLifetime.Dispose(); }
        if (failure is null) completion.TrySetResult(null);
        else completion.TrySetException(failure);
    }

}
