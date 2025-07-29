using OpenCVFindContour.Services;

namespace OpenCVFindContour.ViewModel;

public partial class CannyViewModel : ObservableRecipient, IRecipient<PropertyChangedMessage<ActivatedCameraHandleService>>
{
    IDisposable? currentSubscription;
    ActivatedCameraHandleService? currentCameraService;

    public CannyViewModel()
    {
        IsActive = true;
    }

    [ObservableProperty]
    Mat? _printMat;

    public void Receive(PropertyChangedMessage<ActivatedCameraHandleService> message)
    {
        if (message.PropertyName == nameof(MainWindowViewModel.SelectedCameraHandleService) && message.NewValue is not null)
        {
            currentSubscription?.Dispose();
            currentCameraService = message.NewValue;
            MakeSubscription(currentCameraService);
        }
    }

    public void RefreshSubscription()
    {
        if (currentCameraService is null)
            return;
        MakeSubscription(currentCameraService);
    }

    private void MakeSubscription(ActivatedCameraHandleService service)
    {
        if (currentCameraService is null)
            return;

        currentSubscription?.Dispose();
        currentCameraService = service;
        currentSubscription = currentCameraService.ImageStream
            .ObserveOn(SynchronizationContext.Current!)
            .Subscribe(mat =>
            {
                if (mat.Empty())
                {
                    PrintMat = null;
                    return;
                }
                ProcessImage(mat);
            });
    }

    private void ProcessImage(Mat mat)
    {
        Mat copy_mat = new();
        mat.CopyTo(copy_mat);
        Mat grayscale = new();
        Cv2.CvtColor(copy_mat, grayscale, ColorConversionCodes.BGR2GRAY);
        Mat canny = new();
        Cv2.Canny(
            src: grayscale,
            edges: canny,
            threshold1: 100.0,
            threshold2: 200.0,
            apertureSize: 3,
            L2gradient: true);
        PrintMat = canny;
    }
}
