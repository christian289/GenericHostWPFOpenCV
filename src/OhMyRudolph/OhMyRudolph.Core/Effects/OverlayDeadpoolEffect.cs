namespace OhMyRudolph.Core.Effects;

public sealed class OverlayDeadpoolEffect
{
    private readonly Mat? overlayBgr;
    private readonly Mat? alphaMaskFloat;
    private readonly Size? overlaySize;

    public OverlayDeadpoolEffect()
    {
        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "deadpool.png");

        if (!File.Exists(filePath))
            return;

        Mat overlayPng = Cv2.ImRead(filePath, ImreadModes.Unchanged); // BGRA
        if (overlayPng.Empty() || overlayPng.Channels() != 4)
            return;

        // BGR / Alpha 분리
        Cv2.Split(overlayPng, out Mat[] channels);
        overlayBgr = new Mat();
        Cv2.Merge([channels[0], channels[1], channels[2]], overlayBgr);
        overlaySize = overlayBgr.Size();

        using Mat overlayAlpha = channels[3]; // 투명도
        alphaMaskFloat = new Mat();
        overlayAlpha.ConvertTo(alphaMaskFloat, MatType.CV_32FC1, 1.0 / 255);
    }

    public Mat ProcesingImage(Mat mat, int nosePointX, int nosePointY)
    {
        if (mat.Empty() || overlayBgr is null || alphaMaskFloat is null || overlaySize is null)
            return mat;

        // ROI 범위 체크
        if (nosePointX < 0 || nosePointY < 0 ||
            nosePointX + overlaySize.Value.Width > mat.Width ||
            nosePointY + overlaySize.Value.Height > mat.Height)
            return mat;

        Rect roi = new(nosePointX, nosePointY, overlaySize.Value.Width, overlaySize.Value.Height);
        using Mat roiDst = new(mat, roi);

        // Float 변환
        using Mat roiFloat = new();
        using Mat overlayFloat = new();
        roiDst.ConvertTo(roiFloat, MatType.CV_32FC3);
        overlayBgr.ConvertTo(overlayFloat, MatType.CV_32FC3);

        // alpha3 구성
        using Mat alpha3 = new();
        Cv2.Merge([alphaMaskFloat, alphaMaskFloat, alphaMaskFloat], alpha3);

        // 1 - alpha3 계산
        using Mat one = new(alpha3.Size(), alpha3.Type(), Scalar.All(1.0));
        using Mat invAlpha3 = new();
        Cv2.Subtract(one, alpha3, invAlpha3);

        // 혼합 연산
        using Mat fg = new();
        using Mat bg = new();
        using Mat result = new();

        Cv2.Multiply(overlayFloat, alpha3, fg);
        Cv2.Multiply(roiFloat, invAlpha3, bg);
        Cv2.Add(fg, bg, result);

        // 결과를 원래 ROI에 다시 넣기
        result.ConvertTo(roiDst, MatType.CV_8UC3);

        return mat;
    }
}
