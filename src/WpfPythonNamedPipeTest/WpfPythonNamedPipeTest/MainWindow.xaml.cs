using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Windows;

namespace WpfPythonNamedPipeTest;

public partial class MainWindow : Window
{
    private Process? _pythonProcess;
    private NamedPipeClientStream? _pipeClient;
    private StreamWriter? _writer;
    private StreamReader? _reader;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await StartPythonServerAsync();
        await ConnectToPipeAsync();
    }

    private async Task StartPythonServerAsync()
    {
        try
        {
            string pythonScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "python_server.py");

            LogMessage($"Python 스크립트 경로: {pythonScriptPath}");
            LogMessage($"파일 존재 여부: {File.Exists(pythonScriptPath)}");

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{pythonScriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
            };

            _pythonProcess = Process.Start(processStartInfo);
            if (_pythonProcess == null)
            {
                throw new InvalidOperationException("Python 프로세스 시작 실패");
            }

            LogMessage($"Python 서버 시작됨 (PID: {_pythonProcess.Id})");

            // Python 서버 출력 모니터링
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!_pythonProcess.HasExited)
                    {
                        var output = await _pythonProcess.StandardOutput.ReadLineAsync();
                        if (!string.IsNullOrEmpty(output))
                        {
                            LogMessage($"[Python] {output}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Python 출력 모니터링 오류: {ex.Message}");
                }
            });

            // Python 서버가 파이프를 생성할 시간을 더 많이 줌
            await Task.Delay(5000);
        }
        catch (Exception ex)
        {
            LogMessage($"Python 서버 시작 오류: {ex.Message}");
        }
    }

    private async Task ConnectToPipeAsync()
    {
        try
        {
            LogMessage("Named Pipe 연결 시도 중...");

            _pipeClient = new NamedPipeClientStream(".", "WpfPythonPipe", PipeDirection.InOut);

            // 더 긴 타임아웃 시간과 재시도 로직
            int maxRetries = 3;
            int retryDelay = 2000;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    LogMessage($"연결 시도 {i + 1}/{maxRetries}...");
                    await _pipeClient.ConnectAsync(10000); // 10초 타임아웃

                    LogMessage("Named Pipe 연결 성공");
                    return;
                }
                catch (TimeoutException) when (i < maxRetries - 1)
                {
                    LogMessage($"연결 시도 {i + 1} 타임아웃, {retryDelay}ms 후 재시도...");
                    await Task.Delay(retryDelay);
                }
            }

            throw new TimeoutException("모든 연결 시도가 실패했습니다.");
        }
        catch (Exception ex)
        {
            LogMessage($"Named Pipe 연결 오류: {ex.Message}");

            // 연결 실패 시 Python 프로세스 상태 확인
            if (_pythonProcess != null)
            {
                LogMessage($"Python 프로세스 상태 - 실행 중: {!_pythonProcess.HasExited}");
                if (_pythonProcess.HasExited)
                {
                    LogMessage($"Python 프로세스 종료 코드: {_pythonProcess.ExitCode}");
                }
            }
        }
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pipeClient?.IsConnected != true)
        {
            LogMessage("파이프가 연결되어 있지 않습니다.");
            return;
        }

        try
        {
            string message = MessageTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                LogMessage("메시지를 입력하세요.");
                return;
            }

            // Writer/Reader 재사용 (한 번만 생성)
            if (_writer == null || _reader == null)
            {
                // BOM 없는 UTF-8 인코딩 사용
                var encoding = new UTF8Encoding(false);
                _writer = new StreamWriter(_pipeClient, encoding) { AutoFlush = true };
                _reader = new StreamReader(_pipeClient, encoding);
            }

            // 메시지 전송
            await _writer.WriteLineAsync(message);
            LogMessage($"전송: {message}");

            // 응답 수신 (타임아웃 설정)
            var responseTask = _reader.ReadLineAsync();
            var timeoutTask = Task.Delay(5000);

            var completedTask = await Task.WhenAny(responseTask, timeoutTask);

            if (completedTask == responseTask)
            {
                string? response = await responseTask;
                LogMessage($"수신: {response ?? "null"}");
            }
            else
            {
                LogMessage("응답 수신 타임아웃");
            }

            MessageTextBox.Clear();
        }
        catch (Exception ex)
        {
            LogMessage($"메시지 전송/수신 오류: {ex.Message}");

            // 오류 발생 시 Writer/Reader 재생성
            _writer?.Dispose();
            _reader?.Dispose();
            _writer = null;
            _reader = null;
        }
    }

    private void LogMessage(string message)
    {
        Dispatcher.Invoke(() =>
        {
            LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            LogTextBox.ScrollToEnd();
        });
    }

    private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            // Python 서버에 종료 신호 전송
            if (_pipeClient?.IsConnected == true && _writer != null)
            {
                await _writer.WriteLineAsync("SHUTDOWN");
                LogMessage("서버에 종료 신호 전송");
            }

            // Writer/Reader 정리
            _writer?.Dispose();
            _reader?.Dispose();

            _pipeClient?.Close();
            _pipeClient?.Dispose();

            // Python 프로세스 종료 대기
            if (_pythonProcess != null && !_pythonProcess.HasExited)
            {
                if (!_pythonProcess.WaitForExit(3000))
                {
                    _pythonProcess.Kill();
                    LogMessage("Python 프로세스 강제 종료");
                }
                else
                {
                    LogMessage("Python 프로세스 정상 종료");
                }
            }

            LogMessage("정리 완료");
        }
        catch (Exception ex)
        {
            LogMessage($"정리 중 오류: {ex.Message}");
        }
    }
}