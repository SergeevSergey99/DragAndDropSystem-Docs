using System;
using System.Collections.Generic;
using System.Linq;
using CodeUtils;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UDND.Core;
using UDND.Inventories;
using UDND.Rules;
using UDND.Selection;
using UDND.Slots;
using UDND.Tools;
using UDND.Tools.Inspector;
using UDND.UI;

namespace UDND
{
    /// <summary>
    /// Next-generation drag-and-drop manager
    /// Built around composition, rules, and strategies
    /// </summary>
    [DisallowMultipleComponent]
    public class DragAndDropManager : MonoSingleton<DragAndDropManager>
    {
        [Header("Quick Click Auto-Transfer (LMB)")]
        [SerializeField] private bool _enableQuickClickAutoTransfer = true;
        [SerializeField, Range(0.05f, 1f), Tooltip("Maximum click duration for auto-transfer (seconds)")]
        private float _quickClickTimeThreshold = 0.2f;
        [SerializeField, Range(1f, 50f), Tooltip("Maximum mouse movement for auto-transfer (pixels)")]
        private float _quickClickDistanceThreshold = 5f;

        [Header("Auto-Transfer Animation")]
        [SerializeReference, ManagedReferencePicker, Tooltip("Auto-transfer animation strategy. Null = instant transfer")]
        private AutoTransferAnimationStrategy _autoTransferAnimation;

        // Active animation visuals list to support multiple simultaneous animations
        private List<GameObject> _activeAnimationVisuals = new List<GameObject>();

        private DragContext _currentContext;
        private IDropTarget _activeDropTarget;
        private IDropProcessor _currentProcessor;
        private GlobalRuleValidator _globalRules = new GlobalRuleValidator();

        // Drop target stack for correct handling of nested areas and slots
        private List<IDropTarget> _dropTargetStack = new List<IDropTarget>();

        private readonly AutoTransferService _autoTransferService = new AutoTransferService();
        private bool _isCompletingDrag;
        private bool _isProcessingTransfer;

        public bool IsDragging => _currentContext != null;
        public DragContext CurrentContext => _currentContext;
        public bool HasActiveDropTarget => _currentProcessor != null || _dropTargetStack.Count > 0;
        public bool HasActiveSlotDropTarget => _activeDropTarget?.GetTargetSlot() != null;

        // Exposed for IDropProcessor implementations
        public GlobalRuleValidator GlobalRules => _globalRules;

        // Quick click auto-transfer properties
        public bool IsQuickClickAutoTransferEnabled => _enableQuickClickAutoTransfer;
        public float QuickClickTimeThreshold => _quickClickTimeThreshold;
        public float QuickClickDistanceThreshold => _quickClickDistanceThreshold;

        // Drag-and-drop events
        public static event Action<DragContext> OnDragAttempting;
        public static event Action<DragContext> OnDragStarted;
        public static event Action<DragContext> OnDragEnterSlot;
        public static event Action<DragContext> OnDragExitSlot;
        public static event Action<DragContext> OnDropAttempting;
        public static event Action<DragContext> OnDropCompleted;
        public static event Action<DragContext> OnDragCancelled;
        public static event Action<DragContext> OnDragStackChanged;
        public static event Action<DragContext> OnDragOrientationChanged;
        public static event Action OnDragEnded;

        // Auto-transfer events
        public static event Action<DragContext> OnAutoTransferAttempting;
        public static event Action<DragContext> OnAutoTransferCompleted;
        public static event Action<DragContext> OnAutoTransferFailed;

        // Item swap events
        public static event Action<InventorySwapContext> OnSwapAttempting;
        public static event Action<InventorySwapContext> OnSwapCompleted;

        protected override void Init()
        {
            base.Init();

            // Add base rules
            _globalRules.AddRule(new SameSlotRule());
        }

        protected override void DeInit()
        {
            base.DeInit();

            // Destroy all active animation visuals
            foreach (var visual in _activeAnimationVisuals)
            {
                if (visual != null)
                {
                    Destroy(visual);
                }
            }
            _activeAnimationVisuals.Clear();
        }

        /// <summary>
        /// Add a global rule for all drag-and-drop operations
        /// </summary>
        public void AddGlobalRule(IGlobalRule rule)
        {
            _globalRules.AddRule(rule);
        }

