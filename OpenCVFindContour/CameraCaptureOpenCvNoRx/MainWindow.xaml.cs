using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CameraCaptureOpenCvNoRx;

public partial class MainWindow : System.Windows.Window
{
    VideoCapture videoCapture;

    public MainWindow()
    {
        InitializeComponent();

        videoCapture = new(0, VideoCaptureAPIs.DSHOW);
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        Task.Factory.StartNew(() =>
        {
            if (!videoCapture.IsOpened())
            {
                videoCapture.Open(0, VideoCaptureAPIs.DSHOW);
            }

            if (videoCapture.IsOpened())
            {
                Mat frame = new();
                while (true)
                {
                    videoCapture.Read(frame);
                    if (frame.Empty())
                        continue;
                    
                    Dispatcher.Invoke(() =>
                    {
                        BitmapSource bitmap = frame.ToWriteableBitmap();
                        CameraImage.Source = bitmap;
                    });
                }
            }
        }, TaskCreationOptions.LongRunning);
    }
}