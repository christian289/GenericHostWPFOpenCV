namespace OhMyRudolph.Core.Effects;

public sealed class OverlayDeadpoolEffect
{
    private readonly Mat? overlayBgr;
    private readonly Mat? alphaMask;

    public OverlayDeadpoolEffect()
    {
        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "deadpool.png");
        if (!File.Exists(filePath)) return;

        Mat overlayPng = Cv2.ImRead(filePath, ImreadModes.Unchanged);
        if (overlayPng.Empty() || overlayPng.Channels() != 4) return;

        // BGRA를 BGR + Alpha로 분리 (한 번만)
        Cv2.Split(overlayPng, out Mat[] channels);
        overlayBgr = new Mat();
        Cv2.Merge([channels[0], channels[1], channels[2]], overlayBgr);
        alphaMask = channels[3];

        overlayPng.Dispose();
        for (int i = 0; i < 3; i++) channels[i].Dispose();
    }

    public Mat ProcessImage(Mat mat, int nosePointX, int nosePointY)
    {
        if (mat?.Empty() != false || overlayBgr?.Empty() != false)
            return mat ?? new Mat();

        // 1. 작은 크기로 설정 (50픽셀 고정)
        int overlaySize = 50;

        // 2. 코를 중심으로 위치 계산
        int startX = nosePointX - overlaySize / 2;
        int startY = nosePointY - overlaySize / 2;

        // 3. 경계 검사
        if (startX < 0 || startY < 0 ||
            startX + overlaySize > mat.Width ||
            startY + overlaySize > mat.Height)
        {
            return mat;
        }

        // 4. 리사이즈
        using var resizedOverlay = new Mat();
        using var resizedAlpha = new Mat();
        Cv2.Resize(overlayBgr, resizedOverlay, new Size(overlaySize, overlaySize));
        Cv2.Resize(alphaMask, resizedAlpha, new Size(overlaySize, overlaySize));

        // 5. 간단한 알파 블렌딩 (OpenCV 함수 사용)
        ApplyOverlay(mat, resizedOverlay, resizedAlpha, startX, startY);

        return mat;
    }

    private void ApplyOverlay(Mat background, Mat overlay, Mat alpha, int startX, int startY)
    {
        // ROI 영역 가져오기
        var roi = new Rect(startX, startY, overlay.Width, overlay.Height);
        using var backgroundROI = new Mat(background, roi);

        // 알파를 0~1 범위로 변환
        using var alphaFloat = new Mat();
        alpha.ConvertTo(alphaFloat, MatType.CV_32FC1, 1.0 / 255.0);

        // 3채널로 확장 (RGB 모두에 같은 알파 적용)
        using var alpha3 = new Mat();
        Cv2.Merge([alphaFloat, alphaFloat, alphaFloat], alpha3);

        // Float으로 변환
        using var bgFloat = new Mat();
        using var overlayFloat = new Mat();
        backgroundROI.ConvertTo(bgFloat, MatType.CV_32FC3);
        overlay.ConvertTo(overlayFloat, MatType.CV_32FC3);

        // 블렌딩: result = overlay * alpha + background * (1 - alpha)
        using var foreground = new Mat();
        using var background_part = new Mat();
        using var invAlpha = new Mat();
        using var result = new Mat();

        Cv2.Multiply(overlayFloat, alpha3, foreground);                    // overlay * alpha
        Cv2.Subtract(Scalar.All(1.0), alpha3, invAlpha);                 // 1 - alpha
        Cv2.Multiply(bgFloat, invAlpha, background_part);                 // background * (1-alpha)
        Cv2.Add(foreground, background_part, result);                     // 합치기

        // 다시 8비트로 변환해서 원본에 복사
        result.ConvertTo(backgroundROI, MatType.CV_8UC3);
    }
}