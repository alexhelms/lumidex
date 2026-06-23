using Avalonia.Data.Converters;
using Lumidex.Features.TargetSummary;
using System.Globalization;

namespace Lumidex.Converters;

// Maps the TargetSummarySort enum to a human-readable label for the sort dropdown (the raw
// enum names — "DataAcquired" etc. — read poorly in the UI).
public class TargetSummarySortLabelConverter : IValueConverter
{
    public static readonly TargetSummarySortLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        TargetSummarySort.Alphabetical => "Alphabetical",
        TargetSummarySort.DataAcquired => "Total Data Acquired",
        TargetSummarySort.DataNeeded => "Data Needed",
        TargetSummarySort.Goal => "Goal",
        TargetSummarySort.FirstAcquired => "First Acquired",
        TargetSummarySort.LastAcquired => "Most Recent",
        _ => value?.ToString(),
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
