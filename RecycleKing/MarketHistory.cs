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

    internal static decimal? AverageUnitPrice(IReadOnlyList<uint> completedListings, IReadOnlyList<MarketHistoryLine> history)
        => completedListings.Order().Take(5).Concat(history.Select(sale => sale.Price)).Average(price => (decimal?)price);
}

internal readonly record struct MarketOfferRow(uint ItemId, ulong ListingId, ulong RetainerId, uint Quantity,
    uint UnitPrice, bool HighQuality);

// Prove the native list with fresh public response pages. Never combine request IDs.
internal sealed class MarketOfferPages(uint itemId)
{
    private const int MaxGroups = 8;
    internal const int MaxListings = 100;
    private readonly Dictionary<int, Dictionary<ulong, MarketOfferRow>?> byRequest = [];

    internal void Observe(int requestId, IReadOnlyList<MarketOfferRow> rows)
    {
        if (requestId < 0 || rows.Count is 0 or > MaxListings || rows.Any(row => !Valid(row))) return;
        if (!byRequest.TryGetValue(requestId, out var group))
        {
            if (byRequest.Count >= MaxGroups) return;
            group = [];
            byRequest.Add(requestId, group);
        }
        if (group == null) return;

        foreach (var row in rows)
        {
            if (group.TryAdd(row.ListingId, row))
            {
                if (group.Count <= MaxListings) continue;
            }
            else if (group[row.ListingId] == row) continue;
            byRequest[requestId] = null;
            return;
        }
    }

    internal bool MatchesComplete(IReadOnlyList<MarketOfferRow> nativeRows)
    {
        if (nativeRows.Count is 0 or > MaxListings || nativeRows.Any(row => !Valid(row))
            || nativeRows.Select(row => row.ListingId).Distinct().Count() != nativeRows.Count) return false;
        return byRequest.Values.Any(group => group != null && group.Count == nativeRows.Count
            && nativeRows.All(row => group.TryGetValue(row.ListingId, out var received) && row == received));
    }

    private bool Valid(MarketOfferRow row) => itemId != 0 && row.ItemId == itemId && row.ListingId != 0 && row.RetainerId != 0
        && row.Quantity != 0 && row.UnitPrice != 0;
}
