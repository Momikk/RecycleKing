using System.Globalization;
using System.Text.Json;

namespace RecycleKing;

public sealed record SaleStatistics(DateTimeOffset? OldestSaleAt = null, DateTimeOffset? ObservedAt = null);

internal readonly record struct SaleStatisticsCells(string OldestSaleDate, string DaysAtObservation, string UpdatedDaysAgo);

internal static class SaleStatisticsDisplay
{
    internal static bool IsValid(SaleStatistics value, DateTimeOffset now)
        => (value.OldestSaleAt is not { } oldest || (oldest >= DateTimeOffset.UnixEpoch && oldest <= now))
            && (value.ObservedAt is not { } observed || (observed >= DateTimeOffset.UnixEpoch && observed <= now))
            && (value.OldestSaleAt is not { } first || value.ObservedAt is not { } last || first <= last);

    internal static SaleStatisticsCells Format(SaleStatistics? value, DateTimeOffset now, TimeZoneInfo zone)
    {
        if (value == null || !IsValid(value, now)) return new("", "", "");
        var date = value.OldestSaleAt is { } oldest
            ? TimeZoneInfo.ConvertTime(oldest, zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";
        var atObservation = value.OldestSaleAt is { } first && value.ObservedAt is { } observed
            ? Days(observed - first) : "";
        var age = value.ObservedAt is { } updated ? Days(now - updated) : "";
        return new(date, atObservation, age);
    }

    private static string Days(TimeSpan span) => (span.Ticks / TimeSpan.TicksPerDay).ToString(CultureInfo.InvariantCulture);
}

internal static class SaleStatisticsRules
{
    internal static bool CanReplace(SaleStatistics? previous, SaleStatistics next)
        => previous == null || previous == next || previous.ObservedAt is not { } prior
            || (next.ObservedAt is { } updated && updated > prior);
}

internal sealed record SaleStatisticsUpdate(uint ItemId, DateTimeOffset? OldestSaleAt, DateTimeOffset? ObservedAt);

internal static class SaleStatisticsInput
{
    internal static bool TryParse(string? json, out SaleStatisticsUpdate? update)
    {
        update = null;
        if (string.IsNullOrWhiteSpace(json) || json.Length > 2048) return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return false;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!names.Add(property.Name)) return false;
                if (!property.Name.Equals("oldestSaleAt", StringComparison.OrdinalIgnoreCase)
                    && !property.Name.Equals("observedAt", StringComparison.OrdinalIgnoreCase)) continue;
                if (property.Value.ValueKind == JsonValueKind.Null) continue;
                if (property.Value.ValueKind != JsonValueKind.String) return false;
                var text = property.Value.GetString()!;
                // An unspecified offset must not silently become the receiver's local time.
                if (text.Length < 20 || !text.Contains('T')
                    || !(text.EndsWith('Z') || (text.Length >= 25 && text[^6] is '+' or '-' && text[^3] == ':')))
                    return false;
            }
            update = document.RootElement.Deserialize<SaleStatisticsUpdate>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return update is { ItemId: > 0 };
        }
        catch (JsonException) { return false; }
    }
}
