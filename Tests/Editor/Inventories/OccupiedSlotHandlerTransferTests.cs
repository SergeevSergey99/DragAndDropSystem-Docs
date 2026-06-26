using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UDND.Core;
using UDND.DataBinding;
using UDND.Inventories;
using UDND.Rules;
using UDND.Slots;

namespace UDND.Tests.Inventories
{
    /// <summary>
    /// Regression tests for the Demo5 "drop item onto a container" duplication bug.
    ///
    /// A pre-rule occupied-slot handler inserts the dragged item into the container that sits in
    /// the occupied target slot. The handler used to remove the dragged item from its OWN backing
    /// store (RemoveFromData), which is correct only when the source inventory is the same binding.
    /// When the item is dragged from a DIFFERENT inventory, removing from the handler's own store
    /// leaves the item in its real source store — so it lives in the container AND reappears in the
    /// source on the next reload (two copies). The fix removes the item from the inventory it
    /// actually came from, routing the removal to that inventory's own DataBinding.
    /// </summary>
    [TestFixture]
    public class OccupiedSlotHandlerTransferTests
    {
        private UniversalInventory _source;
        private UniversalInventory _target;
        private UniversalInventory _live;

        [TearDown]
        public void TearDown()
        {
            InventoryBuilder.Destroy(_source);
            InventoryBuilder.Destroy(_target);
            InventoryBuilder.Destroy(_live);
            _source = null;
            _target = null;
            _live = null;
        }

        [Test]
        public void OccupiedHandler_CrossInventory_DropOntoContainer_DoesNotDuplicateSourceItem()
        {
            _source = new InventoryBuilder().WithStrategy(new UniqueItemStrategy()).WithFixedSlots(1).Build();
            _target = new InventoryBuilder().WithStrategy(new UniqueItemStrategy()).WithFixedSlots(1).Build();

            var sourceBinding = _source.gameObject.AddComponent<ContainerStoreBinding>();
            _source.Initialize(sourceBinding);
            var targetBinding = _target.gameObject.AddComponent<ContainerStoreBinding>();
            _target.Initialize(targetBinding);

            // Source inventory holds the gem (slot + backing store agree, as after a real ReloadUI).
            var gem = new Token("gem");
            sourceBinding.Store.Add(gem);
            _source.GetSlot(0).SetStack(ItemStackBuilder.Of(new TokenAdapter(gem)));

            // Target inventory's occupied slot holds a container object.
            var container = new ContainerAdapter("box");
            _target.GetSlot(0).SetStack(ItemStackBuilder.Of(container));

            var context = DragContextBuilder
                .FromSlots(_source, 0)
                .ToTargetSlot(_target.GetSlot(0), _target)
                .Build();

            var processor = new InventoryDropProcessor(_target.GetSlot(0), _target, new GlobalRuleValidator());
            var report = processor.ProcessDropWithReport(context, DropRequestPolicy.WithAlternativeOrderer());

            Assert.IsTrue(report.Success, $"Occupied handler must succeed, got: {report.FailureReason}");

            // The gem moved into the container exactly once.
            Assert.AreEqual(1, container.Children.Count, "Container must hold exactly one gem");
            Assert.AreSame(gem, container.Children[0]);

            // The gem is gone from the SOURCE store — not just the source UI slot.
            CollectionAssert.DoesNotContain(sourceBinding.Store, gem,
                "Source store must lose the gem; leaving it there is the duplication bug");
            Assert.IsEmpty(sourceBinding.Store);
            Assert.IsTrue(_source.GetSlot(0).IsEmpty, "Source slot must be emptied");

            // It was never written into the target binding's own store either.
            CollectionAssert.DoesNotContain(targetBinding.Store, gem);

            // Total instances are conserved: one gem, no duplicate.
            Assert.AreEqual(1, sourceBinding.Store.Count + targetBinding.Store.Count + container.Children.Count,
                "Exactly one gem must exist across all stores after the drop");

            // The container itself is untouched in its slot.
            Assert.AreEqual("container:box", _target.GetSlot(0).Stack.PrimaryAdapter.ItemId);
        }

