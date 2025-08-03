namespace OhMyRudolph.Wpf.UserControls;

public partial class VideoView : UserControl
{
    public VideoView()
    {
        InitializeComponent();
    }

    private void PhotoGrayVideoViewPhotoShotButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var storyboard = new Storyboard();

        // Opacity 애니메이션
        var opacityAnimation = new DoubleAnimation
        {
            From = 0,
            To = 0.9,
            Duration = TimeSpan.FromMilliseconds(100),
            AutoReverse = true,
            EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut }
        };

        // Scale 애니메이션 (살짝 확대 효과)
        var scaleXAnimation = new DoubleAnimation
        {
            From = 0.8,
            To = 1.2,
            Duration = TimeSpan.FromMilliseconds(100),
            AutoReverse = true,
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut }
        };

        var scaleYAnimation = new DoubleAnimation
        {
            From = 0.8,
            To = 1.2,
            Duration = TimeSpan.FromMilliseconds(100),
            AutoReverse = true,
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut }
        };

        Storyboard.SetTarget(opacityAnimation, FlashEffect);
        Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));

        Storyboard.SetTarget(scaleXAnimation, FlashScale);
        Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("ScaleX"));

        Storyboard.SetTarget(scaleYAnimation, FlashScale);
        Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("ScaleY"));

        storyboard.Children.Add(opacityAnimation);
        storyboard.Children.Add(scaleXAnimation);
        storyboard.Children.Add(scaleYAnimation);

        storyboard.Begin();
    }
}
