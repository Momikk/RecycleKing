namespace RecycleKing;

internal static class DeliveryLevelMatch
{
    internal static Dictionary<string, uint> UniqueLevels(IEnumerable<(string Name, uint Level)> items) => items
        .Where(item => item.Level > 0 && !string.IsNullOrWhiteSpace(item.Name))
        .GroupBy(item => item.Name, StringComparer.Ordinal)
        .Where(group => group.Select(item => item.Level).Distinct().Count() == 1)
        .ToDictionary(group => group.Key, group => group.First().Level, StringComparer.Ordinal);

    internal static bool TryGet(IReadOnlyDictionary<string, uint> levels, string visibleName, out uint level)
        => levels.TryGetValue(visibleName.Replace("\uE03C", "").Trim(), out level);
}