        /// <summary>
        /// Remove a global rule
        /// </summary>
        public void RemoveGlobalRule(IGlobalRule rule)
        {
            _globalRules.RemoveRule(rule);
        }

        /// <summary>
        /// Start dragging from a slot
        /// </summary>
        public bool StartDrag(BaseSlot sourceBaseSlot, DragRequestPolicy? requested = null) => sourceBaseSlot != null && StartDrag(new List<BaseSlot> { sourceBaseSlot }, requested);

        /// <summary>
        /// Start dragging from one or more slots
        /// </summary>
        public bool StartDrag(IReadOnlyList<BaseSlot> sourceSlots, DragRequestPolicy? requested = null)
        {
            if (IsDragging || sourceSlots == null || sourceSlots.Count == 0)
                return false;

            // Build entries
            var entries = new List<DragEntry>(sourceSlots.Count);
            foreach (var slot in sourceSlots)
            {
                if (slot == null || slot.IsEmpty || slot.Inventory == null)
                    continue;

                int dragCount = ResolveDragCount(slot, requested);
                var stack = slot.Stack.CreateCopy(dragCount);
                entries.Add(new DragEntry(stack, slot, slot.Inventory));
            }

            if (entries.Count == 0)
                return false;

            var dragContext = new DragContext(entries);
            if (!ValidateShapedDragScope(dragContext, out var shapedFailureReason))
            {
                Extensions.DragAndDropLog($"Cannot start drag: {shapedFailureReason}");
                return false;
            }

            _currentContext = dragContext;

            // Event: starting
            OnDragAttempting?.Invoke(_currentContext);

            // Per-entry validation
            foreach (var entry in _currentContext.Entries)
            {
                var globalResult = _globalRules.ValidateStartDrag(_currentContext, entry);
                if (!globalResult.IsValid)
                {
                    Extensions.DragAndDropLog($"Cannot start batch drag: {globalResult.FailureReason}");
                    _currentContext = null;
                    return false;
                }

                if (entry.SourceInventory is UniversalInventory universalInventory)
                {
                    var inventoryResult = universalInventory.RuleValidator.ValidateStartDrag(_currentContext, entry);
                    if (!inventoryResult.IsValid)
                    {
                        Extensions.DragAndDropLog($"Cannot start batch drag: {inventoryResult.FailureReason}");
                        _currentContext = null;
                        return false;
                    }
                }
            }

            SetDraggedState(_currentContext.Entries, true);
            OnDragStarted?.Invoke(_currentContext);
            ActivateDropTargetForSlot(_currentContext.Entries[0].SourceBaseSlot);
            Extensions.DragAndDropLog($"<color=green>Started dragging ({entries.Count} entries)</color>");
            return true;
        }

        public bool ActivateDropTargetForSlot(BaseSlot targetBaseSlot)
        {
            if (!IsDragging || targetBaseSlot == null)
                return false;

            var target = targetBaseSlot.GetComponent<IDropTarget>();
            if (target == null || !ReferenceEquals(target.GetTargetSlot(), targetBaseSlot))
                return false;

            PushDropTarget(target);
            return true;
        }

        private static bool ValidateShapedDragScope(DragContext context, out string failureReason)
        {
            failureReason = null;
            if (context == null)
            {
                failureReason = "Invalid drag context";
                return false;
            }

            if (context.HasStackedShapedEntries || HasStackedShapedSource(context))
            {
                failureReason = "Shaped items cannot be dragged as stacks";
                return false;
            }

            if (context.IsBatchDrag && context.HasShapedEntries)
            {
                failureReason = "Batch drag does not support shaped items";
                return false;
            }

            return true;
        }

        private static bool HasStackedShapedSource(DragContext context)
        {
            if (context?.Entries == null)
                return false;

            for (int i = 0; i < context.Entries.Count; i++)
            {
                var entry = context.Entries[i];
                if (!entry.IsShaped)
                    continue;

                int sourceCount = entry.SourcePlacement?.Stack?.Count
                    ?? entry.SourceBaseSlot?.Stack?.Count
                    ?? 0;
                if (sourceCount > 1)
                    return true;
            }

            return false;
        }

