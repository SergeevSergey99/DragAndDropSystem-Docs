using System;
using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Slots;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DragAndDropSystem.Interaction
{
    /// <summary>
    /// Интерфейс для действий, поддерживающих превью количества во время удержания.
    /// InputEventRouter обнаруживает его в биндингах и тикает превью каждый кадр.
    /// </summary>
    public interface IHoldPreviewable
    {
        int ComputePreviewAmount(ISlot slot, float holdDuration);
    }

    /// <summary>
    /// Drag action, привязанный к BeginDrag фазе.
    /// Количество предметов для перетаскивания увеличивается по мере удержания нажатия.
    /// Чем дольше удерживаешь перед началом движения — тем больше предметов захватишь.
    /// </summary>
    [Serializable]
    public sealed class ProgressiveDragAction : AssetSafeSlotInteractionAction, IHoldPreviewable
    {
        [SerializeField, Tooltip("Начальное количество предметов при мгновенном начале драга")]
        private int _startAmount = 1;

        [SerializeField, Min(0.01f), Tooltip("Интервал (секунды) между инкрементами количества")]
        private float _intervalSeconds = 0.3f;

        [SerializeField, Min(0), Tooltip("Максимальное количество (0 = без ограничения, берётся весь стак)")]
        private int _maxAmount;

        public override bool IsDragBinding() => true;

        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (DragAndDropManager.Instance.IsDragging)
                return false;

            var slot = adapter?.Slot;
            return slot != null && !slot.IsEmpty && slot.IsInteractable;
        }

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            var slot = adapter?.Slot;
            if (slot == null || slot.IsEmpty || !slot.IsInteractable)
                return ActionResult.Failed("Slot is empty or not interactable");

            int amount = ComputePreviewAmount(slot, InputEventRouter.Instance.GetHoldDuration(inventory));
            var policy = new DragRequestPolicy(DragAmount.Custom, amount);

            return DragAndDropManager.Instance.StartDrag(slot, policy)
                ? ActionResult.Succeeded()
                : ActionResult.Failed("Start drag failed");
        }

        public int ComputePreviewAmount(ISlot slot, float holdDuration)
        {
            if (slot == null || slot.IsEmpty)
                return 0;

            int amount = _startAmount + Mathf.FloorToInt(holdDuration / _intervalSeconds);

            int stackCount = slot.Stack.Count;
            int cap = _maxAmount > 0 ? Mathf.Min(_maxAmount, stackCount) : stackCount;
            return Mathf.Clamp(amount, 1, cap);
        }
    }
}
