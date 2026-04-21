using System.Collections.Generic;
using UnityEngine;
using UniversalDragAndDrop.Inventories;
using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.Tests
{
    /// <summary>
    /// Creates lists of BaseSlot-backed GameObjects for strategy-level tests.
    /// Always call Dispose in [TearDown] to avoid leaking GameObjects between tests.
    /// With a null IInventory, PassesRules short-circuits to true, so slot rules
    /// never interfere with strategy behavior under test.
    /// </summary>
    public static class TestSlotFactory
    {
        public static List<BaseSlot> CreateSlots(int count, IInventory inventory = null)
        {
            var list = new List<BaseSlot>(count);
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"TestSlot_{i}");
                var slot = go.AddComponent<TestSlot>();
                slot.Initialize(i, inventory);
                list.Add(slot);
            }
            return list;
        }

        public static BaseSlot CreatePrefab(string name = "TestSlotPrefab")
        {
            var go = new GameObject(name);
            return go.AddComponent<TestSlot>();
        }

        public static void Dispose(List<BaseSlot> slots)
        {
            if (slots == null) return;
            foreach (var s in slots)
            {
                if (s != null && s.gameObject != null)
                    Object.DestroyImmediate(s.gameObject);
            }
            slots.Clear();
        }

        public static void Dispose(BaseSlot slot)
        {
            if (slot != null && slot.gameObject != null)
                Object.DestroyImmediate(slot.gameObject);
        }
    }
}