        private static void SetDraggedState(IReadOnlyList<DragEntry> entries, bool isDragging)
        {
            if (entries == null)
                return;

            var processedSlots = new HashSet<BaseSlot>();
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (TrySetPlacementDraggedState(entry, isDragging, processedSlots))
                    continue;

                var sourceBaseSlot = entry.SourceBaseSlot;
                if (sourceBaseSlot != null && processedSlots.Add(sourceBaseSlot))
                    sourceBaseSlot.SetDraggedFrom(isDragging);
            }
        }

        private static bool TrySetPlacementDraggedState(DragEntry entry, bool isDragging, HashSet<BaseSlot> processedSlots)
        {
            if (entry.SourcePlacement == null ||
                PlacementShapeUtility.IsSingleCell(entry.SourcePlacement.Shape, entry.SourcePlacement.Orientation) ||
                entry.SourceInventory is not UniversalInventory inventory)
                return false;

            for (int i = 0; i < entry.SourcePlacement.CoveredIndices.Count; i++)
            {
                var slot = inventory.GetSlot(entry.SourcePlacement.CoveredIndices[i]);
                if (slot != null && processedSlots.Add(slot))
                    slot.SetDraggedFrom(isDragging);
            }

            return true;
        }

        /// <summary>
        /// Add a target to the stack (called on OnPointerEnter)
        /// Automatically activates the new top target
        /// </summary>
        public void PushDropTarget(IDropTarget target)
        {
            if (!IsDragging || target == null)
                return;

            // Guard against duplicates
            if (_dropTargetStack.Contains(target))
            {
                Extensions.DragAndDropLog($"<color=yellow>PushDropTarget: Target already in stack, ignoring</color>");
                return;
            }

            // Deactivate the current top
            if (_dropTargetStack.Count > 0)
            {
                var currentTop = _dropTargetStack[_dropTargetStack.Count - 1];
                currentTop.OnBecomeInactiveTarget();
            }

            // Add the new target
            _dropTargetStack.Add(target);

            Extensions.DragAndDropLog($"<color=cyan>PushDropTarget: Added to stack (size={_dropTargetStack.Count})</color>");

            // Activate the new top
            ActivateTopTarget();
        }

        /// <summary>
        /// Remove a target from the stack (called on OnPointerExit or OnDisable)
        /// If the top target is removed, automatically activates the previous target
        /// </summary>
        public void PopDropTarget(IDropTarget target)
        {
            if (target == null)
                return;

            int index = _dropTargetStack.IndexOf(target);
            if (index == -1)
            {
                Extensions.DragAndDropLog($"<color=yellow>PopDropTarget: Target not in stack, ignoring</color>");
                return;
            }

            bool wasTop = (index == _dropTargetStack.Count - 1);

            // Deactivate it if it was the top target
            if (wasTop)
            {
                target.OnBecomeInactiveTarget();
            }

            // Remove it from the stack
            _dropTargetStack.RemoveAt(index);

            Extensions.DragAndDropLog($"<color=cyan>PopDropTarget: Removed from stack (size={_dropTargetStack.Count})</color>");

            // If the top was removed, activate the new top (or clear state)
            if (wasTop)
            {
                ActivateTopTarget();
            }
        }

        /// <summary>
        /// Activate the top target in the stack
        /// </summary>
        private void ActivateTopTarget()
        {
            if (!IsDragging)
                return;

            if (_dropTargetStack.Count > 0)
            {
                var top = _dropTargetStack[_dropTargetStack.Count - 1];
                var slot = top.GetTargetSlot();
                var processor = top.GetDropProcessor();

                // Keep drag enter/exit events bound to active slot-like target transitions.
                if (_activeDropTarget != null && !ReferenceEquals(_activeDropTarget, top))
                {
                    OnDragExitSlot?.Invoke(_currentContext);
                }
                if (!ReferenceEquals(_activeDropTarget, top))
                {
                    OnDragEnterSlot?.Invoke(_currentContext);
                }

                _activeDropTarget = top;
                _currentProcessor = processor;

                // Activate visual target
                top.OnBecomeActiveTarget();

                Extensions.DragAndDropLog($"<color=green>ActivateTopTarget: slot={slot?.Index.ToString() ?? "AREA"}, processor={processor?.GetType().Name}</color>");
            }
            else
            {
                if (_activeDropTarget != null)
                {
                    OnDragExitSlot?.Invoke(_currentContext);
                }
                _activeDropTarget = null;
                _currentProcessor = null;
                _currentContext?.ClearTarget();

                Extensions.DragAndDropLog("<color=yellow>ActivateTopTarget: Stack empty, cleared active target</color>");
            }
        }

