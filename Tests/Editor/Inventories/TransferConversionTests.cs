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
    /// Cross-inventory transfers convert items at the boundary between two adapter domains.
    /// These tests pin the two guarantees that conversion has to provide:
    /// the target's drop rules judge the item as it will exist after the drop, and the object
    /// they judged is the object that actually lands in the target.
    /// </summary>
    [TestFixture]
    public class TransferConversionTests
    {
        private const string SourceDomain = "src";
        private const string TargetDomain = "dst";

        private UniversalInventory _source;
        private UniversalInventory _target;
        private GameObject _bindings;
        private readonly InventoryTransferService _service = new InventoryTransferService();

        [SetUp]
        public void SetUp()
        {
            _bindings = new GameObject(nameof(TransferConversionTests));
        }

        [TearDown]
        public void TearDown()
        {
            InventoryBuilder.Destroy(_source);
            InventoryBuilder.Destroy(_target);
            if (_bindings != null)
                Object.DestroyImmediate(_bindings);

            _source = null;
            _target = null;
            _bindings = null;
        }

        private ConvertingBinding AttachBinding(UniversalInventory inventory, string domain, bool conversionFails = false)
        {
            var binding = _bindings.AddComponent<ConvertingBinding>();
            binding.Configure(domain, conversionFails);
            binding.AttachTo(inventory);
            return binding;
        }

        private static ResolvedDropPolicy DefaultPolicy()
            => new ResolvedDropPolicy(
                BlockedTargetResolutionKind.FindAlternative,
                null,
                allowSameInventoryAlternativePlacement: true,
                partialTransferMode: PartialTransferMode.Allow);

        private EntryTransferResult Transfer(DragContext context, BaseSlot targetSlot)
            => _service.TryTransferEntry(new TransferEntryRequest(
                context,
                context.Entries[0],
                _target,
                targetSlot,
                DefaultPolicy()));

        private DragContext BuildContext(BaseSlot targetSlot)
            => DragContextBuilder
                .FromAllSlots(_source)
                .ToTargetSlot(targetSlot, _target)
                .Build();

        private void BuildInventories(int sourceSlots = 1, int targetSlots = 1)
        {
            _source = new InventoryBuilder()
                .WithStrategy(new StackableItemStrategy())
                .WithMaxStackSize(10)
                .WithFixedSlots(sourceSlots)
                .WithName("Source")
                .Build();
            _target = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(targetSlots)
                .WithName("Target")
                .Build();
        }

        [Test]
        public void DropRules_SeeTheItemInTheTargetDomain()
        {
            // The regression this pins: rules used to run before conversion, so a target whose
            // binding is typed for its own adapter refused every item arriving from elsewhere.
            BuildInventories();
            AttachBinding(_source, SourceDomain);
            var targetBinding = AttachBinding(_target, TargetDomain);

            _source.GetSlot(0).SetStack(ItemStackBuilder.Of(new FakeItemAdapter("gem", SourceDomain)));

            var targetSlot = _target.GetSlot(0);
            var result = Transfer(BuildContext(targetSlot), targetSlot);

            Assert.AreEqual(EntryTransferStatus.Succeeded, result.Status, result.FailureReason);
            CollectionAssert.IsNotEmpty(targetBinding.CanDropSaw, "The target binding's CanDrop must be consulted");
            foreach (var seen in targetBinding.CanDropSaw)
            {
                Assert.AreEqual(TargetDomain, seen.DisplayName,
                    "Drop rules must judge the converted item, not the source-domain one");
            }
        }

        [Test]
        public void CommittedAdapter_IsTheInstanceTheRulesValidated()
        {
            BuildInventories();
            AttachBinding(_source, SourceDomain);
            var targetBinding = AttachBinding(_target, TargetDomain);

            _source.GetSlot(0).SetStack(ItemStackBuilder.Of(new FakeItemAdapter("gem", SourceDomain)));

            var targetSlot = _target.GetSlot(0);
            var context = BuildContext(targetSlot);

            // Probe first, exactly as a hover would, then drop through the same context.
            var probe = _service.Probe(context, _target, targetSlot, DefaultPolicy());
            Assert.IsTrue(probe.CanAttempt, probe.FailureReason);
            CollectionAssert.IsNotEmpty(targetBinding.CanDropSaw);
            var previewed = targetBinding.CanDropSaw[0];

            var result = Transfer(context, targetSlot);

            Assert.AreEqual(EntryTransferStatus.Succeeded, result.Status, result.FailureReason);
            Assert.AreEqual(1, targetBinding.Added.Count);
            Assert.AreSame(previewed, targetBinding.Added[0],
                "The object the preview validated must be the object committed, not an equal copy");
            Assert.AreSame(previewed, targetSlot.Stack.PrimaryAdapter,
                "…and the same object must be the one sitting in the target slot");
        }

        [Test]
        public void ConversionFailure_IsRejectedByRulesWithAReason()
        {
            // An item that cannot cross into the target domain must be refused while the drop is
            // still being judged, so the preview can show it, instead of passing the rules and
            // failing later during mutation.
            BuildInventories();
            AttachBinding(_source, SourceDomain);
            AttachBinding(_target, TargetDomain, conversionFails: true);

            _source.GetSlot(0).SetStack(ItemStackBuilder.Of(new FakeItemAdapter("gem", SourceDomain)));

            var targetSlot = _target.GetSlot(0);
            var probe = _service.Probe(BuildContext(targetSlot), _target, targetSlot, DefaultPolicy());

            Assert.IsFalse(probe.CanAttempt);
            StringAssert.Contains("convert", probe.FailureReason);
            Assert.IsTrue(targetSlot.IsEmpty);
        }

        [Test]
        public void PreviewSlice_TakesTheInstancesSplitWouldTake()
        {
            // ItemStack.Split consumes an entry from the tail. A preview over the head would
            // validate items that never move.
            BuildInventories();
            var adapters = FakeItemAdapter.Many(3, "gem");
            _source.GetSlot(0).SetStack(ItemStackBuilder.Of(adapters));

            var entryStack = ItemStackBuilder.Of(adapters);
            var entry = new DragEntry(entryStack, _source.GetSlot(0), _source);
            var context = new DragContext(new[] { entry });
            var request = new InventoryAcceptanceRequest(_target, adapters[0], 3, context, entry);

            var preview = request.CreatePreviewStack(1);

            Assert.IsNotNull(preview);
            Assert.AreSame(adapters[2], preview.Adapters[0],
                "With nothing transferred yet, a one-item preview must be the last adapter — the one Split takes");
        }

        [Test]
        public void PreviewSlice_AfterPartialTransfer_SkipsAlreadyMovedInstances()
        {
            // DesiredCount is what is left of the entry. Splits ate the tail, so the untransferred
            // remainder is the head, and the next split takes the tail of *that*.
            BuildInventories();
            var adapters = FakeItemAdapter.Many(3, "gem");
            _source.GetSlot(0).SetStack(ItemStackBuilder.Of(adapters));

            var entryStack = ItemStackBuilder.Of(adapters);
            var entry = new DragEntry(entryStack, _source.GetSlot(0), _source);
            var context = new DragContext(new[] { entry });

            // One item already placed: two remain in the source.
            var request = new InventoryAcceptanceRequest(_target, adapters[0], 2, context, entry);

            var preview = request.CreatePreviewStack(1);

            Assert.IsNotNull(preview);
            Assert.AreSame(adapters[1], preview.Adapters[0],
                "adapters[2] is already in the target; validating it again would judge the wrong item");
        }

        [Test]
        public void MultiPlacementEntry_CommitsExactlyTheValidatedInstances()
        {
            // A stack spread over several unique slots exercises the candidate loop, where the
            // slice must advance with the remainder rather than re-reading the entry's tail.
            BuildInventories(sourceSlots: 1, targetSlots: 3);
            AttachBinding(_source, SourceDomain);
            var targetBinding = AttachBinding(_target, TargetDomain);

            _source.GetSlot(0).SetStack(ItemStackBuilder.Of(
                new FakeItemAdapter("gem", SourceDomain),
                new FakeItemAdapter("gem", SourceDomain),
                new FakeItemAdapter("gem", SourceDomain)));

            var context = DragContextBuilder.FromAllSlots(_source).ToTarget(_target).Build();
            var result = _service.TryTransferEntry(new TransferEntryRequest(
                context, context.Entries[0], _target, null, DefaultPolicy()));

            Assert.AreEqual(EntryTransferStatus.Succeeded, result.Status, result.FailureReason);
            Assert.AreEqual(3, targetBinding.Added.Count);
            CollectionAssert.AllItemsAreUnique(targetBinding.Added,
                "Each placement must commit its own converted instance");

            foreach (var committed in targetBinding.Added)
            {
                Assert.Contains(committed, targetBinding.CanDropSaw,
                    "Every committed instance must be one the drop rules actually saw");
            }
        }

        [Test]
        public void AutoTransfer_PartialAmount_TakesTailOfSourceStack()
        {
            // AutoTransferService used to build its entry from the head while execution split the
            // tail, so rules and mutation disagreed about which instances were moving.
            _source = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy()) // ResolveDragAmount == 1, forcing a partial drag
                .WithFixedSlots(1)
                .WithName("Source")
                .Build();
            _target = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(3)
                .WithName("Target")
                .Build();

            var adapters = FakeItemAdapter.Many(3, "gem");
            _source.GetSlot(0).SetStack(ItemStackBuilder.Of(adapters));

            Assert.IsTrue(new AutoTransferService().TryCreateContext(
                new List<BaseSlot> { _source.GetSlot(0) },
                _source,
                _target,
                out var context,
                out var failureReason), failureReason);

            Assert.AreEqual(1, context.Entries[0].Stack.Count);
            Assert.AreSame(adapters[2], context.Entries[0].Stack.PrimaryAdapter,
                "A partial auto-transfer must carry the instances Split will hand to execution");
        }

        /// <summary>
        /// A converter may change the item's footprint at the domain boundary, and a multi-swap
        /// has to decide which placements it displaces from the shape the item will actually have
        /// in the target. Resolving the displaced set from the source placement instead would
        /// vacate one cell and then drop a three-cell item onto it.
        /// </summary>
        [Test]
        public void MultiSwap_ResolvesDisplacedSetFromTheTargetDomainFootprint()
        {
            _source = new InventoryBuilder()
                .WithFixedSlots(3)
                .WithGridTopology(3, 1)
                .WithName("Source")
                .Build();
            _target = new InventoryBuilder()
                .WithFixedSlots(3)
                .WithGridTopology(3, 1)
                .WithName("Target")
                .Build();

            var binding = _bindings.AddComponent<ShapeWideningBinding>();
            binding.AttachTo(_target);

            Assert.IsTrue(_source.TryPlace(new PlacementRequest(
                ItemStackBuilder.Of(new ShapedTestAdapter("blade", 1)), 0)));
            Assert.IsTrue(_target.TryPlace(new PlacementRequest(ItemStackBuilder.Unique(1, "a"), 0)));
            Assert.IsTrue(_target.TryPlace(new PlacementRequest(ItemStackBuilder.Unique(1, "b"), 1)));
            Assert.IsTrue(_target.TryPlace(new PlacementRequest(ItemStackBuilder.Unique(1, "c"), 2)));

            var sourceSlot = _source.GetSlot(0);
            var context = new DragContext(new[]
            {
                new DragEntry(sourceSlot.Stack.CreateCopy(), sourceSlot, _source)
            });
            var processor = new InventoryDropProcessor(
                _target.GetSlot(0), _target, new GlobalRuleValidator());

            var report = processor.ProcessDropWithReport(
                context,
                DropRequestPolicy.WithSwap(SwapDisplacementMode.AllCoveredPlacements));

            Assert.IsTrue(report.Success, report.FailureReason);
            Assert.AreEqual("blade", _target.GetPlacementAt(0).Stack.ID);
            Assert.AreEqual(
                3,
                _target.GetPlacementAt(0).CoveredIndices.Count,
                "The incoming footprint must come from the converted adapter, not the source placement");
            Assert.AreEqual("a", _source.GetPlacementAt(0).Stack.ID);
            Assert.AreEqual("b", _source.GetPlacementAt(1).Stack.ID);
            Assert.AreEqual("c", _source.GetPlacementAt(2).Stack.ID);
        }

        /// <summary>
        /// Converts any adapter into this binding's domain, producing a new instance unless the
        /// item is already there — the same shape as the demo converters.
        /// </summary>
        private sealed class DomainConverter : IItemAdapterConverter
        {
            private readonly string _domain;
            private readonly bool _fails;

            public DomainConverter(string domain, bool fails)
            {
                _domain = domain;
                _fails = fails;
            }

            public IItemAdapter TryConvertIncoming(IItemAdapter itemAdapter)
            {
                if (_fails || itemAdapter is not FakeItemAdapter fake)
                    return null;

                return fake.DisplayName == _domain ? fake : new FakeItemAdapter(fake.ItemId, _domain);
            }

            public IItemAdapter TryConvertOutgoing(IItemAdapter itemAdapter) => itemAdapter;
        }

        /// <summary>
        /// Records what the drop rules were shown and what reached the external data, so tests can
        /// compare the two by reference.
        /// </summary>
        private sealed class ConvertingBinding : ListInventoryDataBinding<FakeItemAdapter, FakeItemAdapter>
        {
            public readonly List<IItemAdapter> CanDropSaw = new();
            public readonly List<FakeItemAdapter> Added = new();

            private string _domain;
            private bool _conversionFails;

            public void Configure(string domain, bool conversionFails)
            {
                _domain = domain;
                _conversionFails = conversionFails;
            }

            public void AttachTo(BaseInventory inventory)
            {
                _inventory = inventory;
                inventory.Initialize(this);
            }

            protected override void Awake() { }

            protected override IReadOnlyList<FakeItemAdapter> GetItems() => null;
            protected override FakeItemAdapter CreateAdapter(FakeItemAdapter item) => item;
            protected override void AddToData(FakeItemAdapter adapter) => Added.Add(adapter);
            protected override void RemoveFromData(FakeItemAdapter adapter) { }

            protected override IItemAdapterConverter CreateItemConverter()
                => new DomainConverter(_domain, _conversionFails);

            protected override RuleResult CanDrop(DragContext context, DragEntry entry)
            {
                var adapter = entry.Stack?.PrimaryAdapter;
                if (adapter != null)
                    CanDropSaw.Add(adapter);

                return RuleResult.Success();
            }
        }

        /// <summary>An adapter whose footprint is a single row of <c>width</c> cells.</summary>
        private sealed class ShapedTestAdapter : IItemAdapter, IItemPlacementShapeProvider
        {
            public ShapedTestAdapter(string itemId, int width)
            {
                ItemId = itemId;
                DisplayName = itemId;
                Width = width;
                PlacementShape = new RectPlacementShape(width, 1);
            }

            public string ItemId { get; }
            public string DisplayName { get; }
            public Sprite Icon => null;
            public int Width { get; }
            public IPlacementShape PlacementShape { get; }
        }

        /// <summary>Widens an arriving one-cell item to three cells; outgoing items are untouched.</summary>
        private sealed class ShapeWideningConverter : IItemAdapterConverter
        {
            public IItemAdapter TryConvertIncoming(IItemAdapter itemAdapter)
                => itemAdapter is ShapedTestAdapter shaped && shaped.Width == 1
                    ? new ShapedTestAdapter(shaped.ItemId, 3)
                    : itemAdapter;

            public IItemAdapter TryConvertOutgoing(IItemAdapter itemAdapter) => itemAdapter;
        }

        private sealed class ShapeWideningBinding : ListInventoryDataBinding<ShapedTestAdapter, ShapedTestAdapter>
        {
            public void AttachTo(BaseInventory inventory)
            {
                _inventory = inventory;
                inventory.Initialize(this);
            }

            protected override void Awake() { }

            protected override IReadOnlyList<ShapedTestAdapter> GetItems() => null;
            protected override ShapedTestAdapter CreateAdapter(ShapedTestAdapter item) => item;
            protected override void AddToData(ShapedTestAdapter adapter) { }
            protected override void RemoveFromData(ShapedTestAdapter adapter) { }

            protected override IItemAdapterConverter CreateItemConverter() => new ShapeWideningConverter();
        }
    }
}
