using OhMyRudolph.Core.Services;

namespace OhMyRudolph.Core.Managers;

public enum CameraManagerState { Stopped, Starting, Running, NoCamera, Failed, Disposed }

public sealed partial class CameraManager : ObservableRecipient, IAsyncDisposable
{
    private readonly ILogger<CameraManager> _logger;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private CameraService? _selectedCamera;
    private bool _disposed;

    public CameraManager(ILogger<CameraManager> logger)
    {
        _logger = logger;
        IsActive = true;
    }

    [ObservableProperty] private ObservableCollection<CameraService> _activatedCameras = [];
    [ObservableProperty] private CameraManagerState _state;
    [ObservableProperty] private string _statusMessage = "카메라가 꺼져 있습니다.";

    public CameraService? SelectedCamera
    {
        get => _selectedCamera;
        private set => SetProperty(ref _selectedCamera, value, true);
    }

    public async Task<bool> CameraStartAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        await _lifecycle.WaitAsync(linked.Token);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (SelectedCamera?.IsCapturing == true)
                return true;
            await StopCoreAsync();
            State = CameraManagerState.Starting;
            StatusMessage = "사용 가능한 카메라를 찾고 있습니다.";

            var discovered = await DiscoverCamerasAsync(linked.Token);
            ActivatedCameras = new ObservableCollection<CameraService>(discovered);
            if (discovered.Count == 0)
            {
                State = CameraManagerState.NoCamera;
                StatusMessage = "카메라를 찾을 수 없습니다. 연결과 접근 권한을 확인해주세요.";
                return false;
            }

            SelectedCamera = discovered[0];
            if (!await SelectedCamera.StartCaptureAsync(linked.Token))
            {
                await StopCoreAsync();
                State = CameraManagerState.Failed;
                StatusMessage = "카메라를 시작할 수 없습니다. 장치 연결을 확인해주세요.";
                return false;
            }
            State = CameraManagerState.Running;
            StatusMessage = "카메라 미리보기 중입니다.";
            return true;
        }
        catch (OperationCanceledException)
        {
            await StopCoreAsync();
            throw;
        }
        catch (Exception ex)
        {
            await StopCoreAsync();
            State = CameraManagerState.Failed;
            StatusMessage = "카메라를 시작하지 못했습니다. 연결과 접근 권한을 확인해주세요.";
            _logger.LogWarning("카메라 시작 오류 유형: {ErrorType}", ex.GetType().Name);
            throw;
        }
        finally { _lifecycle.Release(); }
    }

    private Task<List<CameraService>> DiscoverCamerasAsync(CancellationToken token) => Task.Run(async () =>
    {
        var cameras = new List<CameraService>();
        try
        {
            for (int index = 0; index < 10; index++)
            {
                token.ThrowIfCancellationRequested();
                VideoCapture? capture = new(index, VideoCaptureAPIs.DSHOW);
                try
                {
                    if (capture.IsOpened())
                    {
                        cameras.Add(new CameraService(_logger, index, capture));
                        capture = null;
                    }
                }
                finally { capture?.Dispose(); }
            }
            token.ThrowIfCancellationRequested();
            return cameras;
        }
        catch
        {
            foreach (var camera in cameras)
                await camera.DisposeAsync();
            throw;
        }
    }, token);

    public async Task<bool> ChangeCameraAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        await _lifecycle.WaitAsync(linked.Token);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (ActivatedCameras.Count == 0)
                return false;
            int currentIndex = SelectedCamera is null ? -1 : ActivatedCameras.IndexOf(SelectedCamera);
            var next = ActivatedCameras[(currentIndex + 1) % ActivatedCameras.Count];
            var old = SelectedCamera;
            // Broadcast null first: cancels inference and invalidates presentation before stopping native reads.
            SelectedCamera = null;
            if (old is not null)
                await old.StopCaptureAsync();
            linked.Token.ThrowIfCancellationRequested();
            SelectedCamera = next;
            if (!await next.StartCaptureAsync(linked.Token))
            {
                SelectedCamera = null;
                State = CameraManagerState.Failed;
                StatusMessage = "선택한 카메라를 시작할 수 없습니다.";
                return false;
            }
            State = CameraManagerState.Running;
            StatusMessage = "카메라 미리보기 중입니다.";
            return true;
        }
        catch
        {
            SelectedCamera = null;
            State = CameraManagerState.Failed;
            StatusMessage = "카메라 전환을 완료하지 못했습니다.";
            throw;
        }
        finally { _lifecycle.Release(); }
    }

    public async Task CameraStopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken);
        try { await StopCoreAsync(); }
        finally { _lifecycle.Release(); }
    }

    private async Task StopCoreAsync()
    {
        SelectedCamera = null;
        var cameras = ActivatedCameras.ToArray();
        ActivatedCameras = [];
        foreach (var camera in cameras)
            await camera.DisposeAsync();
        State = _disposed ? CameraManagerState.Disposed : CameraManagerState.Stopped;
        StatusMessage = "카메라가 꺼져 있습니다.";
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        await _lifecycle.WaitAsync();
        try
        {
            if (_disposed)
                return;
            _disposed = true;
            await StopCoreAsync();
            IsActive = false;
        }
        finally { _lifecycle.Release(); }
    }
}
