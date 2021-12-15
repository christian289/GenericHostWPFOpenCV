using DevExpress.Mvvm.POCO;
using Microsoft.Extensions.DependencyInjection;

namespace OpenCVFindContour.ViewModel
{
    public class ViewModelLocator
    {
        public MainWindowViewModel MainWindowViewModel => (MainWindowViewModel)App.ServiceProvider.GetRequiredService(ViewModelSource.GetPOCOType(typeof(MainWindowViewModel)));
        public CannyViewModel CannyViewModel => (CannyViewModel)App.ServiceProvider.GetRequiredService(ViewModelSource.GetPOCOType(typeof(CannyViewModel)));
        public FindContour_MinAreaRectViewModel FindContour_MinAreaRectViewModel => (FindContour_MinAreaRectViewModel)App.ServiceProvider.GetRequiredService(ViewModelSource.GetPOCOType(typeof(FindContour_MinAreaRectViewModel)));
        public FindContour_ApproxPolyDPViewModel FindContour_ApproxPolyDPViewModel => (FindContour_ApproxPolyDPViewModel)App.ServiceProvider.GetRequiredService(ViewModelSource.GetPOCOType(typeof(FindContour_ApproxPolyDPViewModel)));
    }
}