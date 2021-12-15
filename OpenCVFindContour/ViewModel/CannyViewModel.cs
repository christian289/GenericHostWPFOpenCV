using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using OpenCvSharp;

namespace OpenCVFindContour.ViewModel
{
    [POCOViewModel]
    public class CannyViewModel
    {
        public virtual Mat PrintMat { get; set; }

        public CannyViewModel()
        {
            Messenger.Default.Register<Mat>(this, OnProcessMat);
        }

        [Command(isCommand: false)]
        private void OnProcessMat(Mat mat)
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
}
