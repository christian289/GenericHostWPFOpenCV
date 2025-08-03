namespace OhMyRudolph.Core.Helpers;

public static class MatToBitmapSaver
{
    /// <summary>
    /// Mat을 Bitmap으로 변환하여 파일로 저장
    /// </summary>
    public static bool SaveMatAsBitmap(Mat mat, string outputPath, ImageFormat format = null)
    {
        try
        {
            // 1. Mat 유효성 검사
            if (mat == null || mat.Empty())
            {
                Console.WriteLine("Mat이 null이거나 비어있습니다.");
                return false;
            }

            // 2. 디렉토리 존재 확인 및 생성
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 3. 기본 포맷 설정 (확장자 기반)
            if (format == null)
            {
                string extension = Path.GetExtension(outputPath).ToLower();
                format = extension switch
                {
                    ".jpg" or ".jpeg" => ImageFormat.Jpeg,
                    ".png" => ImageFormat.Png,
                    ".bmp" => ImageFormat.Bmp,
                    ".gif" => ImageFormat.Gif,
                    ".tiff" or ".tif" => ImageFormat.Tiff,
                    _ => ImageFormat.Png // 기본값
                };
            }

            // 4. Mat을 Bitmap으로 변환하여 저장
            using Bitmap bitmap = mat.ToBitmap();
            bitmap.Save(outputPath, format);

            // 5. 파일 크기 확인
            FileInfo fileInfo = new FileInfo(outputPath);
            if (fileInfo.Exists && fileInfo.Length > 0)
            {
                Console.WriteLine($"Bitmap 저장 성공: {outputPath} (크기: {fileInfo.Length} bytes)");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Bitmap 저장 중 오류: {ex.Message}");
            return false;
        }
    }
}