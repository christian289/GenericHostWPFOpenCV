namespace OpenCVFindContour.Clients;

public sealed class FaceMeshClient(ILogger<FaceMeshClient> logger)
{
    private const string PipeName = "FaceMeshPipe";
    private NamedPipeClientStream? _pipeClient;
    private Process? _pythonProcess;

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
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                EnvironmentVariables =
                {
                    ["PYTHONIOENCODING"] = "utf-8",
                    ["PYTHONUNBUFFERED"] = "1",
                }
            }
        };
        _pythonProcess.Start();
        _pythonProcess.OutputDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
                Console.WriteLine($"[PYTHON STDOUT] {args.Data}");
        };

        _pythonProcess.ErrorDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
                Console.WriteLine($"[PYTHON STDERR] {args.Data}");
        };
        _pythonProcess.BeginErrorReadLine();
        _pythonProcess.BeginOutputReadLine();
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

    public async Task<IReadOnlyCollection<(int X, int Y)>?> SendImageAndGetNoseAsync(Mat frame)
    {
        if (_pipeClient == null || !_pipeClient.IsConnected)
            return [];

        try
        {
            Cv2.ImEncode(".jpg", frame, out var jpegBytes);
            if (jpegBytes == null || jpegBytes.Length == 0)
                return [];

            byte[] lengthBytes = BitConverter.GetBytes(jpegBytes.Length);
            await _pipeClient.WriteAsync(lengthBytes.AsMemory(0, 4));
            await _pipeClient.WriteAsync(jpegBytes);
            await _pipeClient.FlushAsync();

            byte[] lenBuf = ArrayPool<byte>.Shared.Rent(4);
            await ReadExactAsync(_pipeClient, lenBuf, 4);
            //await _pipeClient.ReadExactlyAsync(lenBuf.AsMemory(0, 4)); // 에러 발생함.
            int resultLength = BitConverter.ToInt32(lenBuf.AsSpan());
            ArrayPool<byte>.Shared.Return(lenBuf);

            logger.ZLogDebug($"json length: {resultLength}");
            if (resultLength <= 0 || resultLength > 100_000) // 비정상 값 필터링
            {
                logger.ZLogError($"[🚨 경고] 수신된 JSON 길이가 비정상: {resultLength}");
                return [];
            }

            byte[] resultBuf = ArrayPool<byte>.Shared.Rent(resultLength);
            //await _pipeClient.ReadExactlyAsync(resultBuf); // 에러 발생함.
            await ReadExactAsync(_pipeClient, resultBuf, resultLength);

            var resultJson = Encoding.UTF8.GetString(resultBuf, 0, resultLength);
            ArrayPool<byte>.Shared.Return(resultBuf);
            logger.ZLogDebug($"파이프 응답: {resultJson}");
            //return [];

            try
            {
                var doc = JsonDocument.Parse(resultJson);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Array)
                    return [];

                List<(int X, int Y)> noseList = new(root.EnumerateArray().Count());
                foreach (var item in root.EnumerateArray())
                {
                    if (item.TryGetProperty("x", out var xProp) &&
                        item.TryGetProperty("y", out var yProp))
                    {
                        int x = xProp.GetInt32();
                        int y = yProp.GetInt32();
                        noseList.Add((x, y));
                        logger.ZLogDebug($"X:{x}, Y:{y}");
                    }
                    else
                    {
                        logger.ZLogWarning($"캐치 불가능");
                    }
                }

                return noseList.AsReadOnly();
            }
            catch (JsonException ex)
            {
                logger.ZLogError(ex, $"[JSON 파싱 실패] 원본: {resultJson}");
                return [];
            }
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"파이프 통신 중 예외 발생");
            return [];
        }
    }

    private async Task ReadExactAsync(Stream stream, byte[] buffer, int count)
    {
        int offset = 0;
        while (offset < count)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(offset, count - offset));
            if (n == 0)
                throw new IOException("파이프 스트림이 끊어졌거나 상대가 종료됨");
            offset += n;
        }
    }
}
