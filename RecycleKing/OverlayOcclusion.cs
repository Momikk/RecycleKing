using System.Numerics;

namespace RecycleKing;

internal readonly record struct OverlayBounds(Vector2 Min, Vector2 Max)
{
    internal bool Valid => float.IsFinite(Min.X) && float.IsFinite(Min.Y)
        && float.IsFinite(Max.X) && float.IsFinite(Max.Y) && Max.X > Min.X && Max.Y > Min.Y;

    internal bool Intersects(OverlayBounds other) => Valid && other.Valid
        && Min.X < other.Max.X && Max.X > other.Min.X && Min.Y < other.Max.Y && Max.Y > other.Min.Y;
}

internal static class OverlayOcclusion
{
    internal static bool Covered(OverlayBounds slot, IReadOnlyList<OverlayBounds> covers)
    {
        // Include the outline/shadow extent so it cannot bleed onto a nearby tooltip.
        var paint = new OverlayBounds(slot.Min - new Vector2(3), slot.Max + new Vector2(3));
        return covers.Any(paint.Intersects);
    }
}
