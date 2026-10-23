using RecycleKing;
using System.Text.Json;

var checks = 0;
void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
    checks++;
}

var ids = new List<uint>();
Check(RecycleList.Add(ids, 42), "NQ add");
Check(!RecycleList.Add(ids, 1_000_042), "HQ shares the NQ entry");
Check(ids.SequenceEqual(new uint[] { 42 }), "one base ID stored");
ids.Clear();
Check(RecycleList.Add(ids, 1_000_042), "HQ first");
Check(!RecycleList.Add(ids, 42), "NQ shares the HQ entry");
Check(!RecycleList.Add(ids, 0), "reject empty slots");
Check(!RecycleList.Add(ids, 2_000_042), "reject collectible encoding");
Check(!RecycleList.Add(ids, 500_042), "reject event-item encoding");
Check(!RecycleList.Add(ids, uint.MaxValue), "reject invalid ID");
Check(RecycleList.Add(ids, 43), "different item remains separate");
var reloaded = JsonSerializer.Deserialize<List<uint>>(JsonSerializer.Serialize(ids))!;
reloaded = RecycleList.Normalize(reloaded);
Check(reloaded.SequenceEqual(new uint[] { 42, 43 }), "saved list round trip");
Check(reloaded.Remove(42) && reloaded.SequenceEqual(new uint[] { 43 }), "remove only chosen item");
Check(RecycleList.Normalize(new uint[] { 1_000_042, 42, 0, 43, 500_042, 2_000_042 }).SequenceEqual(new uint[] { 42, 43 }), "normalize saved legacy duplicates");
reloaded.Clear();
Check(reloaded.Count == 0, "clear list");
Console.WriteLine($"Passed {checks} recycle-list checks.");
var levels = DeliveryLevelMatch.UniqueLevels(new[] { ("A", 100u), ("A", 100u), ("B", 200u), ("C", 300u), ("C", 400u), ("D", 0u) });
Check(DeliveryLevelMatch.TryGet(levels, "B", out var level) && level == 200, "sorted row B");
Check(DeliveryLevelMatch.TryGet(levels, "A", out level) && level == 100, "duplicate copies share level");
Check(DeliveryLevelMatch.TryGet(levels, "A \uE03C", out level) && level == 100, "HQ icon ignored");
Check(!DeliveryLevelMatch.TryGet(levels, "C", out _), "ambiguous level omitted");
Check(!DeliveryLevelMatch.TryGet(levels, "D", out _), "zero level omitted");
Check(!DeliveryLevelMatch.TryGet(levels, "unknown", out _), "unknown row omitted");
Console.WriteLine($"Passed {checks} total list and delivery-level checks.");
var ordered = PinyinOrder.Sort(new[] { (Id: 4u, Name: "紫水晶"), (Id: 2u, Name: "白银"), (Id: 3u, Name: "秘银"), (Id: 1u, Name: "阿尔法") }, x => x.Name, x => x.Id);
Check(ordered.Select(x => x.Id).SequenceEqual(new uint[] { 1, 2, 3, 4 }), "Chinese pinyin order differs from Unicode order");
Check(PinyinOrder.Sort(new[] { (Id: 2u, Name: "白银"), (Id: 1u, Name: "白银") }, x => x.Name, x => x.Id)[0].Id == 1, "deterministic ID tie break");
var overlay = new FakeOverlay { IsOpen = true };
var lease = new OverlayVisibilityLease(overlay);
lease.Hide();
Check(!overlay.IsOpen && overlay.Operation && overlay.OtherWindowOpen, "hide only visibility");
lease.Hide(); lease.Restore();
Check(overlay.IsOpen, "restore originally open overlay after repeated hiding");
overlay.IsOpen = false;
lease.Hide(); lease.Restore();
Check(!overlay.IsOpen, "preserve originally closed overlay");
overlay.IsOpen = true; lease.Hide(); overlay.IsOpen = true; lease.Restore();
Check(overlay.IsOpen, "preserve external reopening");
var replacement = new FakeOverlay { IsOpen = true };
Check(!lease.IsTarget(replacement) && lease.IsTarget(overlay), "reload identities are distinct");
var replacementLease = new OverlayVisibilityLease(replacement); replacementLease.Hide(); replacementLease.Restore();
Check(replacement.IsOpen, "restore replacement independently");
Check(RecycleAppearance.Matches(new HashSet<uint> { 42 }, 1_000_042, 1), "HQ bag match");
Check(!RecycleAppearance.Matches(new HashSet<uint> { 42 }, 42, 0), "empty slot has no marker");
Check(!RecycleAppearance.Matches(new HashSet<uint> { 42 }, 43, 1), "unlisted item has no marker");
Check(!RecycleAppearance.ValidSource(4, 0) && !RecycleAppearance.ValidSource(0, 35) && RecycleAppearance.ValidSource(3, 34), "sorter source bounds");
Check(RecycleAppearance.Pink(1).X == 196 / 255f && RecycleAppearance.Pink(.28f).W == .28f, "pink mask matches UIColor 547 RGB");
Console.WriteLine($"Passed {checks} total checks.");

