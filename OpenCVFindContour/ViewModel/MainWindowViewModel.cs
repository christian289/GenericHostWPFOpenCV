using OpenCVFindContour.Interfaces;

namespace OpenCVFindContour.ViewModel;

public partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(IEnumerable<IActivatedCameraHandleService> cameraHandleServices)
    {
        ActivatedCameraHandleCollection = new ObservableCollection<IActivatedCameraHandleService>(cameraHandleServices);

        CameraStartButtonEnabled = true;
        CameraStopButtonEnabled = false;
    }

    [ObservableProperty]
    ObservableCollection<IActivatedCameraHandleService> _activatedCameraHandleCollection;

    [ObservableProperty]
    bool _cameraStartButtonEnabled;

    [ObservableProperty]
    bool _cameraStopButtonEnabled;

    [RelayCommand]
    public void CameraStart()
    {
        foreach (var cameraHandleService in ActivatedCameraHandleCollection)
            cameraHandleService.StartCapture();

        CameraStartButtonEnabled = false;
        CameraStopButtonEnabled = true;
    }

    [RelayCommand]
    public virtual void CameraStop()
    {
        foreach (var cameraHandleService in ActivatedCameraHandleCollection)
            cameraHandleService.StopCapture();

        CameraStartButtonEnabled = true;
        CameraStopButtonEnabled = false;
    }
}
