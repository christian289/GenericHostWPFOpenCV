namespace OhMyRudolph.Wpf.Core.Converters;

[ValueConversion(typeof(object), typeof(Visibility))]
public sealed class NullToVisibilityConverter : ConverterMarkupExtension<NullToVisibilityConverter>
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isInverse = parameter?.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) == true;
        bool isNull = value == null;

        if (isInverse)
            return isNull ? Visibility.Visible : Visibility.Collapsed;
        else
            return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
