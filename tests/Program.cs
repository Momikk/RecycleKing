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

var fastPump = new BatchPump();
var fastProgress = 0;
var fastCalls = 0;
fastPump.Run(() => { fastCalls++; if (fastProgress < 3) fastProgress++; }, () => fastProgress);
Check(fastProgress == 3 && fastCalls == 4, "ready menu quantity and confirmation transitions drain in one call");
fastCalls = 0;
fastPump.Run(() => fastCalls++, () => fastProgress);
Check(fastCalls == 1, "pending acknowledgement yields without spinning or resubmitting");
fastCalls = 0;
fastPump.Run(() => { fastCalls++; fastProgress++; }, () => fastProgress);
Check(fastCalls == BatchPump.MaxSteps, "immediate work is bounded per frame");
fastCalls = 0;
fastPump.Run(() => { fastCalls++; fastPump.Run(() => fastCalls++, () => fastProgress); }, () => fastProgress);
Check(fastCalls == 1, "synchronous addon events cannot reenter sale submission");
try { fastPump.Run(() => throw new InvalidOperationException("test"), () => fastProgress); }
catch (InvalidOperationException) { }
fastCalls = 0;
fastPump.Run(() => fastCalls++, () => fastProgress);
Check(fastCalls == 1, "exception releases pump guard for later user action");
Console.WriteLine($"Passed {checks} total checks including immediate sale scheduling.");

var slotBounds = new OverlayBounds(new(100, 100), new(140, 140));
Check(OverlayOcclusion.Covered(slotBounds, new[] { new OverlayBounds(new(110, 80), new(400, 300)) }), "tooltip overlapping slot suppresses mask and X");
Check(OverlayOcclusion.Covered(slotBounds, new[] { new OverlayBounds(new(0, 0), new(500, 500)) }), "fully covered slot suppressed");
Check(!OverlayOcclusion.Covered(slotBounds, new[] { new OverlayBounds(new(200, 200), new(400, 400)) }), "uncovered slot keeps highlight");
Check(OverlayOcclusion.Covered(slotBounds, new[] { new OverlayBounds(new(142, 110), new(400, 300)) }), "shadow extent beside tooltip is suppressed");
Check(!OverlayOcclusion.Covered(slotBounds, Array.Empty<OverlayBounds>()), "closing tooltip restores highlight");
Check(OverlayOcclusion.Covered(new(new(600, 500), new(640, 540)), new[] { new OverlayBounds(new(610, 480), new(900, 700)) }), "viewport origin translation preserves overlap");
Check(OverlayOcclusion.Covered(new(new(150, 150), new(210, 210)), new[] { new OverlayBounds(new(165, 120), new(600, 450)) }), "scaled slot and tooltip overlap");
Check(!new OverlayBounds(new(float.NaN, 0), new(10, 10)).Valid && !new OverlayBounds(new(10, 10), new(0, 0)).Valid, "invalid native bounds rejected");
Console.WriteLine($"Passed {checks} total checks including tooltip occlusion.");

var a = new DesynthItem(0, 2, 20713, 1, 0, "condition:30000", "围墙");
var b = new DesynthItem(1, 3, 42, 3, 1, "materia:1", "装备");
var plan = new DesynthesisScope(1, new[] { a, b });
Check(plan.Matches(1, 1, new[] { a, b }), "native tab agrees with finite plan");
Check(!plan.Matches(0, 1, new[] { a, b }) && !plan.Matches(1, 0, new[] { a, b }), "either tab changing blocks submission");
Check(!plan.Matches(1, 1, new[] { b, a }), "reordering cannot silently change processing order");
Check(!plan.Matches(1, 1, new[] { a, b, a with { Slot = 8 } }), "newly listed item never joins plan");
Check(!plan.Matches(1, 1, new[] { b }), "manual removal cannot silently shrink plan");
Check(!plan.Matches(1, 1, new[] { a with { Slot = 9 }, b }), "moved item rejected");
Check(!plan.Matches(1, 1, new[] { a, b with { Quantity = 4 } }), "received quantity cannot expand plan");
Check(!plan.Matches(1, 1, new[] { a, b with { Flags = 0 } }), "HQ replacement rejected");
Check(!plan.Matches(1, 1, new[] { a, b with { State = "different materia" } }), "same-ID equipment state replacement rejected");
Check(DesynthesisScope.OwnSelection(b, b with { Quantity = 1 }), "single-item confirmation can represent stack");
Check(!DesynthesisScope.OwnSelection(b, b with { Quantity = 4 }), "confirmation cannot expand captured quantity");
Check(!DesynthesisScope.OwnSelection(b, b with { Inventory = 2 }), "same item in different inventory is not owned");
Check(!DesynthesisScope.OwnSelection(b, b with { Slot = 7 }), "same item in different slot is not owned");
Check(!DesynthesisScope.OwnSelection(b, b with { Flags = 0 }), "same base ID with different quality not owned");
Check(!plan.ConsumeOne(42, null) && plan.Head == a, "unrelated native result cannot authorize consumption");
Check(!plan.ConsumeOne(a.Id, a) && plan.Head == a, "unchanged inventory waits rather than resubmitting");
Check(plan.ConsumeOne(a.Id, a with { Id = 99 }), "acknowledged single item can be replaced by result material");
Check(plan.Head == b && plan.Matches(1, 1, new[] { b }), "acknowledgement advances only to next captured row");
Check(!plan.ConsumeOne(b.Id, b with { Quantity = 1 }), "unexpected multi-item loss rejected");
Check(!plan.ConsumeOne(b.Id, null), "entire captured stack disappearance rejected for single submission");
Check(!plan.ConsumeOne(b.Id, b with { Quantity = 2, Flags = 0 }), "decrement plus quality swap rejected");
Check(plan.ConsumeOne(1_000_042, b with { Quantity = 2 }), "HQ native result matches captured base ID");
Check(plan.Head.Quantity == 2 && !plan.Empty, "stack shrinks by exactly one");
Check(plan.ConsumeOne(b.Id, b with { Quantity = 1 }), "second stack unit acknowledged");
Check(plan.ConsumeOne(b.Id, null) && plan.Empty, "last stack unit removes final captured row");
Check(plan.Matches(1, 1, Array.Empty<DesynthItem>()) && !plan.ConsumeOne(b.Id, null), "empty tab completes without another submission");
Check(!plan.Matches(1, 1, new[] { a }), "new output item never processed after plan is empty");
foreach (var invalid in new[]
{
    (-1, new[] { a }), (8, new[] { a }), (1, new[] { a, a }),
    (1, new[] { a with { Id = 0 } }), (1, new[] { a with { Quantity = 0 } }),
    (1, new[] { a with { Slot = -1 } }), (1, Enumerable.Range(0, 141).Select(i => a with { Slot = i }).ToArray()),
})
{
    var rejected = false;
    try { _ = new DesynthesisScope(invalid.Item1, invalid.Item2); }
    catch (ArgumentException) { rejected = true; }
    Check(rejected, "ambiguous or invalid captured native tab rejected");
}
Console.WriteLine($"Passed {checks} total checks including current-tab desynthesis boundaries.");

var nativeRow = new DesynthRow(a.Inventory, a.Slot, (uint)a.Quantity);
Check(DesynthesisScope.MatchesRow(nativeRow, nativeRow, a), "native row identity comes from position and real inventory, not presentation icon");
Check(!DesynthesisScope.MatchesRow(nativeRow, nativeRow with { Slot = 8 }, a), "agent/UI row disagreement rejected");
Check(!DesynthesisScope.MatchesRow(nativeRow, nativeRow, a with { Inventory = 2 }), "real container disagreement rejected");
Check(!DesynthesisScope.MatchesRow(nativeRow, nativeRow, a with { Slot = 9 }), "real slot disagreement rejected");
Check(!DesynthesisScope.MatchesRow(nativeRow, nativeRow, a with { Quantity = 2 }), "real quantity disagreement rejected");
Check(!DesynthesisScope.MatchesRow(nativeRow with { Quantity = 0 }, nativeRow with { Quantity = 0 }, a with { Quantity = 0 }), "empty native row rejected");

