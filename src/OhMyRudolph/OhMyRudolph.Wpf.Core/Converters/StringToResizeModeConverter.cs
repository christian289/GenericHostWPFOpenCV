using OhMyRudolph.Core;

namespace OhMyRudolph.Wpf.Core.Converters;

[ValueConversion(typeof(string), typeof(ResizeMode))]
public sealed class StringToResizeModeConverter : ConverterMarkupExtension<StringToResizeModeConverter>
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string strValue)
            return ResizeMode.CanResize;

        return strValue switch
        {
            $"{nameof(WindowResizeMode.NoResize)}" => ResizeMode.NoResize,
            $"{nameof(WindowResizeMode.CanMinimize)}" => ResizeMode.CanMinimize,
            $"{nameof(WindowResizeMode.CanResize)}" => ResizeMode.CanResize,
            $"{nameof(WindowResizeMode.CanResizeWithGrip)}" => ResizeMode.CanResizeWithGrip,
            _ => ResizeMode.CanResize
        };
    }
    public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ResizeMode mode)
            return $"{nameof(WindowResizeMode.CanResize)}";

        return mode switch
        {
            ResizeMode.NoResize => $"{nameof(WindowResizeMode.NoResize)}",
            ResizeMode.CanMinimize => $"{nameof(WindowResizeMode.CanMinimize)}",
            ResizeMode.CanResize => $"{nameof(WindowResizeMode.CanResize)}",
            ResizeMode.CanResizeWithGrip => $"{nameof(WindowResizeMode.CanResizeWithGrip)}",
            _ => $"{nameof(WindowResizeMode.CanResize)}"
        };
    }
}
