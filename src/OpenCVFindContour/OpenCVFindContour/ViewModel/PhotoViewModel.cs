namespace OpenCVFindContour.ViewModel;

public partial class PhotoViewModel : ObservableRecipient
{
    private readonly ILogger<PhotoViewModel> logger;

    public PhotoViewModel(
        ILogger<PhotoViewModel> logger)
    {
        IsActive = true;
        this.logger = logger;
    }
}