using System.Linq;
using NUnit.Framework;
using UDND.Core;
using UDND.Inventories;
using UDND.Rules;

namespace UDND.Tests.Inventories
{
    /// <summary>
    /// Tests for TransferProbe — the advisory acceptance result a UI layer reads to draw
    /// hover feedback and drop previews.
    ///
    /// The probe had no direct coverage, yet it is the contract a UI Toolkit backend will
    /// lean on for preview: it must answer truthfully about the current inventory state
    /// without reserving or mutating anything. A probe that mutates would put item
    /// mutation in the presentation layer, which the architecture forbids.
    /// </summary>
    [TestFixture]
    public class TransferProbeTests
    {
        private UniversalInventory _source;
        private UniversalInventory _target;

        [TearDown]
        public void TearDown()
        {
            InventoryBuilder.Destroy(_source);
            InventoryBuilder.Destroy(_target);
            _source = null;
            _target = null;
        }

        // ---------- the core invariant ----------

        [Test]
        public void Probe_DoesNotMutateInventoryState()
        {
            BuildSourceAndTarget(sourceSlots: 3, targetSlots: 3);
            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "gem"));
            _source.GetSlot(1).SetStack(ItemStackBuilder.Unique(1, "ore"));
            _target.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "occupied"));

            var sourceBefore = DescribeSlots(_source);
            var targetBefore = DescribeSlots(_target);

            var processor = new InventoryDropProcessor(_target, new GlobalRuleValidator());
            var context = DragContextBuilder.FromAllSlots(_source).ToTarget(_target).Build();

            Assert.IsTrue(processor.CanAcceptDrop(context));
            Assert.IsTrue(processor.LastProbe.CanAttempt);

            CollectionAssert.AreEqual(sourceBefore, DescribeSlots(_source),
                "Probing must not touch the source inventory");
            CollectionAssert.AreEqual(targetBefore, DescribeSlots(_target),
                "Probing must not reserve or fill target slots");
        }

        [Test]
        public void Probe_RepeatedCalls_AreStableAndStillDoNotMutate()
        {
            // A hover loop probes every frame. Repeated probing must be idempotent —
            // anything else would accumulate state behind the preview.
            BuildSourceAndTarget(sourceSlots: 1, targetSlots: 1);
            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "gem"));

            var targetBefore = DescribeSlots(_target);
            var processor = new InventoryDropProcessor(_target, new GlobalRuleValidator());
            var context = DragContextBuilder.FromAllSlots(_source).ToTarget(_target).Build();

            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(processor.CanAcceptDrop(context), $"Probe {i} changed its answer");
                Assert.AreSame(_target.GetSlot(0), processor.LastProbe.AnchorSlot);
            }

            CollectionAssert.AreEqual(targetBefore, DescribeSlots(_target));
        }

        // ---------- policy ----------

        [Test]
        public void Probe_RejectsSwapWithMultipleEntries()
        {
            // Swap is defined for a single full entry. The probe must reject a batch before
            // any placement search, so the UI never previews an impossible swap.
            BuildSourceAndTarget(sourceSlots: 2, targetSlots: 2);
            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "first"));
            _source.GetSlot(1).SetStack(ItemStackBuilder.Unique(1, "second"));

            var processor = new InventoryDropProcessor(
                _target.GetSlot(0),
                _target,
                new GlobalRuleValidator());

            var context = DragContextBuilder
                .FromAllSlots(_source)
                .ToTargetSlot(_target.GetSlot(0), _target)
                .Build();

            Assert.IsFalse(processor.CanAcceptDrop(context, DropRequestPolicy.WithSwap()));
            Assert.IsFalse(processor.LastProbe.CanAttempt);
            Assert.AreEqual("Swap requires a single full entry", processor.LastProbe.FailureReason);
        }

        [Test]
        public void Probe_ExplicitTargetSlot_IsMarkedAsExplicitCandidate()
        {
            // Distinguishes the explicit-target path (TryGetCandidate) from automatic
            // placement (GetCandidates). A preview must not silently redirect the item to a
            // different slot while the pointer sits on a valid one.
            BuildSourceAndTarget(sourceSlots: 1, targetSlots: 3);
            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "gem"));

            var targetSlot = _target.GetSlot(2);
            var processor = new InventoryDropProcessor(targetSlot, _target, new GlobalRuleValidator());
            var context = DragContextBuilder
                .FromAllSlots(_source)
                .ToTargetSlot(targetSlot, _target)
                .Build();

            Assert.IsTrue(processor.CanAcceptDrop(context));
            Assert.IsTrue(processor.LastProbe.IsExplicitTargetCandidate);
            Assert.AreSame(targetSlot, processor.LastProbe.AnchorSlot);
            CollectionAssert.AreEqual(
                new[] { targetSlot.Index },
                processor.LastProbe.CoveredSlots.Select(slot => slot.Index).ToArray());
        }

        // ---------- probe vs. execution ----------

        [Test]
        public void Probe_Accepted_MatchesSubsequentProcessDropOutcome()
        {
            BuildSourceAndTarget(sourceSlots: 1, targetSlots: 2);
            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "gem"));

            var processor = new InventoryDropProcessor(_target, new GlobalRuleValidator());
            var context = DragContextBuilder.FromAllSlots(_source).ToTarget(_target).Build();

            Assert.IsTrue(processor.CanAcceptDrop(context));
            var anchorFromProbe = processor.LastProbe.AnchorSlot;

            var report = processor.ProcessDropWithReport(context);

            Assert.IsTrue(report.Success, "An accepted probe must not be contradicted by execution");
            Assert.IsFalse(anchorFromProbe.IsEmpty,
                "The item must land on the slot the probe advertised");
            Assert.IsTrue(_source.GetSlot(0).IsEmpty);
        }

        [Test]
        public void Probe_Rejected_MatchesSubsequentProcessDropOutcome()
        {
            BuildSourceAndTarget(sourceSlots: 1, targetSlots: 1);
            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "gem"));
            _target.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "occupied"));

            var processor = new InventoryDropProcessor(_target, new GlobalRuleValidator());
            var context = DragContextBuilder.FromAllSlots(_source).ToTarget(_target).Build();

            Assert.IsFalse(processor.CanAcceptDrop(context));
            Assert.IsFalse(processor.LastProbe.CanAttempt);

            var report = processor.ProcessDropWithReport(context);

            Assert.IsFalse(report.Success, "A rejected probe must not be contradicted by execution");
            Assert.IsFalse(_source.GetSlot(0).IsEmpty);
        }

        /// <summary>
        /// The probe is advisory, not a reservation: it answers against the inventory state
        /// at the moment it is asked. When that state changes underneath, a previously
        /// accepted probe must go stale rather than keep a slot held for the UI.
        /// </summary>
        [Test]
        public void Probe_GoesStale_WhenTargetStateChangesAfterProbing()
        {
            BuildSourceAndTarget(sourceSlots: 1, targetSlots: 1);
            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "gem"));

            var processor = new InventoryDropProcessor(_target, new GlobalRuleValidator());
            var context = DragContextBuilder.FromAllSlots(_source).ToTarget(_target).Build();

            Assert.IsTrue(processor.CanAcceptDrop(context));

            // Someone else fills the only free slot between hover and release.
            _target.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "sniped"));

            Assert.IsFalse(processor.CanAcceptDrop(context),
                "Re-probing must reflect the new state, not the earlier answer");

            var report = processor.ProcessDropWithReport(context);
            Assert.IsFalse(report.Success);
            Assert.IsFalse(_source.GetSlot(0).IsEmpty, "The stale probe must not have reserved the slot");
        }

        // ---------- rejection edges ----------

        [Test]
        public void Probe_NullContext_IsRejectedWithoutThrowing()
        {
            BuildSourceAndTarget(sourceSlots: 1, targetSlots: 1);

            var processor = new InventoryDropProcessor(_target, new GlobalRuleValidator());

            Assert.IsFalse(processor.CanAcceptDrop(null));
            Assert.IsFalse(processor.LastProbe.CanAttempt);
            Assert.IsNotNull(processor.LastProbe.FailureReason,
                "A rejected probe must carry a reason the UI can surface");
        }

        // ---------- helpers ----------

        private void BuildSourceAndTarget(int sourceSlots, int targetSlots)
        {
            _source = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(sourceSlots)
                .WithName("ProbeSource")
                .Build();

            _target = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(targetSlots)
                .WithName("ProbeTarget")
                .Build();
        }

        /// <summary>Snapshot of slot contents, comparable before and after a probe.</summary>
        private static string[] DescribeSlots(IInventory inventory)
        {
            var described = new string[inventory.SlotCount];
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var slot = inventory.GetSlot(i);
                described[i] = slot.IsEmpty
                    ? $"{i}:empty"
                    : $"{i}:{slot.Stack.PrimaryAdapter.ItemId}x{slot.Stack.Count}";
            }
            return described;
        }
    }
}
