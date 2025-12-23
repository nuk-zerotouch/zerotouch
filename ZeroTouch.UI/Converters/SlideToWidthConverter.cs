using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ZeroTouch.UI.Converters
{
    public class SlideToWidthConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double progress && parameter is string widthStr &&
                double.TryParse(widthStr, out double totalWidth))
            {
                return progress * totalWidth;
            }

            return 0.0;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
