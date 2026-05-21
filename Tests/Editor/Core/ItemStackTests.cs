using NUnit.Framework;
using UDND.Core;

namespace UDND.Tests.Core
{
    [TestFixture]
    public class ItemStackTests
    {
        // ---------- Empty / TryCreate ----------

        [Test]
        public void Empty_HasNoAdaptersAndIsEmpty()
        {
            var stack = ItemStack.Empty();

            Assert.IsTrue(stack.IsEmpty);
            Assert.AreEqual(0, stack.Count);
            Assert.IsNull(stack.PrimaryAdapter);
            Assert.IsNull(stack.ID);
        }

        [Test]
        public void TryCreate_WithNull_ReturnsFalseAndEmptyStack()
        {
            var ok = ItemStack.TryCreate(null, out var stack);

            Assert.IsFalse(ok);
            Assert.IsNotNull(stack);
            Assert.IsTrue(stack.IsEmpty);
        }

        [Test]
        public void TryCreate_WithSingleAdapter_Succeeds()
        {
            var a = new FakeItemAdapter("sword");

            var ok = ItemStack.TryCreate(new[] { a }, out var stack);

            Assert.IsTrue(ok);
            Assert.AreEqual(1, stack.Count);
            Assert.AreSame(a, stack.PrimaryAdapter);
            Assert.AreEqual("sword", stack.ID);
        }

        [Test]
        public void TryCreate_WithMismatchedIds_ReturnsFalse()
        {
            var ok = ItemStack.TryCreate(
                new IItemAdapter[] { new FakeItemAdapter("a"), new FakeItemAdapter("b") },
                out var stack);

            Assert.IsFalse(ok);
            Assert.IsTrue(stack.IsEmpty);
        }

        // ---------- CanStack ----------

        [Test]
        public void CanStack_SameIdAndType_True()
        {
            var stack = ItemStackBuilder.Unique(1, "potion");
            Assert.IsTrue(stack.CanStack(new FakeItemAdapter("potion")));
        }

        [Test]
        public void CanStack_DifferentId_False()
        {
            var stack = ItemStackBuilder.Unique(1, "potion");
            Assert.IsFalse(stack.CanStack(new FakeItemAdapter("sword")));
        }

        [Test]
        public void CanStack_NullAdapter_False()
        {
            var stack = ItemStackBuilder.Unique(1);
            Assert.IsFalse(stack.CanStack(null));
        }

        [Test]
        public void CanStack_EmptyStack_False()
        {
            var stack = ItemStack.Empty();
            Assert.IsFalse(stack.CanStack(new FakeItemAdapter("potion")));
        }

        // ---------- TryAddToStack ----------

        [Test]
        public void TryAddToStack_AppendsCompatibleAdapters()
        {
            var stack = ItemStackBuilder.Unique(2, "gem");

            var ok = stack.TryAddToStack(FakeItemAdapter.Many(3, "gem"));

            Assert.IsTrue(ok);
            Assert.AreEqual(5, stack.Count);
        }

        [Test]
        public void TryAddToStack_RejectsMismatchedAdapter()
        {
            var stack = ItemStackBuilder.Unique(2, "gem");

            var ok = stack.TryAddToStack(new IItemAdapter[] { new FakeItemAdapter("rock") });

            Assert.IsFalse(ok);
            Assert.AreEqual(2, stack.Count, "Stack must be unchanged on rejected add");
        }

        // ---------- RemoveFromStack ----------

        [Test]
        public void RemoveFromStack_PartialFromEnd_LeavesHeadIntact()
        {
            var adapters = FakeItemAdapter.Many(4, "gem");
            ItemStack.TryCreate(adapters, out var stack);

            int removed = stack.RemoveFromStack(2);

            Assert.AreEqual(2, removed);
            Assert.AreEqual(2, stack.Count);
            Assert.AreSame(adapters[0], stack.Adapters[0]);
            Assert.AreSame(adapters[1], stack.Adapters[1]);
        }