var gearsetIds = new HashSet<uint> { 20713, 1_000_042 };
Check(ArmouryMoveRules.Protected(gearsetIds, a), "saved NQ gear protected");
Check(ArmouryMoveRules.Protected(gearsetIds, b), "saved HQ gear protected");
Check(ArmouryMoveRules.Protected(gearsetIds, a with { Slot = 20, State = "another copy" }), "all copies matching saved gear identity protected");
Check(!ArmouryMoveRules.Protected(gearsetIds, b with { Flags = 0 }), "NQ not falsely marked saved when only HQ is saved");
Check(!ArmouryMoveRules.Protected(gearsetIds, a with { Flags = 1 }), "HQ not falsely marked saved when only NQ is saved");
Check(ArmouryMoveRules.Eligible(gearsetIds, a with { Id = 99 }), "unregistered single piece eligible");
Check(!ArmouryMoveRules.Eligible(gearsetIds, a), "registered piece never queued");
Check(!ArmouryMoveRules.Eligible(gearsetIds, a with { Id = 99, Quantity = 2 }), "unexpected stack excluded from equipment moves");
Check(!ArmouryMoveRules.Eligible(gearsetIds, a with { Id = 0 }), "empty source excluded");
Check(!ArmouryMoveRules.Eligible(gearsetIds, a with { Id = 500000 }), "non-normal item excluded");
Check(ArmouryMoveRules.Arrived(a, a with { Inventory = 1, Slot = 10 }), "acknowledged move changes only position");
Check(!ArmouryMoveRules.Arrived(a, a with { Id = 42 }), "other destination item is not acknowledgement");
Check(!ArmouryMoveRules.Arrived(a, a with { Flags = 1 }), "destination HQ mismatch rejected");
Check(!ArmouryMoveRules.Arrived(a, a with { State = "other materia" }), "destination equipment state mismatch rejected");
Check(!ArmouryMoveRules.Arrived(a, a with { Quantity = 2 }), "destination quantity mismatch rejected");
Console.WriteLine($"Passed {checks} total checks including native list matching and saved-gearset move guards.");

Check(DesynthesisScope.OwnResult(37400, 37400, 37400), "ordinary visible result accepted without legacy result-open flag");
Check(!DesynthesisScope.OwnResult(37400, 20713, 37400), "stale native result source rejected");
Check(!DesynthesisScope.OwnResult(37400, 37400, 20713), "stale agent source rejected");
Check(!DesynthesisScope.OwnResult(0, 0, 0), "empty result is not success");
Check(DesynthesisScope.OwnResult(42, 1000042, 42), "HQ result normalization preserved");
var resultPlan = new DesynthesisScope(1, new[] { a, b with { Quantity = 1 } });
Check(DesynthesisScope.OwnResult(resultPlan.Head.Id, a.Id, a.Id) && resultPlan.ConsumeOne(a.Id, null), "first native result acknowledges only captured first item");
Check(resultPlan.Head.Id == b.Id && resultPlan.Matches(1, 1, new[] { b with { Quantity = 1 } }), "after closing first result remaining tab matches next captured item");
Check(DesynthesisScope.OwnResult(resultPlan.Head.Id, b.Id, b.Id) && resultPlan.ConsumeOne(b.Id, null) && resultPlan.Empty, "second captured item completes without reusing old result");
Console.WriteLine($"Passed {checks} total checks including single-result acknowledgement regression.");

var idle = new DesynthesisReadiness();
Check(!idle.CanProceed(36704, true, false, 0), "visible success must still wait while Occupied39 is true");
Check(!idle.CanProceed(36726, false, false, 0), "clearing Occupied39 does not immediately submit another action");
Check(!idle.CanProceed(36925, false, false, 0), "requires full stable idle interval");
Check(idle.CanProceed(36926, false, false, 0), "continues when native processing has ended and idle is stable");
Check(!idle.CanProceed(36927, true, false, 0), "busy reentry invalidates prior idle state");
Check(!idle.CanProceed(36940, false, false, 0), "brief false busy flag cannot reuse old idle timestamp");
Check(idle.CanProceed(37140, false, false, 0), "normal ready state resumes after transient busy");
Check(!idle.CanProceed(37141, false, true, 0), "casting blocks selection and confirmation");
Check(!idle.CanProceed(37142, false, false, 0.1f), "animation lock also blocks next action");
Check(!idle.CanProceed(37143, false, false, float.NaN), "unavailable action state fails closed");
Check(!idle.CanProceed(37144, false, false, float.PositiveInfinity), "invalid animation lock rejected");
Check(!idle.CanProceed(37200, false, false, 0) && idle.CanProceed(37400, false, false, 0), "idle resumes after unavailable state");
idle.Reset();
Check(!idle.CanProceed(37401, false, false, 0), "new submitted item does not inherit readiness from prior item");
Check(!idle.CanProceed(1, false, false, 0), "clock reversal never grants readiness");
Check(idle.CanProceed(201, false, false, 0), "fresh monotonic idle after clock reversal works");
Console.WriteLine($"Passed {checks} total checks including desynthesis busy-state timing regression.");

var consent = new DesynthesisConfirmation();
Check(consent.Evaluate(false, false, true, false, false) == DesynthesisConfirmation.Decision.Reject, "foreign confirmation cannot authorize a warning");
Check(consent.Evaluate(true, true, true, false, false) == DesynthesisConfirmation.Decision.Reject, "bulk mode always rejected even with a warning");
Check(consent.Evaluate(true, false, true, false, false) == DesynthesisConfirmation.Decision.ConfirmWarning, "captured item warning can be acknowledged once");
Check(consent.Evaluate(true, false, true, false, true) == DesynthesisConfirmation.Decision.Wait, "enabled button cannot bypass missing warning acknowledgement");
Check(consent.Evaluate(true, false, true, false, false) == DesynthesisConfirmation.Decision.Wait, "missing native acknowledgement never repeats consent callback");
Check(consent.Evaluate(true, false, true, true, false) == DesynthesisConfirmation.Decision.Wait, "checked warning waits for native button readiness");
Check(consent.Evaluate(true, false, true, true, true) == DesynthesisConfirmation.Decision.Submit, "only checked warning and native enabled button allow submission");
Check(consent.Evaluate(true, true, true, true, true) == DesynthesisConfirmation.Decision.Reject, "bulk toggled after consent still rejects");
consent.Reset();
Check(consent.Evaluate(true, false, false, false, false) == DesynthesisConfirmation.Decision.Wait, "ordinary dialog can wait for native transition without a warning");
Check(consent.Evaluate(true, false, false, false, true) == DesynthesisConfirmation.Decision.Submit, "ordinary item leaves checkbox settings alone");
Check(consent.Evaluate(true, false, true, false, false) == DesynthesisConfirmation.Decision.ConfirmWarning, "new item has independent warning consent");
Console.WriteLine($"Passed {checks} total checks including scoped warning consent regression.");

Check(!ArmouryMoveRules.HasBagRoom(0), "full bags never allow armoury cleanup");
Check(!ArmouryMoveRules.HasBagRoom(4), "less than five free slots does not start cleanup");
Check(!ArmouryMoveRules.HasBagRoom(5), "exactly five free slots is the stopping boundary");
Check(ArmouryMoveRules.HasBagRoom(6) && !ArmouryMoveRules.HasBagRoom(6 - 1), "one move from six slots leaves five and forbids another move");
Check(ArmouryMoveRules.HasBagRoom(140), "empty normal bags allow cleanup");
Check(!ArmouryMoveRules.HasBagRoom(-1) && !ArmouryMoveRules.HasBagRoom(141), "invalid free slot counts fail closed");
Console.WriteLine($"Passed {checks} total checks including five reserved bag slots.");