        /// <summary>
        /// Complete the drag operation
        /// </summary>
        public void CompleteDrag(DropRequestPolicy? requested = null)
        {
            if (!IsDragging || _isCompletingDrag)
                return;

            _ = CompleteDragAsync(requested);
        }

        public bool RotateCurrentDrag(int quarterTurns = 1)
        {
            if (!IsDragging ||
                _currentContext?.Entries == null ||
                _currentContext.Entries.Count == 0 ||
                _isCompletingDrag ||
                _isProcessingTransfer)
                return false;

            int normalizedTurns = ((quarterTurns % 4) + 4) % 4;
            if (normalizedTurns == 0)
                return true;

            var rotatedEntries = new List<DragEntry>(_currentContext.Entries.Count);
            for (int i = 0; i < _currentContext.Entries.Count; i++)
            {
                var entry = _currentContext.Entries[i];
                var orientation = RotateOrientation(entry.Orientation, normalizedTurns);
                if (entry.Shape != null && !entry.Shape.SupportsOrientation(orientation))
                    return false;

                rotatedEntries.Add(entry.WithOrientation(orientation));
            }

            _currentContext = _currentContext.WithEntries(rotatedEntries);
            RefreshActiveDropPreview();
            OnDragOrientationChanged?.Invoke(_currentContext);
            return true;
        }

        private static PlacementOrientation RotateOrientation(PlacementOrientation orientation, int quarterTurns)
        {
            int value = ((int)orientation + quarterTurns) % 4;
            return (PlacementOrientation)value;
        }

        private void RefreshActiveDropPreview()
        {
            if (_activeDropTarget == null)
                return;

            _activeDropTarget.OnBecomeInactiveTarget();
            _activeDropTarget.OnBecomeActiveTarget();
        }

