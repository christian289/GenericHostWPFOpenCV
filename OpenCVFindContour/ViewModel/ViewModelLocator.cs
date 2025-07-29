namespace OpenCVFindContour.ViewModel;

public class ViewModelLocator
{
    public MainWindowViewModel MainWindowViewModel => App.ServiceProvider.GetRequiredService<MainWindowViewModel>();
    public CannyViewModel CannyViewModel => App.ServiceProvider.GetRequiredService<CannyViewModel>();
    public FindContour_MinAreaRectViewModel FindContour_MinAreaRectViewModel => App.ServiceProvider.GetRequiredService<FindContour_MinAreaRectViewModel>();
    public FindContour_ApproxPolyDPViewModel FindContour_ApproxPolyDPViewModel => App.ServiceProvider.GetRequiredService<FindContour_ApproxPolyDPViewModel>();
}