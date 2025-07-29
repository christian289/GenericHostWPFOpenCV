namespace OpenCVFindContour.Converter;

[ValueConversion(typeof(Mat), typeof(WriteableBitmap))]
public class MatToWriteableBitmapConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Mat cnvt)
        {
            return null;
        }

        string pixelFormat = parameter.ToString();

        return pixelFormat switch
        {
            "Bgr101010" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Bgr101010, null),
            "Rgb48" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Rgb48, null),
            "Rgb24" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Rgb24, null),
            "Rgb128Float" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Rgb128Float, null),
            "Prgba64" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Prgba64, null),
            "Prgba128Float" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Prgba128Float, null),
            "Pbgra32" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Pbgra32, null),
            "Indexed8" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Indexed8, null),
            "Indexed4" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Indexed4, null),
            "Indexed2" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Indexed2, null),
            "Indexed1" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Indexed1, null),
            "Gray8" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Gray8, null),
            "Gray4" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Gray4, null),
            "Gray32Float" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Gray32Float, null),
            "Gray2" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Gray2, null),
            "Gray16" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Gray16, null),
            "Default" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Default, null),
            "Cmyk32" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Cmyk32, null),
            "BlackWhite" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.BlackWhite, null),
            "Bgra32" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Bgra32, null),
            "Bgr565" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Bgr565, null),
            "Bgr555" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Bgr555, null),
            "Bgr32" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Bgr32, null),
            "Bgr24" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Bgr24, null),
            "Rgba128Float" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Rgba128Float, null),
            "Rgba64" => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Rgba64, null),
            _ => cnvt.ToWriteableBitmap(cnvt.Width, cnvt.Height, PixelFormats.Bgr24, null)
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
