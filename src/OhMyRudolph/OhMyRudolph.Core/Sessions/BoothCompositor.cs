using OhMyRudolph.Domain.Sessions;

namespace OhMyRudolph.Core.Sessions;

/// <summary>
/// Original 1200 x 1700 two-by-two frame: 60px outer margin, 40px gutters,
/// four 520 x 650 center-cropped photos in reading order; 300px footer.
/// Light/dark neutral paper, red rule and typography are drawn here, with no bundled artwork.
/// Input Mats remain caller-owned; the returned CV_8UC3 Mat must be disposed by its caller.
/// </summary>
public static class BoothCompositor
{
    public static Mat Compose(IReadOnlyList<Mat> shots, BoothFrame frame)
    {
        ArgumentNullException.ThrowIfNull(shots);
        if (shots.Count != BoothSession.ShotCount || shots.Any(shot => shot is null || shot.Empty() || shot.Type() != MatType.CV_8UC3))
            throw new ArgumentException("Four nonempty BGR photos are required.", nameof(shots));
        if (!Enum.IsDefined(frame)) throw new ArgumentOutOfRangeException(nameof(frame));

        var paper = frame == BoothFrame.Light ? 247 : 24;
        var ink = frame == BoothFrame.Light ? 32 : 240;
        var output = new Mat(1700, 1200, MatType.CV_8UC3, new Scalar(paper, paper, paper));
        try
        {
            for (var i = 0; i < shots.Count; i++)
            {
                var shot = shots[i];
                var ratio = 520d / 650;
                var width = Math.Min(shot.Width, Math.Max(1, (int)Math.Round(shot.Height * ratio)));
                var height = Math.Min(shot.Height, Math.Max(1, (int)Math.Round(shot.Width / ratio)));
                using var crop = new Mat(shot, new OpenCvSharp.Rect((shot.Width - width) / 2, (shot.Height - height) / 2, width, height));
                using var cell = new Mat(output, new OpenCvSharp.Rect(60 + i % 2 * 560, 60 + i / 2 * 690, 520, 650));
                Cv2.Resize(crop, cell, new OpenCvSharp.Size(520, 650), 0, 0, InterpolationFlags.Area);
            }
            Cv2.Rectangle(output, new OpenCvSharp.Rect(60, 1460, 1080, 8), new Scalar(47, 47, 182), -1);
            Cv2.PutText(output, "OH MY RUDOLPH", new OpenCvSharp.Point(65, 1550), HersheyFonts.HersheySimplex, 1.7, new Scalar(ink, ink, ink), 3, LineTypes.AntiAlias);
            Cv2.PutText(output, "FOUR MOMENTS / ONE STORY", new OpenCvSharp.Point(65, 1620), HersheyFonts.HersheySimplex, 0.75, new Scalar(ink, ink, ink), 2, LineTypes.AntiAlias);
            return output;
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }
}