var sale = new SaleSlot(0, 3, 42, 99, 1, "condition:materia", 4, "测试物品");
Check(SaleRules.Eligible(42, 99, 4, false, new HashSet<uint> { 42 }), "listed stack is eligible");
Check(!SaleRules.Eligible(42, 99, 0, false, new HashSet<uint> { 42 }), "unsellable item is skipped");
Check(!SaleRules.Eligible(43, 99, 4, false, new HashSet<uint> { 42 }), "unlisted item is never sold");
Check(!SaleRules.Eligible(42, 0, 4, false, new HashSet<uint> { 42 }), "empty slot is skipped");
Check(!SaleRules.Eligible(42, 99, 4, true, new HashSet<uint> { 42 }), "symbolic slot is skipped");
Check(SaleRules.SameSlot(sale, sale), "unchanged snapshot");
Check(!SaleRules.SameSlot(sale, sale with { Flags = 0 }), "HQ replaced by NQ stops");
Check(!SaleRules.SameSlot(sale, sale with { Quantity = 98 }), "stack quantity change stops");
Check(!SaleRules.SameSlot(sale, sale with { Slot = 4 }), "moved item stops");
Check(!SaleRules.SameSlot(sale, sale with { Fingerprint = "other materia" }), "equipment state change stops");
Check(SaleRules.OwnTransaction(sale, 2, 42, 0, 3), "correct native sale transaction");
Check(!SaleRules.OwnTransaction(sale, 1, 42, 0, 3), "buy transaction is never confirmed");
Check(!SaleRules.OwnTransaction(sale, 2, 43, 0, 3), "different item dialog is never confirmed");
Check(!SaleRules.OwnTransaction(sale, 2, 42, 1, 3), "different bag dialog is never confirmed");
Check(!SaleRules.OwnTransaction(sale, 2, 42, 0, 4), "different slot dialog is never confirmed");
Check(SaleRules.Confirmed(sale, 110, 11, 100, 496), "full stack and gil acknowledgement");
Check(!SaleRules.Confirmed(sale, 110, 11, 100, 100), "inventory movement alone is not a sale");
Check(!SaleRules.Confirmed(sale, 110, 110, 100, 496), "gil increase alone is not a sale");
Check(!SaleRules.Confirmed(sale, 110, 12, 100, 496), "partial stack is not full completion");
Check(!SaleRules.Confirmed(sale, 110, 10, 100, 496), "unexpected larger inventory change stops");
Check(!SaleRules.Confirmed(sale, 110, 11, 100, 495), "insufficient proceeds are not confirmed");
Check(SaleRules.CanFit(sale, SaleRules.GilCap - 396) && !SaleRules.CanFit(sale, SaleRules.GilCap - 395), "gil cap boundary");
Check(SaleRules.QuantityMatches(sale, 1, 99) && !SaleRules.QuantityMatches(sale, 1, 100), "quantity prompt must match exact captured stack");
var saleContext = new SaleContext(2, 1, 2, 3, 4, 5, 6);
Check(saleContext != saleContext with { Retainer = 7 } && saleContext != saleContext with { Character = 7 }
    && saleContext != saleContext with { AddonId = 7 } && saleContext != saleContext with { Territory = 7 }, "context changes break batch ownership");
