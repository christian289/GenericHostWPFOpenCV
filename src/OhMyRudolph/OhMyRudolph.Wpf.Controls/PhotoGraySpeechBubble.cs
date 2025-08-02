namespace OhMyRudolph.Wpf.Controls;

public class PhotoGraySpeechBubble : Control
{
    static PhotoGraySpeechBubble()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PhotoGraySpeechBubble), new FrameworkPropertyMetadata(typeof(PhotoGraySpeechBubble)));
    }

    #region (Brush)PathStroke
    public Brush PathStroke
    {
        get { return (Brush)GetValue(PathStrokeProperty); }
        set { SetValue(PathStrokeProperty, value); }
    }

    public static readonly DependencyProperty PathStrokeProperty =
        DependencyProperty.Register("PathStroke", typeof(Brush), typeof(PhotoGraySpeechBubble), new PropertyMetadata(new SolidColorBrush(Colors.Black)));
    #endregion

    #region (Thickness)PathThickness
    public Thickness PathThickness
    {
        get { return (Thickness)GetValue(PathThicknessProperty); }
        set { SetValue(PathThicknessProperty, value); }
    }

    public static readonly DependencyProperty PathThicknessProperty =
        DependencyProperty.Register("PathThickness", typeof(Thickness), typeof(PhotoGraySpeechBubble), new PropertyMetadata(new Thickness(1.5)));
    #endregion

    #region (string)Text
    public string Text
    {
        get { return (string)GetValue(TextProperty); }
        set { SetValue(TextProperty, value); }
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register("Text", typeof(string), typeof(PhotoGraySpeechBubble), new PropertyMetadata(string.Empty));
    #endregion

}
