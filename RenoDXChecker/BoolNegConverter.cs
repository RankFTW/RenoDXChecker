using Microsoft.UI.Xaml.Data;

namespace RenoDXCommander;

/// <summary>
/// Value converter that negates a bool.
/// Used to disable install buttons while IsInstalling is true.
/// </summary>
public sealed class BoolNegConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is bool b ? !b : value;
}
