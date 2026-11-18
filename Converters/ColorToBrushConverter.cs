using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace Smec.Converters
{
    public class ColorToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Color color)
            {
                var brush = new SolidColorBrush(color);
                brush.Freeze(); // Замораживаем кисть для максимальной производительности в WPF
                return brush;
            }

            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                var parsed = (Color)ColorConverter.ConvertFromString(hex);
                var brush = new SolidColorBrush(parsed);
                brush.Freeze();
                return brush;
            }

            return Brushes.Transparent;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is SolidColorBrush brush ? brush.Color : Colors.Transparent;
    }
}
