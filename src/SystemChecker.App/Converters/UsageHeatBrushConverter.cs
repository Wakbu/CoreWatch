using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SystemChecker.Converters;

public sealed class UsageHeatBrushConverter:IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture){var number=value is double d?Math.Max(0,d):0;var intensity=parameter?.ToString()=="memory"?Math.Clamp(Math.Log10(number+1)/4d,0,1):Math.Clamp(number/100d,0,1);var alpha=(byte)(18+intensity*82);return new SolidColorBrush(Color.FromArgb(alpha,53,158,210));}public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;
}

