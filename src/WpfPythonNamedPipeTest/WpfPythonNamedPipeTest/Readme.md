# WPF-Python Named Pipe 통신 데모

이 프로젝트는 WPF 애플리케이션과 Python 프로세스 간의 Named Pipe를 통한 Inter-Process Communication (IPC) 데모입니다.

## 프로젝트 구조

```
WpfNamedPipeDemo/
├── WpfNamedPipeDemo.csproj    # WPF 프로젝트 파일
├── App.xaml                   # WPF 애플리케이션 진입점
├── App.xaml.cs               # WPF 애플리케이션 코드
├── MainWindow.xaml           # 메인 윈도우 XAML
├── MainWindow.xaml.cs        # 메인 윈도우 코드 (Named Pipe Client)
├── python_server.py          # Python Named Pipe Server
├── requirements.txt          # Python 의존성
└── README.md                # 이 파일
```

## 사전 요구사항

1. **Python 3.13** (이미 설치되어 있다고 가정)
2. **.NET 9.0 SDK**
3. **pywin32** 패키지

## 설치 및 실행 방법

### 1. Python 의존성 설치

```bash
pip install -r requirements.txt
```

### 2. WPF 애플리케이션 빌드 및 실행

```bash
# 프로젝트 빌드
dotnet build

# 애플리케이션 실행
dotnet run
```

### 3. Self-Contained 배포 생성

```bash
# Windows x64용 Self-Contained 배포
dotnet publish -c Release -r win-x64 --self-contained true
```

## 작동 원리

### WPF 애플리케이션 (Client)
- `MainWindow.xaml.cs`에서 `NamedPipeClientStream` 사용
- 애플리케이션 시작 시 Python 서버 프로세스 자동 시작
- Named Pipe "WpfPythonPipe"를 통해 Python 서버와 통신
- 애플리케이션 종료 시 Python 프로세스 자동 종료

### Python 서버 (Server)
- `pywin32` 라이브러리의 `win32pipe` 모듈 사용
- `CreateNamedPipe` API로 Named Pipe 서버 생성
- WPF 클라이언트의 연결을 대기하고 메시지 처리
- 에코 서버 기능: 수신한 메시지에 타임스탬프 추가하여 응답

### 통신 프로토콜
1. WPF가 Python 프로세스 시작
2. Python이 Named Pipe 서버 생성 및 연결 대기
3. WPF가 Named Pipe 클라이언트로 연결
4. 양방향 메시지 교환
5. "SHUTDOWN" 메시지로 서버 종료

## 핵심 기술 요소

### C# (.NET 9)
- `System.IO.Pipes.NamedPipeClientStream`: Named Pipe 클라이언트
- `System.Diagnostics.Process`: Python 프로세스 관리
- **Async/Await 패턴**: 비동기 I/O 작업
- **using 문**: 자동 리소스 해제
- **StreamWriter/StreamReader**: 텍스트 기반 통신

### Python (pywin32)
- `win32pipe.CreateNamedPipe`: Named Pipe 서버 생성
- `win32pipe.ConnectNamedPipe`: 클라이언트 연결 수락
- `win32file.ReadFile/WriteFile`: 파이프 I/O 작업
- **예외 처리**: pywintypes.error 처리
- **로깅**: 디버깅 및 모니터링

## 메모리 최적화 및 성능 고려사항

### GC Heap 메모리 최적화
```csharp
// Using 문으로 자동 리소스 해제
using var pipeClient = new NamedPipeClientStream(".", "WpfPythonPipe", PipeDirection.InOut);

// StringBuilder 대신 StreamWriter/StreamReader 사용으로 메모리 효율성
var writer = new StreamWriter(pipeClient, Encoding.UTF8) { AutoFlush = true };
```

### Reactive Extensions 패턴 적용 가능성
```csharp
// 향후 확장 시 Observable 패턴 적용 가능
// IObservable<string> messageStream = Observable.FromAsync(() => reader.ReadLineAsync());
```

### Async/Await 최적화
```csharp
// ConfigureAwait(false) 사용으로 데드락 방지
await pipeClient.ConnectAsync(5000).ConfigureAwait(false);
```

## 디버깅 팁

1. **Python 로그 확인**: `python_server.log` 파일 생성됨
2. **Windows Event Viewer**: Named Pipe 관련 시스템 로그 확인
3. **Process Monitor**: 파일/파이프 액세스 모니터링
4. **Visual Studio Debugger**: WPF 애플리케이션 디버깅

## 확장 가능성

이 기본 구조를 바탕으로 다음과 같은 확장이 가능합니다:

1. **JSON 기반 구조화된 메시지 프로토콜**
2. **바이너리 데이터 전송** (이미지, 파일 등)
3. **멀티플 클라이언트 지원**
4. **보안 강화** (인증, 암호화)
5. **Reactive Extensions** 적용
6. **MVVM 패턴** 적용

## 문제 해결

### 일반적인 오류

1. **"파이프가 연결되어 있지 않습니다"**
   - Python 서버가 정상 시작되었는지 확인
   - 방화벽이나 보안 소프트웨어 확인

2. **"Python 프로세스 시작 실패"**
   - Python이 PATH에 등록되어 있는지 확인
   - `python --version`으로 설치 확인

3. **"pywin32 모듈을 찾을 수 없음"**
   - `pip install pywin32` 실행
   - `python -c "import win32pipe"` 테스트

이 데모는 최소한의 기능으로 Named Pipe 통신의 기본 개념을 이해하고 테스트할 수 있도록 설계되었습니다.