namespace OhMyRudolph.Wpf.Controls;

public class PhotoGrayBackButton : Button
{
    static PhotoGrayBackButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PhotoGrayBackButton), new FrameworkPropertyMetadata(typeof(PhotoGrayBackButton)));
    }
}