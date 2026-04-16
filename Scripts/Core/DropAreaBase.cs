using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UniversalDragAndDrop.Interaction;
using UniversalDragAndDrop.Inventories;
using UniversalDragAndDrop.Slots;
using UniversalDragAndDrop.Tools;

namespace UniversalDragAndDrop.Core
{
    /// <summary>
    /// Base class for drop areas/zones.
    /// Supports two patterns:
    /// 1. Simple consumption: override CanAcceptEntry + ProcessEntry,
    ///    source removal is handled automatically.
    /// 2. Delegation: override GetDropProcessor() to return
    ///    your own IDropProcessor (for example, InventoryDropProcessor).
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
        /// Try to activate this zone as the current drop target.
        /// Called from OnPointerEnter.
        /// By default, checks CanAcceptEntry on the first entry and calls PushDropTarget.
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
        /// Can this zone accept the given drag entry?
        /// Called from CanAcceptDrop (for each entry) and TryActivateAsFocusedTarget (for the first one).
        /// </summary>
        protected virtual bool CanAcceptEntry(DragEntry entry) => true;

        /// <summary>
        /// Process items from the entry. Return true if consumption succeeds.
        /// Source removal is handled automatically by the base class.
        /// </summary>
        /// <param name="stack">Fresh copy of the stack from the source slot</param>
        /// <param name="entry">Original drag entry</param>
        protected virtual bool ProcessEntry(ItemStack stack, DragEntry entry) => false;

        /// <summary>
        /// Called when highlight state changes.
        /// </summary>
        protected virtual void OnHighlightChanged(bool highlighted, bool canAccept) { }

        /// <summary>
        /// Called when the target is deactivated (pointer exit, focus loss).
        /// Used to reset subclass internal state.
        /// </summary>
        protected virtual void OnTargetDeactivated() { }

        /// <summary>
        /// Whether to remove items from the source after ProcessEntry succeeds.
        /// Default: true (standard consumption).
        /// Override to false for copy/preview zones.
        /// </summary>
        protected virtual bool RemoveFromSource => true;

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

        public virtual BaseSlot GetTargetSlot() => null;

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
        //  IDropProcessor (default: template methods + automatic source removal)
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
                var sourceSlot = entry.SourceBaseSlot;

                if (stack == null || stack.IsEmpty)
                {
                    failedEntries++;
                    continue;
                }

                var freshStack = stack.CreateCopy();
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
                        targetBaseSlot: null,
                        targetInventory: null,
                        succeededEntries: succeededEntries,
                        failedEntries: failedEntries,
                        isPartialTransfer: failedEntries > 0)
                    : DropResult.Succeeded(
                        itemAdapter: lastAdapter,
                        amount: totalProcessed,
                        targetBaseSlot: null,
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

#if DNDS_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            InputModalityTracker.OnNavigationModeChanged += HandleNavigationModeChanged;
#endif
        }

        private void UnsubscribeFromStateEvents()
        {
            DragAndDropManager.OnDragStarted -= HandleDragStateChanged;
            DragAndDropManager.OnDragCancelled -= HandleDragStateChanged;
            DragAndDropManager.OnDropCompleted -= HandleDragStateChanged;
            DragAndDropManager.OnDragEnded -= HandleDragEnded;

#if DNDS_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            InputModalityTracker.OnNavigationModeChanged -= HandleNavigationModeChanged;
#endif
        }

        private void HandleDragStateChanged(DragContext _) => RefreshInteractionState();
        private void HandleDragEnded() => RefreshInteractionState();
#if DNDS_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
        private void HandleNavigationModeChanged(bool _) => RefreshInteractionState();
#endif

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