        [Test]
        public void OccupiedHandler_SameInventory_DropOntoContainer_MovesItemWithoutDuplication()
        {
            _source = new InventoryBuilder().WithStrategy(new UniqueItemStrategy()).WithFixedSlots(2).Build();

            var binding = _source.gameObject.AddComponent<ContainerStoreBinding>();
            _source.Initialize(binding);

            var gem = new Token("gem");
            binding.Store.Add(gem);
            _source.GetSlot(0).SetStack(ItemStackBuilder.Of(new TokenAdapter(gem)));

            var container = new ContainerAdapter("box");
            _source.GetSlot(1).SetStack(ItemStackBuilder.Of(container));

            var context = DragContextBuilder
                .FromSlots(_source, 0)
                .ToTargetSlot(_source.GetSlot(1), _source)
                .Build();

            var processor = new InventoryDropProcessor(_source.GetSlot(1), _source, new GlobalRuleValidator());
            var report = processor.ProcessDropWithReport(context, DropRequestPolicy.WithAlternativeOrderer());

            Assert.IsTrue(report.Success, $"Occupied handler must succeed, got: {report.FailureReason}");
            Assert.AreEqual(1, container.Children.Count);
            Assert.AreSame(gem, container.Children[0]);
            Assert.IsEmpty(binding.Store, "Same-inventory move must also drain the backing store");
            Assert.IsTrue(_source.GetSlot(0).IsEmpty);
            Assert.AreEqual("container:box", _source.GetSlot(1).Stack.PrimaryAdapter.ItemId);
        }

        [Test]
        public void OccupiedHandler_OpenContainer_RoutesIntoLiveInventory_Incrementally()
        {
            // The container is "open": a live inventory currently displays its contents. Dropping
            // onto the container slot must route the item into that live inventory through the
            // normal drop pipeline — filling one slot and writing its data via the binding — rather
            // than mutating data out of band (which would force a full reload of the open view).
            _source = new InventoryBuilder().WithStrategy(new UniqueItemStrategy()).WithFixedSlots(1).Build();
            _target = new InventoryBuilder().WithStrategy(new UniqueItemStrategy()).WithFixedSlots(1).Build();
            _live = new InventoryBuilder().WithStrategy(new UniqueItemStrategy()).WithFixedSlots(4).Build();

            var liveBinding = _live.gameObject.AddComponent<ContainerStoreBinding>();
            _live.Initialize(liveBinding);

            var sourceBinding = _source.gameObject.AddComponent<ContainerStoreBinding>();
            _source.Initialize(sourceBinding);
            var targetBinding = _target.gameObject.AddComponent<ContainerStoreBinding>();
            _target.Initialize(targetBinding);
            targetBinding.RouteTarget = _live; // container is open → route into its live inventory

            var gem = new Token("gem");
            sourceBinding.Store.Add(gem);
            _source.GetSlot(0).SetStack(ItemStackBuilder.Of(new TokenAdapter(gem)));

            var container = new ContainerAdapter("box");
            _target.GetSlot(0).SetStack(ItemStackBuilder.Of(container));

            var context = DragContextBuilder
                .FromSlots(_source, 0)
                .ToTargetSlot(_target.GetSlot(0), _target)
                .Build();

            var processor = new InventoryDropProcessor(_target.GetSlot(0), _target, new GlobalRuleValidator());
            var report = processor.ProcessDropWithReport(context, DropRequestPolicy.WithAlternativeOrderer());

            Assert.IsTrue(report.Success, $"Routed occupied handler must succeed, got: {report.FailureReason}");

            // The item entered the live inventory through the pipeline: one slot filled + data written.
            Assert.Contains(gem, liveBinding.Store, "Live inventory's binding must receive the item via AddToData");
            Assert.AreEqual(1, CountFilled(_live), "Exactly one live slot must be filled (incremental, not a rebuild)");

            // Source drained, container-holder slot untouched, no duplication.
            Assert.IsTrue(_source.GetSlot(0).IsEmpty, "Source slot must be emptied");
            Assert.IsEmpty(sourceBinding.Store);
            Assert.AreEqual("container:box", _target.GetSlot(0).Stack.PrimaryAdapter.ItemId);
            Assert.AreEqual(1,
                sourceBinding.Store.Count + targetBinding.Store.Count + liveBinding.Store.Count + container.Children.Count,
                "Exactly one gem must exist across all stores after the routed drop");
        }

