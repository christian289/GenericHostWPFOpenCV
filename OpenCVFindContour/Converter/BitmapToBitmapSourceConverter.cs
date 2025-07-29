namespace OpenCVFindContour.Converter;


[ValueConversion(typeof(Bitmap), typeof(BitmapSource))]
public sealed class BitmapToBitmapSourceConverter : ConverterMarkupExtension<BitmapToBitmapSourceConverter>
{
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteObject(IntPtr value);

    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null || value is not Image myImage)
        {
            return null;
        }

        IntPtr bmpPt;

        try
        {
            Bitmap bitmap = new(myImage);
            bmpPt = bitmap.GetHbitmap();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);

            return null;
        }

        try
        {
            BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                bitmap: bmpPt,
                palette: IntPtr.Zero,
                sourceRect: Int32Rect.Empty,
                sizeOptions: BitmapSizeOptions.FromEmptyOptions());

            bitmapSource.Freeze();

            return bitmapSource;
        }
        finally
        {
            DeleteObject(bmpPt);
        }
    }

    public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
