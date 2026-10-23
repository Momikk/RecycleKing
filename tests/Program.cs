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
