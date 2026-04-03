using DragAndDropSystem.Interaction;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Slots;
using DragAndDropSystem.Tools;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DragAndDropSystem.Core
{
    /// <summary>
    /// Базовый класс для зон дропа (area/zone).
    /// Поддерживает два паттерна:
    /// 1. Простое потребление — переопределить CanAcceptEntry + ProcessEntry,
    ///    удаление из источника выполняется автоматически.
    /// 2. Делегирование — переопределить GetDropProcessor() для возврата
    ///    своего IDropProcessor (напр. InventoryDropProcessor).
    /// </summary>
    public abstract class DropAreaBase : Selectable, IDropTarget, IDropProcessor
    {
        private bool _canAcceptCurrentDrag;
        private Graphic _raycastGraphic;

        protected DragAndDropManager DragManager =>
            DragAndDropManager.IsInstanceExist ? DragAndDropManager.AutoCreateInstance : null;

        // ══════════════════════════════════════════════════════════
        //  Override points
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Попытаться активировать эту зону как текущую цель дропа.
        /// Вызывается из OnPointerEnter.
        /// По умолчанию: проверяет CanAcceptEntry на первом entry и вызывает PushDropTarget.
        /// </summary>
        internal virtual bool TryActivateAsFocusedTarget()
        {
            var context = DragManager.CurrentContext;
            if (context == null || context.Entries.Count == 0)
                return false;

            _canAcceptCurrentDrag = CanAcceptEntry(context.Entries[0]);
            DragManager.PushDropTarget(this);

            Extensions.DragAndDropLog(
                $"<color=cyan>[{GetType().Name}] Entered, canAccept={_canAcceptCurrentDrag}</color>");
            return true;
        }

        /// <summary>
        /// Может ли зона принять данный drag entry?
        /// Вызывается из CanAcceptDrop (на каждом entry) и TryActivateAsFocusedTarget (на первом).
        /// </summary>
        protected virtual bool CanAcceptEntry(DragEntry entry) => true;

        /// <summary>
        /// Обработать предметы из entry. Вернуть true если потребление успешно.
        /// Удаление из источника выполняется автоматически базовым классом.
        /// </summary>
        /// <param name="freshStack">Свежая копия стака из source slot</param>
        /// <param name="entry">Исходный drag entry</param>
        protected virtual bool ProcessEntry(ItemStack freshStack, DragEntry entry) => false;

        /// <summary>
        /// Вызывается при изменении состояния подсветки.
        /// </summary>
        protected virtual void OnHighlightChanged(bool highlighted, bool canAccept) { }

        /// <summary>
        /// Вызывается при деактивации цели (pointer exit, потеря фокуса).
        /// Для сброса внутреннего состояния подкласса.
        /// </summary>
        protected virtual void OnTargetDeactivated() { }

        /// <summary>
        /// Удалять ли предметы из источника после успешного ProcessEntry.
        /// По умолчанию: true (стандартное потребление).
        /// Переопределить в false для зон копирования/предпросмотра.
        /// </summary>
        protected virtual bool RemoveFromSource => true;

        // ══════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════

        protected override void OnValidate()
        {
            base.OnValidate();
            if (_raycastGraphic == null)
                _raycastGraphic = GetComponent<Graphic>();
        }

        protected override void Awake()
        {
            base.Awake();

            if (navigation.mode == Navigation.Mode.None)
            {
                var nav = navigation;
                nav.mode = Navigation.Mode.Automatic;
                navigation = nav;
            }

            if (_raycastGraphic == null)
                _raycastGraphic = GetComponent<Graphic>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SubscribeToStateEvents();
            RefreshInteractionState();
        }

        protected override void OnDisable()
        {
            UnsubscribeFromStateEvents();
            base.OnDisable();
            if (!DragAndDropManager.IsInstanceExist) return;
            if (DragManager != null && DragManager.IsDragging)
                DragManager.PopDropTarget(this);
            OnHighlightChanged(false, true);
        }

        // ══════════════════════════════════════════════════════════
        //  Pointer handling
        // ══════════════════════════════════════════════════════════

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);

            if (DragManager == null || !DragManager.IsDragging)
                return;

            TryActivateAsFocusedTarget();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);

            if (DragManager == null || !DragManager.IsDragging)
                return;

            DragManager.PopDropTarget(this);
            OnTargetDeactivated();
            _canAcceptCurrentDrag = false;

            Extensions.DragAndDropLog($"<color=cyan>[{GetType().Name}] Exited</color>");
        }

        // ══════════════════════════════════════════════════════════
        //  IDropTarget
        // ══════════════════════════════════════════════════════════

        public virtual ISlot GetTargetSlot() => null;

        public virtual IDropProcessor GetDropProcessor() => this;

        public void OnBecomeActiveTarget()
        {
            OnHighlightChanged(true, _canAcceptCurrentDrag);
        }

        public void OnBecomeInactiveTarget()
        {
            OnHighlightChanged(false, true);
        }

        // ══════════════════════════════════════════════════════════
        //  IDropProcessor (default: шаблонные методы + авто source removal)
        // ══════════════════════════════════════════════════════════

        public virtual bool CanAcceptDrop(DragContext context)
        {
            if (context == null || context.Entries.Count == 0)
                return false;

            for (int i = 0; i < context.Entries.Count; i++)
            {
                if (!CanAcceptEntry(context.Entries[i]))
                {
                    Extensions.DragAndDropLog(
                        $"<color=cyan>[{GetType().Name}] CanAcceptDrop: false (entry {i} rejected)</color>");
                    return false;
                }
            }

            Extensions.DragAndDropLog($"<color=cyan>[{GetType().Name}] CanAcceptDrop: true</color>");
            return true;
        }

        public virtual DropResult ProcessDrop(DragContext context)
        {
            if (context == null || context.Entries.Count == 0)
                return DropResult.Failed("Invalid drag context");

            int totalProcessed = 0;
            int succeededEntries = 0;
            int failedEntries = 0;
            IItemAdapter lastAdapter = null;

            for (int i = 0; i < context.Entries.Count; i++)
            {
                var entry = context.Entries[i];
                var stack = entry.Stack;
                var sourceSlot = entry.SourceSlot;

                if (stack == null || stack.IsEmpty)
                {
                    failedEntries++;
                    continue;
                }

                var freshStack = sourceSlot?.Stack?.CreateCopy(stack.Count);
                if (freshStack == null || freshStack.IsEmpty)
                    freshStack = stack.CreateCopy();

                if (freshStack == null || freshStack.IsEmpty)
                {
                    failedEntries++;
                    continue;
                }

                if (!ProcessEntry(freshStack, entry))
                {
                    failedEntries++;
                    continue;
                }

                // Auto source removal
                if (RemoveFromSource && sourceSlot?.Inventory is UniversalInventory sourceUniversal)
                {
                    int removed = sourceUniversal.RemoveItemsFromSlot(sourceSlot, freshStack);
                    Extensions.DragAndDropLog(
                        $"<color=green>[{GetType().Name}] Removed {removed} items from source slot {sourceSlot.Index}</color>");
                }

                totalProcessed += freshStack.Count;
                lastAdapter = freshStack.PrimaryAdapter;
                succeededEntries++;
            }

            if (totalProcessed > 0)
            {
                return context.Entries.Count > 1
                    ? DropResult.SucceededBatch(
                        itemAdapter: lastAdapter,
                        amount: totalProcessed,
                        targetSlot: null,
                        targetInventory: null,
                        succeededEntries: succeededEntries,
                        failedEntries: failedEntries,
                        isPartialTransfer: failedEntries > 0)
                    : DropResult.Succeeded(
                        itemAdapter: lastAdapter,
                        amount: totalProcessed,
                        targetSlot: null,
                        targetInventory: null);
            }

            return DropResult.Failed($"[{GetType().Name}] Failed to process any entries");
        }

        // ══════════════════════════════════════════════════════════
        //  State management
        // ══════════════════════════════════════════════════════════

        private void SubscribeToStateEvents()
        {
            DragAndDropManager.OnDragStarted += HandleDragStateChanged;
            DragAndDropManager.OnDragCancelled += HandleDragStateChanged;
            DragAndDropManager.OnDropCompleted += HandleDragStateChanged;
            DragAndDropManager.OnDragEnded += HandleDragEnded;

            InputModalityTracker.OnNavigationModeChanged += HandleNavigationModeChanged;
        }

        private void UnsubscribeFromStateEvents()
        {
            DragAndDropManager.OnDragStarted -= HandleDragStateChanged;
            DragAndDropManager.OnDragCancelled -= HandleDragStateChanged;
            DragAndDropManager.OnDropCompleted -= HandleDragStateChanged;
            DragAndDropManager.OnDragEnded -= HandleDragEnded;

            InputModalityTracker.OnNavigationModeChanged -= HandleNavigationModeChanged;
        }

        private void HandleDragStateChanged(DragContext _) => RefreshInteractionState();
        private void HandleDragEnded() => RefreshInteractionState();
        private void HandleNavigationModeChanged(bool _) => RefreshInteractionState();

        private void RefreshInteractionState()
        {
            if (_raycastGraphic != null)
                _raycastGraphic.raycastTarget = DragManager != null && DragManager.IsDragging;

            bool shouldBeInteractable = DragManager != null && DragManager.IsDragging;
            if (interactable == shouldBeInteractable)
                return;

            interactable = shouldBeInteractable;

            if (!shouldBeInteractable &&
                EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }
}
