using System.Numerics;

namespace RecycleKing;

internal static class RecycleAppearance
{
    // UIColor 547 is #C43963 in all eight native themes, shared by the K prefix and bag marker.
    internal const ushort PrefixColor = 547;
    internal static Vector4 Pink(float alpha) => new(196 / 255f, 57 / 255f, 99 / 255f, alpha);
    internal static bool ValidSource(int page, int slot) => page is >= 0 and < 4 && slot is >= 0 and < 35;
    internal static bool Matches(IReadOnlySet<uint> ids, uint itemId, int quantity) => quantity > 0 && ids.Contains(RecycleList.BaseId(itemId));
}
