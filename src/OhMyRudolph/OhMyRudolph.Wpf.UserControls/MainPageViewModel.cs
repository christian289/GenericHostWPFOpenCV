namespace OhMyRudolph.Wpf.UserControls;

public partial class MainPageViewModel : ObservableRecipient
{
    public MainPageViewModel()
    {
        IsActive = true;
    }

    [RelayCommand]
    public void NavigateSelectMode()
    {
        Messenger.Send(new ValueChangedMessage<string>(nameof(SelectModeViewModel)));
    }
}