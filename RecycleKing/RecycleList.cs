namespace RecycleKing;

/// <summary>Stores base item IDs only; a quality variant never creates another entry.</summary>
internal static class RecycleList
{
    internal static uint BaseId(uint id) => id is >= 1_000_000 and < 2_000_000 ? id - 1_000_000 : id;

    internal static List<uint> Normalize(IEnumerable<uint> ids) => ids.Select(BaseId)
        .Where(id => id is > 0 and < 500_000).Distinct().ToList();

    internal static bool Add(List<uint> ids, uint rawId)
    {
        var id = BaseId(rawId);
        if (id is 0 or >= 500_000 || ids.Contains(id)) return false;
        ids.Add(id);
        return true;
    }
}
