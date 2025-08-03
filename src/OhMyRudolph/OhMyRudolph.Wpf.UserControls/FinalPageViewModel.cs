namespace OhMyRudolph.Wpf.UserControls;

public partial class FinalPageViewModel : ObservableRecipient
{
    public FinalPageViewModel()
    {
        IsActive = true;
    }

    [RelayCommand]
    public void NavigateSelectMode()
    {
        Messenger.Send(new ValueChangedMessage<string>(nameof(SelectModeViewModel)));
    }
}