        [Test]
        public void OccupiedHandler_OpenContainer_DropFromSameLiveInventoryOntoItsContainer_UsesPolicyAndDoesNotFindAlternative()
        {
            _source = new InventoryBuilder().WithStrategy(new UniqueItemStrategy()).WithFixedSlots(2).Build();
            _target = new InventoryBuilder().WithStrategy(new UniqueItemStrategy()).WithFixedSlots(2).Build();

            var sourceBinding = _source.gameObject.AddComponent<ContainerStoreBinding>();
            _source.Initialize(sourceBinding);

            var targetBinding = _target.gameObject.AddComponent<ContainerStoreBinding>();
            _target.Initialize(targetBinding);
            targetBinding.RouteTarget = _source;

            var gem = new Token("gem");
            sourceBinding.Store.Add(gem);
            _source.GetSlot(0).SetStack(ItemStackBuilder.Of(new TokenAdapter(gem)));

            var container = new ContainerAdapter("box");
            _target.GetSlot(0).SetStack(ItemStackBuilder.Of(container));

            var context = DragContextBuilder
                .FromSlots(_source, 0)
                .ToTargetSlot(_target.GetSlot(0), _target)
                .Build();

            var processor = new InventoryDropProcessor(_target.GetSlot(0), _target, new GlobalRuleValidator());
            var report = processor.ProcessDropWithReport(context);

            Assert.IsFalse(report.Success, "Policy must reject same-live-inventory alternative placement");
            Assert.AreEqual("gem", _source.GetSlot(0).Stack.PrimaryAdapter.ItemId);
            Assert.IsTrue(_source.GetSlot(1).IsEmpty, "FindAlternative must not move the item to another live slot");
            Assert.Contains(gem, sourceBinding.Store);
            Assert.AreEqual(0, container.Children.Count);
            Assert.AreEqual("container:box", _target.GetSlot(0).Stack.PrimaryAdapter.ItemId);
            Assert.IsTrue(_target.GetSlot(1).IsEmpty);
        }

        // A same-inventory programmatic drop whose explicit target slot belongs to a DIFFERENT
        // inventory (exactly what the container redirect produces for the same-live case). The slot
        // is unplaceable in the target inventory, so the blocked-target policy decides — and
        // AllowSameInventoryAlternativePlacement must control whether the engine relocates the item
        // to a free slot or rejects. Before the foreign-target guard, the occupied handler re-fired
        // and BOTH policy values behaved identically (the bug); this proves the policy now bites.
        [TestCase(true, true)]    // allow → relocate to the free slot, success
        [TestCase(false, false)]  // forbid → reject, item stays put
        public void ForeignExplicitTarget_SameInventory_PolicyControlsAlternativeSearch(
            bool allowSameInventory, bool expectSuccess)
        {
            _source = new InventoryBuilder().WithStrategy(new UniqueItemStrategy()).WithFixedSlots(2).Build();
            _target = new InventoryBuilder().WithStrategy(new UniqueItemStrategy()).WithFixedSlots(1).Build();

            var sourceBinding = _source.gameObject.AddComponent<ContainerStoreBinding>();
            _source.Initialize(sourceBinding);

            var gem = new Token("gem");
            sourceBinding.Store.Add(gem);
            _source.GetSlot(0).SetStack(ItemStackBuilder.Of(new TokenAdapter(gem)));

            // The explicit target slot lives in _target, but the drop targets _source (== source).
            var container = new ContainerAdapter("box");
            _target.GetSlot(0).SetStack(ItemStackBuilder.Of(container));

            var context = DragContextBuilder
                .FromSlots(_source, 0)
                .ToTargetSlot(_target.GetSlot(0), _source)
                .Build();

            var policy = DropRequestPolicy.WithAlternativeOrderer(
                allowSameInventoryAlternativePlacement: allowSameInventory);
            var report = new InventoryDropProcessor(_target.GetSlot(0), _source, new GlobalRuleValidator())
                .ProcessDropWithReport(context, policy);

            Assert.AreEqual(expectSuccess, report.Success,
                $"allowSameInventory={allowSameInventory} must drive the outcome");
            Assert.AreEqual(0, container.Children.Count, "the foreign occupied slot must never be touched");

            if (expectSuccess)
            {
                Assert.IsTrue(_source.GetSlot(0).IsEmpty, "item left its original slot");
                Assert.AreEqual("gem", _source.GetSlot(1).Stack.PrimaryAdapter.ItemId,
                    "FindAlternative relocated the item to the free slot");
            }
            else
            {
                Assert.AreEqual("gem", _source.GetSlot(0).Stack.PrimaryAdapter.ItemId, "item stayed put");
                Assert.IsTrue(_source.GetSlot(1).IsEmpty, "no alternative slot was used");
            }
        }

