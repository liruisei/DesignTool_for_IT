using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WinDesign.Client.Converters;

/// <summary>
/// 剩余台数着色：大于 0 表示还缺设备（红色），小于等于 0 表示够用或有冗余（绿色）。
/// </summary>
public sealed class RemainingBrushConverter : IValueConverter
{
    public Brush PositiveBrush { get; set; } = Brushes.Red;

    public Brush NonPositiveBrush { get; set; } = Brushes.Green;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int remaining && remaining > 0 ? PositiveBrush : NonPositiveBrush;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