var saleHandoff = new SaleHandoff();
Check(!saleHandoff.Ready(10, true), "sale completion does not allow same-frame next sale");
Check(!saleHandoff.Ready(10, true), "addon PostUpdate in same framework frame cannot release handoff");
Check(saleHandoff.Ready(11, true), "later framework frame confirms idle native shop");
saleHandoff.Reset();
Check(!saleHandoff.Ready(20, true) && !saleHandoff.Ready(21, false), "busy or unavailable handler breaks stable readiness");
Check(!saleHandoff.Ready(22, true) && saleHandoff.Ready(23, true), "clearing busy requires a fresh later framework observation");
var handoffPump = new BatchPump();
var handoffProgress = 0;
var handoffFrame = 30L;
var openedNextSale = 0;
var handoffPhase = 0;
saleHandoff.Reset();
void AdvanceSaleHandoff()
{
    if (handoffPhase == 0) { handoffPhase = 1; handoffProgress++; return; }
    if (handoffPhase == 1 && saleHandoff.Ready(handoffFrame, true))
    { handoffPhase = 2; handoffProgress++; openedNextSale++; }
}
handoffPump.Run(AdvanceSaleHandoff, () => handoffProgress);
Check(openedNextSale == 0, "actual BatchPump cannot open a new transaction in the acknowledgement call");
handoffPump.Run(AdvanceSaleHandoff, () => handoffProgress);
Check(openedNextSale == 0, "same-frame addon event cannot open a new transaction");
handoffFrame++;
handoffPump.Run(AdvanceSaleHandoff, () => handoffProgress);
Check(openedNextSale == 1, "next framework observation opens exactly one transaction");
handoffPump.Run(AdvanceSaleHandoff, () => handoffProgress);
Check(openedNextSale == 1, "repeated pump cannot duplicate released transaction");
Console.WriteLine($"Passed {checks} total checks including cross-frame sale handoff.");

var output = new DesynthItem(0, 2, 500, 2, 0, "condition:0", "木柴");
var anotherOutput = output with { Slot = 8, Id = 501, Quantity = 1, Name = "另一种产物" };
var outputPlan = new DesynthesisScope(1, new[] { a, b });
Check(outputPlan.ConsumeOne(a.Id, null), "source consumption is acknowledged before admitting desynthesis outputs");
Check(outputPlan.Match(1, 1, new[] { output, b }) == null, "unverified new output cannot authorize a changed list");
outputPlan.RecordReceivedOutput(output.Id, output.Quantity);
Check(outputPlan.Match(1, 1, new[] { output, b }) == new DesynthListMatch(1, 1), "verified output at the front selects the original item at its real native row");
Check(outputPlan.Match(1, 1, new[] { b, output }) == new DesynthListMatch(0, 1), "verified output after the original item leaves its native row unchanged");
Check(outputPlan.Head == b && outputPlan.Remaining.Count == 1, "a verified output never joins the captured work plan");
Check(outputPlan.Match(0, 1, new[] { output, b }) == null && outputPlan.Match(1, 0, new[] { output, b }) == null, "output accounting never permits either category to change");
Check(outputPlan.Match(1, 1, new[] { output with { Id = 502 }, b }) == null, "a different unverified output ID stops the plan");
Check(outputPlan.Match(1, 1, new[] { output with { Quantity = 3 }, b }) == null, "visible output quantity cannot exceed acknowledged receipts");
Check(outputPlan.Match(1, 1, new[] { output with { Quantity = 0 }, b }) == null, "empty output row is rejected");
Check(outputPlan.Match(1, 1, new[] { output, b with { Quantity = 4 } }) == null, "verified output cannot hide an expanded original stack");
Check(outputPlan.Match(1, 1, new[] { output, b with { Flags = 0 } }) == null, "verified output cannot hide an original quality swap");
Check(outputPlan.Match(1, 1, new[] { output, b with { State = "changed" } }) == null, "verified output cannot hide an original equipment state change");
Check(outputPlan.Match(1, 1, new[] { output, b with { Inventory = 2 } }) == null, "verified output cannot hide an original inventory move");
Check(outputPlan.Match(1, 1, new[] { output, b with { Slot = 9 } }) == null, "verified output cannot hide an original slot move");
Check(outputPlan.Match(1, 1, new[] { output with { Inventory = b.Inventory, Slot = b.Slot }, b }) == null, "an output cannot impersonate an original item at its captured slot");
Check(outputPlan.Match(1, 1, new[] { output, output, b }) == null, "duplicate visible output slots are rejected");
Check(outputPlan.Match(1, 1, new[] { output with { Quantity = 1 }, output with { Slot = 9, Quantity = 2 }, b }) == null, "split output stacks share the same received quantity budget");
Check(outputPlan.Match(1, 1, new[] { output with { Quantity = 1 }, output with { Slot = 9, Quantity = 1 }, b }) == null, "splitting a previously bound output stack cannot masquerade as unchanged receipts");
var splitOutputPlan = new DesynthesisScope(1, new[] { b });
splitOutputPlan.RecordReceivedOutput(output.Id, 2);
Check(splitOutputPlan.Match(1, 1, new[] { output with { Quantity = 1 }, output with { Slot = 9, Quantity = 1 }, b }) == new DesynthListMatch(2, 2), "outputs first observed across two slots are counted without selecting either output");
outputPlan.RecordReceivedOutput(anotherOutput.Id, 1);
Check(outputPlan.Match(1, 1, new[] { output, anotherOutput, b }) == new DesynthListMatch(2, 2), "multiple verified output types before the original item advance the real native index");

var nextOriginal = a with { Inventory = 2, Slot = 5, Id = 600, Name = "后续家具" };
var interleavedPlan = new DesynthesisScope(1, new[] { b, nextOriginal });
interleavedPlan.RecordReceivedOutput(output.Id, 2);
Check(interleavedPlan.Match(1, 1, new[] { b, output, nextOriginal }) == new DesynthListMatch(0, 1), "verified output between original rows preserves original processing order");
Check(interleavedPlan.Match(1, 1, new[] { nextOriginal, output, b }) == null, "interleaved verified output never permits original rows to reorder");
Check(interleavedPlan.Match(1, 1, new[] { output, b }) == null, "interleaved verified output never conceals a missing original row");

outputPlan.RecordReceivedOutput(output.Id, 1);
Check(outputPlan.Match(1, 1, new[] { output with { Quantity = 3 }, anotherOutput, b }) == new DesynthListMatch(2, 2), "separately acknowledged results accumulate the same output type while previous outputs remain intact");
Check(outputPlan.Match(1, 1, new[] { output with { Quantity = 4 }, anotherOutput, b }) == null, "accumulated output receipts still enforce their total bound");
Check(outputPlan.ConsumeOne(b.Id, b with { Quantity = 2 }) && outputPlan.ConsumeOne(b.Id, b with { Quantity = 1 }) && outputPlan.ConsumeOne(b.Id, null), "all original units must still be acknowledged individually");
Check(outputPlan.Empty && outputPlan.Match(1, 1, new[] { output with { Quantity = 3 }, anotherOutput }) == new DesynthListMatch(-1, 2), "completed original plan reports verified output rows and no selectable head");
Check(outputPlan.Match(1, 1, Array.Empty<DesynthItem>()) == null, "completed plan cannot ignore disappearance of its previously bound outputs");
var noOutputCompletionPlan = new DesynthesisScope(1, new[] { a });
Check(noOutputCompletionPlan.ConsumeOne(a.Id, null) && noOutputCompletionPlan.Match(1, 1, Array.Empty<DesynthItem>()) == new DesynthListMatch(-1, 0), "completed plan that received no visible outputs retains zero output rows");
Check(outputPlan.Match(1, 1, new[] { output, anotherOutput with { Id = 502 } }) == null, "completed plan does not accept an unrelated remaining row");
Check(!outputPlan.ConsumeOne(output.Id, null), "completed original plan cannot consume verified output material");
var freshOutputPlan = new DesynthesisScope(1, new[] { b });
Check(freshOutputPlan.Match(1, 1, new[] { output, b }) == null, "a fresh batch never inherits the previous batch output receipts");
Console.WriteLine($"Passed {checks} total checks including verified desynthesis output rows.");

