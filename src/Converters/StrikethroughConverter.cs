using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TVideo.Converters;

public class StrikethroughConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var boolValue = value is bool?
            ? (bool?)value
            : false;

        if (boolValue.GetValueOrDefault())
        {
            return new TextDecorationCollection
            {
                TextDecorations.Strikethrough
            };
        }

        return new TextDecorationCollection();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
