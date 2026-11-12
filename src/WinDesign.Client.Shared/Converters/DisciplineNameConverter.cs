using System.Globalization;
using System.Windows.Data;
using WinDesign.Client.ViewModels;

namespace WinDesign.Client.Converters;

/// <summary>把专业代码（Instrument / Telecom / IT）转换为中文显示名。</summary>
public sealed class DisciplineNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        DisciplineCatalog.Display(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