var hqOutput = output with { Slot = 10, Quantity = 3, Flags = 1 };
var qualityOutputPlan = new DesynthesisScope(1, new[] { b });
qualityOutputPlan.RecordReceivedOutput(output.Id, 2);
Check(qualityOutputPlan.Match(1, 1, new[] { hqOutput with { Quantity = 2 }, b }) == null, "NQ receipt cannot authorize an HQ output row of the same base ID");
qualityOutputPlan.RecordReceivedOutput(1_000_000 + hqOutput.Id, 3);
Check(qualityOutputPlan.Match(1, 1, new[] { output, hqOutput, b }) == new DesynthListMatch(2, 2), "native HQ encoded result IDs produce a separate verified HQ row");
Check(qualityOutputPlan.Match(1, 1, new[] { output with { Quantity = 3 }, hqOutput with { Quantity = 2 }, b }) == null, "unused HQ output budget never covers extra NQ quantity");
Check(qualityOutputPlan.Match(1, 1, new[] { output with { Quantity = 1 }, hqOutput with { Quantity = 4 }, b }) == null, "unused NQ output budget never covers extra HQ quantity");
var onlyHqOutputPlan = new DesynthesisScope(1, new[] { b });
onlyHqOutputPlan.RecordReceivedOutput(1_000_000 + output.Id, 2);
Check(onlyHqOutputPlan.Match(1, 1, new[] { output, b }) == null, "HQ receipt cannot authorize an NQ output row of the same base ID");
Check(onlyHqOutputPlan.Match(1, 1, new[] { output with { Flags = 1 }, b }) == new DesynthListMatch(1, 1), "normal native HQ encoding preserves the base item with HQ quality");
var invalidReceiptPlan = new DesynthesisScope(1, new[] { b });
invalidReceiptPlan.RecordReceivedOutput(2_000_000 + output.Id, 2);
invalidReceiptPlan.RecordReceivedOutput(0, 2);
invalidReceiptPlan.RecordReceivedOutput(output.Id, 0);
invalidReceiptPlan.RecordReceivedOutput(output.Id, -1);
Check(invalidReceiptPlan.Match(1, 1, new[] { output, b }) == null, "invalid result item types and nonpositive receipt amounts authorize no output");

var heldOutput = output with { Slot = 20, Quantity = 10 };
var baselineOutputPlan = new DesynthesisScope(1, new[] { b }, new[] { b, heldOutput });
baselineOutputPlan.RecordReceivedOutput(output.Id, 2);
Check(baselineOutputPlan.Match(1, 1, new[] { heldOutput, b }) == null, "an initially owned stack with no growth is not a newly verified output row");
Check(baselineOutputPlan.Match(1, 1, new[] { heldOutput with { Quantity = 11 }, b }) == new DesynthListMatch(1, 1), "only the positive growth above an initial stack counts against output receipts");
Check(baselineOutputPlan.Match(1, 1, new[] { heldOutput with { Quantity = 12 }, b }) == new DesynthListMatch(1, 1), "a bound output stack may grow within the already acknowledged output budget");
Check(baselineOutputPlan.Match(1, 1, new[] { heldOutput with { Quantity = 13 }, b }) == null, "initial quantity is deducted without allowing output growth beyond the receipt budget");
Check(baselineOutputPlan.Match(1, 1, new[] { heldOutput with { Quantity = 12, State = "changed" }, b }) == null, "existing stack growth requires its initial equipment state to remain identical");
Check(baselineOutputPlan.Match(1, 1, new[] { heldOutput with { Quantity = 12, Flags = 1 }, b }) == null, "existing stack growth cannot switch its initially held quality");
baselineOutputPlan.RecordReceivedOutput(output.Id, 1);
Check(baselineOutputPlan.Match(1, 1, new[] { heldOutput with { Quantity = 13 }, b }) == new DesynthListMatch(1, 1), "later native receipts permit further growth of the same bound initial stack");
var occupiedOutputSlot = heldOutput with { Id = 502, Name = "原有其他物品" };
var occupiedOutputPlan = new DesynthesisScope(1, new[] { b }, new[] { b, occupiedOutputSlot });
occupiedOutputPlan.RecordReceivedOutput(output.Id, 2);
Check(occupiedOutputPlan.Match(1, 1, new[] { heldOutput with { Quantity = 2 }, b }) == null, "a previously occupied unrelated slot cannot be replaced and claimed as a desynthesis output");
var consumedOutputSlotPlan = new DesynthesisScope(1, new[] { a, b }, new[] { a, b });
Check(consumedOutputSlotPlan.ConsumeOne(a.Id, null), "acknowledged original consumption releases its original bag slot");
consumedOutputSlotPlan.RecordReceivedOutput(output.Id, 2);
Check(consumedOutputSlotPlan.Match(1, 1, new[] { output, b }) == new DesynthListMatch(1, 1), "verified output can legitimately reuse the consumed original item's slot");

var boundOutputPlan = new DesynthesisScope(1, new[] { b });
boundOutputPlan.RecordReceivedOutput(output.Id, 2);
Check(boundOutputPlan.Match(1, 1, new[] { output, b }) == new DesynthListMatch(1, 1), "first fully verified output row binds its inventory identity");
Check(boundOutputPlan.Match(1, 1, new[] { output with { Slot = 11 }, b }) == null, "a bound output moved to another physical slot is rejected without another receipt");
Check(boundOutputPlan.Match(1, 1, new[] { output with { Inventory = 1 }, b }) == null, "a bound output moved to another bag is rejected without another receipt");
Check(boundOutputPlan.Match(1, 1, new[] { output with { Flags = 1 }, b }) == null, "a bound output cannot change quality");
Check(boundOutputPlan.Match(1, 1, new[] { output with { State = "changed" }, b }) == null, "a bound output cannot change its complete equipment state");
Check(boundOutputPlan.Match(1, 1, new[] { output with { Quantity = 1 }, b }) == null, "a bound output cannot silently lose quantity through manual movement");
Check(boundOutputPlan.Match(1, 1, new[] { output with { Quantity = 3 }, b }) == null, "a bound output cannot grow before its next native receipt");
boundOutputPlan.RecordReceivedOutput(output.Id, 1);
Check(boundOutputPlan.Match(1, 1, new[] { output with { Quantity = 3 }, b }) == new DesynthListMatch(1, 1), "a later acknowledged result permits a bound output to gain exactly its newly received quantity");
boundOutputPlan.RecordReceivedOutput(anotherOutput.Id, 1);
Check(boundOutputPlan.Match(1, 1, new[] { output with { Quantity = 3 }, anotherOutput, b }) == new DesynthListMatch(2, 2), "later verified output types may bind new empty slots while old output identity is preserved");
boundOutputPlan.RecordReceivedOutput(output.Id, 2);
Check(boundOutputPlan.Match(1, 1, new[] { output with { Quantity = 3 }, output with { Slot = 11 }, anotherOutput, b }) == new DesynthListMatch(3, 3), "later receipts may add a new stack of the same output while the original bound stack stays intact");
var atomicOutputPlan = new DesynthesisScope(1, new[] { b, nextOriginal });
atomicOutputPlan.RecordReceivedOutput(output.Id, 2);
Check(atomicOutputPlan.Match(1, 1, new[] { output, b }) == null, "an output preceding an incomplete original list cannot bind through a failed match");
Check(atomicOutputPlan.Match(1, 1, new[] { output with { Slot = 11 }, b, nextOriginal }) == new DesynthListMatch(1, 1), "a failed whole-list match leaves no output binding that could poison a later valid observation");
var invalidOutputPositionPlan = new DesynthesisScope(1, new[] { b });
invalidOutputPositionPlan.RecordReceivedOutput(output.Id, 2);
Check(invalidOutputPositionPlan.Match(1, 1, new[] { output with { Inventory = 4 }, b }) == null, "a matching output in a non-bag inventory is never admitted");
Check(invalidOutputPositionPlan.Match(1, 1, new[] { output with { Slot = 35 }, b }) == null, "a matching output outside normal physical bag slot bounds is never admitted");
Check(invalidOutputPositionPlan.Match(1, 1, new[] { output with { Slot = -1 }, b }) == null, "a matching output with a negative slot is never admitted");
Console.WriteLine($"Passed {checks} total checks including output quality, bag baselines, and bound output identities.");

// Simulate the reported list shape; these are synthetic source IDs and slots, not a live-game test.
var originalFurniture = Enumerable.Range(0, 18)
    .Select(i => new DesynthItem(0, i, (uint)(40_000 + i), 1, 0, "condition:30000", $"原批家具{i + 1}"))
    .ToArray();
