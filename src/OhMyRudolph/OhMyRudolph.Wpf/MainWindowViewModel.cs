using OhMyRudolph.Wpf.UserControls;

namespace OhMyRudolph.Wpf;

public partial class MainWindowViewModel : ObservableRecipient, IRecipient<ValueChangedMessage<string>>
{
    public MainWindowViewModel(
        MainPageViewModel mainPageViewModel,
        SelectModeViewModel selectModeViewModel)
    {
        MainPageViewModel = mainPageViewModel;
        SelectModeViewModel = selectModeViewModel;
        IsActive = true;

        CurrentViewModel = MainPageViewModel;
    }

    public MainPageViewModel MainPageViewModel { get; init; }

    public SelectModeViewModel SelectModeViewModel { get; init; }

    [ObservableProperty]
    ObservableRecipient _currentViewModel;

    public void Receive(ValueChangedMessage<string> message)
    {
        if (string.IsNullOrWhiteSpace(message.Value)) return;

        string viewModelName = message.Value;

        if (viewModelName == nameof(MainPageViewModel))
            CurrentViewModel = MainPageViewModel;
        else if (viewModelName == nameof(SelectModeViewModel))
            CurrentViewModel = SelectModeViewModel;
    }
}