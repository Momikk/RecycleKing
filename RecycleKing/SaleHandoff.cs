namespace RecycleKing;

/// <summary>Only a later Framework.Update can confirm stable readiness between sales.</summary>
internal sealed class SaleHandoff
{
    private long? idleFrame;

    internal void Reset() => idleFrame = null;

    internal bool Ready(long frame, bool nativeReady)
    {
        if (!nativeReady) { Reset(); return false; }
        if (idleFrame is null || frame < idleFrame.Value)
        {
            idleFrame = frame;
            return false;
        }
        return frame > idleFrame.Value;
    }
}