var furnitureOutputPlan = new DesynthesisScope(1, originalFurniture, originalFurniture);
var firewood = new DesynthItem(0, 9, 6494, 1, 0, "condition:0", "木柴");
for (var i = 0; i < originalFurniture.Length; i++)
{
    var furnitureList = i < 10 ? furnitureOutputPlan.Remaining.ToArray()
        : new[] { firewood }.Concat(furnitureOutputPlan.Remaining).ToArray();
    Check(furnitureOutputPlan.Head == originalFurniture[i]
        && furnitureOutputPlan.Match(1, 1, furnitureList) == new DesynthListMatch(i < 10 ? 0 : 1, i < 10 ? 0 : 1),
        $"18-item scenario keeps original item {i + 1} and skips the leading output after the tenth success");
    Check(furnitureOutputPlan.ConsumeOne(originalFurniture[i].Id, null), $"18-item scenario acknowledges original item {i + 1} exactly once");
    if (i == 9)
    {
        furnitureOutputPlan.RecordReceivedOutput(firewood.Id, firewood.Quantity);
        Check(!furnitureOutputPlan.ConsumeOne(firewood.Id, null)
            && furnitureOutputPlan.Head == originalFurniture[10], "the tenth result's firewood cannot replace the eleventh original item");
    }
}
Check(furnitureOutputPlan.Empty && furnitureOutputPlan.Match(1, 1, new[] { firewood }) == new DesynthListMatch(-1, 1), "18 original furniture items complete with exactly one verified firewood output row left");
Check(!furnitureOutputPlan.ConsumeOne(firewood.Id, null), "the 18-item scenario never consumes firewood after the original plan completes");
Console.WriteLine($"Passed {checks} total checks including the simulated 18-item furniture output regression.");

var statisticsNow = DateTimeOffset.Parse("2026-11-19T14:00:00Z");
var statisticsZone = TimeZoneInfo.CreateCustomTimeZone("Statistics UTC+8", TimeSpan.FromHours(8), "UTC+8", "UTC+8");
var emptyStatisticsCells = new SaleStatisticsCells("", "", "");
Check(SaleStatisticsDisplay.Format(null, statisticsNow, statisticsZone) == emptyStatisticsCells,
    "items with no sale statistics leave every new cell blank rather than inventing zero or a date");
Check(SaleStatisticsDisplay.Format(new(null, null), statisticsNow, statisticsZone) == emptyStatisticsCells,
    "a statistics record with no timestamps also leaves every cell blank");
var saleOnly = new SaleStatistics(statisticsNow.AddDays(-120), null);
Check(SaleStatisticsDisplay.IsValid(saleOnly, statisticsNow)
    && SaleStatisticsDisplay.Format(saleOnly, statisticsNow, statisticsZone) == new SaleStatisticsCells("2026-07-22", "", ""),
    "a known oldest sale without an observation timestamp displays only its date");
var observationOnly = new SaleStatistics(null, statisticsNow.AddDays(-2).AddHours(-23));
Check(SaleStatisticsDisplay.IsValid(observationOnly, statisticsNow)
    && SaleStatisticsDisplay.Format(observationOnly, statisticsNow, statisticsZone) == new SaleStatisticsCells("", "", "2"),
    "a known observation without sales displays only complete days since its update");
var sameMomentStatistics = new SaleStatistics(statisticsNow, statisticsNow);
Check(SaleStatisticsDisplay.Format(sameMomentStatistics, statisticsNow, statisticsZone) == new SaleStatisticsCells("2026-11-19", "0", "0"),
    "genuine timestamps at the current instant display two legitimate zero-day ages");
Check(SaleStatisticsDisplay.Format(new(statisticsNow.AddHours(-23).AddMinutes(-59), statisticsNow), statisticsNow, statisticsZone).DaysAtObservation == "0",
    "a sale less than 24 hours before observation counts as zero complete days");
Check(SaleStatisticsDisplay.Format(new(statisticsNow.AddDays(-1), statisticsNow), statisticsNow, statisticsZone).DaysAtObservation == "1",
    "a sale exactly 24 hours before observation counts as one complete day");
Check(SaleStatisticsDisplay.Format(new(null, statisticsNow.AddHours(-23).AddMinutes(-59)), statisticsNow, statisticsZone).UpdatedDaysAgo == "0",
    "an observation less than 24 hours old counts as zero complete update days");
Check(SaleStatisticsDisplay.Format(new(null, statisticsNow.AddDays(-1)), statisticsNow, statisticsZone).UpdatedDaysAgo == "1",
    "an observation exactly 24 hours old counts as one complete update day");
var recordedStatistics = new SaleStatistics(statisticsNow.AddDays(-120).AddHours(-10), statisticsNow.AddDays(-3));
var recordedCells = SaleStatisticsDisplay.Format(recordedStatistics, statisticsNow, statisticsZone);
var laterRecordedCells = SaleStatisticsDisplay.Format(recordedStatistics, statisticsNow.AddDays(5), statisticsZone);
Check(recordedCells.DaysAtObservation == "117" && recordedCells.UpdatedDaysAgo == "3",
    "the two age columns use sale-to-observation and observation-to-now as different intervals");
Check(laterRecordedCells.DaysAtObservation == recordedCells.DaysAtObservation && laterRecordedCells.UpdatedDaysAgo == "8",
    "aging a saved observation never changes the historical age at observation");
var yearBoundaryStatistics = new SaleStatistics(DateTimeOffset.Parse("2025-12-31T23:15:00Z"), DateTimeOffset.Parse("2026-01-02T00:00:00Z"));
var yearBoundaryNow = DateTimeOffset.Parse("2026-01-03T00:00:00Z");
Check(SaleStatisticsDisplay.Format(yearBoundaryStatistics, yearBoundaryNow, statisticsZone) == new SaleStatisticsCells("2026-01-01", "1", "1"),
    "cross-year dates use the chosen display zone while elapsed days retain their full timestamps");
Check(SaleStatisticsDisplay.Format(yearBoundaryStatistics, yearBoundaryNow, TimeZoneInfo.Utc).OldestSaleDate == "2025-12-31",
    "the sale date follows the explicit display zone rather than a hardcoded local date");
var sameInstantDifferentOffsets = new SaleStatistics(DateTimeOffset.Parse("2026-11-18T15:00:00+08:00"), DateTimeOffset.Parse("2026-11-18T07:00:00Z"));
Check(SaleStatisticsDisplay.IsValid(sameInstantDifferentOffsets, statisticsNow)
    && SaleStatisticsDisplay.Format(sameInstantDifferentOffsets, statisticsNow, statisticsZone).DaysAtObservation == "0",
    "different UTC offsets representing the same instant neither reverse time nor add a day");
foreach (var invalidStatistics in new[]
{
    new SaleStatistics(statisticsNow.AddSeconds(1), null),
    new SaleStatistics(null, statisticsNow.AddSeconds(1)),
    new SaleStatistics(statisticsNow.AddDays(-1), statisticsNow.AddDays(-3)),
    new SaleStatistics(statisticsNow.AddSeconds(1), statisticsNow.AddSeconds(2)),
})
{
    Check(!SaleStatisticsDisplay.IsValid(invalidStatistics, statisticsNow), "future or reversed statistics timestamps are rejected");
    Check(SaleStatisticsDisplay.Format(invalidStatistics, statisticsNow, statisticsZone) == emptyStatisticsCells,
        "invalid timestamps never render negative ages or partially plausible data");
}
var statisticsJson = System.Text.Json.JsonSerializer.Serialize(recordedStatistics);
var restoredStatistics = System.Text.Json.JsonSerializer.Deserialize<SaleStatistics>(statisticsJson);
Check(restoredStatistics == recordedStatistics
    && SaleStatisticsDisplay.Format(restoredStatistics, statisticsNow, statisticsZone) == recordedCells,
    "stored statistics round-trip without replacing either timestamp with the current time");
var missingStatisticsJson = System.Text.Json.JsonSerializer.Deserialize<SaleStatistics>("{}");
Check(missingStatisticsJson is { OldestSaleAt: null, ObservedAt: null }
    && SaleStatisticsDisplay.Format(missingStatisticsJson, statisticsNow, statisticsZone) == emptyStatisticsCells,
    "legacy or missing timestamp fields deserialize as unknown values and remain visually empty");
var partialStatisticsJson = System.Text.Json.JsonSerializer.Serialize(saleOnly);
var restoredPartialStatistics = System.Text.Json.JsonSerializer.Deserialize<SaleStatistics>(partialStatisticsJson);
Check(restoredPartialStatistics?.ObservedAt == null
    && SaleStatisticsDisplay.Format(restoredPartialStatistics, statisticsNow, statisticsZone).DaysAtObservation == ""
    && SaleStatisticsDisplay.Format(restoredPartialStatistics, statisticsNow, statisticsZone).UpdatedDaysAgo == "",
    "nullable statistics persist as unknown ages rather than default dates or zero-day updates");
