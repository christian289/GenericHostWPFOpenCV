using OhMyRudolph.Wpf.UserControls;

namespace OhMyRudolph.Wpf;

public partial class MainWindowViewModel : ObservableRecipient, IRecipient<ValueChangedMessage<string>>
{
    public MainWindowViewModel(
        MainPageViewModel mainPageViewModel,
        SelectModeViewModel selectModeViewModel,
        ReadyScreenViewModel readyScreenViewModel,
        VideoViewModel videoViewModel,
        PhotoViewModel photoViewModel)
    {
        MainPageViewModel = mainPageViewModel;
        SelectModeViewModel = selectModeViewModel;
        ReadyScreenViewModel = readyScreenViewModel;
        VideoViewModel = videoViewModel;
        PhotoViewModel = photoViewModel;

        IsActive = true;

        CurrentViewModel = MainPageViewModel;
    }

    public MainPageViewModel MainPageViewModel { get; init; }

    public SelectModeViewModel SelectModeViewModel { get; init; }

    public ReadyScreenViewModel ReadyScreenViewModel { get; init; }

    public VideoViewModel VideoViewModel { get; init; }

    public PhotoViewModel PhotoViewModel { get; init; }

    [ObservableProperty]
    ObservableObject _currentViewModel;

    public void Receive(ValueChangedMessage<string> message)
    {
        if (string.IsNullOrWhiteSpace(message.Value)) return;

        string viewModelName = message.Value;

        if (viewModelName == nameof(MainPageViewModel))
            CurrentViewModel = MainPageViewModel;
        else if (viewModelName == nameof(SelectModeViewModel))
            CurrentViewModel = SelectModeViewModel;
        else if (viewModelName == nameof(ReadyScreenViewModel))
            CurrentViewModel = ReadyScreenViewModel;
        else if (viewModelName == nameof(VideoViewModel))
            CurrentViewModel = VideoViewModel;
        else if (viewModelName == nameof(PhotoViewModel))
            CurrentViewModel = PhotoViewModel;
    }
}