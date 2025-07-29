using OpenCVFindContour.Services;
using Point = OpenCvSharp.Point;

namespace OpenCVFindContour.ViewModel;

public partial class FindContour_MinAreaRectViewModel : ObservableRecipient, IRecipient<PropertyChangedMessage<ActivatedCameraHandleService>>
{
    IDisposable? currentSubscription;
    ActivatedCameraHandleService? currentCameraService;

    public FindContour_MinAreaRectViewModel()
    {
        IsActive = true;
    }

    [ObservableProperty]
    Mat? _printMat;

    public void Receive(PropertyChangedMessage<ActivatedCameraHandleService> message)
    {
        if (message.PropertyName == nameof(MainWindowViewModel.SelectedCameraHandleService) && message.NewValue is not null)
        {
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
                Cv2.DrawContours(image: copy_mat, contours: contours, contourIdx: i, color: Scalar.Red, thickness: 2, hierarchy: hierarchys); // roi를 지정하게 된다면 나중에 offset 파라미터에 roi location을 넣어서 보정가능.
            }
            else
            {
                Point[][] points = new Point[1][];
                points[0] = Cv2.ConvexHull(contour);
                double sourceArea = Math.Abs(Cv2.ContourArea(points[0]));
                RotatedRect minRect = Cv2.MinAreaRect(InputArray.Create(points[0]));
                Point2f[] boxpoints = Cv2.BoxPoints(minRect);

                if (sourceArea > 100)
                {
                    Point[][] rect_points = new Point[1][];
                    rect_points[0] = boxpoints.Select(x => x.ToPoint()).ToArray();
                    Cv2.DrawContours(image: copy_mat, contours: rect_points, contourIdx: 0, color: Scalar.Red, thickness: 2);

                    //List<OpenCvSharp.Point> rectPoints = boxpoints.Select(x => x.ToPoint()).ToList();

                    //for (int j = 0; j < rectPoints.Count; j++)
                    //{
                    //    if (j == rectPoints.Count - 1)
                    //    {
                    //        Cv2.Line(src_copy, rectPoints[j], rectPoints[0], Scalar.Red, 2, LineTypes.AntiAlias);
                    //    }
                    //    else
                    //    {
                    //        Cv2.Line(src_copy, rectPoints[j], rectPoints[j + 1], Scalar.Red, 2, LineTypes.AntiAlias);
                    //    }
                    //}
                }
            }
        }

        PrintMat = copy_mat;
    }
}
