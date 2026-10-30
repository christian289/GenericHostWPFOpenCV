namespace OhMyRudolph.Wpf.UserControls;

public partial class SelectModeViewModel : ObservableRecipient
{
    public SelectModeViewModel(ILogger<SelectModeViewModel> logger, VideoViewModel videoViewModel)
    {
        VideoViewModel = videoViewModel;
        IsActive = true;
    }

    public VideoViewModel VideoViewModel { get; }

    [NotifyCanExecuteChangedFor(nameof(NavigatePhotoViewCommand))]
    [NotifyCanExecuteChangedFor(nameof(NavigateVideoViewCommand))]
    [NotifyCanExecuteChangedFor(nameof(NavigateMainPageCommand))]
    [ObservableProperty] private bool _isNavigating;

    private bool CanNavigate() => !IsNavigating;

    [RelayCommand(CanExecute = nameof(CanNavigate))]
    public async Task NavigatePhotoView()
    {
        IsNavigating = true;
        try
        {
            // Stop only this app's camera work; never enumerate/kill unrelated Python processes.
            await VideoViewModel.DeactivateAsync();
            Messenger.Send(new ValueChangedMessage<string>(nameof(PhotoViewModel)));
        }
        finally { IsNavigating = false; }
    }

    [RelayCommand(CanExecute = nameof(CanNavigate))]
    public void NavigateVideoView()
    {
        // The booth's frame-selection screen is camera-free. Its explicit preview action starts capture.
        Messenger.Send(new ValueChangedMessage<string>(nameof(VideoViewModel)));
    }

    [RelayCommand(CanExecute = nameof(CanNavigate))]
    public void NavigateMainPage() => Messenger.Send(new ValueChangedMessage<string>(nameof(MainPageViewModel)));
}
