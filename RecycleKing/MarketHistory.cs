namespace RecycleKing;

internal readonly record struct MarketHistoryLine(DateTime PurchaseTime, uint Quantity, uint Price);

internal static class MarketHistory
{
    internal static bool TrySummarize(uint expectedItem, uint responseItem, IReadOnlyList<MarketHistoryLine> entries,
        DateTimeOffset observedAt, out SaleStatistics? statistics)
    {
        statistics = null;
        if (expectedItem == 0 || responseItem != expectedItem || entries.Count > 20 || observedAt < DateTimeOffset.UnixEpoch)
            return false;

        DateTimeOffset? oldest = null;
        foreach (var sale in entries)
        {
            if (sale.PurchaseTime.Kind != DateTimeKind.Utc || sale.Quantity == 0 || sale.Price == 0) return false;
            var purchased = new DateTimeOffset(sale.PurchaseTime);
            if (purchased < DateTimeOffset.UnixEpoch || purchased > observedAt) return false;
            if (oldest == null || purchased < oldest.Value) oldest = purchased;
        }

        statistics = new(oldest, observedAt);
        return true;
    }
}