        [Test]
        public void RemoveFromStack_OverCount_ClampsToCountAndEmpties()
        {
            var stack = ItemStackBuilder.Unique(3);

            int removed = stack.RemoveFromStack(99);

            Assert.AreEqual(3, removed);
            Assert.IsTrue(stack.IsEmpty);
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void RemoveFromStack_ZeroOrNegative_IsNoOp(int amount)
        {
            var stack = ItemStackBuilder.Unique(3);

            int removed = stack.RemoveFromStack(amount);

            Assert.AreEqual(0, removed);
            Assert.AreEqual(3, stack.Count);
        }

        // ---------- Split ----------

        [Test]
        public void Split_TakesFromEnd_AndShrinksOriginal()
        {
            var adapters = FakeItemAdapter.Many(5, "gem");
            ItemStack.TryCreate(adapters, out var stack);

            var split = stack.Split(2);

            Assert.AreEqual(2, split.Count);
            Assert.AreEqual(3, stack.Count);
            Assert.AreSame(adapters[3], split.Adapters[0]);
            Assert.AreSame(adapters[4], split.Adapters[1]);
        }

        [Test]
        public void Split_All_LeavesOriginalEmpty()
        {
            var stack = ItemStackBuilder.Unique(3);

            var split = stack.Split(3);

            Assert.AreEqual(3, split.Count);
            Assert.IsTrue(stack.IsEmpty);
        }

        [Test]
        public void Split_OverCount_TakesOnlyAvailable()
        {
            var stack = ItemStackBuilder.Unique(2);

            var split = stack.Split(10);

            Assert.AreEqual(2, split.Count);
            Assert.IsTrue(stack.IsEmpty);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Split_ZeroOrNegative_ReturnsEmptyAndLeavesOriginal(int amount)
        {
            var stack = ItemStackBuilder.Unique(3);

            var split = stack.Split(amount);

            Assert.IsTrue(split.IsEmpty);
            Assert.AreEqual(3, stack.Count);
        }

        // ---------- RemoveAdapters ----------

        [Test]
        public void RemoveAdapters_ByReference_RemovesOnlyThoseInstances()
        {
            var adapters = FakeItemAdapter.Many(4, "gem");
            ItemStack.TryCreate(adapters, out var stack);

            int removed = stack.RemoveAdapters(new IItemAdapter[] { adapters[1], adapters[3] });

            Assert.AreEqual(2, removed);
            Assert.AreEqual(2, stack.Count);
            Assert.Contains(adapters[0], (System.Collections.ICollection)stack.Adapters);
            Assert.Contains(adapters[2], (System.Collections.ICollection)stack.Adapters);
        }

        [Test]
        public void RemoveAdapters_UnknownReference_IsIgnored()
        {
            var stack = ItemStackBuilder.Unique(2, "gem");
            var foreign = new FakeItemAdapter("gem");

            int removed = stack.RemoveAdapters(new IItemAdapter[] { foreign });

            Assert.AreEqual(0, removed);
            Assert.AreEqual(2, stack.Count);
        }

        // ---------- Clear ----------

        [Test]
        public void Clear_EmptiesStackAndResetsHeader()
        {
            var stack = ItemStackBuilder.Unique(3);

            stack.Clear();

            Assert.IsTrue(stack.IsEmpty);
            Assert.IsNull(stack.PrimaryAdapter);
            Assert.IsNull(stack.ID);
        }

        // ---------- CreateCopy ----------

        [Test]
        public void CreateCopy_Default_ReturnsIndependentCopyOfAllAdapters()
        {
            var stack = ItemStackBuilder.Unique(3, "gem");

            var copy = stack.CreateCopy();
            stack.RemoveFromStack(3);

            Assert.AreEqual(3, copy.Count);
            Assert.IsTrue(stack.IsEmpty);
        }

        [Test]
        public void CreateCopy_PartialCount_TakesFromEnd()
        {
            var adapters = FakeItemAdapter.Many(4, "gem");
            ItemStack.TryCreate(adapters, out var stack);

            var copy = stack.CreateCopy(2);

            Assert.AreEqual(2, copy.Count);
            Assert.AreSame(adapters[2], copy.Adapters[0]);
            Assert.AreSame(adapters[3], copy.Adapters[1]);
            Assert.AreEqual(4, stack.Count);
        }

        // ---------- TryConvertAdapters ----------

        [Test]
        public void TryConvertAdapters_ReplacesEachAdapter()
        {
            var stack = ItemStackBuilder.Unique(3, "gem");

            var ok = stack.TryConvertAdapters(_ => new FakeItemAdapter("gem", "converted"));

            Assert.IsTrue(ok);
            Assert.AreEqual(3, stack.Count);
            foreach (var a in stack.Adapters)
                Assert.AreEqual("converted", a.DisplayName);
        }

        [Test]
        public void TryConvertAdapters_NullResult_LeavesStackUnchanged()
        {
            var adapters = FakeItemAdapter.Many(3, "gem");
            ItemStack.TryCreate(adapters, out var stack);

            var ok = stack.TryConvertAdapters(a => a == adapters[1] ? null : a);

            Assert.IsFalse(ok);
            Assert.AreEqual(3, stack.Count);
            Assert.AreSame(adapters[0], stack.Adapters[0]);
            Assert.AreSame(adapters[1], stack.Adapters[1]);
            Assert.AreSame(adapters[2], stack.Adapters[2]);
        }
    }
}
