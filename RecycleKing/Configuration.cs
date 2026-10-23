using Dalamud.Configuration;

namespace RecycleKing;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public List<uint> ItemIds { get; set; } = [];
    public bool HideAutoRetainerGcOverlay { get; set; } = true;
    public bool HighlightListedItems { get; set; } = true;
}
