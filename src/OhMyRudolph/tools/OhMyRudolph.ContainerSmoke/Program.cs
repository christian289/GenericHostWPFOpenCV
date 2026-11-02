using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OhMyRudolph.Abstractions.FaceMesh;
using OhMyRudolph.Core.Clients;
using OhMyRudolph.Core.Containers;
using OpenCvSharp;

string phase = "입력 확인";
try
{
    if (args.Length == 3 && args[0] == "--prepare-fixture")
    {
        // Exact public-domain source identified by scikit-image 0.25.2 documentation.
        await using var source = File.OpenRead(args[1]);
        string digest = Convert.ToHexStringLower(await SHA256.HashDataAsync(source));
        if (digest != "88431cd9653ccd539741b555fb0a46b61558b301d4110412b5bc28b5e3ea6cb5")
            throw new InvalidDataException("허용된 공개 시험 PNG가 아닙니다.");
        using var image = Cv2.ImRead(args[1], ImreadModes.Color);
        if (image.Width != 512 || image.Height != 512 || !Cv2.ImWrite(args[2], image, [new ImageEncodingParam(ImwriteFlags.JpegQuality, 95)]))
            throw new InvalidDataException("공개 시험 JPEG 변환 실패");
        await using var jpeg = File.OpenRead(args[2]);
        Console.WriteLine("공개 시험 JPEG SHA256: " + Convert.ToHexStringLower(await SHA256.HashDataAsync(jpeg)));
        return 0;
    }
    if (args.Length == 0 || args[0] == "--help")
    {
        Console.WriteLine("사용법: --prepare-fixture <공개 PNG> <JPEG 출력> 또는 --manifest <manifest> --fixture <공개 JPEG> [--podman <실행파일>] [--connection <이름>]");
        Console.WriteLine("실제 컨테이너/모델에 합성·공개 프레임을 보냅니다. 카메라·UI·사용자 촬영 저장 없음.");
        return 0;
    }
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    for (int i = 0; i < args.Length; i += 2)
    {
        if (i + 1 == args.Length || args[i] is not ("--manifest" or "--fixture" or "--podman" or "--connection") || !options.TryAdd(args[i], args[i + 1]))
            throw new ArgumentException("잘못된 검증 옵션");
    }
    Environment.SetEnvironmentVariable("OHMYRUDOLPH_IMAGE_MANIFEST", Path.GetFullPath(options["--manifest"]));
    if (options.TryGetValue("--podman", out string? podman)) Environment.SetEnvironmentVariable("OHMYRUDOLPH_PODMAN", Path.GetFullPath(podman));
    if (options.TryGetValue("--connection", out string? connection)) Environment.SetEnvironmentVariable("OHMYRUDOLPH_PODMAN_CONNECTION", connection);

    using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(8));
    await using var runtime = new PodmanProcessingRuntime(new Progress<string>(Console.WriteLine));
    await using var client = new ContainerFaceMeshClient(runtime);
    using var black = new Mat(240, 320, MatType.CV_8UC3, Scalar.Black);
    using var portrait = Cv2.ImRead(options["--fixture"], ImreadModes.Color);
    if (portrait.Width != 512 || portrait.Height != 512) throw new InvalidDataException("공개 시험 프레임 크기 불일치");
    var latencies = new List<double>();
    var total = Stopwatch.StartNew();
    for (int sessionIndex = 0; sessionIndex < 3; sessionIndex++)
    {
        phase = "컨테이너 시작";
        await client.StartAsync(deadline.Token);
        string sessionId = "synthetic-" + sessionIndex;
        for (int frameId = 0; frameId < 12; frameId++)
        {
            phase = "합성 얼굴 없음 추론";
            var meta = new FaceMeshFrameMetadata(sessionId, "synthetic", frameId, black.Width, black.Height, false, 0);
            var timer = Stopwatch.StartNew();
            var result = await client.RequestAsync(black, meta, deadline.Token);
            latencies.Add(timer.Elapsed.TotalMilliseconds);
            if (result.Status != FaceMeshStatus.NoFace || result.Landmarks.Count != 0 || result.Metadata != meta)
                throw new InvalidDataException("실제 모델의 합성 프레임 응답 불일치");
        }
        phase = "공개 얼굴 양성 추론";
        var portraitMeta = new FaceMeshFrameMetadata(sessionId, "public-fixture", 12, portrait.Width, portrait.Height, false, 0);
        var face = await client.RequestAsync(portrait, portraitMeta, deadline.Token);
        if (face.Status != FaceMeshStatus.Ok || face.Metadata != portraitMeta || face.Landmarks.Count is < 1 or > 5 ||
            face.Landmarks.Any(point => point.X < 0 || point.Y < 0 || point.X >= portrait.Width || point.Y >= portrait.Height))
            throw new InvalidDataException("실제 모델의 공개 양성 프레임 응답 불일치");
        Console.WriteLine($"실제 컨테이너 세션 {sessionIndex + 1}: 합성 12회·공개 양성 1회·응답 식별/좌표 범위 통과 (좌표는 출력하지 않음)");
        if (sessionIndex == 0)
        {
            var endpoint = await runtime.StartAsync(deadline.Token);
            phase = "HTTP 오류 경계";
            using var http = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false });
            using (var unauthenticated = await http.GetAsync(new Uri(endpoint.Address, "/health/ready"), deadline.Token))
                RequireStatus(unauthenticated, HttpStatusCode.Unauthorized);
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", endpoint.Token);
            using (var malformed = await http.PostAsync(new Uri(endpoint.Address, "/v1/frames/detect"), Body([0, 0, 0, 0, 0]), deadline.Token))
                RequireStatus(malformed, HttpStatusCode.BadRequest);
            using (var oversized = await http.PostAsync(new Uri(endpoint.Address, "/v1/frames/detect"), Body(new byte[8 * 1024 * 1024 + 1]), deadline.Token))
                RequireStatus(oversized, HttpStatusCode.RequestEntityTooLarge);
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(portraitMeta, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            byte[] brokenImage = new byte[json.Length + 8];
            BinaryPrimitives.WriteInt32LittleEndian(brokenImage, json.Length);
            json.CopyTo(brokenImage, 4);
            using (var invalidImage = await http.PostAsync(new Uri(endpoint.Address, "/v1/frames/detect"), Body(brokenImage), deadline.Token))
                RequireStatus(invalidImage, HttpStatusCode.BadRequest);
            phase = "잘린 HTTP 본문 수신 시간제한";
            await VerifyPartialBodyDeadlineAsync(endpoint, deadline.Token);
            Console.WriteLine("실제 호스트: 인증 실패·잘못된 봉투·과대 요청·손상 JPEG·본문 수신 시간제한 통과");
            phase = "heartbeat 유휴 생존";
            await Task.Delay(TimeSpan.FromSeconds(35), deadline.Token);
            var resumedMetadata = new FaceMeshFrameMetadata(sessionId, "synthetic", 13, black.Width, black.Height, false, 0);
            var resumed = await client.RequestAsync(black, resumedMetadata, deadline.Token);
            if (resumed.Status != FaceMeshStatus.NoFace || resumed.Metadata != resumedMetadata || resumed.Landmarks.Count != 0)
                throw new InvalidDataException("heartbeat 유휴 뒤 같은 처리 세션을 재사용하지 못했습니다.");
            Console.WriteLine("실제 heartbeat: 35초 유휴 뒤 같은 세션 추론 통과 (30초 lease 유지)");
        }
        phase = "소유 컨테이너 종료";
        await client.DisconnectAsync(deadline.Token);
    }
    latencies.Sort();
    Console.WriteLine($"실제 Linux C++/.NET/Windows HTTP: 3세션·정상 추론40회 통과, 총 {total.Elapsed.TotalSeconds:F2}초");
    Console.WriteLine($"합성 320x240 왕복 p50={latencies[latencies.Count / 2]:F2}ms p95={latencies[(int)Math.Ceiling(latencies.Count * .95) - 1]:F2}ms; 실카메라 지연 아님");
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine($"컨테이너 검증 실패: {phase} ({error.GetType().Name}). 원문 경로·토큰·사진·좌표는 출력하지 않습니다.");
    return 1;
}

