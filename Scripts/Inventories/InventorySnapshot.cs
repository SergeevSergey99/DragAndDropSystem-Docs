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
    /// Снимок одного слота. Хранит полный список адаптеров — каждый экземпляр предмета отдельно.
    /// </summary>
    public struct InventorySlotState
    {
        public InventorySlotState(IReadOnlyList<IItemAdapter> adapters)
        {
            Adapters = adapters != null ? new List<IItemAdapter>(adapters) : new List<IItemAdapter>();
        }

        public List<IItemAdapter> Adapters;

        // Свойства совместимости — используются в логике сравнения снапшотов
        public IItemAdapter ItemAdapter => Adapters != null && Adapters.Count > 0 ? Adapters[0] : null;
        public int Count => Adapters?.Count ?? 0;

        public bool IsEmpty => Adapters == null || Adapters.Count == 0;
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
