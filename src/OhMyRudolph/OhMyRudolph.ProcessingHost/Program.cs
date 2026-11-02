using System.Buffers;
using System.Net.Http.Headers;
using System.Reflection;
using OhMyRudolph.Abstractions.FaceMesh;
using OhMyRudolph.Interop;
using OhMyRudolph.ProcessingHost;

try
{
    string tokenValue = Environment.GetEnvironmentVariable("OHMYRUDOLPH_TOKEN") ?? "";
    string resourceRoot = Environment.GetEnvironmentVariable("OHMYRUDOLPH_RESOURCE_ROOT") ?? "";
    string hostVersion = (typeof(FrameProtocol).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?? throw new InvalidOperationException("Host version metadata is unavailable.")).InformationalVersion.Split('+')[0];
    var token = new BearerToken(tokenValue);
    if (!int.TryParse(Environment.GetEnvironmentVariable("OHMYRUDOLPH_LEASE_SECONDS") ?? "30",
        System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int leaseSeconds) ||
        leaseSeconds is < 20 or > 300)
        throw new InvalidOperationException("Owner lease must be between 20 and 300 seconds.");
    var lease = new OwnerLease(TimeSpan.FromSeconds(leaseSeconds));
    await using var leaseMonitor = new OwnerLeaseMonitor(lease);
    if (!Path.IsPathFullyQualified(resourceRoot) || !Directory.Exists(resourceRoot))
        throw new InvalidOperationException("Native resource directory is unavailable.");

    // The engine is initialized before the listener starts, and its SafeHandle defers
    // native destruction if shutdown overlaps a call already in progress.
    using var engine = new NativeFaceMesh(resourceRoot);
    var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
    {
        Args = args,
        EnvironmentName = Environments.Production
    });
    builder.Logging.ClearProviders();
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.AddServerHeader = false;
        // Enforced while streaming below, including requests with no Content-Length.
        options.Limits.MaxRequestBodySize = null;
    });
    var app = builder.Build();
    var slot = new SemaphoreSlim(1, 1);

    app.Use(async (context, next) =>
    {
        if (!token.IsAuthorized(context.Request.Headers.Authorization.ToString()))
        {
            await Results.Json(new { code = "unauthorized" }, statusCode: 401).ExecuteAsync(context);
            return;
        }
        lease.Renew();
        await next(context);
    });

    app.MapGet("/health/ready", () => Results.Json(new { appVersion = hostVersion, contractVersion = 1, abiVersion = NativeFaceMesh.AbiVersion }));
    app.MapPost("/v1/frames/detect", async (HttpContext context) =>
    {
        if (!MediaTypeHeaderValue.TryParse(context.Request.ContentType, out var mediaType) ||
            !string.Equals(mediaType.MediaType, "application/octet-stream", StringComparison.OrdinalIgnoreCase) ||
            context.Request.Headers.ContainsKey("Content-Encoding"))
            return Error(400, "invalid_request");
        if (context.Request.ContentLength is > FrameProtocol.MaxBodyBytes)
            return Error(413, "request_too_large");
        if (!slot.Wait(0)) return Error(429, "busy");

        byte[]? buffer = null;
        using var receiveDeadline = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        receiveDeadline.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            buffer = ArrayPool<byte>.Shared.Rent(FrameProtocol.MaxBodyBytes + 1);
            int count = 0;
            while (count <= FrameProtocol.MaxBodyBytes)
            {
                int read = await context.Request.Body.ReadAsync(buffer.AsMemory(count, FrameProtocol.MaxBodyBytes + 1 - count), receiveDeadline.Token);
                if (read == 0) break;
                count += read;
            }
            if (count > FrameProtocol.MaxBodyBytes) return Error(413, "request_too_large");
            return Detect(engine, buffer, count);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            return Results.Empty;
        }
        catch (OperationCanceledException) when (receiveDeadline.IsCancellationRequested)
        {
            return Error(408, "request_timeout");
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == 413)
        {
            return Error(413, "request_too_large");
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == 408)
        {
            return Error(408, "request_timeout");
        }
        catch (BadHttpRequestException)
        {
            return Error(400, "invalid_request");
        }
        catch
        {
            return Error(500, "processing_failed");
        }
        finally
        {
            if (buffer is not null) ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            slot.Release();
        }
    });
    await app.RunAsync();
    return 0;
}
catch (Exception)
{
    // Do not expose paths, secrets, or exception text on startup failure.
    Console.Error.WriteLine("Face mesh host startup or execution failed; check token, resource availability, native library and ABI.");
    return 1;
}

static IResult Detect(NativeFaceMesh engine, byte[] buffer, int count)
{
    if (!FrameProtocol.TryParse(buffer.AsSpan(0, count), out var frame))
        return Error(400, "invalid_request");
    Span<NativePoint> points = stackalloc NativePoint[NativeFaceMesh.MaxFaces];
    int written;
    try
    {
        written = engine.Detect(buffer.AsSpan(frame.JpegOffset, count - frame.JpegOffset),
            frame.Metadata.Width, frame.Metadata.Height, points);
    }
    catch (NativeFaceMeshException exception)
    {
        int status = FrameProtocol.StatusCode(exception.Status);
        return Error(status, status == 400 ? "invalid_image" : "processing_failed");
    }
    var landmarks = new FaceMeshPoint[written];
    for (int i = 0; i < written; i++)
    {
        landmarks[i] = new FaceMeshPoint(points[i].X, points[i].Y);
    }
    var response = new FaceMeshResponse(frame.Metadata, written == 0 ? FaceMeshStatus.NoFace : FaceMeshStatus.Ok, landmarks);
    return Results.Json(response);
}

static IResult Error(int status, string code) => Results.Json(new { code }, statusCode: status);
