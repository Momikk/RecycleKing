namespace RecycleKing;

internal enum RecycleSortColumn { Name, OldestSale, DaysAtObservation, UpdatedDaysAgo }

internal static class RecycleSort
{
    internal static List<T> Sort<T>(IEnumerable<T> rows, Func<T, string> name, Func<T, uint> id,
        Func<T, SaleStatistics?> statistics, RecycleSortColumn column, bool descending, DateTimeOffset now)
    {
        var named = PinyinOrder.Sort(rows, name, id, column == RecycleSortColumn.Name && descending);
        if (column == RecycleSortColumn.Name) return named;
        var values = named.Select(row => (Row: row, Key: Key(statistics(row), column, now)));
        // Keep unknown values last in either direction. Stable ordering retains
        // the pinyin/name/ID order when displayed values tie.
        var knownFirst = values.OrderBy(value => value.Key.HasValue ? 0 : 1);
        return (descending ? knownFirst.ThenByDescending(value => value.Key) : knownFirst.ThenBy(value => value.Key))
            .Select(value => value.Row).ToList();
    }

    private static long? Key(SaleStatistics? statistics, RecycleSortColumn column, DateTimeOffset now)
    {
        if (statistics == null || !SaleStatisticsDisplay.IsValid(statistics, now)) return null;
        return column switch
        {
            RecycleSortColumn.OldestSale => statistics.OldestSaleAt?.UtcTicks,
            RecycleSortColumn.DaysAtObservation when statistics is { OldestSaleAt: { } oldest, ObservedAt: { } observed }
                => (observed - oldest).Ticks / TimeSpan.TicksPerDay,
            RecycleSortColumn.UpdatedDaysAgo when statistics.ObservedAt is { } updated
                => (now - updated).Ticks / TimeSpan.TicksPerDay,
            _ => null,
        };
    }
}
