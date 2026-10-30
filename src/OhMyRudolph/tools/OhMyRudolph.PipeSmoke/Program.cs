using System.Diagnostics;
using System.Buffers.Binary;
using System.IO.Pipes;
using Microsoft.Extensions.Logging.Abstractions;
using OhMyRudolph.Abstractions.FaceMesh;
using OhMyRudolph.Core.Clients;
using OpenCvSharp;

// 실제 Python/MediaPipe/Windows pipe를 사용한다. 카메라·사용자 이미지·파일 저장은 사용하지 않는다.
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
using var frame = new Mat(240, 320, MatType.CV_8UC3, Scalar.Black);
string pipeName = "rudolph-smoke-" + Guid.NewGuid().ToString("N");
await using var client = new FaceMeshClient(NullLogger<FaceMeshClient>.Instance, pipeName);
var clock = Stopwatch.StartNew();
var latencies = new List<double>();
long before = Process.GetCurrentProcess().PrivateMemorySize64;
for (int session = 0; session < 3; session++)
{
    await client.StartAsync(deadline.Token);
    await client.ConnectAsync(deadline.Token);
    for (int index = 0; index < 12; index++)
    {
        var metadata = new FaceMeshFrameMetadata($"synthetic-{session}", "synthetic", index, frame.Width, frame.Height, false, 0);
        var timer = Stopwatch.StartNew();
        var result = await client.RequestAsync(frame, metadata, deadline.Token);
        latencies.Add(timer.Elapsed.TotalMilliseconds);
        if (result.Status != FaceMeshStatus.NoFace || result.Landmarks.Count != 0 || result.Metadata != metadata)
            throw new InvalidOperationException("합성 검은 프레임의 실제 서버 응답이 계약과 다릅니다.");
    }
    await client.DisconnectAsync(deadline.Token);
}
// 반대 방향(실제 Python 수신부)의 경계도 검사한다. 모든 연결/프로세스는 이 실행의 고유 pipe에 속한다.
await client.StartAsync(deadline.Token);
pipeName = client.OwnedServerPipeName ?? throw new InvalidOperationException("소유한 서버 pipe 이름이 없습니다.");
foreach (int length in new[] { 0, -1, 8 * 1024 * 1024 + 1 })
{
    await using var raw = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
    await raw.ConnectAsync(deadline.Token);
    byte[] invalid = new byte[4];
    BinaryPrimitives.WriteInt32LittleEndian(invalid, length);
    await raw.WriteAsync(invalid, deadline.Token);
    var eof = new byte[1];
    try
    {
        if (await raw.ReadAsync(eof, deadline.Token) != 0)
            throw new InvalidOperationException("실제 서버가 잘못된 요청 길이를 수락했습니다.");
    }
    catch (IOException) { /* Python이 해당 연결을 끊은 정상 거부 경로. */ }
}
foreach (byte[] truncated in new[] { new byte[] { 5, 0 }, new byte[] { 10, 0, 0, 0, 1 } })
{
    await using var raw = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
    await raw.ConnectAsync(deadline.Token);
    await raw.WriteAsync(truncated, deadline.Token);
    // Dispose로 EOF를 보낸 다음 새 연결에서 실제 서버의 재수락을 검사한다.
}
await using (var stalled = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
{
    await stalled.ConnectAsync(deadline.Token);
    await stalled.WriteAsync(new byte[] { 5, 0 }, deadline.Token);
    var eof = new byte[1];
    try
    {
        if (await stalled.ReadAsync(eof, deadline.Token) != 0)
            throw new InvalidOperationException("서버가 잘린 헤더 시간초과 뒤 연결을 닫지 않았습니다.");
    }
    catch (IOException) { }
}
await client.ConnectAsync(deadline.Token);
var recoveredMetadata = new FaceMeshFrameMetadata("recovered", "synthetic", 1, frame.Width, frame.Height, false, 0);
if ((await client.RequestAsync(frame, recoveredMetadata, deadline.Token)).Status != FaceMeshStatus.NoFace)
    throw new InvalidOperationException("잘린 요청 뒤 실제 서버의 재연결이 실패했습니다.");
int ownedPid = client.OwnedServerProcessId ?? throw new InvalidOperationException("소유한 서버 PID가 없습니다.");
using (var ownedServer = Process.GetProcessById(ownedPid))
{
    ownedServer.Kill();
    await ownedServer.WaitForExitAsync(deadline.Token);
}
// 죽은 파이프에 I/O하지 않은 채 재시작해야 IsConnected 캐시 경합을 드러낼 수 있다.
await client.StartAsync(deadline.Token);
await client.ConnectAsync(deadline.Token);
if ((await client.RequestAsync(frame, recoveredMetadata with { FrameId = 2 }, deadline.Token)).Status != FaceMeshStatus.NoFace)
    throw new InvalidOperationException("소유 서버 종료 후 재시작이 실패했습니다.");
Console.WriteLine("실제 소유 Python 실행 중 종료→중간 I/O 없이 재시작·재연결 통과");
await client.DisconnectAsync(deadline.Token);
Console.WriteLine("실제 Python 수신부: 잘못된 길이 3종, 잘린 헤더/본문 2종, 헤더 시간초과 뒤 재연결 통과");
latencies.Sort();
long after = Process.GetCurrentProcess().PrivateMemorySize64;
Console.WriteLine($"실제 Python 파이프: 세션 3회, 요청 {latencies.Count}회, 얼굴 없음·식별 일치 통과");
Console.WriteLine($"합성 입력: 320x240 BGR, 미러 없음, 회전 0도; 경과 {clock.Elapsed.TotalSeconds:F2}초");
Console.WriteLine($"요청 왕복 p50={latencies[latencies.Count / 2]:F2}ms p95={latencies[(int)Math.Ceiling(latencies.Count * .95) - 1]:F2}ms");
Console.WriteLine($"C# 프로세스 private bytes 전={before} 후={after}; 장기 누수/카메라 지연 측정이 아님");
