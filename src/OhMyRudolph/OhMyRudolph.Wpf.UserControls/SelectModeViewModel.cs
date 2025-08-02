using OhMyRudolph.Core.Managers;

namespace OhMyRudolph.Wpf.UserControls;

public partial class SelectModeViewModel : ObservableRecipient
{
    private readonly ILogger<SelectModeViewModel> logger;

    public SelectModeViewModel(
        ILogger<SelectModeViewModel> logger,
        CameraManager cameraManager,
        VideoViewModel videoViewModel)
    {
        this.logger = logger;
        CameraManager = cameraManager;
        VideoViewModel = videoViewModel;
        IsActive = true;
    }

    public CameraManager CameraManager { get; init; }

    public VideoViewModel VideoViewModel { get; init; }

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

    [RelayCommand]
    public void NavigatePhotoView()
    {
        Messenger.Send(new ValueChangedMessage<string>(nameof(PhotoViewModel)));
    }

    [RelayCommand]
    public async Task NavigateVideoView()
    {
        Messenger.Send(new ValueChangedMessage<string>(nameof(ReadyScreenViewModel)));
        var minimumDelayTask = Task.Delay(TimeSpan.FromSeconds(5));
        var operationsTask = ExecuteSequentialOperationsAsync();
        await Task.WhenAll(minimumDelayTask, operationsTask);
        Messenger.Send(new ValueChangedMessage<string>(nameof(VideoViewModel)));
    }

    private async Task ExecuteSequentialOperationsAsync()
    {
        await KillPythonProcessesViaWmiAsync();
        await CameraManager.CameraStartAsync();
        await VideoViewModel.CameraStartAsync();
    }

    [RelayCommand]
    public void NavigateMainPage()
    {
        Messenger.Send(new ValueChangedMessage<string>(nameof(MainPageViewModel)));
    }
}
