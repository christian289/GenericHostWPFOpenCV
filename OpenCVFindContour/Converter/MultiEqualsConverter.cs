namespace OpenCVFindContour.Converter;

public sealed class MultiEqualsConverter : MultiConverterMarkupExtension<MultiEqualsConverter>
{
    public override object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 2)
            return false;

        var selected = values[0];
        var current = values[1];

        return Equals(selected, current);
    }

    public override object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        // 첫 번째 값은 SelectedCameraHandleService에 바인딩될 값
        if (value is bool isChecked && isChecked)
        {
            return new object[] { Binding.DoNothing, parameter }; // 일반적으로 current를 ViewModel로 돌려주고 싶으면 아래에 직접 작성
        }

        return new object[] { Binding.DoNothing, Binding.DoNothing };
    }
}