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
        public InventorySlotState(IReadOnlyList<IItemAdapter> adapters)
        {
            Adapters = adapters ?? System.Array.Empty<IItemAdapter>();
        }

        public IReadOnlyList<IItemAdapter> Adapters;

        public IItemAdapter ItemAdapter => Adapters.Count > 0 ? Adapters[0] : null;
        public int Count => Adapters.Count;

        public bool IsEmpty => Adapters == null || Adapters.Count <= 0;
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
