namespace OpenCVFindContour.Clients;

public sealed class FaceMeshClient : IDisposable
{
    private const string PipeName = "FaceMeshPipe";
    private readonly ILogger<FaceMeshClient> logger;
    private NamedPipeClientStream _pipeClient;
    private Process? _pythonProcess;

    public FaceMeshClient(ILogger<FaceMeshClient> logger)
    {
        this.logger = logger;

        _pythonProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NamedPipeFaceMeshServer.py")}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
            }
        };
        _pythonProcess.Start();
        this.logger.ZLogInformation($"NamedPipeFaceMeshServer 프로세스 시작: {_pythonProcess.Id}");

        Thread.Sleep(TimeSpan.FromSeconds(3)); // 파이썬 준비 시간

        this.logger.ZLogInformation($"Windows Named Pipe 할당: {PipeName}");
        //_pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        _pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
        _pipeClient.Connect(TimeSpan.FromSeconds(5));
    }

    public async Task<(int X, int Y)?> SendImageAndGetNoseAsync(Mat frame)
    {
        if (_pipeClient == null || !_pipeClient.IsConnected)
            return null;

        byte[] jpegBytes;
        using (var ms = new MemoryStream())
        {
            Cv2.ImEncode(".jpg", frame, out jpegBytes);
            jpegBytes = ms.ToArray();
        }

        // 길이(prefix) + 데이터 전송
        byte[] lengthBytes = BitConverter.GetBytes(jpegBytes.Length);
        await _pipeClient.WriteAsync(lengthBytes, 0, lengthBytes.Length);
        await _pipeClient.WriteAsync(jpegBytes, 0, jpegBytes.Length);
        await _pipeClient.FlushAsync();

        // 결과 길이 수신
        var lenBuf = new byte[4];
        await _pipeClient.ReadExactlyAsync(lenBuf.AsMemory(0, 4));
        int resultLength = BitConverter.ToInt32(lenBuf, 0);

        // JSON 수신
        var resultBuf = new byte[resultLength];
        await _pipeClient.ReadExactlyAsync(resultBuf.AsMemory(0, resultLength));

        var resultJson = Encoding.UTF8.GetString(resultBuf);
        try
        {
            var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;
            return (
                root.GetProperty("x").GetInt32(),
                root.GetProperty("y").GetInt32()
            );
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _pipeClient.Dispose();
        if (_pythonProcess != null && !_pythonProcess.HasExited)
            _pythonProcess.Kill();
    }
}
