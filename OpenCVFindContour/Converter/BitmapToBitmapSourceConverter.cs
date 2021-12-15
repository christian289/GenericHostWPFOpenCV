using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace OpenCVFindContour.Converter
{

    [ValueConversion(typeof(Bitmap), typeof(BitmapSource))]
    public class BitmapToBitmapSourceConverter : IValueConverter
    {
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteObject(IntPtr value);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
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

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
