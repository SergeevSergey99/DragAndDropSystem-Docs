using System;
using System.Collections.Generic;
using UnityEngine;
using UDND.Core;
using UDND.Inventories;
using UDND.Rules;
using UDND.Slots;
using UDND.Tools;
using UDND.Tools.Inspector;

namespace UDND.DataBinding
{
    /// <summary>
    /// Base class that connects an inventory UI with external data (for example, GameManager)
    /// Uses the Adapter pattern for two-way synchronization:
    /// - UI changes -> external data (through OnItemAddedToUI / OnItemRemovedFromUI)
    /// - External data -> UI (through SyncToUI)
    /// </summary>
    public abstract class InventoryDataBindingBase : MonoBehaviour
    {
        [Header("UI Reference")]
        [SerializeField, Tooltip("Inventory UI representation")]
        protected BaseInventory _inventory;

        /*
        [FoldoutGroup("Rules", false)]
        [SerializeField, HideLabel]
        [Tooltip("Rules for validating whether items can be transferred into this inventory")]
        private InventoryRuleValidator _ruleValidator = new();
        */

        private int _syncDepth = 0;

        /// <summary>
        /// Whether we are currently in synchronization mode (Data -> UI).
        /// When true, OnItemAddedToUI/OnItemRemovedFromUI events are not invoked.
        /// </summary>
        protected bool IsSyncing => _syncDepth > 0;

        /// <summary>
        /// Begin a synchronization scope. Use with using:
        /// using (BeginSync()) { ... }
        /// Exception-safe and supports nesting.
        /// </summary>
        protected SyncScope BeginSync() => new SyncScope(this);

        protected readonly struct SyncScope : IDisposable
        {
            private readonly InventoryDataBindingBase _owner;

            public SyncScope(InventoryDataBindingBase owner)
            {
                _owner = owner;
                _owner._syncDepth++;
            }

            public void Dispose()
            {
                _owner._syncDepth--;
            }
        }

        protected virtual void Awake()
        {
            if (_inventory == null)
            {
                Debug.LogError($"[{GetType().Name}] Inventory reference is null. Please assign it in the Inspector.", this);
                return;
            }

            _inventory.Initialize(this);
        }

        /*private void OnValidate()
        {
            // Sort rules when values change in the Inspector
            _ruleValidator?.OnValidate();
        }*/

        protected virtual void OnEnable()
        {
            if (_inventory == null)
                return;

            if (_inventory != null)
            {
                _inventory.OnSwapAttempting += HandleSwapAttempting;
                _inventory.OnSwapCompleted += HandleSwapCompleted;
            }

            UDNDEvents.OnDropCompleted += HandleDropCompleted;
            ReloadUI();
        }

        protected virtual void OnDisable()
        {
            if (_inventory != null)
            {
                _inventory.OnSwapAttempting -= HandleSwapAttempting;
                _inventory.OnSwapCompleted -= HandleSwapCompleted;
            }

            UDNDEvents.OnDropCompleted -= HandleDropCompleted;
        }

        /// <summary>
        /// Swap-attempt event handler (inventory-scoped, no filtering needed)
        /// </summary>
        private void HandleSwapAttempting(InventorySwapContext context)
        {
            if (context.Cancel)
                return;

            var result = CanSwap(context);
            if (!result.IsValid)
            {
                Extensions.DragAndDropLog($"[{GetType().Name}] CanSwap rejected: {result.FailureReason}");
                context.Cancel = true;
            }
        }

        /// <summary>
        /// Successful swap event handler (inventory-scoped, no filtering needed)
        /// </summary>
        private void HandleSwapCompleted(InventorySwapContext context)
        {
            OnSwapCompleted(context);
        }

        private void HandleDropCompleted(DragContext context)
        {
            if (context == null)
                return;

            bool isSource = false;
            bool isTarget = ReferenceEquals(context.TargetInventory, Inventory);

            for (int i = 0; i < context.Entries.Count; i++)
            {
                if (ReferenceEquals(context.Entries[i].SourceInventory, Inventory))
                {
                    isSource = true;
                    break;
                }
            }

            if (isSource)
                OnDropCompletedFrom(context);
            if (isTarget)
                OnDropCompletedTo(context);
        }