var previousStatisticsCulture = System.Globalization.CultureInfo.CurrentCulture;
try
{
    System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("ar-SA");
    Check(SaleStatisticsDisplay.Format(yearBoundaryStatistics, yearBoundaryNow, statisticsZone) == new SaleStatisticsCells("2026-01-01", "1", "1"),
        "date and numeric cells retain a stable Gregorian format across current cultures");
}
finally { System.Globalization.CultureInfo.CurrentCulture = previousStatisticsCulture; }
foreach (var uninitializedStatistics in new[]
{
    new SaleStatistics(DateTimeOffset.MinValue, null),
    new SaleStatistics(null, DateTimeOffset.MinValue),
    new SaleStatistics(DateTimeOffset.UnixEpoch.AddSeconds(-1), null),
    new SaleStatistics(null, DateTimeOffset.UnixEpoch.AddSeconds(-1)),
})
{
    Check(!SaleStatisticsDisplay.IsValid(uninitializedStatistics, statisticsNow),
        "default and pre-Unix timestamps are not accepted as recorded market statistics");
    Check(SaleStatisticsDisplay.Format(uninitializedStatistics, statisticsNow, statisticsZone) == emptyStatisticsCells,
        "uninitialized dates never leak year-one or pre-Unix placeholder values into the list");
}
Check(SaleStatisticsDisplay.IsValid(new(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch), statisticsNow),
    "an explicit timestamp exactly at the documented Unix boundary remains valid");
Check(SaleStatisticsRules.CanReplace(null, recordedStatistics), "a first statistics snapshot can populate a previously unknown item");
Check(SaleStatisticsRules.CanReplace(recordedStatistics, recordedStatistics with { }),
    "repeating an identical observation is idempotently accepted");
Check(SaleStatisticsRules.CanReplace(saleOnly, recordedStatistics),
    "an old record without an observation timestamp can receive a complete observation");
Check(SaleStatisticsRules.CanReplace(recordedStatistics, recordedStatistics with { ObservedAt = statisticsNow.AddDays(-2) }),
    "a newer verified observation may replace an older recorded snapshot");
Check(!SaleStatisticsRules.CanReplace(recordedStatistics, recordedStatistics with { ObservedAt = statisticsNow.AddDays(-4) }),
    "a delayed older observation cannot overwrite the current saved statistics");
Check(!SaleStatisticsRules.CanReplace(recordedStatistics, recordedStatistics with { OldestSaleAt = statisticsNow.AddDays(-100) }),
    "conflicting sale data at the same observation timestamp cannot overwrite a snapshot");
Check(!SaleStatisticsRules.CanReplace(recordedStatistics, recordedStatistics with { ObservedAt = null }),
    "an undated update cannot erase the timestamp of an already observed snapshot");
Check(SaleStatisticsRules.CanReplace(recordedStatistics, recordedStatistics with
{
    OldestSaleAt = recordedStatistics.OldestSaleAt!.Value.ToOffset(TimeSpan.FromHours(8)),
    ObservedAt = recordedStatistics.ObservedAt!.Value.ToOffset(TimeSpan.FromHours(8)),
}), "equivalent timestamps expressed with another UTC offset are the same idempotent observation");
Check(SaleStatisticsDisplay.Format(new(statisticsNow.AddDays(-1).AddTicks(1), statisticsNow), statisticsNow, statisticsZone).DaysAtObservation == "0",
    "an observation interval one tick short of 24 hours remains zero complete days without floating-point rounding");
Check(SaleStatisticsDisplay.Format(new(null, statisticsNow.AddDays(-1).AddTicks(1)), statisticsNow, statisticsZone).UpdatedDaysAgo == "0",
    "update age one tick short of 24 hours also remains zero complete days");
Check(SaleStatisticsInput.TryParse("{\"itemId\":5187,\"oldestSaleAt\":\"2026-08-01T12:30:00Z\",\"observedAt\":\"2026-11-18T12:30:00Z\"}", out var camelStatisticsInput)
    && camelStatisticsInput is { ItemId: 5187 }
    && camelStatisticsInput.OldestSaleAt == DateTimeOffset.Parse("2026-08-01T12:30:00Z")
    && camelStatisticsInput.ObservedAt == DateTimeOffset.Parse("2026-11-18T12:30:00Z"),
    "camelCase IPC statistics preserve both explicit UTC timestamps and the item identity");
Check(SaleStatisticsInput.TryParse("{\"ItemId\":5187,\"OldestSaleAt\":\"2026-08-01T20:30:00+08:00\",\"ObservedAt\":\"2026-11-18T20:30:00+08:00\"}", out var pascalStatisticsInput)
    && pascalStatisticsInput is { ItemId: 5187 }
    && pascalStatisticsInput.OldestSaleAt == camelStatisticsInput!.OldestSaleAt
    && pascalStatisticsInput.ObservedAt == camelStatisticsInput.ObservedAt,
    "PascalCase IPC properties and explicit UTC offsets resolve to the same recorded instants");
Check(SaleStatisticsInput.TryParse("{\"itemId\":1005187,\"oldestSaleAt\":null,\"observedAt\":null}", out var hqStatisticsInput)
    && hqStatisticsInput is { ItemId: 1005187, OldestSaleAt: null, ObservedAt: null },
    "the statistics parser accepts the raw HQ item encoding for subsequent base-ID normalization");
Check(SaleStatisticsInput.TryParse("{\"itemId\":5187}", out var missingStatisticsInput)
    && missingStatisticsInput is { ItemId: 5187, OldestSaleAt: null, ObservedAt: null },
    "omitted IPC statistics timestamps remain null instead of borrowing the receiver's current time");
foreach (var invalidStatisticsInput in new string?[]
{
    null, "", "   ", "null", "[]", "42", "true", "{", "{}", "{\"itemId\":0}", "{\"itemId\":-1}",
    "{\"itemId\":\"5187\"}",
    "{\"itemId\":5187,\"oldestSaleAt\":\"2026-08-01T12:30:00\"}",
    "{\"itemId\":5187,\"observedAt\":\"2026-11-18T12:30:00\"}",
    "{\"itemId\":5187,\"oldestSaleAt\":\"2026-08-01Z\"}",
    "{\"itemId\":5187,\"observedAt\":\"2026-11-18T12:30:00+0800\"}",
    "{\"itemId\":5187,\"observedAt\":12}",
    "{\"itemId\":5187,\"oldestSaleAt\":true}",
    "{\"itemId\":5187,\"oldestSaleAt\":\"2026-02-30T12:30:00Z\"}",
    "{\"itemId\":5187,\"observedAt\":\"2026-11-18T12:30:00+25:00\"}",
    "{\"itemId\":5187,\"itemId\":5188}",
    "{\"itemId\":5187,\"ItemID\":5187}",
    "{\"itemId\":5187,\"observedAt\":null,\"ObservedAt\":\"2026-11-18T12:30:00Z\"}",
    "{\"itemId\":5187,\"note\":1,\"NOTE\":2}",
    "{\"itemId\":5187,\"note\":\"" + new string('x', 2048) + "\"}",
})
{
    Check(!SaleStatisticsInput.TryParse(invalidStatisticsInput, out _),
        "ambiguous, malformed, oversized, duplicate-property or timezone-free IPC statistics are rejected");
}
Check(SaleStatisticsInput.TryParse("{\"itemId\":5187,\"observedAt\":\"2030-01-01T00:00:00Z\"}", out var futureStatisticsInput)
    && !SaleStatisticsDisplay.IsValid(new(futureStatisticsInput!.OldestSaleAt, futureStatisticsInput.ObservedAt), statisticsNow),
    "a syntactically valid future date parses but is separately rejected by business timestamp validation");
Check(SaleStatisticsInput.TryParse("{\"itemId\":5187,\"oldestSaleAt\":\"2026-11-18T12:30:00Z\",\"observedAt\":\"2026-08-01T12:30:00Z\"}", out var reversedStatisticsInput)
    && !SaleStatisticsDisplay.IsValid(new(reversedStatisticsInput!.OldestSaleAt, reversedStatisticsInput.ObservedAt), statisticsNow),
    "chronologically reversed dates remain a separate business validation failure after unambiguous parsing");