        private async Task CompleteDragAsync(DropRequestPolicy? requested)
        {
            if (_isProcessingTransfer)
            {
                Extensions.DragAndDropLog("<color=red>CompleteDrag: Another transfer is in progress</color>");
                OnDragCancelled?.Invoke(_currentContext);
                EndDrag();
                return;
            }

            _isCompletingDrag = true;
            _isProcessingTransfer = true;
            bool success = false;
            DropResult result = default;
            IDropProcessor processorToUse = _currentProcessor;
            var dragContext = _currentContext;

            try
            {
                if (processorToUse != null)
                {
                    OnDropAttempting?.Invoke(dragContext);

                    bool canDrop;
                    var requestProcessor = processorToUse as IDropRequestProcessor;
                    if (requestProcessor != null)
                        canDrop = requestProcessor.CanAcceptDrop(dragContext, requested);
                    else
                        canDrop = processorToUse.CanAcceptDrop(dragContext);

                    if (canDrop)
                    {
                        if (processorToUse is InventoryDropProcessor inventoryProcessor)
                        {
                            result = (await inventoryProcessor.ProcessDropWithSummaryAsync(dragContext, requested, CancellationToken.None)).DropResult;
                        }
                        else if (requestProcessor != null)
                        {
                            result = requestProcessor.ProcessDrop(dragContext, requested);
                        }
                        else
                        {
                            result = processorToUse.ProcessDrop(dragContext);
                        }

                        success = result.Success;

                        if (success)
                        {
                            if (result.TargetBaseSlot != null && result.TargetInventory != null)
                            {
                                dragContext.SetTarget(result.TargetBaseSlot, result.TargetInventory);
                            }

                            if (dragContext.IsBatchDrag && SelectionManager.IsInstanceExist)
                                SelectionManager.AutoCreateInstance.Clear();

                            OnDropCompleted?.Invoke(dragContext);
                        }
                        else
                        {
                            Extensions.DragAndDropLog($"<color=red>CompleteDrag: Handler.ProcessDrop failed: {result.FailureReason}</color>");
                        }
                    }
                    else
                    {
                        Extensions.DragAndDropLog("<color=red>CompleteDrag: Handler.CanAcceptDrop returned false</color>");
                    }
                }

                if (!success)
                {
                    OnDragCancelled?.Invoke(dragContext);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                if (!success)
                {
                    OnDragCancelled?.Invoke(dragContext);
                }
            }
            finally
            {
                EndDrag();
                _isCompletingDrag = false;
                _isProcessingTransfer = false;
            }
        }

        /// <summary>
        /// Drop a portion of the dragged stack without ending the drag.
        /// The remaining items stay in the active drag context.
        /// When the stack has &lt;= splitCount items left, delegates to CompleteDrag.
        /// </summary>
        public bool SplitDrop(DropRequestPolicy? requested = null, int splitCount = 1)
        {
            if (!IsDragging || _isCompletingDrag || _isProcessingTransfer)
                return false;
            if (_currentProcessor == null || _currentContext.IsBatchDrag)
                return false;

            var entry = _currentContext.Entries[0];
            if (entry.Stack.IsEmpty)
                return false;

            if (entry.Stack.Count <= splitCount)
            {
                CompleteDrag(requested);
                return true;
            }

            _isProcessingTransfer = true;
            try
            {
                var splitStack = entry.Stack.Split(splitCount);
                if (splitStack.IsEmpty)
                    return false;

                var splitEntry = new DragEntry(
                    splitStack,
                    entry.SourceBaseSlot,
                    entry.SourceInventory,
                    entry.SourcePlacement,
                    entry.GrabOffset,
                    entry.Orientation);
                var splitContext = new DragContext(new[] { splitEntry });

                bool success = false;
                if (_currentProcessor is IDropRequestProcessor reqProcessor)
                {
                    if (reqProcessor.CanAcceptDrop(splitContext, requested))
                    {
                        var result = reqProcessor.ProcessDrop(splitContext, requested);
                        success = result.Success;
                    }
                }
                else if (_currentProcessor.CanAcceptDrop(splitContext))
                {
                    var result = _currentProcessor.ProcessDrop(splitContext);
                    success = result.Success;
                }

                if (!success)
                {
                    entry.Stack.TryAddToStack(splitStack);
                    return false;
                }

                OnDragStackChanged?.Invoke(_currentContext);
                return true;
            }
            finally
            {
                _isProcessingTransfer = false;
            }
        }

        private static int ResolveDragCount(BaseSlot baseSlot, DragRequestPolicy? requested)
        {
            if (baseSlot == null || baseSlot.IsEmpty || baseSlot.Inventory == null)
                return 0;

            if (!requested.HasValue || !requested.Value.Amount.HasValue)
                return baseSlot.Inventory.GetDragAmount(baseSlot);

            if (baseSlot.Inventory is UniversalInventory universal)
                return universal.GetDragAmount(baseSlot, requested.Value.Amount.Value, requested.Value.CustomAmount);

            return baseSlot.Inventory.GetDragAmount(baseSlot);
        }

        /// <summary>
        /// Cancel dragging
        /// </summary>
        public void CancelDrag()
        {
            if (!IsDragging || _isCompletingDrag)
                return;

            OnDragCancelled?.Invoke(_currentContext);
            EndDrag();
        }

        private void EndDrag()
        {
            var dragContext = _currentContext;

            // Deactivate all targets in the stack
            foreach (var target in _dropTargetStack)
            {
                target.OnBecomeInactiveTarget();
            }
            _dropTargetStack.Clear();

            if (_activeDropTarget != null)
            {
                OnDragExitSlot?.Invoke(_currentContext);
            }

            SetDraggedState(dragContext?.Entries, false);
            RefreshDragInventories(dragContext);
            _currentContext = null;
            _activeDropTarget = null;
            _currentProcessor = null;
            OnDragEnded?.Invoke();
        }

        private static void RefreshDragInventories(DragContext dragContext)
        {
            if (dragContext == null)
                return;

            var inventories = new HashSet<UniversalInventory>();
            if (dragContext.TargetInventory is UniversalInventory targetInventory)
                inventories.Add(targetInventory);

            if (dragContext.Entries != null)
            {
                for (int i = 0; i < dragContext.Entries.Count; i++)
                {
                    if (dragContext.Entries[i].SourceInventory is UniversalInventory sourceInventory)
                        inventories.Add(sourceInventory);
                }
            }

            foreach (var inventory in inventories)
                inventory.UpdateAllVisuals();
        }

        /// <summary>
        /// Perform auto-transfer of an item from a slot to the target inventory
        /// </summary>
        public bool TryAutoTransfer(BaseSlot sourceBaseSlot, IInventory sourceInventory, IInventory targetInventory)
        {
            if (sourceBaseSlot == null)
                return false;

            return TryAutoTransfer(
                new[] { sourceBaseSlot },
                sourceInventory,
                targetInventory);
        }

        /// <summary>
        /// Perform auto-transfer of one or more slots into the target inventory through the shared transfer pipeline.
        /// </summary>
        public bool TryAutoTransfer(IReadOnlyList<BaseSlot> sourceSlots, IInventory sourceInventory, IInventory targetInventory)
        {
            if (sourceSlots == null || sourceSlots.Count == 0 || sourceInventory == null || targetInventory == null)
                return false;

            if (_isProcessingTransfer)
            {
                Extensions.DragAndDropLog("<color=red>TryAutoTransfer: Another transfer is in progress</color>");
                return false;
            }

            _ = TryAutoTransferAsync(sourceSlots, sourceInventory, targetInventory, CancellationToken.None);
            return true;
        }

        public async Task<bool> TryAutoTransferAsync(
            IReadOnlyList<BaseSlot> sourceSlots,
            IInventory sourceInventory,
            IInventory targetInventory,
            CancellationToken cancellationToken = default)
        {
            if (sourceSlots == null || sourceSlots.Count == 0 || sourceInventory == null || targetInventory == null)
            {
                Extensions.DragAndDropLog("<color=red>TryAutoTransfer: Invalid parameters</color>");
                return false;
            }

            if (_isProcessingTransfer)
            {
                Extensions.DragAndDropLog("<color=red>TryAutoTransfer: Another transfer is in progress</color>");
                return false;
            }

            _isProcessingTransfer = true;
            try
            {
                if (IsDragging && _currentContext != null && _currentContext.Entries.Count > 0)
                {
                    for (int i = 0; i < sourceSlots.Count; i++)
                    {
                        if (sourceSlots[i] != null && sourceSlots[i] == _currentContext.Entries[0].SourceBaseSlot)
                        {
                            Extensions.DragAndDropLog("<color=red>TryAutoTransfer: Source slot is currently dragged manually</color>");
                            return false;
                        }
                    }
                }

                if (!_autoTransferService.TryCreateContext(sourceSlots, sourceInventory, targetInventory, out var context, out var createFailure))
                {
                    Extensions.DragAndDropLog($"<color=red>TryAutoTransfer: {createFailure}</color>");
                    return false;
                }

                OnAutoTransferAttempting?.Invoke(context);

                var (dropResult, executionSummary) = await _autoTransferService.ExecuteAsync(
                    context,
                    targetInventory,
                    _globalRules,
                    RaiseSwapAttempting,
                    RaiseSwapCompleted,
                    cancellationToken);

                if (!dropResult.Success)
                {
                    Extensions.DragAndDropLog($"<color=red>TryAutoTransfer failed: {dropResult.FailureReason}</color>");
                    OnAutoTransferFailed?.Invoke(context);
                    return false;
                }

                NotifyAutoTransferSourceSlots(context);
                FinalizeAutoTransferSuccess(context, targetInventory, dropResult, executionSummary);
                return true;
            }
            finally
            {
                _isProcessingTransfer = false;
            }
        }

        private static void NotifyAutoTransferSourceSlots(DragContext context)
        {
            if (context?.Entries == null)
                return;

            for (int i = 0; i < context.Entries.Count; i++)
            {
                var entry = context.Entries[i];
                if (entry.SourceInventory is UniversalInventory sourceUniversal &&
                    entry.SourceBaseSlot != null &&
                    entry.SourceBaseSlot.IsEmpty)
                {
                    sourceUniversal.HandleSlotEmptied(entry.SourceBaseSlot);
                }
            }
        }

        private void FinalizeAutoTransferSuccess(
            DragContext context,
            IInventory targetInventory,
            DropResult dropResult,
            TransferExecutionSummary executionSummary)
        {
            var transferredItem = dropResult.ItemAdapter;
            int transferredAmount = dropResult.Amount;
            var finalTargetSlot = dropResult.TargetBaseSlot;
            var executedEntries = executionSummary?.ExecutedEntries;
            bool canAnimate = _autoTransferAnimation != null
                              && executedEntries != null
                              && executedEntries.Count > 0;

            string itemName = transferredItem?.DisplayName ?? "Unknown";
            string targetName = targetInventory?.GetType().Name ?? "Unknown";
            Extensions.DragAndDropLog($"<color=green>AutoTransfer success: {transferredAmount}x {itemName} → {targetName} (slot {finalTargetSlot?.Index.ToString() ?? "-"})</color>");

            if (canAnimate)
            {
                int pendingAnimations = 0;
                System.Action animationCompleted = () =>
                {
                    pendingAnimations--;
                    if (pendingAnimations > 0)
                        return;

                    OnDropCompleted?.Invoke(context);
                    OnAutoTransferCompleted?.Invoke(context);
                };

                for (int i = 0; i < executedEntries.Count; i++)
                {
                    var entry = executedEntries[i];
                    if (entry.SourceBaseSlot == null || entry.TargetBaseSlot == null || entry.ItemAdapter == null || entry.Amount <= 0)
                        continue;

                    if (!ItemStack.TryCreate(entry.TargetBaseSlot.Stack.Adapters.Take(entry.Amount), out var visualStack))
                        continue;

                    if (entry.TargetBaseSlot != null)
                        entry.TargetBaseSlot.SetDraggedTo(true);

                    var presenter = DragVisualPresenter.AutoCreateInstance;
                    var visualPrefab = presenter.ResolveVisualPrefab(entry.SourceBaseSlot.Inventory);

                    pendingAnimations++;

                    GameObject animationVisual = _autoTransferAnimation.AnimateTransfer(
                        visualStack,
                        entry.SourceBaseSlot,
                        entry.TargetBaseSlot,
                        visualPrefab,
                        presenter.VisualContainer,
                        presenter.PresentationCanvas,
                        () =>
                        {
                            if (entry.TargetBaseSlot != null)
                                entry.TargetBaseSlot.SetDraggedTo(false);
                            animationCompleted();
                        });

                    if (animationVisual != null)
                    {
                        _activeAnimationVisuals.Add(animationVisual);
                        StartCoroutine(RemoveAnimationVisualWhenDestroyed(animationVisual));
                    }
                }

                if (pendingAnimations == 0)
                {
                    OnDropCompleted?.Invoke(context);
                    OnAutoTransferCompleted?.Invoke(context);
                }
            }
            else
            {
                OnDropCompleted?.Invoke(context);
                OnAutoTransferCompleted?.Invoke(context);
            }
        }

        /// <summary>
        /// Coroutine that automatically removes a visual from the list after it is destroyed
        /// </summary>
        private System.Collections.IEnumerator RemoveAnimationVisualWhenDestroyed(GameObject visual)
        {
            // Wait while the visual still exists
            while (visual != null)
            {
                yield return null;
            }

            // The visual was destroyed, remove it from the list
            _activeAnimationVisuals.Remove(visual);
        }

        public bool RaiseSwapAttempting(InventorySwapContext context)
        {
            // Inventory-scoped events (specific) — fire on both participating inventories
            var source = context.SourceInventory as UniversalInventory;
            var target = context.TargetInventory as UniversalInventory;

            source?.EmitSwapAttempting(context);
            if (target != null && !ReferenceEquals(target, source))
                target.EmitSwapAttempting(context);

            // Global event (general)
            OnSwapAttempting?.Invoke(context);

            return context != null && !context.Cancel;
        }

        public void RaiseSwapCompleted(InventorySwapContext context)
        {
            // Inventory-scoped events (specific)
            var source = context.SourceInventory as UniversalInventory;
            var target = context.TargetInventory as UniversalInventory;

            source?.EmitSwapCompleted(context);
            if (target != null && !ReferenceEquals(target, source))
                target.EmitSwapCompleted(context);

            // Global event (general)
            OnSwapCompleted?.Invoke(context);
        }
    }
}