        /// <summary>
        /// Called directly by the inventory when an item is added.
        /// </summary>
        internal void HandleItemAdded(InventoryItemEventContext context)
        {
            if (IsSyncing) return;

            Extensions.DragAndDropLog($"[{GetType().Name}] PrimaryAdapter added: {context.Stack.PrimaryAdapter?.DisplayName} x{context.Stack.Count} (from: {context.SourceInventory?.GetType().Name ?? "null"})");
            OnItemAddedToUI(context);
        }

        /// <summary>
        /// Called directly by the inventory when an item is removed.
        /// </summary>
        internal void HandleItemRemoved(InventoryItemEventContext context)
        {
            if (IsSyncing) return;

            Extensions.DragAndDropLog($"[{GetType().Name}] PrimaryAdapter removed: {context.Stack.PrimaryAdapter?.DisplayName} x{context.Stack.Count} (to: {context.TargetInventory?.GetType().Name ?? "null"})");
            OnItemRemovedFromUI(context);
        }

        #region Abstract Methods

        /// <summary>
        /// Called when an item is added in the UI
        /// Update external data here (for example, add it to a GameManager list)
        /// </summary>
        /// <param name="context">Event arguments containing item, amount, and transfer context (SourceInventory, TargetInventory)</param>
        protected abstract void OnItemAddedToUI(InventoryItemEventContext context);

        /// <summary>
        /// Called when an item is removed in the UI
        /// Update external data here (for example, remove it from a GameManager list)
        /// </summary>
        /// <param name="context">Event arguments containing item, amount, and transfer context (SourceInventory, TargetInventory)</param>
        protected abstract void OnItemRemovedFromUI(InventoryItemEventContext context);

        /// <summary>
        /// Called after a drop operation completes if this inventory was the source.
        /// Useful for deferred handling (for example, consuming crafting ingredients).
        /// </summary>
        protected virtual void OnDropCompletedFrom(DragContext context) { }

        /// <summary>
        /// Called after a drop operation completes if this inventory was the target.
        /// </summary>
        protected virtual void OnDropCompletedTo(DragContext context) { }

        /// <summary>
        /// Synchronize the UI with external data.
        /// Clears the UI and calls OnReloadUI() inside a sync scope.
        /// </summary>
        public void ReloadUI()
        {
            if (!Application.isPlaying || Inventory == null) return;

            using (BeginSync())
            {
                Inventory.ClearAll();
                OnReloadUI();
                Inventory.UpdateAllVisuals();
                Inventory.NotifyContentRefreshed();
            }
        }

        /// <summary>
        /// Populate the UI using data from the external source.
        /// Called inside a sync scope, so events are suppressed and the UI is already cleared.
        /// Use AddToUIQuiet() to add items.
        /// </summary>
        protected abstract void OnReloadUI();

        #endregion

        #region Helper Methods

        /// <summary>
        /// Clear the UI inventory
        /// </summary>
        protected void ClearUI()
        {
            if (Inventory != null)
            {
                using (BeginSync())
                    Inventory.ClearAll();
            }
        }

        /// <summary>
        /// Add items to the UI without triggering events.
        /// Each list element must be a separate adapter instance.
        /// </summary>
        protected void AddToUIQuiet(IEnumerable<IItemAdapter> adapters, int targetSlotIndex = -1)
        {
            if (Inventory == null || adapters == null)
                return;

            if (!ItemStack.TryCreate(adapters, out var stack))
                return;

            if (targetSlotIndex < 0)
            {
                Inventory.TryAddStackQuiet(stack, -1);
                return;
            }

            var slot = Inventory.GetSlot(targetSlotIndex);
            if (slot != null)
                slot.SetStack(stack);
        }

        /// <summary>
        /// Add multiple items to the UI through a factory that creates a unique adapter for each stack element.
        /// </summary>
        protected void AddToUIQuiet(Func<IItemAdapter> createAdapter, int count, int targetSlotIndex = -1)
        {
            if (Inventory == null || createAdapter == null || count <= 0)
                return;

            var adapters = new List<IItemAdapter>(count);
            for (int i = 0; i < count; i++)
            {
                var adapter = createAdapter();
                if (adapter == null)
                    return;

                if (ContainsReference(adapters, adapter))
                {
                    Debug.LogWarning($"[{GetType().Name}] AddToUIQuiet received the same adapter instance more than once. Aborting stack creation.");
                    return;
                }

                adapters.Add(adapter);
            }

            AddToUIQuiet(adapters, targetSlotIndex);
        }

        private static bool ContainsReference(List<IItemAdapter> adapters, IItemAdapter candidate)
        {
            foreach (var adapter in adapters)
            {
                if (ReferenceEquals(adapter, candidate))
                    return true;
            }

            return false;
        }

