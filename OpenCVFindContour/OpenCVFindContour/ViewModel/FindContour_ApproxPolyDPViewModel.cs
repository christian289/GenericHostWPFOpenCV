using OpenCVFindContour.Managers;
using OpenCVFindContour.Services;
using Point = OpenCvSharp.Point;

namespace OpenCVFindContour.ViewModel;

public partial class FindContour_ApproxPolyDPViewModel : ObservableRecipient, IRecipient<PropertyChangedMessage<ActivatedCameraHandleService>>
{
    IDisposable? currentSubscription;
    ActivatedCameraHandleService? currentCameraService;

    public FindContour_ApproxPolyDPViewModel()
    {
        IsActive = true;
    }

    [ObservableProperty]
    Mat? _printMat;

    public void Receive(PropertyChangedMessage<ActivatedCameraHandleService> message)
    {
        if (message.PropertyName == nameof(CameraManager.SelectedCameraHandleService) && message.NewValue is not null)
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
        Mat src_copy = new();
        mat.CopyTo(src_copy);
        Mat grayscale = new();

        if (mat.Channels() == 3)
            Cv2.CvtColor(mat, grayscale, ColorConversionCodes.BGR2GRAY);
        else // 1 채널일 경우
            Cv2.CvtColor(mat, grayscale, ColorConversionCodes.GRAY2BGR);
        
        Mat canny = new();
        Cv2.Canny(
            src: grayscale,
            edges: canny,
            threshold1: 100.0,
            threshold2: 200.0,
            apertureSize: 3,
            L2gradient: true);
        Cv2.FindContours(
            image: canny,
            contours: out Point[][] contours,
            hierarchy: out HierarchyIndex[] hierarchys,
            mode: RetrievalModes.Tree,
            method: ContourApproximationModes.ApproxSimple);

        for (int i = 0; i < contours.Length; i++)
        {
            Point[] contour = contours[i];
            double length = Cv2.ArcLength(contour, true);

            if (length < 300) continue; // 곡선 길이가 너무 짧은 것은 필터링 함.

            if (Cv2.IsContourConvex(contour))
            {
                Cv2.DrawContours(image: src_copy, contours: contours, contourIdx: i, color: Scalar.Red, thickness: 2, hierarchy: hierarchys); // roi를 지정하게 된다면 나중에 offset 파라미터에 roi location을 넣어서 보정가능.
            }
            else
            {
                Point[][] points = [Cv2.ConvexHull(contour)];
                double sourceArea = Math.Abs(Cv2.ContourArea(points[0]));

                double rate2 = 0.02;
                List<Point> approx = [];
                Cv2.ApproxPolyDP(InputArray.Create(points[0]), OutputArray.Create(approx), length * rate2, true);

                if (sourceArea > 100 && approx.Count == 4)
                {
                    Cv2.DrawContours(image: src_copy, contours: points, contourIdx: 0, color: Scalar.Red, thickness: 2);
                }
            }
        }

        PrintMat = src_copy;
    }
}