Console.WriteLine($"Passed {checks} total checks including batch-sale guards.");

Check(SaleRules.RetainerAvailable(true, 42, 42), "live retainer supports sales without inventory panel");
Check(!SaleRules.RetainerAvailable(true, 0, 42), "historical retainer does not enable sales");
Check(!SaleRules.RetainerAvailable(false, 42, 42), "inactive retainer cannot sell");
Check(!SaleRules.RetainerAvailable(true, 42, 43), "mismatched retainer cannot sell");
var retainerLabels = new[] { "委托雇员出售物品" };
var menuRows = new[] {
    new SaleMenuRow("到市场出售", true, true, false),
    new SaleMenuRow("交给雇员保管", true, true, false),
    new SaleMenuRow("交给雇员保管指定数量", true, true, false),
    new SaleMenuRow("委托雇员出售物品", true, true, false),
    new SaleMenuRow("舍弃", true, true, false),
    new SaleMenuRow("加入回收清单", false, false, false)
};
Check(SaleRules.NativeSaleIndex(menuRows, retainerLabels) == 3, "reported retainer menu selects sell not market or discard");
Check(SaleRules.NativeSaleIndex(menuRows.Take(3).ToArray(), retainerLabels) == -1, "storage and market alone cannot sell");
Check(SaleRules.NativeSaleIndex(menuRows.Reverse().ToArray(), retainerLabels) == 2, "resolve actual reordered display index");
Check(SaleRules.NativeSaleIndex(new[] { menuRows[5] }.Concat(menuRows).ToArray(), retainerLabels) == 4, "plugin prefix rows do not shift sale target incorrectly");
Check(SaleRules.NativeSaleIndex(new[] { menuRows[3] with { Enabled = false } }, retainerLabels) == -1, "disabled sale rejected");
Check(SaleRules.NativeSaleIndex(new[] { menuRows[3] with { Plain = false } }, retainerLabels) == -1, "decorated plugin item with identical text rejected");
Check(SaleRules.NativeSaleIndex(new[] { menuRows[3] with { Submenu = true } }, retainerLabels) == -1, "submenu rejected");
Check(SaleRules.NativeSaleIndex(new[] { menuRows[3], menuRows[3] }, retainerLabels) == -1, "duplicate native sale labels rejected");
Check(SaleRules.NativeSaleIndex(new[] { menuRows[3], menuRows[3] with { Enabled = false } }, retainerLabels) == -1, "enabled and disabled duplicate rejected");
Check(SaleRules.NativeSaleIndex(new[] { menuRows[3] with { Text = "委托雇员出售物品其他" } }, retainerLabels) == -1, "partial match rejected");
Check(SaleRules.NativeSaleIndex(Array.Empty<SaleMenuRow>(), retainerLabels) == -1, "empty menu rejected");
Check(SaleRules.NativeSaleIndex(new[] { default(SaleMenuRow) }, retainerLabels) == -1, "nontext row rejected");
Check(SaleRules.NativeSaleIndex(new[] { new SaleMenuRow("Have Retainer Sell Items", true, true, false) }, new[] { "Have Retainer Sell Items" }) == 0, "localized game label supported");
Console.WriteLine($"Passed {checks} total checks including native sale-menu regression cases.");

sealed class FakeOverlay
{
    public bool IsOpen { get; set; }
    public bool Operation { get; set; } = true;
    public bool OtherWindowOpen { get; set; } = true;
}
