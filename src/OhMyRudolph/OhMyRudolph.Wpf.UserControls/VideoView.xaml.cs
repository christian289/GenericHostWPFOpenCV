using System.Windows.Media.Imaging;

namespace OhMyRudolph.Wpf.UserControls;

public partial class VideoView : UserControl
{
    public VideoView()
    {
        InitializeComponent();
        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "deadpool.png");
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();

        deadpoolSymbol.Source = bitmap;
    }
}
