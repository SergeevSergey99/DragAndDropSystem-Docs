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

        /// <summary>
        /// Drop target currently under the pointer/focus (the one that will receive the drop).
        /// Remains valid through OnDropAttempting/OnDropCompleted and is cleared by EndDrag.
        /// Lets listeners (for example FreeFormSlotLayout) detect which area received the drop.
        /// </summary>
        public IDropTarget ActiveDropTarget => _activeDropTarget;

        // Exposed for IDropProcessor implementations
        public GlobalRuleValidator GlobalRules => _globalRules;

        // Quick click auto-transfer properties
        public bool IsQuickClickAutoTransferEnabled => _enableQuickClickAutoTransfer;
        public float QuickClickTimeThreshold => _quickClickTimeThreshold;
        public float QuickClickDistanceThreshold => _quickClickDistanceThreshold;

        // Global drag/drop/auto-transfer/swap events now live on UDNDEvents (single subscription
        // surface + one Fast-Enter-Play-Mode reset). This manager raises them via UDNDEvents.Raise*.

        protected override void Init()
        {
            base.Init();

            // Add base rules
            _globalRules.AddRule(new SameSlotRule());
        }

        protected override void DeInit()
        {
            base.DeInit();
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
            _currentContext = dragContext;

            // Event: starting
            UDNDEvents.RaiseDragAttempting(_currentContext);

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

                if (entry.SourceInventory is IInventoryRuleEvaluator ruleEvaluator)
                {
                    var inventoryResult = ruleEvaluator.RuleValidator.ValidateStartDrag(_currentContext, entry);
                    if (!inventoryResult.IsValid)
                    {
                        Extensions.DragAndDropLog($"Cannot start batch drag: {inventoryResult.FailureReason}");
                        _currentContext = null;
                        return false;
                    }
                }
            }

            SetDraggedState(_currentContext.Entries, true);
            UDNDEvents.RaiseDragStarted(_currentContext);
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
            var placement = ResolveCurrentSourcePlacement(entry);
            if (placement == null ||
                PlacementShapeUtility.IsSingleCell(
                    placement.Shape,
                    placement.Orientation,
                    entry.OrientationTopology) ||
                entry.SourceInventory == null)
                return false;

            for (int i = 0; i < placement.CoveredIndices.Count; i++)
            {
                var slot = entry.SourceInventory.GetSlot(placement.CoveredIndices[i]);
                if (slot != null && processedSlots.Add(slot))
                    slot.SetDraggedFrom(isDragging);
            }

            return true;
        }

        private static Placement ResolveCurrentSourcePlacement(DragEntry entry)
        {
            if (entry.SourceInventory is IPlacementInventory placementInventory &&
                entry.SourceBaseSlot != null)
            {
                var currentPlacement = placementInventory.GetPlacementAt(entry.SourceBaseSlot);
                if (currentPlacement != null)
                    return currentPlacement;
            }

            return entry.SourcePlacement;
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
                if (IsTargetAlive(currentTop))
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
            if (wasTop && IsTargetAlive(target))
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
        /// A drop target backed by a destroyed UnityEngine.Object is unusable: touching it
        /// raises MissingReferenceException. The stack stores IDropTarget, so the engine's
        /// null overload does not apply and the check has to be explicit.
        ///
        /// This happens whenever a view disappears without popping itself — a destroyed
        /// inventory GameObject, or a UI Toolkit element detached from its panel, where no
        /// OnDisable equivalent exists to run the pop.
        /// </summary>
        private static bool IsTargetAlive(IDropTarget target)
        {
            if (target == null)
                return false;

            return !(target is UnityEngine.Object unityTarget) || unityTarget != null;
        }

        /// <summary>
        /// Drops every target whose backing object no longer exists. Returns true if the
        /// active target was among them.
        /// </summary>
        private bool PruneDeadDropTargets()
        {
            bool removedActive = false;

            for (int i = _dropTargetStack.Count - 1; i >= 0; i--)
            {
                if (IsTargetAlive(_dropTargetStack[i]))
                    continue;

                if (ReferenceEquals(_dropTargetStack[i], _activeDropTarget))
                    removedActive = true;

                _dropTargetStack.RemoveAt(i);
                Extensions.DragAndDropLog(
                    "<color=yellow>Dropped a destroyed target from the stack</color>");
            }

            if (removedActive || !IsTargetAlive(_activeDropTarget))
            {
                _activeDropTarget = null;
                _currentProcessor = null;
            }

            return removedActive;
        }

        /// <summary>
        /// Activate the top target in the stack
        /// </summary>
        private void ActivateTopTarget()
        {
            if (!IsDragging)
                return;

            PruneDeadDropTargets();

            if (_dropTargetStack.Count > 0)
            {
                var top = _dropTargetStack[_dropTargetStack.Count - 1];
                var slot = top.GetTargetSlot();
                var processor = top.GetDropProcessor();

                // Keep drag enter/exit events bound to active slot-like target transitions.
                if (_activeDropTarget != null && !ReferenceEquals(_activeDropTarget, top))
                {
                    UDNDEvents.RaiseDragExitSlot(_currentContext);
                }
                if (!ReferenceEquals(_activeDropTarget, top))
                {
                    UDNDEvents.RaiseDragEnterSlot(_currentContext);
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
                    UDNDEvents.RaiseDragExitSlot(_currentContext);
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

            // Fire-and-forget: the returned task is the only place an exception raised outside
            // CompleteDragAsync's own try block (its finally, most notably) can surface. Dropping
            // it on the floor would let the drag pipeline fail without a single line in the console.
            CompleteDragAsync(requested).LogFaults();
        }

        public bool RotateCurrentDrag(int orientationSteps = 1)
        {
            if (!IsDragging ||
                _currentContext?.Entries == null ||
                _currentContext.Entries.Count == 0 ||
                _isCompletingDrag ||
                _isProcessingTransfer)
                return false;

            var topology = ResolveDragOrientationTopology();
            if (topology == null)
                return false;

            int normalizedSteps =
                ((orientationSteps % topology.OrientationCount) + topology.OrientationCount) %
                topology.OrientationCount;
            if (normalizedSteps == 0)
                return true;

            var rotatedEntries = new List<DragEntry>(_currentContext.Entries.Count);
            for (int i = 0; i < _currentContext.Entries.Count; i++)
            {
                var entry = _currentContext.Entries[i];
                float currentAngle = entry.OrientationTopology
                    .GetVisualAngleDegrees(entry.Orientation);
                var currentOrientation = topology
                    .GetOrientationForVisualAngleDegrees(currentAngle);
                var orientation = topology.Rotate(currentOrientation, normalizedSteps);
                var offsets = topology.GetPlacementOffsets(entry.Shape, orientation);
                if (offsets == null || offsets.Count == 0)
                    return false;

                rotatedEntries.Add(entry.WithOrientation(orientation, topology));
            }

            _currentContext = _currentContext.WithEntries(rotatedEntries);
            RefreshActiveDropPreview();
            UDNDEvents.RaiseDragOrientationChanged(_currentContext);
            return true;
        }

        private IInventoryTopology ResolveDragOrientationTopology()
        {
            if (_currentContext?.TargetInventory is IPlacementInventory targetPlacementInventory &&
                targetPlacementInventory.Topology != null &&
                targetPlacementInventory.Topology.OrientationCount > 1)
            {
                return targetPlacementInventory.Topology;
            }

            var entries = _currentContext?.Entries;
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var topology = entries[i].OrientationTopology;
                    if (topology != null && topology.OrientationCount > 1)
                        return topology;
                }
            }

            return new RectGridTopology(1, 1);
        }

        private void RefreshActiveDropPreview()
        {
            if (_activeDropTarget == null)
                return;

            if (!IsTargetAlive(_activeDropTarget))
            {
                // ActivateTopTarget prunes and falls back to whatever is still under the
                // pointer, so a dead preview target does not block rotation.
                ActivateTopTarget();
                return;
            }

            _activeDropTarget.OnBecomeInactiveTarget();
            _activeDropTarget.OnBecomeActiveTarget();
        }

        private async Task CompleteDragAsync(DropRequestPolicy? requested)
        {
            if (_isProcessingTransfer)
            {
                Extensions.DragAndDropLog("<color=red>CompleteDrag: Another transfer is in progress</color>");
                UDNDEvents.RaiseDragCancelled(_currentContext);
                EndDrag();
                return;
            }

            _isCompletingDrag = true;
            _isProcessingTransfer = true;
            bool success = false;
            DropResult result = default;

            // Drop targets that died between hover and release would otherwise hand us a
            // destroyed processor (DropAreaBase is its own processor), turning the drop into
            // a logged exception instead of a clean no-op.
            PruneDeadDropTargets();

            IDropProcessor processorToUse = _currentProcessor;
            var dragContext = _currentContext;

            try
            {
                if (processorToUse != null)
                {
                    UDNDEvents.RaiseDropAttempting(dragContext);

                    var requestProcessor = processorToUse as IDropRequestProcessor;
                    bool canDrop = processorToUse is InventoryDropProcessor ||
                        (requestProcessor != null
                            ? requestProcessor.CanAcceptDrop(dragContext, requested)
                            : processorToUse.CanAcceptDrop(dragContext));

                    if (canDrop)
                    {
                        if (processorToUse is InventoryDropProcessor inventoryProcessor)
                        {
                            var report = await inventoryProcessor.ProcessDropWithReportAsync(
                                dragContext,
                                requested);
                            result = report.ToDropResult(dragContext.TargetInventory);
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

                            UDNDEvents.RaiseDropCompleted(dragContext);
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
                    UDNDEvents.RaiseDragCancelled(dragContext);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                if (!success)
                {
                    UDNDEvents.RaiseDragCancelled(dragContext);
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
            if (splitCount <= 0)
                return false;

            if (entry.Stack.Count <= splitCount)
            {
                CompleteDrag(requested);
                return true;
            }

            SplitDropAsync(requested, splitCount).LogFaults();
            return true;
        }

        private async Task<bool> SplitDropAsync(DropRequestPolicy? requested, int splitCount)
        {
            _isProcessingTransfer = true;
            ItemStack splitStack = null;
            DragEntry entry = default;
            try
            {
                if (_currentContext?.Entries == null ||
                    _currentContext.Entries.Count == 0 ||
                    _currentProcessor == null)
                    return false;

                entry = _currentContext.Entries[0];
                splitStack = entry.Stack.Split(splitCount);
                if (splitStack.IsEmpty)
                    return false;
                UDNDEvents.RaiseDragStackChanged(_currentContext);

                var splitEntry = new DragEntry(
                    splitStack,
                    entry.SourceBaseSlot,
                    entry.SourceInventory,
                    entry.SourcePlacement,
                    entry.GrabOffset,
                    entry.Orientation,
                    entry.OrientationTopology);
                var splitContext = new DragContext(new[] { splitEntry });

                bool success = false;
                if (_currentProcessor is InventoryDropProcessor inventoryProcessor)
                {
                    var report = await inventoryProcessor.ProcessDropWithReportAsync(
                        splitContext,
                        requested);
                    success = report.Success;
                }
                else if (_currentProcessor is IDropRequestProcessor reqProcessor)
                {
                    if (reqProcessor.CanAcceptDrop(splitContext, requested))
                    {
                        var result = reqProcessor.ProcessDrop(splitContext, requested);
                        success = result.Success;
                    }
                }
                else if (_currentProcessor.CanAcceptDrop(splitContext))
                {
                    success = _currentProcessor.ProcessDrop(splitContext).Success;
                }

                if (!success)
                {
                    entry.Stack.TryAddToStack(splitStack);
                    UDNDEvents.RaiseDragStackChanged(_currentContext);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                if (splitStack != null && !splitStack.IsEmpty && entry.Stack != null)
                {
                    entry.Stack.TryAddToStack(splitStack);
                    UDNDEvents.RaiseDragStackChanged(_currentContext);
                }

                Debug.LogException(ex);
                return false;
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

            return baseSlot.Inventory.GetDragAmount(baseSlot, requested.Value.Amount.Value, requested.Value.CustomAmount);
        }

        /// <summary>
        /// Cancel dragging
        /// </summary>
        public void CancelDrag()
        {
            if (!IsDragging || _isCompletingDrag)
                return;

            UDNDEvents.RaiseDragCancelled(_currentContext);
            EndDrag();
        }

        private void EndDrag()
        {
            var dragContext = _currentContext;

            // Deactivate all targets in the stack.
            //
            // This runs from CompleteDragAsync's finally block, so it must not throw: an
            // escaping exception would skip the state reset below and strand the manager
            // with IsDragging stuck true, refusing every later StartDrag. A destroyed or
            // misbehaving presenter is not worth losing the whole drag system over.
            foreach (var target in _dropTargetStack)
            {
                if (!IsTargetAlive(target))
                    continue;

                try
                {
                    target.OnBecomeInactiveTarget();
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
            _dropTargetStack.Clear();

            if (_activeDropTarget != null)
            {
                try
                {
                    UDNDEvents.RaiseDragExitSlot(_currentContext);
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            // Drop the drag state before any presentation work. Everything below this point talks to
            // slots and inventories that may have been destroyed or rebuilt mid-drag, and a throw
            // there must not leave IsDragging stuck true.
            _currentContext = null;
            _activeDropTarget = null;
            _currentProcessor = null;

            try
            {
                SetDraggedState(dragContext?.Entries, false);
                RefreshDragInventories(dragContext);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }

            UDNDEvents.RaiseDragEnded();
        }

        private static void RefreshDragInventories(DragContext dragContext)
        {
            if (dragContext == null)
                return;

            var inventories = new HashSet<IInventory>();
            if (dragContext.TargetInventory != null)
                inventories.Add(dragContext.TargetInventory);

            if (dragContext.Entries != null)
            {
                for (int i = 0; i < dragContext.Entries.Count; i++)
                {
                    var sourceInventory = dragContext.Entries[i].SourceInventory;
                    if (sourceInventory != null)
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

            TryAutoTransferAsync(sourceSlots, sourceInventory, targetInventory, CancellationToken.None).LogFaults();
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

                UDNDEvents.RaiseAutoTransferAttempting(context);

                var (dropResult, executionReport) = await _autoTransferService.ExecuteAsync(
                    context,
                    targetInventory,
                    _globalRules,
                    RaiseSwapAttempting,
                    RaiseSwapCompleted,
                    cancellationToken);

                if (!dropResult.Success)
                {
                    Extensions.DragAndDropLog($"<color=red>TryAutoTransfer failed: {dropResult.FailureReason}</color>");
                    UDNDEvents.RaiseAutoTransferFailed(context);
                    return false;
                }

                NotifyAutoTransferSourceSlots(context);
                FinalizeAutoTransferSuccess(context, targetInventory, dropResult, executionReport);
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
                if (entry.SourceInventory is IDynamicSlotLifecycle dynamicSlotLifecycle &&
                    entry.SourceBaseSlot != null &&
                    entry.SourceBaseSlot.IsEmpty)
                {
                    dynamicSlotLifecycle.HandleSlotEmptied(entry.SourceBaseSlot);
                }
            }
        }

        private void FinalizeAutoTransferSuccess(
            DragContext context,
            IInventory targetInventory,
            DropResult dropResult,
            TransferExecutionReport executionReport)
        {
            var transferredItem = dropResult.ItemAdapter;
            int transferredAmount = dropResult.Amount;
            var finalTargetSlot = dropResult.TargetBaseSlot;

            string itemName = transferredItem?.DisplayName ?? "Unknown";
            string targetName = targetInventory?.GetType().Name ?? "Unknown";
            Extensions.DragAndDropLog($"<color=green>AutoTransfer success: {transferredAmount}x {itemName} → {targetName} (slot {finalTargetSlot?.Index.ToString() ?? "-"})</color>");

            // Flight animation and its transient visuals are owned by the presenter (the visual layer).
            // We keep ownership of the lifecycle events and fire them once the flights complete.
            void FireCompleted()
            {
                UDNDEvents.RaiseDropCompleted(context);
                UDNDEvents.RaiseAutoTransferCompleted(context);
            }

            if (DragVisualPresenter.IsInstanceExist)
                DragVisualPresenter.AutoCreateInstance.PlayAutoTransfer(CollectOutcomes(executionReport), FireCompleted);
            else
                FireCompleted();
        }

        private static IReadOnlyList<PlacementTransferOutcome> CollectOutcomes(TransferExecutionReport report)
        {
            if (report == null)
                return System.Array.Empty<PlacementTransferOutcome>();

            var outcomes = new List<PlacementTransferOutcome>();
            foreach (var entry in report.EntryResults)
                foreach (var outcome in entry.Outcomes)
                    outcomes.Add(outcome);

            return outcomes;
        }

        public bool RaiseSwapAttempting(InventorySwapContext context)
        {
            // Inventory-scoped events (specific) — fire on both participating inventories
            var source = context.SourceInventory as IInventoryEventSink;
            var target = context.TargetInventory as IInventoryEventSink;

            source?.EmitSwapAttempting(context);
            if (target != null && !ReferenceEquals(target, source))
                target.EmitSwapAttempting(context);

            // Global event (general)
            UDNDEvents.RaiseSwapAttempting(context);

            return context != null && !context.Cancel;
        }

        public void RaiseSwapCompleted(InventorySwapContext context)
        {
            // Inventory-scoped events (specific)
            var source = context.SourceInventory as IInventoryEventSink;
            var target = context.TargetInventory as IInventoryEventSink;

            source?.EmitSwapCompleted(context);
            if (target != null && !ReferenceEquals(target, source))
                target.EmitSwapCompleted(context);

            // Global event (general)
            UDNDEvents.RaiseSwapCompleted(context);
        }
    }
}
