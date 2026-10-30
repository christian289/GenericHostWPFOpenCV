namespace OhMyRudolph.Wpf.UserControls;

public partial class VideoView : UserControl
{
    private VideoViewModel? _activeViewModel;

    public VideoView() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not VideoViewModel viewModel) return;
        _activeViewModel = viewModel;
        await viewModel.ActivateAsync();
    }

    private async void OnUnloaded(object sender, RoutedEventArgs e)
    {
        var viewModel = _activeViewModel;
        _activeViewModel = null;
        if (viewModel is not null) await viewModel.DeactivateAsync();
    }
}