Console.WriteLine($"Passed {checks} total checks including sale statistics dates, missing values, complete-day ages and IPC input validation.");

List<SortStatisticsRow> SortStatistics(IEnumerable<SortStatisticsRow> rows, RecycleSortColumn column, bool descending = false)
    => RecycleSort.Sort(rows, row => row.Name, row => row.Id, row => row.Statistics, column, descending, statisticsNow);
uint[] SortedStatisticsIds(IEnumerable<SortStatisticsRow> rows, RecycleSortColumn column, bool descending = false)
    => SortStatistics(rows, column, descending).Select(row => row.Id).ToArray();
var nameStatisticsRows = new[]
{
    new SortStatisticsRow(20, "香蕉", null),
    new SortStatisticsRow(11, "苹果", null),
    new SortStatisticsRow(30, "阿尔法", null),
    new SortStatisticsRow(10, "苹果", null),
    new SortStatisticsRow(40, "菠萝", null),
};
Check(SortedStatisticsIds(nameStatisticsRows, RecycleSortColumn.Name).SequenceEqual(new uint[] { 30, 40, 10, 11, 20 }),
    "name sorting uses Chinese pinyin rather than Unicode codepoint order and orders equal names by item ID");
Check(SortedStatisticsIds(nameStatisticsRows, RecycleSortColumn.Name, true).SequenceEqual(new uint[] { 20, 10, 11, 40, 30 }),
    "descending names reverse name groups while preserving stable ascending item IDs within an equal name");
Check(nameStatisticsRows.Select(row => row.Id).SequenceEqual(new uint[] { 20, 11, 30, 10, 40 }),
    "sorting leaves the original collection in its original order");
var numericStatisticsRows = new[]
{
    new SortStatisticsRow(10, "菠萝", new(statisticsNow.AddDays(-20), statisticsNow.AddDays(-10))),
    new SortStatisticsRow(100, "苹果", new(statisticsNow.AddDays(-101), statisticsNow.AddDays(-1))),
    new SortStatisticsRow(2, "香蕉", new(statisticsNow.AddDays(-3), statisticsNow.AddDays(-1))),
};
Check(SortedStatisticsIds(numericStatisticsRows, RecycleSortColumn.DaysAtObservation).SequenceEqual(new uint[] { 2, 10, 100 }),
    "age at observation is sorted numerically as 2, 10, 100 rather than lexicographically");
Check(SortedStatisticsIds(numericStatisticsRows, RecycleSortColumn.DaysAtObservation, true).SequenceEqual(new uint[] { 100, 10, 2 }),
    "descending age at observation reverses numeric values");
Check(SortedStatisticsIds(numericStatisticsRows, RecycleSortColumn.UpdatedDaysAgo).SequenceEqual(new uint[] { 100, 2, 10 }),
    "update-age sorting uses observation-to-now instead of the historical sale-to-observation interval");
Check(SortedStatisticsIds(numericStatisticsRows, RecycleSortColumn.UpdatedDaysAgo, true).SequenceEqual(new uint[] { 10, 100, 2 }),
    "equal update ages keep ascending pinyin ties even in descending numeric order");
Check(SortedStatisticsIds(numericStatisticsRows, RecycleSortColumn.OldestSale).SequenceEqual(new uint[] { 100, 10, 2 }),
    "oldest-sale ascending order places the chronologically earliest actual sale first");
Check(SortedStatisticsIds(numericStatisticsRows, RecycleSortColumn.OldestSale, true).SequenceEqual(new uint[] { 2, 10, 100 }),
    "oldest-sale descending order places the chronologically latest actual sale first");
var unknownStatisticsRows = new[]
{
    new SortStatisticsRow(303, "香蕉", new(statisticsNow.AddDays(-30), statisticsNow.AddSeconds(1))),
    new SortStatisticsRow(302, "菠萝", new(null, null)),
    new SortStatisticsRow(301, "阿尔法", null),
};
var mixedStatisticsRows = unknownStatisticsRows.Concat(numericStatisticsRows).ToArray();
foreach (var statisticsColumn in new[] { RecycleSortColumn.OldestSale, RecycleSortColumn.DaysAtObservation, RecycleSortColumn.UpdatedDaysAgo })
{
    foreach (var descending in new[] { false, true })
    {
        var mixedIds = SortedStatisticsIds(mixedStatisticsRows, statisticsColumn, descending);
        Check(mixedIds.Take(3).All(id => id < 300) && mixedIds.Skip(3).SequenceEqual(new uint[] { 301, 302, 303 }),
            "missing and invalid statistics stay after all known values in either direction, with stable pinyin ties");
    }
}
var partialSortRows = numericStatisticsRows.Concat(new[]
{
    new SortStatisticsRow(401, "樱桃", new(statisticsNow.AddDays(-4), null)),
    new SortStatisticsRow(402, "桃子", new(null, statisticsNow.AddDays(-2))),
}).ToArray();
Check(SortedStatisticsIds(partialSortRows, RecycleSortColumn.OldestSale).SequenceEqual(new uint[] { 100, 10, 401, 2, 402 }),
    "a sale-only timestamp can sort by oldest sale while an observation-only record is unknown in that column");
Check(SortedStatisticsIds(partialSortRows, RecycleSortColumn.DaysAtObservation).SequenceEqual(new uint[] { 2, 10, 100, 402, 401 }),
    "either missing endpoint makes sale-to-observation age unknown, after complete rows");
Check(SortedStatisticsIds(partialSortRows, RecycleSortColumn.UpdatedDaysAgo).SequenceEqual(new uint[] { 100, 2, 402, 10, 401 }),
    "an observation-only timestamp can sort by update age while a sale-only timestamp stays unknown");
Check(SortedStatisticsIds(partialSortRows, RecycleSortColumn.OldestSale, true).Last() == 402
    && SortedStatisticsIds(partialSortRows, RecycleSortColumn.UpdatedDaysAgo, true).Last() == 401,
    "partial records remain unknown-last in descending dates and update ages too");
var flooredObservationRows = new[]
{
    new SortStatisticsRow(602, "香蕉", new(statisticsNow.AddDays(-5).AddHours(-1), statisticsNow.AddDays(-3))),
    new SortStatisticsRow(601, "苹果", new(statisticsNow.AddDays(-5).AddHours(-23), statisticsNow.AddDays(-3))),
    new SortStatisticsRow(600, "苹果", new(statisticsNow.AddDays(-5).AddHours(-23), statisticsNow.AddDays(-3))),
};
Check(SortedStatisticsIds(flooredObservationRows, RecycleSortColumn.DaysAtObservation).SequenceEqual(new uint[] { 600, 601, 602 }),
    "ages with the same displayed complete-day value use pinyin and ID ties instead of hidden fractional hours");
Check(SortedStatisticsIds(flooredObservationRows, RecycleSortColumn.DaysAtObservation, true).SequenceEqual(new uint[] { 600, 601, 602 }),
    "descending complete-day ages retain the same deterministic ties");
var flooredUpdateRows = new[]
{
    new SortStatisticsRow(611, "香蕉", new(null, statisticsNow.AddDays(-2).AddHours(-1))),
    new SortStatisticsRow(610, "苹果", new(null, statisticsNow.AddDays(-2).AddHours(-23))),
};
Check(SortedStatisticsIds(flooredUpdateRows, RecycleSortColumn.UpdatedDaysAgo).SequenceEqual(new uint[] { 610, 611 })
    && SortedStatisticsIds(flooredUpdateRows, RecycleSortColumn.UpdatedDaysAgo, true).SequenceEqual(new uint[] { 610, 611 }),
    "update-age ordering also uses whole days matching the displayed cells in both directions");
var lastTickObservationRows = new[]
{
    new SortStatisticsRow(631, "苹果", new(statisticsNow.AddDays(-1).AddTicks(1), statisticsNow)),
    new SortStatisticsRow(632, "香蕉", new(statisticsNow, statisticsNow)),
};
Check(SortedStatisticsIds(lastTickObservationRows, RecycleSortColumn.DaysAtObservation).SequenceEqual(new uint[] { 631, 632 }),
    "sorting treats an interval one tick short of a day as the same visible zero-day value as an immediate sale");
