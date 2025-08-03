namespace OhMyRudolph.Wpf.Core.Converters;

[ValueConversion(typeof(object), typeof(bool))]
public sealed class NullToBooleanConverter : ConverterMarkupExtension<NullToBooleanConverter>
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isInverse = parameter?.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) == true;
        bool isNull = value == null;

        if (isInverse)
            return isNull;
        else
            return !isNull;
    }

    public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}