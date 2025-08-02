using OpenCVFindContour.Enums;
using OpenCVFindContour.Managers;

namespace OpenCVFindContour.ViewModel;

public partial class MainWindowViewModel : ObservableRecipient
{
    readonly ILogger<MainWindowViewModel> logger;

    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger,
        CameraManager cameraManager,
        VideoViewModel videoViewModel,
        PhotoViewModel photoViewModel)
    {
        IsActive = true;

        this.logger = logger;
        CameraManager = cameraManager;
        VideoViewModel = videoViewModel;
        PhotoViewModel = photoViewModel;
        Initialize();
    }

    private static async Task KillPythonProcessesViaWmiAsync()
    {
        await Task.Run(() =>
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, Name FROM Win32_Process WHERE Name LIKE '%python%'");

            using var results = searcher.Get();

            foreach (ManagementObject process in results.Cast<ManagementObject>())
            {
                try
                {
                    var processId = Convert.ToInt32(process["ProcessId"]);
                    var processName = process["Name"].ToString();

                    using var targetProcess = Process.GetProcessById(processId);
                    targetProcess.Kill();

                    Console.WriteLine($"WMI를 통해 종료됨: {processName} (PID: {processId})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"WMI 프로세스 종료 실패: {ex.Message}");
                }
                finally
                {
                    process?.Dispose();
                }
            }
        });
    }

    public CameraManager CameraManager { get; init; }
    public VideoViewModel VideoViewModel { get; init; }
    public PhotoViewModel PhotoViewModel { get; init; }

    [ObservableProperty]
    object _selectedViewModel;

    [ObservableProperty]
    string _resizeMode;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CameraStopCommand))]
    [NotifyCanExecuteChangedFor(nameof(CameraStartCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyEffectCommand))]
    bool _isCameraStartButtonEnabled;

    partial void OnIsCameraStartButtonEnabledChanged(bool value)
    {
        if (value)
        {
            ResizeMode = $"{WindowResizeMode.CanResize}";
            IsCameraStopButtonEnabled = false;
            ApplyingEffect = false;
        }
        else
        {
            ResizeMode = $"{WindowResizeMode.NoResize}";
            IsCameraStopButtonEnabled = true;
        }
    }

    [ObservableProperty]
    bool _isCameraStopButtonEnabled;

    [ObservableProperty]
    bool _applyingEffect;

    private void Initialize()
    {
        ResizeMode = $"{WindowResizeMode.CanResize}";
        CameraManager.SelectedCameraHandleService = CameraManager.ActivatedCameraHandleCollection!.First();
        IsCameraStartButtonEnabled = true;
        ApplyingEffect = false;
        SelectedViewModel = VideoViewModel;
    }

    [RelayCommand(CanExecute = nameof(CanCameraStart))]
    public async Task CameraStart()
    {
        await KillPythonProcessesViaWmiAsync();
        await CameraManager.CameraStartAsync();
        await VideoViewModel.CameraStartAsync();
        IsCameraStartButtonEnabled = false;
    }

    public bool CanCameraStart() => !IsCameraStopButtonEnabled && IsCameraStartButtonEnabled;

    [RelayCommand(CanExecute = nameof(CanCameraStop))]
    public async Task CameraStop()
    {
        await CameraManager.CameraStopAsync();

        IsCameraStartButtonEnabled = true;
        ApplyingEffect = false;
        Messenger.Send(new PropertyChangedMessage<bool>(this, nameof(ApplyingEffect), !ApplyingEffect, ApplyingEffect));
    }

    public bool CanCameraStop() => IsCameraStopButtonEnabled && !IsCameraStartButtonEnabled;

    [RelayCommand(CanExecute = nameof(CanApplyEffect))]
    public void ApplyEffect()
    {
        Messenger.Send(new PropertyChangedMessage<bool>(this, nameof(ApplyingEffect), !ApplyingEffect, ApplyingEffect));
    }

    public bool CanApplyEffect()
    {
        return !IsCameraStartButtonEnabled;
    }

    [RelayCommand]
    public void ChangeSelectedViewModelToVideoView() => SelectedViewModel = VideoViewModel;

    [RelayCommand]
    public void ChangeSelectedViewModelToPhotoView() => SelectedViewModel = PhotoViewModel;
}
