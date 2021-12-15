using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using OpenCvSharp;
using System;
using System.Collections.Generic;

namespace OpenCVFindContour.ViewModel
{
    [POCOViewModel]
    public class FindContour_ApproxPolyDPViewModel
    {
        public virtual Mat PrintMat { get; set; }

        public FindContour_ApproxPolyDPViewModel()
        {
            Messenger.Default.Register<Mat>(this, OnProcessMat);
        }

        [Command(isCommand: false)]
        public void OnProcessMat(Mat src)
        {
            Mat src_copy = new();
            src.CopyTo(src_copy);
            Mat grayscale = new();
            Cv2.CvtColor(src, grayscale, ColorConversionCodes.BGR2GRAY);
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
                    Point[][] points = new Point[1][];
                    points[0] = Cv2.ConvexHull(contour);
                    double sourceArea = Math.Abs(Cv2.ContourArea(points[0]));

                    double rate2 = 0.02;
                    List<Point> approx = new();
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
}