        private static int CountFilled(IInventory inventory)
        {
            int n = 0;
            for (int i = 0; i < inventory.SlotCount; i++)
                if (!inventory.GetSlot(i).IsEmpty) n++;
            return n;
        }

        // ── Test doubles ───────────────────────────────────────────────────────────────────────

        private sealed class Token
        {
            public readonly string Id;
            public Token(string id) => Id = id;
        }

        private sealed class TokenAdapter : IItemAdapter
        {
            public readonly Token Token;
            public TokenAdapter(Token token) => Token = token;
            public string ItemId => Token.Id;
            public string DisplayName => Token.Id;
            public Sprite Icon => null;
        }

        private sealed class ContainerAdapter : IItemAdapter
        {
            public readonly List<Token> Children = new();
            public ContainerAdapter(string id) => ItemId = "container:" + id;
            public string ItemId { get; }
            public string DisplayName => ItemId;
            public Sprite Icon => null;
        }

        /// <summary>
        /// Store-backed binding mirroring the Demo5 container bindings: it owns a list of items and
        /// a pre-rule occupied-slot handler that drops the dragged item into the target container,
        /// removing it from whichever inventory it came from (not from this binding's own store).
        /// </summary>
        private sealed class ContainerStoreBinding
            : ListInventoryDataBinding<Token, TokenAdapter>, IPreRuleOccupiedSlotDropHandler
        {
            public readonly List<Token> Store = new();

            /// <summary>When set, the container is "open": route the drop into this live inventory.</summary>
            public IInventory RouteTarget;

            protected override void Awake() { }

            protected override IReadOnlyList<Token> GetItems() => Store;
            protected override TokenAdapter CreateAdapter(Token item) => new(item);
            protected override void AddToData(TokenAdapter adapter) => Store.Add(adapter.Token);
            protected override void RemoveFromData(TokenAdapter adapter) => Store.Remove(adapter.Token);

            public bool CheckOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedBaseSlot)
                => occupiedBaseSlot?.Stack?.PrimaryAdapter is ContainerAdapter
                   && entry.Stack?.PrimaryAdapter is TokenAdapter;

            public bool ExecuteOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedBaseSlot)
            {
                if (occupiedBaseSlot?.Stack?.PrimaryAdapter is not ContainerAdapter container)
                    return false;
                if (entry.Stack?.PrimaryAdapter is not TokenAdapter token)
                    return false;

                // Open container → route through its live inventory's normal pipeline (incremental).
                if (RouteTarget != null)
                {
                    var routedSourceInventory = entry.SourceInventory ?? entry.SourceBaseSlot?.Inventory;
                    var routedTargetSlot = ReferenceEquals(RouteTarget, routedSourceInventory)
                        ? occupiedBaseSlot
                        : null;
                    var dropContext = new DragContext(
                        entry.Stack,
                        entry.SourceBaseSlot,
                        routedSourceInventory,
                        routedTargetSlot,
                        RouteTarget);
                    return new InventoryDropProcessor(routedTargetSlot, RouteTarget, new GlobalRuleValidator())
                        .ProcessDropWithReport(dropContext)
                        .Success;
                }

                // Closed container → mutate data directly and remove from the real source.
                container.Children.Add(token.Token);

                var sourceInventory = entry.SourceInventory ?? entry.SourceBaseSlot?.Inventory;
                sourceInventory?.RemoveItemsFromSlot(entry.SourceBaseSlot, entry.Stack);
                return true;
            }
        }
    }
}
