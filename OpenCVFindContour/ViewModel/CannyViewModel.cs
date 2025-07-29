namespace OpenCVFindContour.ViewModel;

public partial class CannyViewModel : ObservableRecipient, IRecipient<Mat>
{
    [ObservableProperty]
    public Mat? _printMat;

    public CannyViewModel()
    {
        IsActive = true;
    }

    public void Receive(Mat mat)
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
