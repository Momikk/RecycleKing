namespace RecycleKing;

/// <summary>Single-item warning consent never authorizes bulk or an unowned selection.</summary>
internal sealed class DesynthesisConfirmation
{
    internal enum Decision { Reject, ConfirmWarning, Wait, Submit }
    private bool warningSent;

    internal void Reset() => warningSent = false;

    internal Decision Evaluate(bool owned, bool bulk, bool warningPresent, bool warningChecked, bool buttonEnabled)
    {
        if (!owned || bulk) return Decision.Reject;
        if (warningPresent && !warningChecked)
        {
            if (warningSent) return Decision.Wait;
            warningSent = true;
            return Decision.ConfirmWarning;
        }
        return buttonEnabled ? Decision.Submit : Decision.Wait;
    }
}
