namespace OhMyRudolph.Wpf.Core.Converters;

[ValueConversion(typeof(double), typeof(double))]
public sealed class FontSizeStaticIncrementConverter : ConverterMarkupExtension<FontSizeStaticIncrementConverter>
{
    public double Increment { get; set; } = 2.0; // 기본 증가값

    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double fontSize)
        {
            double increment = Increment;

            if (parameter is string str && double.TryParse(str, out double parsedParam))
                increment = parsedParam;

            return fontSize + increment;
        }

        return value;
    }

    public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double fontSize)
        {
            double increment = Increment;

            if (parameter is string str && double.TryParse(str, out double parsedParam))
                increment = parsedParam;

            return fontSize - increment;
        }

        return value;
    }
}
