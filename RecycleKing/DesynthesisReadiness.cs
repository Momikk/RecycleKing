namespace RecycleKing;

/// <summary>Native result visibility does not imply the action has become idle.</summary>
internal sealed class DesynthesisReadiness
{
    internal const int StableIdleMilliseconds = 200;
    private long? idleSince;

    internal void Reset() => idleSince = null;

    internal bool CanProceed(long now, bool occupied, bool casting, float animationLock)
    {
        if (occupied || casting || !float.IsFinite(animationLock) || animationLock > 0)
        {
            Reset();
            return false;
        }
        idleSince ??= now;
        if (now < idleSince.Value) { idleSince = now; return false; }
        return now - idleSince.Value >= StableIdleMilliseconds;
    }
}
