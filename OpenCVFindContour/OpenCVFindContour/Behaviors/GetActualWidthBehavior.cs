namespace OpenCVFindContour.Behaviors;

public sealed class GetActualWidthBehavior : Behavior<FrameworkElement>
{
    public double ActualWidth
    {
        get { return (double)GetValue(ActualWidthProperty); }
        set { SetValue(ActualWidthProperty, value); }
    }

    public static readonly DependencyProperty ActualWidthProperty =
        DependencyProperty.Register(
            nameof(ActualWidth),
            typeof(double),
            typeof(GetActualWidthBehavior),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.SizeChanged += OnSizeChanged;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.SizeChanged -= OnSizeChanged;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ActualWidth = AssociatedObject.ActualWidth;
    }
}
