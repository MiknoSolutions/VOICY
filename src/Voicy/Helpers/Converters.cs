using System.Globalization;
using System.Windows.Data;

namespace Voicy.Helpers;

public class EnumBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is not string paramStr)
            return false;

        if (!Enum.IsDefined(value.GetType(), value))
            return false;

        var paramValue = Enum.Parse(value.GetType(), paramStr);
        return value.Equals(paramValue);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is not string paramStr)
            return Binding.DoNothing;

        return (bool)value ? Enum.Parse(targetType, paramStr) : Binding.DoNothing;
    }
}

public class BoolToCaptureTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? "Press keys..." : "Set";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class BoolToStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? "#4CAF50" : "#9E9E9E";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
