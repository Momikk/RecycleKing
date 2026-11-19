using Dalamud.Configuration;

namespace RecycleKing;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public List<uint> ItemIds { get; set; } = [];
    public Dictionary<uint, SaleStatistics> SaleStatistics { get; set; } = [];
    public bool HideAutoRetainerGcOverlay { get; set; } = true;
    public bool HighlightListedItems { get; set; } = true;
    public bool ShowExpertDeliveryLevels { get; set; } = true;
}
