using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Снимок состояния инвентаря. Хранит содержимое каждого слота и их количество.
    /// </summary>
    public sealed class InventorySnapshot
    {
        public InventorySnapshot(List<InventorySlotState> slots)
        {
            Slots = slots ?? new List<InventorySlotState>();
        }

        public List<InventorySlotState> Slots { get; }
    }

    /// <summary>
    /// Снимок одного слота.
    /// </summary>
    public struct InventorySlotState
    {
        public InventorySlotState(IItemAdapter itemAdapter, int count)
        {
            ItemAdapter = itemAdapter;
            Count = count;
        }

        public IItemAdapter ItemAdapter;
        public int Count;

        public bool IsEmpty => ItemAdapter == null || Count <= 0;
    }

    /// <summary>
    /// Интерфейс для инвентарей, умеющих делать снапшоты состояния (для отката операций).
    /// </summary>
    public interface IInventorySnapshotProvider
    {
        InventorySnapshot CaptureSnapshot();
        void RestoreSnapshot(InventorySnapshot snapshot);
    }
}
