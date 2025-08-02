namespace OhMyRudolph.Wpf.UserControls;

public partial class SelectModeViewModel : ObservableRecipient
{
    public SelectModeViewModel()
    {
        IsActive = true;
    }

    [RelayCommand]
    public void NavigateMainPage()
    {
        Messenger.Send(new ValueChangedMessage<string>(nameof(MainPageViewModel)));
    }
}