static ByteArrayContent Body(byte[] bytes)
{
    var content = new ByteArrayContent(bytes);
    content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    return content;
}
static void RequireStatus(HttpResponseMessage response, HttpStatusCode expected)
{
    if (response.StatusCode != expected) throw new InvalidDataException($"예상 HTTP {(int)expected}, 실제 {(int)response.StatusCode}");
}
static async Task VerifyPartialBodyDeadlineAsync(ProcessingSession endpoint, CancellationToken cancellationToken)
{
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeout.CancelAfter(TimeSpan.FromSeconds(6));
    using var connection = new TcpClient();
    await connection.ConnectAsync(IPAddress.Loopback, endpoint.Address.Port, timeout.Token);
    await using var stream = connection.GetStream();
    string header = $"POST /v1/frames/detect HTTP/1.1\r\nHost: 127.0.0.1\r\nAuthorization: Bearer {endpoint.Token}\r\nContent-Type: application/octet-stream\r\nContent-Length: 100\r\nConnection: close\r\n\r\n";
    await stream.WriteAsync(Encoding.ASCII.GetBytes(header), timeout.Token);
    await stream.WriteAsync(new byte[] { 5, 0 }, timeout.Token);
    using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
    string? statusLine = await reader.ReadLineAsync(timeout.Token);
    if (statusLine is null || !statusLine.StartsWith("HTTP/1.1 408 ", StringComparison.Ordinal))
        throw new InvalidDataException("부분 본문이 명시적인 수신 시간초과로 거부되지 않았습니다.");
}
