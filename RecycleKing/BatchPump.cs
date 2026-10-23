namespace RecycleKing;

/// <summary>Drains ready state transitions together, yielding on a pending result.</summary>
internal sealed class BatchPump
{
    internal const int MaxSteps = 8;
    private bool active;

    internal void Run(Action advance, Func<int> progress)
    {
        // Native callbacks can raise addon events synchronously. They must not
        // re-enter a partially submitted transaction or submit the same action twice.
        if (active) return;
        active = true;
        try
        {
            for (var i = 0; i < MaxSteps; i++)
            {
                var before = progress();
                advance();
                if (progress() == before) break;
            }
        }
        finally { active = false; }
    }
}
