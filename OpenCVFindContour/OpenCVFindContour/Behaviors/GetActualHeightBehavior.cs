namespace OpenCVFindContour.Behaviors;

public sealed class GetActualHeightBehavior : Behavior<FrameworkElement>
{
    public double ActualHeight
    {
        get { return (double)GetValue(ActualHeightProperty); }
        set { SetValue(ActualHeightProperty, value); }
    }

    public static readonly DependencyProperty ActualHeightProperty =
        DependencyProperty.Register(
            nameof(ActualHeight),
            typeof(double),
            typeof(GetActualHeightBehavior),
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
        ActualHeight = AssociatedObject.ActualHeight;
    }
}