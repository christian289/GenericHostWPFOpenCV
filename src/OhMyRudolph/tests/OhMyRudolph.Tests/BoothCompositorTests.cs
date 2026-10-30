using OhMyRudolph.Core.Sessions;
using OhMyRudolph.Domain.Sessions;
using OpenCvSharp;

namespace OhMyRudolph.Tests;

public sealed class BoothCompositorTests
{
    [Theory]
    [InlineData(BoothFrame.Light, 247)]
    [InlineData(BoothFrame.Dark, 24)]
    public void CompositePlacesFourShotsInReadingOrderAndKeepsSourceOwnership(BoothFrame frame, byte background)
    {
        using var first = new Mat(40, 80, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var second = new Mat(80, 40, MatType.CV_8UC3, new Scalar(40, 50, 60));
        using var third = new Mat(40, 40, MatType.CV_8UC3, new Scalar(70, 80, 90));
        using var fourth = new Mat(40, 40, MatType.CV_8UC3, new Scalar(100, 110, 120));
        using var composite = BoothCompositor.Compose([first, second, third, fourth], frame);

        Assert.Equal(1200, composite.Width);
        Assert.Equal(1700, composite.Height);
        Assert.Equal(new Vec3b(background, background, background), composite.At<Vec3b>(0, 0));
        Assert.Equal(new Vec3b(10, 20, 30), composite.At<Vec3b>(385, 320));
        Assert.Equal(new Vec3b(40, 50, 60), composite.At<Vec3b>(385, 880));
        Assert.Equal(new Vec3b(70, 80, 90), composite.At<Vec3b>(1075, 320));
        Assert.Equal(new Vec3b(100, 110, 120), composite.At<Vec3b>(1075, 880));
        Assert.Equal(80, first.Width);
        Assert.Equal(new Vec3b(10, 20, 30), first.At<Vec3b>(0, 0));
    }

    [Fact]
    public void CompositeRejectsIncompletePhotosInsteadOfExportingBlankSlots()
    {
        using var shot = new Mat(20, 20, MatType.CV_8UC3, Scalar.Black);
        Assert.Throws<ArgumentException>(() => BoothCompositor.Compose([shot], BoothFrame.Light));
    }

    [Fact]
    public void CompositeCenterCropsWidePhotoWithoutStretching()
    {
        using var wide = new Mat(100, 300, MatType.CV_8UC3, new Scalar(0, 0, 255));
        using (var center = new Mat(wide, new Rect(110, 0, 80, 100)))
        {
            center.SetTo(new Scalar(0, 255, 0));
        }
        using var output = BoothCompositor.Compose([wide, wide, wide, wide], BoothFrame.Light);
        Assert.Equal(new Vec3b(0, 255, 0), output.At<Vec3b>(385, 65));
        Assert.Equal(new Vec3b(0, 255, 0), output.At<Vec3b>(385, 575));
    }
}
