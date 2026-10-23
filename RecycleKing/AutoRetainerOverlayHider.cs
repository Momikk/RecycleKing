using System.Reflection;

namespace RecycleKing;

/// <summary>Optional compatibility with the existing managed overlay; no runtime dependency on AutoRetainer.</summary>
internal sealed class AutoRetainerOverlayHider(Plugin plugin) : IDisposable
{
    private readonly List<OverlayVisibilityLease> leases = [];
    private long nextDiscovery;
    private bool warned;

    internal void Draw()
    {
        try
        {
            if (!plugin.Config.HideAutoRetainerGcOverlay) { Restore(); return; }
            if (Environment.TickCount64 >= nextDiscovery)
            {
                nextDiscovery = Environment.TickCount64 + 1000;
                if (!Plugin.PluginInterface.InstalledPlugins.Any(p => p.InternalName == "AutoRetainer" && p.IsLoaded))
                {
                    Restore();
                    return;
                }
                leases.RemoveAll(lease => !lease.IsAlive);
                // Only read this published source member from the already loaded AutoRetainer assembly.
                // Scanning all matching load contexts also covers unload/reload with the same version.
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name != "AutoRetainer") continue;
                    var module = assembly.GetType("AutoRetainer.Modules.GcHandin.AutoGCHandin", false);
                    var field = module?.GetField("Overlay", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                    var overlay = field?.GetValue(null);
                    if (overlay?.GetType().FullName != "AutoRetainer.UI.Overlays.AutoGCHandinOverlay") continue;
                    var windowName = overlay.GetType().GetProperty("WindowName")?.GetValue(overlay) as string;
                    if (windowName != "AutoRetainer GC Handin overlay") continue;
                    if (!leases.Any(lease => lease.IsTarget(overlay))) leases.Add(new(overlay));
                }
            }
            foreach (var lease in leases) lease.Hide();
            warned = false;
        }
        catch (Exception exception)
        {
            if (!warned) Plugin.Log.Warning(exception, "无法隐藏 AutoRetainer 军票悬浮框；可能需要适配当前版本。");
            warned = true;
            nextDiscovery = Environment.TickCount64 + 5000;
            Restore();
        }
    }

    private void Restore()
    {
        foreach (var lease in leases)
        {
            try { lease.Restore(); }
            catch (Exception exception) { Plugin.Log.Warning(exception, "无法恢复 AutoRetainer 军票悬浮框。"); }
        }
        leases.Clear();
    }

    public void Dispose() => Restore();
}
