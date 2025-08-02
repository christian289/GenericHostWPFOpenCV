namespace OhMyRudolph.Core.Effects;

public sealed class DrawingRudolphEffect
{
    public Mat ProcesingImage(Mat mat, int nosePointX, int nosePointY)
    {
        if (mat.Empty())
            return mat;

        Cv2.Circle(mat, new Point(nosePointX, nosePointY), 30, new Scalar(0, 0, 255), -1);

        return mat;
    }
}
