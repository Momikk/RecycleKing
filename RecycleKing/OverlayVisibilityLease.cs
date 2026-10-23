using System.Reflection;

namespace RecycleKing;

/// <summary>Weak ownership of one managed visibility flag; does not hold a plugin assembly alive.</summary>
internal sealed class OverlayVisibilityLease(object target)
{
    private readonly WeakReference<object> target = new(target);
    private bool changed;
    private bool original;

    internal bool IsTarget(object value) => target.TryGetTarget(out var current) && ReferenceEquals(current, value);
    internal bool IsAlive => target.TryGetTarget(out _);

    internal void Hide()
    {
        if (!target.TryGetTarget(out var value)) return;
        var property = OpenProperty(value);
        var open = (bool)property.GetValue(value)!;
        if (!changed) { original = open; changed = open; }
        if (open) property.SetValue(value, false);
    }

    internal void Restore()
    {
        if (changed && target.TryGetTarget(out var value))
        {
            var property = OpenProperty(value);
            if (property.GetValue(value) is false) property.SetValue(value, original);
        }
        changed = false;
    }

    private static PropertyInfo OpenProperty(object value)
    {
        var property = value.GetType().GetProperty("IsOpen", BindingFlags.Public | BindingFlags.Instance);
        if (property?.PropertyType != typeof(bool) || !property.CanRead || !property.CanWrite)
            throw new NotSupportedException("Overlay visibility property is unavailable.");
        return property;
    }
}