        #endregion

        #region Public API

        public BaseInventory Inventory => _inventory;

        /// <summary>
        /// Force UI synchronization (can be called from external code)
        /// </summary>
        [Button("Force Sync To UI")]
        public void ForceSyncToUI()
        {
            ReloadUI();
        }

        /// <summary>
        /// Check DataBinding drag-start conditions
        /// </summary>
        internal RuleResult ValidateStartDragRules(DragContext context, DragEntry entry)
        {
            /*var rulesResult = _ruleValidator.ValidateStartDrag(context, entry);
            if (!rulesResult.IsValid)
                return rulesResult;*/

            return CanStartDrag(context, entry);
        }

        internal bool CheckOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedBaseSlot)
            => CanHandleOccupiedSlotDrop(entry, occupiedBaseSlot);

        internal bool DoOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedBaseSlot)
            => ExecuteOccupiedSlotDrop(entry, occupiedBaseSlot);

        /// <summary>
        /// Check DataBinding drop conditions
        /// </summary>
        internal RuleResult ValidateDropRules(DragContext context, DragEntry entry)
        {
            /*var rulesResult = _ruleValidator.ValidateDrop(context, entry);
            if (!rulesResult.IsValid)
                return rulesResult;*/

            return CanDrop(context, entry);
        }

        #endregion

        #region Virtual Methods for Transfer Validation

        private IItemAdapterConverter _itemConverter;
        public IItemAdapterConverter ItemConverter => _itemConverter ??= CreateItemConverter();
        
        /// <summary>
        /// Create a converter used to transform items when entering/leaving the inventory.
        /// Return null if conversion is not needed.
        /// </summary>
        protected virtual IItemAdapterConverter CreateItemConverter() => IdentityItemAdapterConverter.Instance;

        /// <summary>
        /// Check whether dragging can start from this inventory
        /// Override this method to add custom validation logic
        /// </summary>
        /// <param name="context">Drag context</param>
        /// <returns>Validation result</returns>
        protected virtual RuleResult CanStartDrag(DragContext context, DragEntry entry)
        {
            // Allowed by default
            return RuleResult.Success();
        }

        /// <summary>
        /// Check whether an item can be dropped into this inventory
        /// Override this method to add custom validation logic
        /// </summary>
        /// <param name="context">Drag context</param>
        /// <param name="entry">Specific drag entry</param>
        /// <returns>Validation result</returns>
        protected virtual RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            // Allowed by default
            return RuleResult.Success();
        }

        /// <summary>
        /// Check whether a swap can be performed
        /// Override this method to add custom swap validation logic
        ///
        /// NOTE: Base validation (CanStartDrag and CanDrop for both directions)
        /// has already been performed in DragAndDropManager.ValidateSwap()
        /// This method is intended for additional logic specific to your DataBinding
        /// </summary>
        /// <param name="args">Swap event arguments containing both stacks and slots</param>
        /// <returns>Validation result. Returning Failure cancels the swap</returns>
        protected virtual RuleResult CanSwap(InventorySwapContext args)
        {
            // Allowed by default (base validation has already been performed)
            return RuleResult.Success();
        }

        /// <summary>
        /// Called after a swap completes successfully
        /// Override to add custom swap handling logic
        ///
        /// For example:
        /// - Updating external data (if the swap happened between different DataBindings)
        /// - Logging the swap
        /// - Special equipment handling (if a swap occurred between inventory and an equipment slot)
        /// </summary>
        /// <param name="args">Swap event arguments containing both stacks and slots</param>
        protected virtual void OnSwapCompleted(InventorySwapContext args)
        {
            // Do nothing by default
            // OnItemAdded/OnItemRemoved events have already been emitted for both inventories
        }

        /// <summary>
        /// Called when an item is dropped onto an occupied slot, before swap or alternative-slot handling.
        /// Return true if this DataBinding can handle such a drop (for example, putting an item inside a container).
        /// If false, the pipeline continues with the default logic (swap, findAlternative, reject).
        /// </summary>
        protected virtual bool CanHandleOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedBaseSlot) => false;

        /// <summary>
        /// Executes a drop onto an occupied slot when CanHandleOccupiedSlotDrop returned true.
        /// The implementation must handle the transfer fully: add the item to the target place,
        /// clear the source slot, and update the data.
        /// </summary>
        protected virtual bool ExecuteOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedBaseSlot) => false;

        #endregion
    }
}