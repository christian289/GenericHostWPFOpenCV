using Microsoft.Win32;
using OhMyRudolph.Core.Sessions;
using OhMyRudolph.Domain.Sessions;
using OpenCvSharp.WpfExtensions;
using System.Windows.Media.Imaging;

namespace OhMyRudolph.Wpf.UserControls;

/// <summary>WPF image conversion and the local save dialog stay outside the session state/ViewModel.</summary>
internal static class BoothImageService
{
    public static object CreateThumbnail(Mat photo)
    {
        using var thumbnail = new Mat();
        var scale = Math.Min(1d, 260d / Math.Max(photo.Width, photo.Height));
        Cv2.Resize(photo, thumbnail, new OpenCvSharp.Size(Math.Max(1, (int)(photo.Width * scale)), Math.Max(1, (int)(photo.Height * scale))));
        return FreezeImage(thumbnail);
    }

    public static object FreezeImage(Mat image)
    {
        BitmapSource source = image.ToBitmapSource();
        source.Freeze();
        return source;
    }

    public static object CreateFramePreview(BoothFrame frame)
    {
        using var placeholder = new Mat(650, 520, MatType.CV_8UC3, new Scalar(85, 85, 85));
        using var composite = BoothCompositor.Compose([placeholder, placeholder, placeholder, placeholder], frame);
        using var preview = new Mat();
        Cv2.Resize(composite, preview, new OpenCvSharp.Size(240, 340));
        return FreezeImage(preview);
    }

    public static async Task<bool> SaveAsync(Mat composite)
    {
        var dialog = new SaveFileDialog
        {
            Title = "4컷 사진 저장",
            FileName = "OhMyRudolph",
            Filter = "PNG 이미지 (*.png)|*.png|JPEG 이미지 (*.jpg;*.jpeg)|*.jpg;*.jpeg",
            DefaultExt = ".png",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog() != true) return false;
        var fileName = dialog.FileName;
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg"))
            throw new IOException("PNG 또는 JPEG 확장자를 선택해 주세요.");
        await Task.Run(() =>
        {
            if (!Cv2.ImWrite(fileName, composite, Array.Empty<int>()))
                throw new IOException("이미지를 저장하지 못했습니다.");
        });
        return true;
    }
}
