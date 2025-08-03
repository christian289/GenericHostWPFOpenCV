using OhMyRudolph.Core.Models;

namespace OhMyRudolph.Core.Clients;

public sealed class FastApiFaceMeshClient(ILogger<FastApiFaceMeshClient> logger, HttpClient httpClient)
{
    private Process? _pythonProcess;
    private readonly string serverUrl = "http://127.0.0.1:9320";
    private bool isDisposed;

    public bool IsRunning { get; private set; } = false;

    public void StartPythonServer()
    {
        if (_pythonProcess != null && !_pythonProcess.HasExited)
        {
            logger.ZLogInformation($"FastAPI 서버가 이미 실행 중입니다.");
            return;
        }

        try
        {
            var serverPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FaceMeshFastApiServer.py");

            _pythonProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{serverPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    EnvironmentVariables =
                    {
                        ["PYTHONIOENCODING"] = "utf-8",
                        ["PYTHONUNBUFFERED"] = "1"
                    }
                }
            };
            _pythonProcess.Start();
            _pythonProcess.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    logger.ZLogInformation($"[FastAPI STDOUT] {e.Data}");
            };
            _pythonProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    logger.ZLogWarning($"[FastAPI STDERR] {e.Data}");
            };

            _pythonProcess.BeginOutputReadLine();
            _pythonProcess.BeginErrorReadLine();

            logger.ZLogInformation($"FastAPI 서버 시작됨 (PID: {_pythonProcess.Id})");
}
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"FastAPI 서버 시작 실패");
            throw;
        }
    }

    /// <summary>
    /// 서버가 준비될 때까지 대기합니다
    /// </summary>
    public async Task WaitForServerAsync(int timeoutSeconds = 30)
    {
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                var response = await httpClient.GetAsync($"{serverUrl}/");
                if (response.IsSuccessStatusCode)
                {
                    IsRunning = true;
                    logger.ZLogInformation($"FastAPI 서버 연결 성공");
                    return;
                }
            }
            catch (Exception ex)
            {
                // 서버가 아직 준비되지 않음
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"FastAPI 서버가 {timeoutSeconds}초 내에 응답하지 않습니다.");
    }

    /// <summary>
    /// 이미지에서 코 좌표를 검출합니다
    /// </summary>
    public async Task<IReadOnlyCollection<NosePosition>?> SendImageAndGetNoseAsync(Mat frame)
    {
        if (isDisposed)
            return null;

        try
        {
            // Mat을 JPEG로 인코딩
            if (!Cv2.ImEncode(".jpg", frame, out var imageBytes))
            {
                logger.ZLogError($"이미지 인코딩 실패");
                return null;
            }

            // MultipartFormDataContent 생성
            using var content = new MultipartFormDataContent();
            using var byteContent = new ByteArrayContent(imageBytes);
            byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            content.Add(byteContent, "file", "frame.jpg");

            // API 호출
            var response = await httpClient.PostAsync($"{serverUrl}/detect-noses", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                logger.ZLogError($"API 호출 실패: {response.StatusCode}, {errorContent}");
                return null;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            logger.ZLogDebug($"API 응답: {jsonResponse}");

            var apiResponse = JsonSerializer.Deserialize<FaceMeshApiResponse>(jsonResponse);

            if (apiResponse?.Success != true || apiResponse.Noses == null)
            {
                logger.ZLogWarning($"API 응답이 유효하지 않음");
                return null;
            }

            var nosePositions = apiResponse.Noses
                .Select(n => new NosePosition(n.X, n.Y))
                .ToList()
                .AsReadOnly();

            logger.ZLogDebug($"검출된 코 개수: {nosePositions.Count}");
            return nosePositions;
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"코 검출 API 호출 중 오류");
            return null;
        }
    }

    /// <summary>
    /// Python 서버를 종료합니다
    /// </summary>
    public void StopPythonServer()
    {
        try
        {
            if (_pythonProcess != null && !_pythonProcess.HasExited)
            {
                _pythonProcess.Kill();
                _pythonProcess.WaitForExit(5000);
                logger.ZLogInformation($"FastAPI 서버 종료됨");
            }
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"FastAPI 서버 종료 중 오류");
        }
        finally
        {
            IsRunning = false;
        }
    }

    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;
        StopPythonServer();
        httpClient?.Dispose();
        _pythonProcess?.Dispose();
    }
}

/// <summary>
/// FastAPI 응답 모델
/// </summary>
internal class FaceMeshApiResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("filename")]
    public string? Filename { get; set; }

    [JsonPropertyName("noses")]
    public List<NoseCoordinate>? Noses { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

/// <summary>
/// 코 좌표 모델
/// </summary>
internal class NoseCoordinate
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }
}
