using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OhMyRudolph.Core.Containers;

public sealed class PodmanProcessingRuntime : IProcessingRuntime
{
    private const string OwnerLabel = "io.ohmyrudolph.owner";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _sync = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly IProgress<string>? _progress;
    private readonly HttpClient _http = new(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
    private ProcessingSession? _session;
    private string? _name, _nonce, _containerId, _cli, _connection;
    private bool _disposed, _createUncertain;
    private Task? _disposeTask;
    private CancellationTokenSource? _heartbeatCancellation;
    private Task? _heartbeatTask;
    private Exception? _heartbeatFailure;

    public event Action<Exception>? SessionFailed;

    public PodmanProcessingRuntime(IProgress<string>? progress = null) => _progress = progress;

    public async Task<ProcessingSession> StartAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_session is not null)
            {
                ThrowIfUnhealthy();
                if (await IsRunningAsync(linked.Token).ConfigureAwait(false)) return _session;
                throw new IOException("Processing container exited unexpectedly; disconnect before restarting.");
            }
            if (_name is not null) await RemoveOwnedAsync(linked.Token).ConfigureAwait(false);
            string manifestPath = Environment.GetEnvironmentVariable("OHMYRUDOLPH_IMAGE_MANIFEST") ?? Path.Combine(AppContext.BaseDirectory, "runtime", "processing-image.json");
            var manifest = await ProcessingImageManifest.LoadAndVerifyAsync(manifestPath, linked.Token).ConfigureAwait(false);
            _cli = ResolveCli();
            _connection = Environment.GetEnvironmentVariable("OHMYRUDOLPH_PODMAN_CONNECTION") ?? manifest.PodmanConnection;
            if (string.IsNullOrWhiteSpace(_connection)) throw new InvalidOperationException("A Podman connection is required.");
            var machine = await RunAsync(false, ["machine", "inspect", manifest.PodmanMachine, "--format", "{{.State}}"], TimeSpan.FromSeconds(20), linked.Token).ConfigureAwait(false);
            if (machine.ExitCode != 0 || !machine.Output.Trim().Equals("running", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The required Podman machine is not running.");
            var info = await RunAsync(true, ["info", "--format", "json"], TimeSpan.FromSeconds(30), linked.Token).ConfigureAwait(false);
            if (info.ExitCode != 0)
                throw new InvalidOperationException("The configured Podman connection is unavailable.");
            try
            {
                using var document = JsonDocument.Parse(info.Output);
                JsonElement host = document.RootElement.GetProperty("host");
                string? os = host.GetProperty("os").GetString();
                string? arch = host.GetProperty("arch").GetString();
                if ($"{os}/{arch}" != manifest.Platform)
                    throw new InvalidOperationException("The configured Podman connection is not linux/amd64.");
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
            {
                throw new InvalidOperationException("The configured Podman connection did not report a valid linux/amd64 platform.", ex);
            }
            var exists = await RunAsync(true, ["image", "exists", manifest.ImageId], TimeSpan.FromSeconds(30), linked.Token).ConfigureAwait(false);
            if (exists.ExitCode == 1)
            {
                string archive = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(manifestPath))!, manifest.ArchiveFile);
                _progress?.Report("처리 이미지 가져오기 중 (최대 5분)");
                var load = await RunAsync(true, ["load", "--input", archive], TimeSpan.FromMinutes(5), linked.Token).ConfigureAwait(false);
                if (load.ExitCode != 0) throw new IOException("Unable to import the verified processing image.");
            }
            else if (exists.ExitCode != 0) throw new IOException("Unable to inspect processing image availability.");
            var image = await RunAsync(true, ["image", "inspect", manifest.ImageId, "--format", "{{.Id}}"], TimeSpan.FromSeconds(30), linked.Token).ConfigureAwait(false);
            string inspectedId = image.Output.Trim();
            if (image.ExitCode != 0 ||
                !(string.Equals(inspectedId, manifest.ImageId, StringComparison.Ordinal) ||
                  (inspectedId.Length == 64 && inspectedId.AsSpan().SequenceEqual(manifest.ImageId.AsSpan(7)))))
                throw new InvalidDataException("Loaded processing image ID does not match the manifest.");
            _nonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            string token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            _name = "ohmyrudolph-" + Guid.NewGuid().ToString("N");
            try
            {
                // Once sent, a remote create must finish independently of caller cancellation.
                // Killing the local client can leave a late, unstarted container in the VM.
                _createUncertain = true;
                var create = await RunAsync(true, ["create", "--pull", "never", "--rm", "--name", _name, "--label", OwnerLabel + "=" + _nonce,
                    "--network", "pasta", "--publish", "127.0.0.1::8080", "--user", "10001:10001", "--cap-drop", "all",
                    "--env", "OHMYRUDOLPH_LEASE_SECONDS=30", "--env", "OHMYRUDOLPH_TOKEN", manifest.ImageId], TimeSpan.FromSeconds(60), CancellationToken.None, token).ConfigureAwait(false);
                if (create.ExitCode != 0) throw new IOException("Unable to create the processing container; remote outcome is uncertain.");
                _createUncertain = false;
                string containerId = create.Output.Trim();
                if (containerId.Length != 64 || !containerId.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'))
                    throw new IOException("Podman returned an invalid container ID.");
                _containerId = containerId;
                linked.Token.ThrowIfCancellationRequested();
                var start = await RunAsync(true, ["start", _name], TimeSpan.FromSeconds(60), linked.Token).ConfigureAwait(false);
                if (start.ExitCode != 0) throw new IOException("Unable to start the processing container.");
                var port = await RunAsync(true, ["port", _name, "8080/tcp"], TimeSpan.FromSeconds(30), linked.Token).ConfigureAwait(false);
                string output = port.Output.Trim();
                if (port.ExitCode != 0 || !output.StartsWith("127.0.0.1:", StringComparison.Ordinal) ||
                    !int.TryParse(output.AsSpan("127.0.0.1:".Length), out int portNumber) || portNumber is < 1 or > 65535)
                    throw new IOException("Processing container did not expose a loopback-only port.");
                var session = new ProcessingSession(new Uri($"http://127.0.0.1:{portNumber}/"), token);
                await AwaitReadyAsync(session, manifest, linked.Token).ConfigureAwait(false);
                _session = session;
                Volatile.Write(ref _heartbeatFailure, null);
                _heartbeatCancellation = new CancellationTokenSource();
                _heartbeatTask = HeartbeatAsync(session, manifest, _heartbeatCancellation.Token);
                return session;
            }
            catch (Exception startupFailure)
            {
                try { await RemoveOwnedAsync(CancellationToken.None).ConfigureAwait(false); }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException("Processing startup failed and container ownership could not be resolved.", startupFailure, cleanupFailure);
                }
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    private async Task AwaitReadyAsync(ProcessingSession session, ProcessingImageManifest manifest, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            while (true)
            {
                timeout.Token.ThrowIfCancellationRequested();
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(session.Address, "/health/ready"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
                    using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                    if (response.StatusCode == HttpStatusCode.Unauthorized) throw new ProcessingHttpException(response.StatusCode, "unauthorized");
                    if (response.IsSuccessStatusCode)
                    {
                        byte[] body = await ReadBoundedAsync(response.Content, 4096, timeout.Token).ConfigureAwait(false);
                        using var json = JsonDocument.Parse(body);
                        var root = json.RootElement;
                        if (root.GetProperty("appVersion").GetString() != manifest.HostVersion || root.GetProperty("contractVersion").GetInt32() != manifest.ContractVersion || root.GetProperty("abiVersion").GetInt32() != manifest.AbiVersion)
                            throw new InvalidDataException("Processing host version mismatch.");
                        return;
                    }
                }
                catch (HttpRequestException) { }
                if (!await IsRunningAsync(timeout.Token).ConfigureAwait(false)) throw new IOException("Processing container exited during startup.");
                await Task.Delay(500, timeout.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new TimeoutException("Processing container readiness timed out."); }
    }

    public void ThrowIfUnhealthy()
    {
        Exception? failure = Volatile.Read(ref _heartbeatFailure);
        if (failure is not null) throw new IOException("Processing session heartbeat failed; disconnect and restart the session.", failure);
    }

    private async Task HeartbeatAsync(ProcessingSession session, ProcessingImageManifest manifest, CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(3));
                using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(session.Address, "/health/ready"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    throw new ProcessingHttpException(response.StatusCode, response.StatusCode == HttpStatusCode.Unauthorized ? "unauthorized" : "http_error");
                byte[] body = await ReadBoundedAsync(response.Content, 4096, timeout.Token).ConfigureAwait(false);
                using var json = JsonDocument.Parse(body);
                var root = json.RootElement;
                if (root.GetProperty("appVersion").GetString() != manifest.HostVersion ||
                    root.GetProperty("contractVersion").GetInt32() != manifest.ContractVersion ||
                    root.GetProperty("abiVersion").GetInt32() != manifest.AbiVersion)
                    throw new InvalidDataException("Processing host heartbeat version mismatch.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            var failure = ex is OperationCanceledException ? new TimeoutException("Processing heartbeat timed out.", ex) : ex;
            Volatile.Write(ref _heartbeatFailure, failure);
            try { SessionFailed?.Invoke(failure); } catch (Exception) { }
        }
    }

    private async Task<bool> IsRunningAsync(CancellationToken cancellationToken)
    {
        if (_name is null) return false;
        var result = await RunAsync(true, ["inspect", "--format", "{{.State.Running}}", _name], TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0 && result.Output.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _session = null;
            if (_heartbeatCancellation is not null)
            {
                _heartbeatCancellation.Cancel();
                if (_heartbeatTask is not null) await _heartbeatTask.ConfigureAwait(false);
                _heartbeatCancellation.Dispose();
                _heartbeatCancellation = null;
                _heartbeatTask = null;
            }
            await RemoveOwnedAsync(CancellationToken.None).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally { _gate.Release(); }
    }

    private async Task RemoveOwnedAsync(CancellationToken cancellationToken)
    {
        if (_name is null || _nonce is null) return;
        var inspect = await RunAsync(true, ["inspect", "--format", "{{.Id}} {{index .Config.Labels \"" + OwnerLabel + "\"}}", _name], TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
        if (inspect.ExitCode != 0)
        {
            var exists = await RunAsync(true, ["container", "exists", _name], TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
            if (exists.ExitCode != 1) throw new IOException("Cannot verify ownership of processing container.");
            if (_createUncertain)
                throw new IOException("Remote container creation outcome is uncertain; ownership is retained for later resolution.");
        }
        else
        {
            string[] fields = inspect.Output.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 2 || fields[1] != _nonce || fields[0].Length != 64 ||
                !fields[0].All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') ||
                (_containerId is not null && fields[0] != _containerId))
                throw new InvalidOperationException("Processing container ownership label or ID mismatch.");
            var remove = await RunAsync(true, ["rm", "--force", fields[0]], TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            if (remove.ExitCode != 0) throw new IOException("Unable to remove the owned processing container.");
        }
        _name = _nonce = _containerId = null;
        _createUncertain = false;
        _session = null;
    }

    public ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (_disposeTask is not null) return new ValueTask(_disposeTask);
            _disposed = true;
            _lifetime.Cancel();
            _disposeTask = DisposeCoreAsync();
            return new ValueTask(_disposeTask);
        }
    }
    private async Task DisposeCoreAsync()
    {
        try { await StopAsync().ConfigureAwait(false); }
        finally { _http.Dispose(); _lifetime.Dispose(); }
    }

    private static string ResolveCli()
    {
        string? explicitPath = Environment.GetEnvironmentVariable("OHMYRUDOLPH_PODMAN");
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            if (!File.Exists(explicitPath)) throw new FileNotFoundException("Configured Podman executable was not found.");
            return Path.GetFullPath(explicitPath);
        }
        string bundled = Path.Combine(AppContext.BaseDirectory, "runtime", "podman", "podman.exe");
        if (File.Exists(bundled)) return bundled;
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory.Trim('"'), OperatingSystem.IsWindows() ? "podman.exe" : "podman");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("Podman CLI was not found; install or explicitly configure the supported runtime.");
    }

    private async Task<CommandResult> RunAsync(bool connection, string[] args, TimeSpan timeout, CancellationToken cancellationToken, string? token = null)
    {
        var start = new ProcessStartInfo(_cli!) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        if (connection) { start.ArgumentList.Add("--connection"); start.ArgumentList.Add(_connection!); }
        foreach (string arg in args) start.ArgumentList.Add(arg);
        if (token is not null) start.Environment["OHMYRUDOLPH_TOKEN"] = token;
        using var process = new Process { StartInfo = start };
        try { if (!process.Start()) throw new IOException("Unable to start Podman CLI."); }
        catch (System.ComponentModel.Win32Exception ex) { throw new IOException("Unable to start Podman CLI.", ex); }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        Task<string> stdout = DrainAsync(process.StandardOutput, deadline.Token);
        Task<string> stderr = DrainAsync(process.StandardError, deadline.Token);
        try
        {
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            await Task.WhenAll(stdout, stderr).WaitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            await process.WaitForExitAsync().ConfigureAwait(false);
            try { await Task.WhenAll(stdout, stderr).ConfigureAwait(false); } catch (OperationCanceledException) { }
            if (!cancellationToken.IsCancellationRequested) throw new TimeoutException("Podman command timed out.");
            throw;
        }
        return new CommandResult(process.ExitCode, await stdout.ConfigureAwait(false));
    }
    private static async Task<string> DrainAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var output = new StringBuilder();
        char[] buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) != 0)
            if (output.Length < 65536) output.Append(buffer, 0, Math.Min(count, 65536 - output.Length));
        return output.ToString();
    }
    private sealed record CommandResult(int ExitCode, string Output);

    internal static async Task<byte[]> ReadBoundedAsync(HttpContent content, int limit, CancellationToken token)
    {
        if (content.Headers.ContentLength > limit) throw new IOException("Processing response is too large.");
        await using var stream = await content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var output = new MemoryStream();
        byte[] buffer = new byte[4096];
        while (true)
        {
            int count = await stream.ReadAsync(buffer, token).ConfigureAwait(false);
            if (count == 0) return output.ToArray();
            if (output.Length + count > limit) throw new IOException("Processing response is too large.");
            output.Write(buffer, 0, count);
        }
    }
}
