using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Tools;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DragAndDropSystem.Slots
{
    public class DragDropEventListener : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IPointerEnterHandler, IPointerExitHandler, IDropTarget
    {
        private DragAndDropManager _dragManager => DragAndDropManager.Instance;
        [SerializeField] private UniversalSlot _slot;

        public UniversalSlot Slot => _slot;
        
        // Quick click detection
        private bool _pointerDownForDrag;
        private float _pointerDownTime;
        private Vector2 _pointerDownPosition;
        private Coroutine _dragStartCoroutine;
        
        private void OnDisable()
        {
            // Останавливаем корутину при отключении слота
            if (_dragStartCoroutine != null)
            {
                StopCoroutine(_dragStartCoroutine);
                _dragStartCoroutine = null;
            }
            _pointerDownForDrag = false;

            // Удаляем себя из стека drop targets если находимся в процессе драга
            if (!DragAndDropManager.IsInstanceExist) return;
            
            if (_dragManager != null && _dragManager.IsDragging)
            {
                _dragManager.PopDropTarget(this);
            }

            if (_slot != null && _slot.Inventory is UniversalInventory universalInventory)
            {
                universalInventory.NotifyPointerExit(_slot);
            }
        }
        
        // Event Handlers
        public void OnPointerDown(PointerEventData eventData)
        {
            if (_slot.IsEmpty)
                return;

            // Проверяем интерактивность слота (для фильтрации)
            if (!_slot.IsInteractable)
            {
                Extentions.DragAndDropLog($"OnPointerDown blocked - slot {name} is not interactable (filtered)");
                return;
            }

            Extentions.DragAndDropLog("OnPointerDown called on slot " + name + " with button " + eventData.button);

            // Проверяем, настроен ли автоперенос для этого инвентаря
            if (_slot.Inventory is UniversalInventory universalInventory)
            {
                universalInventory.NotifySlotInteracted(_slot);

                // ЛКМ - запоминаем время и позицию для определения быстрого клика
                if (eventData.button == PointerEventData.InputButton.Left)
                {
                    _pointerDownForDrag = true;
                    _pointerDownTime = Time.time;
                    _pointerDownPosition = eventData.position;

                    // Запускаем корутину для отложенного старта драга
                    if (_dragStartCoroutine != null)
                        StopCoroutine(_dragStartCoroutine);
                    _dragStartCoroutine = StartCoroutine(DelayedDragStart());
                }
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Extentions.DragAndDropLog("OnBeginDrag called on slot " + name);
            // Unity автоматически вызывает это когда мышь начинает двигаться
            // Если это ЛКМ и мы ждем начала драга
            if (_pointerDownForDrag && eventData.button == PointerEventData.InputButton.Left)
            {
                _pointerDownForDrag = false;

                // Отменяем корутину - драг начинается сейчас
                if (_dragStartCoroutine != null)
                {
                    StopCoroutine(_dragStartCoroutine);
                    _dragStartCoroutine = null;
                }

                _dragManager.StartDrag(_slot);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // Только для ЛКМ
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            // Отменяем корутину отложенного драга
            if (_dragStartCoroutine != null)
            {
                StopCoroutine(_dragStartCoroutine);
                _dragStartCoroutine = null;
            }
        }

        private System.Collections.IEnumerator DelayedDragStart()
        {
            // Ждем threshold времени
            var mousePosition = Input.mousePosition;
            var threshold = _dragManager.QuickClickTimeThreshold;
            var thresholdDistance = _dragManager.QuickClickDistanceThreshold;
            while (threshold > 0)
            {
                // Если мышь сдвинулась слишком далеко, отменяем
                if (Vector2.Distance(mousePosition, Input.mousePosition) > thresholdDistance)
                {
                    _pointerDownForDrag = false;
                    _dragStartCoroutine = null;

                    Extentions.DragAndDropLog($"<color=yellow>Delayed drag start after move</color>");
                    _dragManager.StartDrag(_slot);
                    yield break;
                }
                threshold -= Time.deltaTime;
                yield return null;
            }

            // Если за это время не было OnPointerUp или OnBeginDrag
            if (_pointerDownForDrag)
            {
                _pointerDownForDrag = false;
                _dragStartCoroutine = null;

                Extentions.DragAndDropLog($"<color=yellow>Delayed drag start after {_dragManager.QuickClickTimeThreshold}s</color>");
                _dragManager.StartDrag(_slot);
            }
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            Extentions.DragAndDropLog($"Pointer entered slot {name}");
            if (_slot != null && _slot.Inventory is UniversalInventory universalInventory)
            {
                universalInventory.NotifyPointerEnter(_slot);
            }

            if (_dragManager.IsDragging)
            {
                // Не регистрируем как цель дропа если слот неинтерактивен (отфильтрован)
                if (!_slot.IsInteractable)
                {
                    Extentions.DragAndDropLog($"Slot {name} is not interactable - not registering as drop target");
                    return;
                }

                _dragManager.PushDropTarget(this);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Extentions.DragAndDropLog($"Pointer exited slot {name}");
            if (_slot != null && _slot.Inventory is UniversalInventory universalInventory)
            {
                universalInventory.NotifyPointerExit(_slot);
            }

            if (_dragManager.IsDragging)
            {
                _dragManager.PopDropTarget(this);
            }
        }

        // ===== IDropTarget Implementation =====

        public ISlot GetTargetSlot() => _slot;

        public IDropProcessor GetDropProcessor()
        {
            System.Func<InventorySwapContext, bool> swapAttempting = _dragManager != null
                ? _dragManager.RaiseSwapAttempting
                : null;
            System.Action<InventorySwapContext> swapCompleted = _dragManager != null
                ? _dragManager.RaiseSwapCompleted
                : null;

            return new InventoryDropProcessor(
                _slot,
                _slot?.Inventory,
                _dragManager?.GlobalRules,
                _dragManager?.TransferService,
                policyOverride: null,
                swapAttempting: swapAttempting,
                swapCompleted: swapCompleted);
        }

        public void OnBecomeActiveTarget()
        {
            // Подсвечиваем слот когда становимся активной целью
            if (_slot is UniversalSlot universalSlot)
            {
                universalSlot.Highlight(true);
            }
        }

        public void OnBecomeInactiveTarget()
        {
            // Снимаем подсветку когда перестаём быть активной целью
            if (_slot is UniversalSlot universalSlot)
            {
                universalSlot.Highlight(false);
            }
        }
    }
}
