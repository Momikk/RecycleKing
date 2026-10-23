using System.Globalization;

namespace RecycleKing;

internal static class PinyinOrder
{
    private static readonly StringComparer Names = StringComparer.Create(CultureInfo.GetCultureInfo("zh-CN"), true);

    internal static List<T> Sort<T>(IEnumerable<T> items, Func<T, string> name, Func<T, uint> id) => items
        .OrderBy(name, Names).ThenBy(name, StringComparer.Ordinal).ThenBy(id).ToList();
}
