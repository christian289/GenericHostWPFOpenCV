using OpenCVFindContour.Enums;
using OpenCVFindContour.Managers;

namespace OpenCVFindContour.ViewModel;

public partial class MainWindowViewModel : ObservableRecipient
{
    readonly ILogger<MainWindowViewModel> logger;

    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger,
        CameraManager cameraManager,
        NormalViewModel normalViewModel,
        DetectingNoseViewModel detectingNoseViewModel,
        CannyViewModel cannyViewModel,
        FindContour_ApproxPolyDPViewModel findContour_ApproxPolyDPViewModel,
        FindContour_MinAreaRectViewModel findContour_MinAreaRectViewModel)
    {
        IsActive = true;

        this.logger = logger;
        CameraManager = cameraManager;
        NormalViewModel = normalViewModel;
        DetectingNoseViewModel = detectingNoseViewModel;
        CannyViewModel = cannyViewModel;
        FindContour_ApproxPolyDPViewModel = findContour_ApproxPolyDPViewModel;
        FindContour_MinAreaRectViewModel = findContour_MinAreaRectViewModel;

        ResizeMode = $"{WindowResizeMode.CanResize}";

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
    public NormalViewModel NormalViewModel { get; init; }
    public DetectingNoseViewModel DetectingNoseViewModel { get; init; }
    public CannyViewModel CannyViewModel { get; init; }
    public FindContour_ApproxPolyDPViewModel FindContour_ApproxPolyDPViewModel { get; init; }
    public FindContour_MinAreaRectViewModel FindContour_MinAreaRectViewModel { get; init; }

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
        CameraManager.SelectedCameraHandleService = CameraManager.ActivatedCameraHandleCollection!.First();
        IsCameraStartButtonEnabled = true;
        ApplyingEffect = false;
    }

    [RelayCommand(CanExecute = nameof(CanCameraStart))]
    public async Task CameraStart()
    {
        await KillPythonProcessesViaWmiAsync();
        await CameraManager.CameraStartAsync();

        NormalViewModel.RefreshSubscription();
        await DetectingNoseViewModel.RefreshSubscription();
        CannyViewModel.RefreshSubscription();
        FindContour_ApproxPolyDPViewModel.RefreshSubscription();
        FindContour_MinAreaRectViewModel.RefreshSubscription();

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
}
