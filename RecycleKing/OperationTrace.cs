namespace RecycleKing;

/// <summary>Default-level Dalamud logging, once per transition rather than once per frame.</summary>
internal sealed class OperationTrace(string operation)
{
    private string run = "idle";
    private long started;

    internal void Start(string details)
    {
        run = Guid.NewGuid().ToString("N")[..8];
        started = Environment.TickCount64;
        Info("开始", details);
    }

    internal void Info(string stage, string details) => Plugin.Log.Information(
        "[{Operation}:{Run}] {Stage} elapsedMs={Elapsed} {Details}",
        operation, run, stage, started == 0 ? 0 : Environment.TickCount64 - started, details);

    internal void Warning(string stage, string details) => Plugin.Log.Warning(
        "[{Operation}:{Run}] {Stage} elapsedMs={Elapsed} {Details}",
        operation, run, stage, started == 0 ? 0 : Environment.TickCount64 - started, details);
}
