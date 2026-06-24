using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UDND.Core;
using UDND.DataBinding;

namespace UDND.Tests.DataBinding
{
    /// <summary>
    /// Regression tests for <see cref="PlacementInventoryDataBinding{TData,TAdapter}"/>.
    /// The template used to commit only the stack's PrimaryAdapter, silently dropping the
    /// other adapter instances of a multi-item placement. These tests assert that every
    /// distinct adapter instance reaches the external data on add and on remove.
    /// </summary>
    [TestFixture]
    public class PlacementInventoryDataBindingTests
    {
        private GameObject _go;
        private RecordingPlacementBinding _binding;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject(nameof(PlacementInventoryDataBindingTests));
            _binding = _go.AddComponent<RecordingPlacementBinding>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void OnItemAdded_MultipleDistinctInstances_AllForwardedToData()
        {
            var adapters = FakeItemAdapter.Many(3, "gem");
            var stack = ItemStackBuilder.Of(adapters);
            var context = new InventoryItemEventContext(stack, slotIndex: 0);

            _binding.TestAdd(context);

            Assert.AreEqual(3, _binding.Added.Count,
                "Every adapter instance in the placement must be committed, not just the primary one");
            CollectionAssert.AreEqual(adapters, _binding.Added,
                "Committed data must preserve the exact distinct instances in order");
        }

        [Test]
        public void OnItemRemoved_MultipleDistinctInstances_AllForwardedToData()
        {
            var adapters = FakeItemAdapter.Many(3, "gem");
            var stack = ItemStackBuilder.Of(adapters);
            var context = new InventoryItemEventContext(stack, slotIndex: 0);

            _binding.TestRemove(context);

            Assert.AreEqual(3, _binding.Removed.Count,
                "Every adapter instance in the placement must be removed, not just the primary one");
            CollectionAssert.AreEqual(adapters, _binding.Removed,
                "Removed data must preserve the exact distinct instances in order");
        }

        [Test]
        public void OnItemAdded_NoAnchor_StillForwardsEveryInstance()
        {
            // Slot inventories collapse a placement into a single slot; the anchor may be -1.
            // The commit must still carry every instance.
            var adapters = FakeItemAdapter.Many(2, "potion");
            var stack = ItemStackBuilder.Of(adapters);
            var context = new InventoryItemEventContext(stack, slotIndex: -1);

            _binding.TestAdd(context);

            CollectionAssert.AreEqual(adapters, _binding.Added);
        }

        /// <summary>
        /// Minimal concrete binding. TData and TAdapter are both FakeItemAdapter so
        /// CreateAdapter/ExtractData are identity, and committed data can be compared by reference.
        /// </summary>
        private sealed class RecordingPlacementBinding
            : PlacementInventoryDataBinding<FakeItemAdapter, FakeItemAdapter>
        {
            public readonly List<FakeItemAdapter> Added = new();
            public readonly List<FakeItemAdapter> Removed = new();

            protected override void Awake() { }

            protected override IEnumerable<PlacementData<FakeItemAdapter>> GetPlacements()
                => Array.Empty<PlacementData<FakeItemAdapter>>();

            protected override FakeItemAdapter CreateAdapter(FakeItemAdapter item) => item;
            protected override FakeItemAdapter ExtractData(FakeItemAdapter adapter) => adapter;

            protected override void AddPlacementData(PlacementCommitContext<FakeItemAdapter, FakeItemAdapter> context)
                => Added.AddRange(context.Data);

            protected override void RemovePlacementData(PlacementCommitContext<FakeItemAdapter, FakeItemAdapter> context)
                => Removed.AddRange(context.Data);

            public void TestAdd(InventoryItemEventContext context) => OnItemAddedToUI(context);
            public void TestRemove(InventoryItemEventContext context) => OnItemRemovedFromUI(context);
        }
    }
}