var lastTickUpdateRows = new[]
{
    new SortStatisticsRow(641, "苹果", new(null, statisticsNow.AddDays(-1).AddTicks(1))),
    new SortStatisticsRow(642, "香蕉", new(null, statisticsNow)),
};
Check(SortedStatisticsIds(lastTickUpdateRows, RecycleSortColumn.UpdatedDaysAgo).SequenceEqual(new uint[] { 641, 642 }),
    "sorting uses the same exact tick boundary as the displayed update-age cell");
var sameDisplayDateRows = new[]
{
    new SortStatisticsRow(501, "阿尔法", new(DateTimeOffset.Parse("2026-11-18T15:00:00Z"), null)),
    new SortStatisticsRow(502, "香蕉", new(DateTimeOffset.Parse("2026-11-18T01:00:00Z"), null)),
};
Check(SaleStatisticsDisplay.Format(sameDisplayDateRows[0].Statistics, statisticsNow, statisticsZone).OldestSaleDate
        == SaleStatisticsDisplay.Format(sameDisplayDateRows[1].Statistics, statisticsNow, statisticsZone).OldestSaleDate
    && SortedStatisticsIds(sameDisplayDateRows, RecycleSortColumn.OldestSale).SequenceEqual(new uint[] { 502, 501 }),
    "oldest-sale sorting distinguishes actual instants even when both rendered calendar dates are identical");
Check(SortedStatisticsIds(sameDisplayDateRows, RecycleSortColumn.OldestSale, true).SequenceEqual(new uint[] { 501, 502 }),
    "oldest-sale descending order also retains the actual intraday chronology");
var offsetSortRows = new[]
{
    new SortStatisticsRow(521, "香蕉", new(DateTimeOffset.Parse("2026-11-18T07:30:00+08:00"), null)),
    new SortStatisticsRow(522, "苹果", new(DateTimeOffset.Parse("2026-11-18T00:00:00Z"), null)),
};
Check(SortedStatisticsIds(offsetSortRows, RecycleSortColumn.OldestSale).SequenceEqual(new uint[] { 521, 522 }),
    "date ordering compares UTC instants rather than textual local timestamps with different offsets");
Check(SortStatistics(Array.Empty<SortStatisticsRow>(), RecycleSortColumn.UpdatedDaysAgo).Count == 0,
    "an empty recycling list sorts without inventing rows");
Check(SortedStatisticsIds(new[] { numericStatisticsRows[0] }, RecycleSortColumn.DaysAtObservation, true).SequenceEqual(new uint[] { 10 }),
    "a singleton recycling list remains unchanged when a sort column is toggled");
Console.WriteLine($"Passed {checks} total checks including pinyin, date, numeric, missing-value and stable recycling-list sorting.");

var historyObservedAt = DateTimeOffset.Parse("2026-11-21T21:45:00+08:00");
Check(MarketHistory.TrySummarize(5187, 5187, Array.Empty<MarketHistoryLine>(), historyObservedAt, out var emptyMarketHistory)
    && emptyMarketHistory is { OldestSaleAt: null } && emptyMarketHistory.ObservedAt == historyObservedAt,
    "a genuine empty response for the requested item retains the supplied observation time and an unknown oldest sale");
Check(SaleStatisticsDisplay.Format(emptyMarketHistory, historyObservedAt, statisticsZone) == new SaleStatisticsCells("", "", "0"),
    "an observed empty market history displays no invented sale date or sale age");
var unorderedMarketHistory = new[]
{
    new MarketHistoryLine(historyObservedAt.UtcDateTime.AddDays(-3), 999, 10),
    new MarketHistoryLine(historyObservedAt.UtcDateTime.AddDays(-120), 2, 40),
    new MarketHistoryLine(historyObservedAt.UtcDateTime.AddDays(-8), 99, 20),
};
var unorderedMarketHistoryBefore = unorderedMarketHistory.ToArray();
Check(MarketHistory.TrySummarize(5187, 5187, unorderedMarketHistory, historyObservedAt, out var shortMarketHistory)
    && shortMarketHistory?.OldestSaleAt == historyObservedAt.AddDays(-120)
    && shortMarketHistory.ObservedAt == historyObservedAt,
    "a short unsorted history uses the oldest actual transaction across all entries rather than row order or sold quantity");
Check(unorderedMarketHistory.SequenceEqual(unorderedMarketHistoryBefore),
    "summarizing market history never changes the caller's entry order");
var fullMarketHistory = Enumerable.Range(0, 20)
    .Select(i => new MarketHistoryLine(historyObservedAt.UtcDateTime.AddHours(-i), (uint)(i + 1), (uint)(i + 10)))
    .Reverse().ToArray();
Check(MarketHistory.TrySummarize(5187, 5187, fullMarketHistory, historyObservedAt, out var twentyMarketHistory)
    && twentyMarketHistory?.OldestSaleAt == historyObservedAt.AddHours(-19)
    && twentyMarketHistory.ObservedAt == historyObservedAt,
    "all twenty returned transaction rows are accepted even when total sold units exceed twenty");
Check(!MarketHistory.TrySummarize(5187, 5187, fullMarketHistory.Append(fullMarketHistory[0]).ToArray(), historyObservedAt, out var oversizedMarketHistory)
    && oversizedMarketHistory == null,
    "more than twenty transaction rows are rejected without returning a partial oldest date");
foreach (var historyIds in new[] { (Expected: 5187u, Response: 5188u), (Expected: 0u, Response: 0u), (Expected: 5187u, Response: 0u) })
{
    Check(!MarketHistory.TrySummarize(historyIds.Expected, historyIds.Response, unorderedMarketHistory, historyObservedAt, out var wrongItemMarketHistory)
        && wrongItemMarketHistory == null,
        "a history response must belong to the correct nonzero requested item before any statistics are accepted");
}
Check(!MarketHistory.TrySummarize(5187, 5188, Array.Empty<MarketHistoryLine>(), historyObservedAt, out _),
    "an empty history for another item cannot erase the requested item's recorded oldest sale");
var goodHistoryLine = unorderedMarketHistory[0];
foreach (var invalidHistoryLine in new[]
{
    goodHistoryLine with { PurchaseTime = DateTime.SpecifyKind(goodHistoryLine.PurchaseTime, DateTimeKind.Unspecified) },
    goodHistoryLine with { PurchaseTime = DateTime.SpecifyKind(goodHistoryLine.PurchaseTime, DateTimeKind.Local) },
    goodHistoryLine with { PurchaseTime = historyObservedAt.UtcDateTime.AddTicks(1) },
    goodHistoryLine with { PurchaseTime = DateTime.UnixEpoch.AddTicks(-1) },
    goodHistoryLine with { Quantity = 0 },
    goodHistoryLine with { Price = 0 },
})
{
    Check(!MarketHistory.TrySummarize(5187, 5187, new[] { goodHistoryLine, invalidHistoryLine, unorderedMarketHistory[1] }, historyObservedAt, out var invalidMarketHistory)
        && invalidMarketHistory == null,
        "one non-UTC, future, pre-Unix, zero-quantity or zero-price row rejects the complete response rather than silently filtering it");
}
Check(MarketHistory.TrySummarize(5187, 5187, new[] { new MarketHistoryLine(DateTime.UnixEpoch, 1, 1) }, historyObservedAt, out var epochMarketHistory)
    && epochMarketHistory?.OldestSaleAt == DateTimeOffset.UnixEpoch,
    "a UTC purchase exactly at the Unix boundary remains valid");
Check(MarketHistory.TrySummarize(5187, 5187, new[] { goodHistoryLine with { PurchaseTime = historyObservedAt.UtcDateTime } }, historyObservedAt, out var currentMarketHistory)
    && currentMarketHistory?.OldestSaleAt == historyObservedAt && currentMarketHistory.ObservedAt == historyObservedAt,
    "a purchase exactly at the supplied observation time is accepted without replacing that timestamp with the current clock");
Console.WriteLine($"Passed {checks} total checks including bounded native market history and oldest-sale observation.");

sealed class FakeOverlay
{
    public bool IsOpen { get; set; }
    public bool Operation { get; set; } = true;
    public bool OtherWindowOpen { get; set; } = true;
}

sealed record SortStatisticsRow(uint Id, string Name, SaleStatistics? Statistics);
