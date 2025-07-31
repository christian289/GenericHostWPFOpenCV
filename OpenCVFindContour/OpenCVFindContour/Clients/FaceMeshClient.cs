using System.Buffers;

namespace OpenCVFindContour.Clients;

public sealed class FaceMeshClient
{
    private const string PipeName = "FaceMeshPipe";
    private readonly ILogger<FaceMeshClient> logger;
    private NamedPipeClientStream _pipeClient;
    private Process? _pythonProcess;

    public FaceMeshClient(ILogger<FaceMeshClient> logger)
    {
        this.logger = logger;
    }

    public void StartPythonProcess()
    {
        _pythonProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NamedPipeFaceMeshServer.py")}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = false,
                RedirectStandardOutput = false,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
            }
        };
        _pythonProcess.Start();
        logger.ZLogInformation($"파이썬 서버 시작 (PID: {_pythonProcess.Id})");
    }

    public async Task ConnectPipe()
    {
        _pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        for (int retryCount = 0; retryCount < 10; retryCount++)
        {
            try
            {
                await _pipeClient.ConnectAsync();
                if (_pipeClient.IsConnected)
                {
                    logger.ZLogInformation($"파이프 연결 성공");
                    break;
                }
            }
            catch (IOException ex)
            {
                logger.ZLogWarning($"파이프 연결 대기 중... {retryCount + 1}/10: {ex.Message}");
                Thread.Sleep(500);
            }
        }

        if (!_pipeClient.IsConnected)
            throw new IOException("Named pipe 서버 연결 실패");
    }

    public void DisconnectPipe()
    {
        if (_pythonProcess != null && !_pythonProcess.HasExited)
            _pythonProcess.Kill();

        if (_pipeClient != null && _pipeClient.IsConnected)
        {
            _pipeClient.Dispose();
            logger.ZLogInformation($"파이프 연결 해제");
        }
    }

    public async Task<(int X, int Y)?> SendImageAndGetNoseAsync(Mat frame)
    {
        if (_pipeClient == null || !_pipeClient.IsConnected)
            return null;

        try
        {
            Cv2.ImEncode(".jpg", frame, out var jpegBytes);
            if (jpegBytes == null || jpegBytes.Length == 0)
                return null;

            byte[] lengthBytes = BitConverter.GetBytes(jpegBytes.Length);
            await _pipeClient.WriteAsync(lengthBytes.AsMemory(0, 4));
            await _pipeClient.WriteAsync(jpegBytes);
            await _pipeClient.FlushAsync();

            byte[] lenBuf = ArrayPool<byte>.Shared.Rent(4);
            int readLen = await _pipeClient.ReadAsync(lenBuf.AsMemory(0, 4));
            if (readLen != 4)
                throw new IOException("응답 길이 수신 실패");

            int resultLength = BitConverter.ToInt32(lenBuf, 0);
            byte[] resultBuf = ArrayPool<byte>.Shared.Rent(resultLength);
            int totalRead = 0;

            while (totalRead < resultLength)
            {
                int n = await _pipeClient.ReadAsync(resultBuf.AsMemory(totalRead, resultLength - totalRead));
                if (n == 0)
                    throw new IOException("응답 데이터 부족");
                totalRead += n;
            }

            var resultJson = Encoding.UTF8.GetString(resultBuf, 0, resultLength);

            try
            {
                var doc = JsonDocument.Parse(resultJson);
                var root = doc.RootElement;

                if (!root.TryGetProperty("x", out var xProp) ||
                    !root.TryGetProperty("y", out var yProp))
                {
                    logger.ZLogWarning($"캐치 불가능");
                    return null;
                }

                int x = xProp.GetInt32();
                int y = yProp.GetInt32();
                logger.ZLogInformation($"X:{x}, Y:{y}");
                return (x, y);
            }
            catch (JsonException ex)
            {
                logger.ZLogError(ex, $"[JSON 파싱 실패] 원본: {resultJson}");
                return null;
            }
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"파이프 통신 중 예외 발생");
            return null;
        }
    }
}
