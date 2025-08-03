using OhMyRudolph.Core.Clients;
using OhMyRudolph.Core.Effects;
using OhMyRudolph.Core.Helpers;
using OhMyRudolph.Core.Models;

namespace OhMyRudolph.Wpf.UserControls;

public partial class PhotoViewModel : ObservableRecipient
{
    private readonly ILogger<PhotoViewModel> logger;
    private readonly FastApiFaceMeshClient client;
    private Mat? _originalMat;

    public PhotoViewModel(
        ILogger<PhotoViewModel> logger,
        FastApiFaceMeshClient client)
    {
        this.logger = logger;
        this.client = client;
        IsActive = true;
    }

    [ObservableProperty]
    Mat? _originalImage;

    [ObservableProperty]
    Mat? _processedImage;

    [RelayCommand]
    public void LoadImage()
    {
        try
        {
            var openFileDialog = new CommonOpenFileDialog
            {
                Title = "이미지 파일 선택",
                RestoreDirectory = true
            };
            openFileDialog.Filters.Add(new CommonFileDialogFilter("이미지 파일", "*.jpg;*.jpeg;*.png;*.bmp"));
            openFileDialog.Filters.Add(new CommonFileDialogFilter("모든 파일", "*.*"));

            if (openFileDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                // OpenCV로 이미지 로드
                _originalMat = Cv2.ImRead(openFileDialog.FileName!, ImreadModes.Color);

                if (_originalMat == null || _originalMat.Empty())
                {
                    logger.ZLogError($"이미지 로드 실패: {openFileDialog.FileName}");
                    return;
                }

                OriginalImage?.Dispose();
                OriginalImage = _originalMat;
                ProcessedImage?.Dispose();
                ProcessedImage = null; // 새 이미지 로드 시 처리된 이미지 초기화

                logger.ZLogInformation($"이미지 로드 완료: {openFileDialog.FileName}");
            }
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"이미지 로드 중 오류 발생: {ex.Message}");
        }
    }

    [RelayCommand]
    public void NoEffect()
    {
        if (_originalMat == null || _originalMat.Empty())
        {
            logger.ZLogWarning($"원본 이미지가 없습니다.");
            return;
        }

        try
        {
            ProcessedImage?.Dispose();
            var copy = new Mat();
            _originalMat.CopyTo(copy);
            ProcessedImage = copy;

            logger.ZLogInformation($"효과 없이 원본 이미지 표시");
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"이미지 처리 중 오류 발생: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task ApplyDrawingRudolphEffect()
    {
        await ApplyEffect(true);
    }

    [RelayCommand]
    public async Task ApplyOverlayDeadpoolEffect()
    {
        await ApplyEffect(false);
    }

    private async Task ApplyEffect(bool selectedEffectDrawingRudolph)
    {
        if (_originalMat == null || _originalMat.Empty())
        {
            logger.ZLogWarning($"원본 이미지가 없습니다.");
            return;
        }

        try
        {
            client.StartPythonServer();
            await client.WaitForServerAsync();
            IReadOnlyCollection<NosePosition>? response = await client.SendImageAndGetNoseAsync(_originalMat);

            if (response is null || response.Count <= 0) return;

            var processedMat = new Mat();
            _originalMat.CopyTo(processedMat);

            foreach (var nosePoint in response)
            {
                if (selectedEffectDrawingRudolph)
                {
                    _ = DrawingRudolphEffect.ProcessImage(processedMat, nosePoint.X, nosePoint.Y);
                }
                else
                {
                    using OverlayDeadpoolEffect effect = new();
                    _ = effect.ProcessImage(processedMat, nosePoint.X, nosePoint.Y);
                }
            }

            logger.ZLogInformation($"효과 적용 완료. 검출된 얼굴 수: {response?.Count}");
            ProcessedImage = processedMat;
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"효과 적용 중 오류 발생: {ex.Message}");
            client.StopPythonServer();
        }
    }

    [RelayCommand]
    public void SaveImage()
    {
        if (ProcessedImage == null)
        {
            logger.ZLogWarning($"저장할 이미지가 없습니다.");

            return;
        }

        try
        {
            var saveFileDialog = new CommonSaveFileDialog
            {
                Title = "이미지 저장",
                DefaultExtension = ".png",
            };
            saveFileDialog.Filters.Add(new CommonFileDialogFilter("PNG 파일", "*.png"));
            saveFileDialog.Filters.Add(new CommonFileDialogFilter("JPEG 파일", "*.jpg"));
            saveFileDialog.Filters.Add(new CommonFileDialogFilter("BMP 파일", "*.bmp"));

            if (saveFileDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                var ext = Path.GetExtension(saveFileDialog.FileName!);
                ImageFormat imageFormat = ext.ToLower() switch
                {
                    ".png" => ImageFormat.Png,
                    ".jpg" => ImageFormat.Jpeg,
                    ".bmp" => ImageFormat.Bmp,
                    _ => ImageFormat.Jpeg
                };
                MatToBitmapSaver.SaveMatAsBitmap(ProcessedImage, saveFileDialog.FileName!, imageFormat);

                logger.ZLogInformation($"이미지 저장 완료: {saveFileDialog.FileName}");
            }
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"이미지 저장 중 오류 발생: {ex.Message}");
        }
    }

    [RelayCommand]
    public void NavigateSelectMode()
    {
        client.StopPythonServer();
        OriginalImage?.Dispose();
        OriginalImage = null;
        ProcessedImage?.Dispose();
        ProcessedImage = null;
        Messenger.Send(new ValueChangedMessage<string>(nameof(SelectModeViewModel)));
    }
}
