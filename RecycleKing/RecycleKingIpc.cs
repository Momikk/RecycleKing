using System.Text.Json;

namespace RecycleKing;

internal sealed class RecycleKingIpc(Plugin plugin) : IDisposable
{
    private const string Prefix = "RecycleKing.";
    private readonly List<Action> unregister = [];

    internal void Register()
    {
        RegisterOnFramework("GetItemIds", () => plugin.Config.ItemIds.ToArray());
        RegisterOnFramework<uint, bool>("AddItem", plugin.AddItem);
        RegisterOnFramework<uint, bool>("RemoveItem", plugin.RemoveItem);
        RegisterOnFramework("ClearItems", plugin.Clear);
        RegisterOnFramework<bool, bool>("SetListOpen", plugin.SetListOpen);
        RegisterOnFramework<bool, bool>("SetHighlightListedItems", plugin.SetHighlightListedItems);
        RegisterOnFramework<bool, bool>("SetHideAutoRetainerGcOverlay", plugin.SetHideAutoRetainerGcOverlay);
        RegisterOnFramework<bool, bool>("SetShowExpertDeliveryLevels", plugin.SetShowExpertDeliveryLevels);
        RegisterOnFramework("StartBatchSale", plugin.Sales.TryStart);
        RegisterOnFramework("StopBatchSale", plugin.Sales.RequestStop);
        RegisterOnFramework("StartBatchDesynthesis", plugin.Desynthesis.TryStart);
        RegisterOnFramework("StopBatchDesynthesis", plugin.Desynthesis.RequestStop);
        RegisterOnFramework("StartArmouryCleanup", plugin.Armoury.TryStart);
        RegisterOnFramework("StopArmouryCleanup", plugin.Armoury.RequestStop);
        RegisterOnFramework("GetStatus", GetStatus);
        RegisterSync("GetApiVersion", () => 1);
    }

    private string GetStatus() => JsonSerializer.Serialize(new
    {
        listOpen = plugin.IsListOpen,
        highlightListedItems = plugin.Config.HighlightListedItems,
        hideAutoRetainerGcOverlay = plugin.Config.HideAutoRetainerGcOverlay,
        showExpertDeliveryLevels = plugin.Config.ShowExpertDeliveryLevels,
        batchSale = new
        {
            running = plugin.Sales.IsRunning,
            confirmedItems = plugin.Sales.ConfirmedItems,
            confirmedStacks = plugin.Sales.ConfirmedStacks,
            message = plugin.Sales.Status,
        },
        batchDesynthesis = new
        {
            running = plugin.Desynthesis.IsRunning,
            confirmedItems = plugin.Desynthesis.ConfirmedItems,
            outputRows = plugin.Desynthesis.OutputRows,
            message = plugin.Desynthesis.Status,
        },
        armouryCleanup = new
        {
            running = plugin.Armoury.IsRunning,
            confirmedItems = plugin.Armoury.ConfirmedItems,
            message = plugin.Armoury.Status,
        },
    });

    private void RegisterSync<TResult>(string name, Func<TResult> action)
    {
        var gate = Plugin.PluginInterface.GetIpcProvider<TResult>(Prefix + name);
        gate.RegisterFunc(action);
        unregister.Add(gate.UnregisterFunc);
    }

    private void RegisterOnFramework<TResult>(string name, Func<TResult> action)
        => RegisterSync<Task<TResult>>(name, () => OnFramework(action));

    private void RegisterOnFramework<TArgument, TResult>(string name, Func<TArgument, TResult> action)
    {
        var gate = Plugin.PluginInterface.GetIpcProvider<TArgument, Task<TResult>>(Prefix + name);
        gate.RegisterFunc(argument => OnFramework(() => action(argument)));
        unregister.Add(gate.UnregisterFunc);
    }

    private Task<TResult> OnFramework<TResult>(Func<TResult> action) => Plugin.Framework.RunOnFrameworkThread(() =>
    {
        if (plugin.IsDisposing) throw new ObjectDisposedException(nameof(RecycleKingIpc));
        return action();
    });

    public void Dispose()
    {
        for (var i = unregister.Count - 1; i >= 0; i--) unregister[i]();
        unregister.Clear();
    }
}
