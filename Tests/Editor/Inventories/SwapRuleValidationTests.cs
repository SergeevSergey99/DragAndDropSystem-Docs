using NUnit.Framework;
using UDND.Core;
using UDND.Inventories;
using UDND.Rules;
using UDND.Slots;

namespace UDND.Tests.Inventories
{
    /// <summary>
    /// A swap moves two items in opposite directions, so both must satisfy the rules of the
    /// inventory they land in. Only the forward direction used to be validated, which turned a
    /// swap into a way to place an item where a plain drop would have been refused.
    /// </summary>
    [TestFixture]
    public class SwapRuleValidationTests
    {
        private UniversalInventory _source;
        private UniversalInventory _target;
        private readonly InventoryTransferService _service = new InventoryTransferService();

        [TearDown]
        public void TearDown()
        {
            InventoryBuilder.Destroy(_source);
            InventoryBuilder.Destroy(_target);
            _source = null;
            _target = null;
        }

        private void BuildInventories()
        {
            _source = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(1)
                .WithName("Source")
                .Build();
            _target = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(1)
                .WithName("Target")
                .Build();

            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "sword"));
            _target.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "cursed"));
        }

        private static ResolvedDropPolicy SwapPolicy()
            => new ResolvedDropPolicy(
                BlockedTargetResolutionKind.Swap,
                null,
                allowSameInventoryAlternativePlacement: true,
                partialTransferMode: PartialTransferMode.Allow);

        private DragContext BuildSwapContext()
            => DragContextBuilder
                .FromAllSlots(_source)
                .ToTargetSlot(_target.GetSlot(0), _target)
                .Build();

        private EntryTransferResult ExecuteSwap(DragContext context)
            => _service.TryTransferEntry(new TransferEntryRequest(
                context,
                context.Entries[0],
                _target,
                _target.GetSlot(0),
                SwapPolicy()));

        /// <summary>Rejects any item whose ItemId matches, whichever direction it travels.</summary>
        private static CustomRule RejectDropOf(string itemId, string reason)
            => new CustomRule(canDrop: (context, entry) =>
                entry.Stack?.PrimaryAdapter?.ItemId == itemId
                    ? RuleResult.Failure(reason)
                    : RuleResult.Success());

        /// <summary>
        /// A swap moves whole stacks through the placement primitives, skipping the candidate loop
        /// that caps capacity everywhere else. It still has to respect what the receiving strategy
        /// allows in one placement: ten gems must not land in a unique slot that holds one item.
        /// </summary>
        [Test]
        public void Swap_CounterpartStackExceedsUniqueCapacity_IsRefused()
        {
            _source = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(10)
                .WithName("Unique")
                .Build();
            _target = new InventoryBuilder()
                .WithStrategy(new StackableItemStrategy())
                .WithMaxStackSize(10)
                .WithFixedSlots(1)
                .WithName("Stackable")
                .Build();

            _source.GetSlot(0).SetStack(ItemStackBuilder.Of(new FakeItemAdapter("sword")));
            _target.GetSlot(0).SetStack(ItemStackBuilder.Of(FakeItemAdapter.Many(10, "gem")));

            // Rules on other slots must not enter into it: the counterpart travels to slot 0 alone.
            for (int i = 5; i < 10; i++)
            {
                _source.GetSlot(i).SlotRuleValidator.AddRule(
                    new ItemIdFilterRule(new[] { "gem" }, whitelist: false));
            }

            var report = ExecuteSwapDrop();

            Assert.IsFalse(report.Success);
            StringAssert.Contains("does not accept", report.EntryResults[0].FailureReason);
            Assert.AreEqual("sword", _source.GetSlot(0).Stack.ID);
            Assert.AreEqual(10, _target.GetSlot(0).Stack.Count);
        }

        /// <summary>The same limit applies to a stackable receiver whose maximum is simply smaller.</summary>
        [Test]
        public void Swap_CounterpartStackExceedsStackableLimit_IsRefused()
        {
            _source = new InventoryBuilder()
                .WithStrategy(new StackableItemStrategy())
                .WithMaxStackSize(4)
                .WithFixedSlots(1)
                .WithName("SmallStacks")
                .Build();
            _target = new InventoryBuilder()
                .WithStrategy(new StackableItemStrategy())
                .WithMaxStackSize(10)
                .WithFixedSlots(1)
                .WithName("BigStacks")
                .Build();

            _source.GetSlot(0).SetStack(ItemStackBuilder.Of(new FakeItemAdapter("sword")));
            _target.GetSlot(0).SetStack(ItemStackBuilder.Of(FakeItemAdapter.Many(10, "gem")));

            var report = ExecuteSwapDrop();

            Assert.IsFalse(report.Success);
            StringAssert.Contains("does not accept", report.EntryResults[0].FailureReason);
            Assert.AreEqual("sword", _source.GetSlot(0).Stack.ID);
            Assert.AreEqual(10, _target.GetSlot(0).Stack.Count);
        }

        /// <summary>A counterpart that fits the receiving limit still swaps.</summary>
        [Test]
        public void Swap_CounterpartStackWithinLimit_Succeeds()
        {
            _source = new InventoryBuilder()
                .WithStrategy(new StackableItemStrategy())
                .WithMaxStackSize(10)
                .WithFixedSlots(1)
                .WithName("BigStacks")
                .Build();
            _target = new InventoryBuilder()
                .WithStrategy(new StackableItemStrategy())
                .WithMaxStackSize(10)
                .WithFixedSlots(1)
                .WithName("AlsoBig")
                .Build();

            _source.GetSlot(0).SetStack(ItemStackBuilder.Of(new FakeItemAdapter("sword")));
            _target.GetSlot(0).SetStack(ItemStackBuilder.Of(FakeItemAdapter.Many(10, "gem")));

            var report = ExecuteSwapDrop();

            Assert.IsTrue(report.Success, report.EntryResults[0].FailureReason);
            Assert.AreEqual("gem", _source.GetSlot(0).Stack.ID);
            Assert.AreEqual(10, _source.GetSlot(0).Stack.Count);
            Assert.AreEqual("sword", _target.GetSlot(0).Stack.ID);
        }

        private TransferExecutionReport ExecuteSwapDrop()
        {
            var sourceSlot = _source.GetSlot(0);
            var context = new DragContext(new[]
            {
                new DragEntry(sourceSlot.Stack.CreateCopy(), sourceSlot, _source)
            });
            var processor = new InventoryDropProcessor(
                _target.GetSlot(0), _target, new GlobalRuleValidator());
            return processor.ProcessDropWithReport(context, DropRequestPolicy.WithSwap());
        }

        [Test]
        public void Swap_SourceInventoryRejectsCounterpart_IsRefused()
        {
            BuildInventories();
            _source.RuleValidator.AddRule(RejectDropOf("cursed", "Cursed items cannot be carried"));

            var result = ExecuteSwap(BuildSwapContext());

            Assert.AreEqual(EntryTransferStatus.Failed, result.Status,
                "The counterpart item is refused by the source inventory, so the swap must not happen");
            StringAssert.Contains("Cursed items cannot be carried", result.FailureReason);
        }

        [Test]
        public void Swap_SourceInventoryRejectsCounterpart_LeavesBothInventoriesUntouched()
        {
            BuildInventories();
            _source.RuleValidator.AddRule(RejectDropOf("cursed", "Cursed items cannot be carried"));

            ExecuteSwap(BuildSwapContext());

            Assert.AreEqual("sword", _source.GetSlot(0).Stack.PrimaryAdapter.ItemId,
                "A refused swap must not move the forward item either");
            Assert.AreEqual("cursed", _target.GetSlot(0).Stack.PrimaryAdapter.ItemId);
        }

        [Test]
        public void Swap_TargetSlotRulesRejectCounterpartLeaving_IsRefused()
        {
            BuildInventories();
            _source.GetSlot(0).SlotRuleValidator.AddRule(
                new ItemIdFilterRule(new[] { "sword" }, whitelist: true));

            var result = ExecuteSwap(BuildSwapContext());

            Assert.AreEqual(EntryTransferStatus.Failed, result.Status,
                "The source slot only accepts swords, so the counterpart cannot land there");
        }

        [Test]
        public void Swap_BothDirectionsAllowed_Succeeds()
        {
            BuildInventories();
            // A rule that rejects something neither item is: the barrier must not be indiscriminate.
            _source.RuleValidator.AddRule(RejectDropOf("bomb", "No bombs"));

            var result = ExecuteSwap(BuildSwapContext());

            Assert.AreEqual(EntryTransferStatus.Succeeded, result.Status, result.FailureReason);
            Assert.AreEqual("cursed", _source.GetSlot(0).Stack.PrimaryAdapter.ItemId);
            Assert.AreEqual("sword", _target.GetSlot(0).Stack.PrimaryAdapter.ItemId);
        }

        [Test]
        public void Probe_SwapRejectedByCounterpartRules_DoesNotPromiseTheSwap()
        {
            // The preview must refuse a swap the execution would refuse; otherwise the slot
            // highlights green and the drop silently does nothing.
            BuildInventories();
            _source.RuleValidator.AddRule(RejectDropOf("cursed", "Cursed items cannot be carried"));

            var probe = _service.Probe(
                BuildSwapContext(),
                _target,
                _target.GetSlot(0),
                SwapPolicy());

            Assert.IsFalse(probe.CanAttempt);
            StringAssert.Contains("Cursed items cannot be carried", probe.FailureReason);
        }

        [Test]
        public void Probe_SwapAllowedInBothDirections_IsAccepted()
        {
            BuildInventories();

            var probe = _service.Probe(
                BuildSwapContext(),
                _target,
                _target.GetSlot(0),
                SwapPolicy());

            Assert.IsTrue(probe.CanAttempt, probe.FailureReason);
            Assert.IsTrue(probe.IsExplicitTargetCandidate);
            Assert.AreSame(_target.GetSlot(0), probe.AnchorSlot);
        }
    }
}
