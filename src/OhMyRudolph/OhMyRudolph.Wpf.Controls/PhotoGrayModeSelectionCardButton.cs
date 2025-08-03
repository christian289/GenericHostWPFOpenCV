namespace OhMyRudolph.Wpf.Controls;

public class PhotoGrayModeSelectionCardButton : Button
{
    static PhotoGrayModeSelectionCardButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PhotoGrayModeSelectionCardButton), new FrameworkPropertyMetadata(typeof(PhotoGrayModeSelectionCardButton)));
    }

    #region (string)ItemString
    public string ItemString
    {
        get { return (string)GetValue(ItemStringProperty); }
        set { SetValue(ItemStringProperty, value); }
    }

    public static readonly DependencyProperty ItemStringProperty =
        DependencyProperty.Register("ItemString", typeof(string), typeof(PhotoGrayModeSelectionCardButton));
    #endregion

    #region (Brush)ItemStringForeground
    public Brush ItemStringForeground
    {
        get { return (Brush)GetValue(ItemStringForegroundProperty); }
        set { SetValue(ItemStringForegroundProperty, value); }
    }

    public static readonly DependencyProperty ItemStringForegroundProperty =
        DependencyProperty.Register("ItemStringForeground", typeof(Brush), typeof(PhotoGrayModeSelectionCardButton));
    #endregion

    #region (Thickness)InternalMargin
    public Thickness InternalMargin
    {
        get { return (Thickness)GetValue(InternalMarginProperty); }
        set { SetValue(InternalMarginProperty, value); }
    }

    public static readonly DependencyProperty InternalMarginProperty =
        DependencyProperty.Register("InternalMargin", typeof(Thickness), typeof(PhotoGrayModeSelectionCardButton), new PropertyMetadata(new Thickness(10, 10, 10, 10)));
    #endregion
}
