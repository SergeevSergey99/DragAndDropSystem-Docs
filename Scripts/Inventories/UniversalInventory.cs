using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UDND.Core;
using UDND.DataBinding;
using UDND.Rules;
using UDND.Slots;
using UDND.Tools;
using UDND.Tools.Inspector;

namespace UDND.Inventories
{
    /// <summary>
    /// Universal inventory built around composition
    /// Does not require inheritance and is configured through strategies and rules
    /// </summary>
    public class UniversalInventory : BaseInventory, IPlacementInventory, IShapedDragTargetResolver, IInventorySnapshotProvider, IDropPolicyProvider, IInventoryRuleEvaluator, IDynamicSlotLifecycle, IInventoryEventSink, IInventoryInteraction, IInventorySlotCreationCapacity
    {
        [FoldoutGroup("Slot Setup", expanded: true)]
        [SerializeField, Required, Tooltip("Slot container")]
        private Transform _slotContainer;

        [FormerlySerializedAs("_slotPrefab")]
        [FoldoutGroup("Slot Setup")]
        [SerializeField, Required, Tooltip("Slot prefab")]
        private BaseSlot baseSlotPrefab;

        [FoldoutGroup("Slot Setup")]
        [SerializeField, Tooltip("Initial slot count")]
        private int _initialSlotCount = 10;

        [FoldoutGroup("Strategy", expanded: true)]
        [InfoBox("Inventory strategy controls placement, drag defaults, and stack behavior.", InfoMessageType.Info)]
        [SerializeReference, ManagedReferencePicker, InlineProperty, HideLabel]
        private InventoryStrategyBase _inventoryStrategy = new StackableItemStrategy();

        [FoldoutGroup("Strategy")]
        [SerializeReference, ManagedReferencePicker, InlineProperty, HideLabel]
        private SlotManagementSettingsBase _slotManagementSettings = new FixedSlotManagementSettings();

        [FoldoutGroup("Rules")]
        [SerializeField, HideLabel]
        private InventoryRuleValidator _ruleValidator = new InventoryRuleValidator();

        [FoldoutGroup("Drop Policy")]
        [SerializeField, HideLabel]
        private DropPolicySettings _dropPolicy = new DropPolicySettings();

        [FoldoutGroup("Slot Setup", expanded: true)]
        [SerializeField, Tooltip("Slots created in the scene. Can be assigned manually in the Inspector. If empty, they will be found automatically.")]
        private List<BaseSlot> _slots = new List<BaseSlot>();

        [FoldoutGroup("Placement", expanded: false)]
        [SerializeField, Tooltip("Use fixed row-major grid topology for placement queries.")]
        private bool _useGridTopology;

        [FoldoutGroup("Placement")]
        [SerializeField, Tooltip("Grid dimensions used when grid topology is enabled."), ShowIf(nameof(_useGridTopology))]
        private GridTopology _gridTopology = new GridTopology(1, 1);

        [FoldoutGroup("Placement")]
        [SerializeReference, ManagedReferencePicker, InlineProperty, HideLabel, Tooltip("Controls how a hovered grid slot is converted to a shaped-item placement anchor."), ShowIf(nameof(_useGridTopology))]
        private ShapedPlacementAnchorStrategyBase _shapedPlacementAnchorStrategy = new RotatedGrabOffsetAnchorStrategy();

        private IStrategy _strategy;
        private BaseSlot _pointerHoveredBaseSlot;
        private BaseSlot _lastInteractedBaseSlot;
        private StrategyConfiguration _appliedStrategyConfiguration;
        
        private PlacementStore _placementStore;
        private bool _placementStoreUsesGrid;
        private GridTopology _placementStoreGridTopology;
        private DropPreviewController _dropPreviewController;

        public override IReadOnlyList<BaseSlot> Slots => _slots.AsReadOnly();
        public override int SlotCount => _slots.Count;
        public IReadOnlyCollection<Placement> Placements => EnsurePlacementStore().Placements;

        public IInventoryTopology Topology => EnsurePlacementStore().Topology;
        public InventoryRuleValidator RuleValidator => _ruleValidator;
        public BaseSlot BaseSlotPrefab => baseSlotPrefab;
        bool IInventorySlotCreationCapacity.CanCreateNewSlot =>
            _slotManagementSettings != null && _slotManagementSettings.CanCreateNewSlot(this, _slots.Count);
        int IInventorySlotCreationCapacity.PotentialNewSlots =>
            _slotManagementSettings?.GetPotentialNewSlots(this, _slots.Count) ?? 0;
        BaseSlot IInventorySlotCreationCapacity.BaseSlotPrefab => baseSlotPrefab;
        public override Transform SlotContainer => _slotContainer;
        
        public IStrategy Strategy
        {
            get
            {
                EnsureStrategyInitialized();
                return _strategy;
            }
        }

        public override InventoryDataBindingBase DataBinding { get; protected set; }

        /// <summary>
        /// Event raised when a new slot is created (after Instantiate + Initialize).
        /// Used by FreeFormSlotLayout to position dynamically created slots.
        /// </summary>
        public override event Action<BaseSlot> OnSlotCreated;

        /// <summary>
        /// Event raised when an item is added to this inventory
        /// </summary>
        public override event Action<InventoryItemEventContext> OnItemAdded;

        /// <summary>
        /// Event raised when an item is removed from this inventory
        /// </summary>
        public override event Action<InventoryItemEventContext> OnItemRemoved;

        /// <summary>
        /// Event raised when inventory content is refreshed in bulk (for example, after DataBinding.ReloadUI).
        /// Useful when items are changed via quiet APIs and item-level events are suppressed.
        /// </summary>
        public override event Action OnContentRefreshed;

        /// <summary>
        /// Event raised when an item swap affecting this inventory is attempted.
        /// A subscriber can cancel the swap via context.Cancel = true.
        /// </summary>
        public override event Action<InventorySwapContext> OnSwapAttempting;

        /// <summary>
        /// Event raised when an item swap affecting this inventory completes successfully.
        /// </summary>
        public override event Action<InventorySwapContext> OnSwapCompleted;

        public void EmitItemAdded(
            ItemStack stack,
            int slotIndex,
            IInventory sourceInventory,
            BaseSlot sourceBaseSlot,
            BaseSlot targetBaseSlot,
            PlacementSnapshot placementSnapshot = null)
        {
            placementSnapshot ??= ResolvePlacementSnapshot(targetBaseSlot);
            var context = new InventoryItemEventContext(
                stack,
                slotIndex,
                sourceInventory,
                this,
                sourceBaseSlot,
                targetBaseSlot,
                placementSnapshot);

            DataBinding?.HandleItemAdded(context);
            OnItemAdded?.Invoke(context);
        }

        public void EmitItemRemoved(
            ItemStack stack,
            int slotIndex,
            IInventory targetInventory,
            BaseSlot sourceBaseSlot,
            BaseSlot targetBaseSlot,
            PlacementSnapshot placementSnapshot = null)
        {
            placementSnapshot ??= ResolvePlacementSnapshot(sourceBaseSlot);
            var context = new InventoryItemEventContext(
                stack,
                slotIndex,
                this,
                targetInventory,
                sourceBaseSlot,
                targetBaseSlot,
                placementSnapshot);

            DataBinding?.HandleItemRemoved(context);
            OnItemRemoved?.Invoke(context);
        }

        private PlacementSnapshot ResolvePlacementSnapshot(BaseSlot baseSlot)
        {
            if (baseSlot == null || !ReferenceEquals(baseSlot.Inventory, this))
                return null;

            var placement = EnsurePlacementStore().GetAt(baseSlot.Index);
            if (placement != null)
                return PlacementSnapshot.FromPlacement(placement, GetSlot);

            return new PlacementSnapshot(
                baseSlot.Index,
                0,
                Vector2Int.one,
                new[] { baseSlot.Index },
                baseSlot,
                new[] { baseSlot },
                coveredOffsets: new[] { Vector2Int.zero });
        }

        /// <summary>
        /// Remove items from a slot in this inventory and emit events.
        /// Used by external drop processors such as WorldDropZone.
        /// </summary>
        /// <returns>Number of removed items, or 0 if nothing was removed</returns>
        public override int RemoveItemsFromSlot(BaseSlot sourceBaseSlot, ItemStack stackToRemove, IInventory targetInventory = null, BaseSlot targetBaseSlot = null)
        {
            if (sourceBaseSlot == null || stackToRemove == null || stackToRemove.IsEmpty)
                return 0;

            if (!TryRemoveFromSlot(sourceBaseSlot, stackToRemove.Adapters, out int removed) || removed <= 0)
                return 0;

            sourceBaseSlot.UpdateVisuals();

            EmitItemRemoved(stackToRemove, sourceBaseSlot.Index, targetInventory, sourceBaseSlot, targetBaseSlot);
            HandleSlotEmptied(sourceBaseSlot);

            return removed;
        }

        public void EmitSwapAttempting(InventorySwapContext context)
        {
            OnSwapAttempting?.Invoke(context);
        }

        public void EmitSwapCompleted(InventorySwapContext context)
        {
            OnSwapCompleted?.Invoke(context);
        }

        public override void NotifyContentRefreshed()
        {
            OnContentRefreshed?.Invoke();
        }

        private void Start()
        {
            EnsureStrategyInitialized();
        }

        public override void Initialize(InventoryDataBindingBase inventoryDataBindingBase)
        {
            DataBinding = inventoryDataBindingBase;
            EnsureStrategyInitialized();
        }

        private void OnValidate()
        {
            EnsureInventoryStrategySettings();
            EnsureSlotManagementSettings();
            EnsurePlacementSettings();
            ResolveShapedPlacementAnchorStrategy();

            // Sort rules when values change in the Inspector
            _ruleValidator?.OnValidate();

            if (!Application.isPlaying || _strategy == null)
                return;

            var currentConfiguration = CaptureStrategyConfiguration();
            if (_appliedStrategyConfiguration.Equals(currentConfiguration))
                return;

            RefreshStrategy();
        }

        private void OnDisable()
        {
            _pointerHoveredBaseSlot = null;
            _lastInteractedBaseSlot = null;
        }

        [FoldoutGroup("Slot Setup", expanded: true), Button("Cache Slots")]
        private void CacheSlots()
        {
            if (_slotContainer == null)
            {
                Debug.LogError($"[{name}] CacheSlots: _slotContainer is NULL!");
                return;
            }
            if (_slots == null)
            {
                _slots = new List<BaseSlot>();
            }

            // Remove possible null references
            _slots.RemoveAll(slot => slot == null);


            var autoSlots = _slotContainer.GetComponentsInChildren<BaseSlot>(includeInactive: true).ToList();
            foreach (var slot in autoSlots)
            {
                if (slot != null && !_slots.Contains(slot))
                {
                    _slots.Add(slot);
                }
            }
            Extensions.DragAndDropLog($"<color=magenta>[{name}] CacheSlots completed! Found {_slots.Count} slots.</color>");
        }
        private void InitializeSlots()
        {
            CacheSlots();

            // Slots destroyed since the last cache would otherwise keep their place in the list and
            // push every following slot onto an index the placement store knows nothing about.
            _slots.RemoveAll(slot => slot == null);

            for (int i = 0; i < _slots.Count; i++)
            {
                _slots[i].Initialize(i, this);
            }

            for (int i = _slots.Count; i < _initialSlotCount; i++)
            {
                CreateSlot();
            }
        }

        private BaseSlot CreateSlot()
        {
            Extensions.DragAndDropLog($"<color=magenta>[{name}] CreateSlot called! Current count: {_slots.Count}</color>");

            if (baseSlotPrefab == null)
            {
                Extensions.DragAndDropLog($"[{name}] CreateSlot: _slotPrefab is NULL!");
                return null;
            }

            if (_slotContainer == null)
            {
                Extensions.DragAndDropLog($"[{name}] CreateSlot: _slotContainer is NULL!");
                return null;
            }

            var slotGO = Instantiate(baseSlotPrefab, _slotContainer);
            slotGO.Initialize(_slots.Count, this);
            _slots.Add(slotGO);

            Extensions.DragAndDropLog($"<color=magenta>[{name}] CreateSlot SUCCESS! New count: {_slots.Count}</color>");
            OnSlotCreated?.Invoke(slotGO);
            return slotGO;
        }

        private void InitializeStrategy()
        {
            EnsureInventoryStrategySettings();
            EnsureSlotManagementSettings();
            EnsurePlacementSettings();

            _strategy = _inventoryStrategy ?? throw new ArgumentNullException(nameof(_inventoryStrategy));
            Extensions.DragAndDropLog($"<color=yellow>[{name}] Strategy: {_inventoryStrategy.GetType().Name}</color>");
            _appliedStrategyConfiguration = CaptureStrategyConfiguration();
        }

        public void RefreshStrategy()
        {
            Debug.Log($"[{name}] RefreshStrategy called! Current strategy: {_strategy?.GetType().Name}, current config: {_appliedStrategyConfiguration}");
            if (_slots == null)
                _slots = new List<BaseSlot>();

            if (_slots.Count == 0)
                InitializeSlots();

            EnsurePlacementStore();
            InitializeStrategy();

            if (Application.isPlaying && DataBinding != null)
            {
                DataBinding.ReloadUI();
            }

            EnsureFreeSlots();
            UpdateAllVisuals();
        }

        private ShapedPlacementAnchorStrategyBase ResolveShapedPlacementAnchorStrategy()
        {
            return _shapedPlacementAnchorStrategy ??= new RotatedGrabOffsetAnchorStrategy();
        }

        private void EnsureStrategyInitialized()
        {
            if (_strategy != null)
                return;

            Extensions.DragAndDropLog($"<color=yellow>[{name}] Strategy not initialized, initializing now...</color>");
            InitializeSlots();
            EnsurePlacementStore();
            InitializeStrategy();
            EnsureFreeSlots();
            UpdateAllVisuals();
        }

        private StrategyConfiguration CaptureStrategyConfiguration()
        {
            EnsureInventoryStrategySettings();

            return new StrategyConfiguration(
                _inventoryStrategy?.GetType().AssemblyQualifiedName,
                _inventoryStrategy?.CaptureConfigurationJson(),
                _slotManagementSettings?.GetType().AssemblyQualifiedName,
                _slotManagementSettings?.CaptureConfigurationJson(),
                _useGridTopology,
                _gridTopology.Normalized().ToString());
        }

        /// <summary>
        /// Add a rule to the inventory
        /// </summary>
        public void AddRule(IInventoryRule rule)
        {
            _ruleValidator.AddRule(rule);
        }

        /// <summary>
        /// Remove a rule
        /// </summary>
        public void RemoveRule(IInventoryRule rule)
        {
            _ruleValidator.RemoveRule(rule);
        }

        public override BaseSlot GetSlot(int index)
        {
            if (index >= 0 && index < _slots.Count)
                return _slots[index];
            return null;
        }

        public Placement GetPlacementAt(BaseSlot baseSlot)
        {
            if (baseSlot == null || !ReferenceEquals(baseSlot.Inventory, this))
                return null;

            return GetPlacementAt(baseSlot.Index);
        }

        public Placement GetPlacementAt(int cellIndex)
        {
            return EnsurePlacementStore().GetAt(cellIndex);
        }

        public IReadOnlyList<int> GetCoveredCells(
            int anchorIndex,
            IPlacementShape shape,
            int orientation = 0)
        {
            return BuildCoveredCells(anchorIndex, shape, orientation);
        }

        private Vector2Int GetCellForIndex(int index)
        {
            EnsurePlacementSettings();
            return IndexToCell(index);
        }

        private bool TryGetIndexForCell(Vector2Int cell, out int index)
        {
            EnsurePlacementSettings();
            return EnsurePlacementStore().Topology.TryToIndex(cell, out index);
        }

        public override Vector2Int GetGrabOffset(Placement placement, BaseSlot baseSlot)
        {
            if (placement == null || baseSlot == null || !ReferenceEquals(baseSlot.Inventory, this))
                return Vector2Int.zero;

            return GetCellForIndex(baseSlot.Index) - placement.AnchorCell;
        }

        public void SetShapedPlacementAnchorStrategy(ShapedPlacementAnchorStrategyBase strategy)
        {
            _shapedPlacementAnchorStrategy = strategy ?? new RotatedGrabOffsetAnchorStrategy();
        }

        public bool TryResolveShapedPlacementAnchorCell(
            BaseSlot targetBaseSlot,
            DragContext context,
            DragEntry entry,
            IPlacementShape shape,
            IItemAdapter targetItemAdapter,
            out Vector2Int anchorCell)
        {
            anchorCell = Vector2Int.zero;
            if (targetBaseSlot == null || !ReferenceEquals(targetBaseSlot.Inventory, this))
                return false;

            var strategyContext = new ShapedPlacementAnchorContext(
                this,
                targetBaseSlot,
                context,
                entry,
                shape,
                entry.Orientation,
                targetItemAdapter);
            return ResolveShapedPlacementAnchorStrategy().TryResolveAnchorCell(strategyContext, out anchorCell);
        }

        public bool TryResolveShapedPlacementAnchor(
            BaseSlot targetBaseSlot,
            DragContext context,
            DragEntry entry,
            IPlacementShape shape,
            IItemAdapter targetItemAdapter,
            out Vector2Int anchorCell,
            out int anchorIndex)
        {
            anchorIndex = -1;
            if (!TryResolveShapedPlacementAnchorCell(
                    targetBaseSlot,
                    context,
                    entry,
                    shape,
                    targetItemAdapter,
                    out anchorCell))
                return false;

            return TryGetIndexForCell(anchorCell, out anchorIndex);
        }

        public bool CanPlace(
            PlacementRequest request,
            Placement ignoredA = null,
            Placement ignoredB = null)
        {
            return EnsurePlacementStore().CanPlace(request, ignoredA, ignoredB);
        }

        public bool TryGetDropPreviewSlots(
            BaseSlot targetBaseSlot,
            DragContext context,
            out IReadOnlyList<BaseSlot> previewSlots,
            out bool canPlace)
        {
            return EnsureDropPreviewController()
                .TryGetDropPreviewSlots(targetBaseSlot, context, out previewSlots, out canPlace);
        }

        public bool ShowDropPreview(BaseSlot targetBaseSlot, DragContext context)
        {
            return EnsureDropPreviewController().ShowDropPreview(targetBaseSlot, context);
        }

        public void ClearDropPreview()
        {
            EnsureDropPreviewController().ClearDropPreview();
        }

        public bool TryPlace(PlacementRequest request)
        {
            return TryPlace(request, out _);
        }

        public bool TryPlace(PlacementRequest request, out Placement placement)
        {
            return EnsurePlacementStore().TryPlace(request, out placement);
        }

        public bool RemovePlacement(Placement placement)
        {
            if (placement == null)
                return false;

            return EnsurePlacementStore().Remove(placement);
        }

        public bool RemovePlacementAt(BaseSlot baseSlot)
        {
            if (baseSlot == null || !ReferenceEquals(baseSlot.Inventory, this))
                return false;

            return RemovePlacementAt(baseSlot.Index);
        }

        public bool RemovePlacementAt(int cellIndex)
        {
            return EnsurePlacementStore().RemoveAt(cellIndex);
        }

        public override bool TryGetStackForSlot(BaseSlot baseSlot, out IReadOnlyItemStack stack)
        {
            stack = null;
            if (baseSlot == null || !ReferenceEquals(baseSlot.Inventory, this))
                return false;

            stack = EnsurePlacementStore().GetAt(baseSlot.Index)?.Stack ?? ItemStack.Empty();
            return true;
        }

        private bool TryGetMutableStackForSlot(BaseSlot baseSlot, out ItemStack stack)
        {
            stack = null;
            if (baseSlot == null || !ReferenceEquals(baseSlot.Inventory, this))
                return false;

            stack = EnsurePlacementStore().GetAt(baseSlot.Index)?.MutableStack ?? ItemStack.Empty();
            return true;
        }

        public override bool TrySetStackForSlot(BaseSlot baseSlot, ItemStack stack)
        {
            if (baseSlot == null || !ReferenceEquals(baseSlot.Inventory, this))
                return false;

            var placementStore = EnsurePlacementStore();
            var existingPlacement = placementStore.GetAt(baseSlot.Index);

            if (stack == null || stack.IsEmpty)
            {
                if (existingPlacement != null)
                    placementStore.Remove(existingPlacement);

                return true;
            }

            if (existingPlacement != null && existingPlacement.AnchorIndex != baseSlot.Index)
                return false;

            var request = new PlacementRequest(
                stack,
                baseSlot.Index,
                0,
                PlacementShapeUtility.Resolve(stack.PrimaryAdapter));

            if (existingPlacement != null && !placementStore.CanPlace(request, existingPlacement))
                return false;

            if (existingPlacement != null)
                placementStore.Remove(existingPlacement);

            if (!placementStore.TryPlace(request, out _))
            {
                if (existingPlacement != null)
                {
                    var rollbackRequest = new PlacementRequest(
                        existingPlacement.MutableStack,
                        existingPlacement.AnchorIndex,
                        existingPlacement.Orientation,
                        existingPlacement.Shape);
                    placementStore.TryPlace(rollbackRequest, out _);
                }

                return false;
            }

            return true;
        }

        public override bool TryClearSlot(BaseSlot baseSlot)
            => TrySetStackForSlot(baseSlot, ItemStack.Empty());

        public override bool TryGetPlacementAt(BaseSlot baseSlot, out Placement placement)
        {
            placement = GetPlacementAt(baseSlot);
            return placement != null;
        }

        public override bool TrySplitFromSlot(BaseSlot baseSlot, int amount, out ItemStack splitStack)
        {
            splitStack = ItemStack.Empty();
            if (!TryGetMutableStackForSlot(baseSlot, out var stack) || stack == null || stack.IsEmpty)
                return false;

            splitStack = stack.Split(amount);
            if (splitStack == null || splitStack.IsEmpty)
                return false;

            if (stack.IsEmpty)
                RemovePlacementAt(baseSlot);

            baseSlot.UpdateVisuals();
            return true;
        }

        /// <summary>
        /// Split <paramref name="amount"/> items out of <paramref name="sourceBaseSlot"/> into a brand new slot,
        /// without merging back into the source or any existing stack.
        /// Intended for free-form layouts where a partial drop should become a separate stack
        /// at the drop point (the new slot fires <see cref="OnSlotCreated"/> so a layout can position it).
        /// A full-stack move should reposition the existing slot instead of calling this.
        /// </summary>
        /// <returns>True if a new slot was created and filled with the split portion.</returns>
        public bool TrySplitIntoNewSlot(BaseSlot sourceBaseSlot, int amount, out BaseSlot newBaseSlot)
        {
            newBaseSlot = null;

            if (sourceBaseSlot == null || !ReferenceEquals(sourceBaseSlot.Inventory, this) || amount <= 0)
                return false;

            EnsureStrategyInitialized();

            // Partial split only: leave at least one item in the source slot.
            if (sourceBaseSlot.IsEmpty || sourceBaseSlot.Stack == null || sourceBaseSlot.Stack.Count <= amount)
                return false;

            if (!TrySplitFromSlot(sourceBaseSlot, amount, out var splitStack) || splitStack == null || splitStack.IsEmpty)
                return false;

            // Keep a copy for events: TrySetStackForSlot takes ownership of splitStack.
            var movedStackForEvents = splitStack.CreateCopy();

            var createdSlot = CreateSlot();
            if (createdSlot == null || !TrySetStackForSlot(createdSlot, splitStack))
            {
                // Roll back: return the split portion to the source slot.
                if (!TryAddToSlotStack(sourceBaseSlot, splitStack))
                    TrySetStackForSlot(sourceBaseSlot, splitStack);
                sourceBaseSlot.UpdateVisuals();

                if (createdSlot != null && createdSlot.IsEmpty)
                    TryRemoveSlot(createdSlot);

                return false;
            }

            createdSlot.UpdateVisuals();
            sourceBaseSlot.UpdateVisuals();

            // Report as a slot-to-slot move within this inventory.
            EmitItemRemoved(movedStackForEvents, sourceBaseSlot.Index, this, sourceBaseSlot, createdSlot);
            EmitItemAdded(movedStackForEvents, createdSlot.Index, this, sourceBaseSlot, createdSlot);

            newBaseSlot = createdSlot;
            return true;
        }

        public override bool TryAddToSlotStack(BaseSlot baseSlot, ItemStack stack)
        {
            if (baseSlot == null || stack == null || stack.IsEmpty)
                return false;

            if (!TryGetMutableStackForSlot(baseSlot, out var existingStack) || existingStack == null || existingStack.IsEmpty)
                return TrySetStackForSlot(baseSlot, stack);

            bool added = existingStack.TryAddToStack(stack);
            if (added)
                baseSlot.UpdateVisuals();

            return added;
        }

        public override bool TryRemoveFromSlot(BaseSlot baseSlot, IReadOnlyList<IItemAdapter> adapters, out int removed)
        {
            removed = 0;
            if (!TryGetMutableStackForSlot(baseSlot, out var stack) || stack == null || stack.IsEmpty)
                return false;

            removed = stack.RemoveAdapters(adapters);
            if (removed <= 0)
                return false;

            if (stack.IsEmpty)
                RemovePlacementAt(baseSlot);

            baseSlot.UpdateVisuals();
            return true;
        }

        private PlacementStore EnsurePlacementStore()
        {
            var normalizedGrid = _gridTopology.Normalized();
            bool settingsChanged =
                _placementStore == null ||
                _placementStoreUsesGrid != _useGridTopology ||
                !_placementStoreGridTopology.Equals(normalizedGrid);

            if (!settingsChanged)
                return _placementStore;

            var previousPlacements = _placementStore?.Placements != null
                ? _placementStore.Placements.ToList()
                : null;
            _placementStore = new PlacementStore(CreatePlacementTopology(normalizedGrid));
            _placementStoreUsesGrid = _useGridTopology;
            _placementStoreGridTopology = normalizedGrid;

            if (previousPlacements != null)
            {
                int droppedPlacements = 0;
                for (int i = 0; i < previousPlacements.Count; i++)
                {
                    var placement = previousPlacements[i];
                    if (placement == null || placement.MutableStack == null || placement.MutableStack.IsEmpty)
                        continue;

                    var request = new PlacementRequest(
                        placement.MutableStack,
                        placement.AnchorIndex,
                        placement.Orientation,
                        placement.Shape);
                    if (!_placementStore.TryPlace(request, out _))
                    {
                        droppedPlacements++;
                        Debug.LogWarning(
                            $"[{name}] Dropped placement at anchor {placement.AnchorIndex} ({placement.BoundingSize}, {placement.Orientation}) after placement store settings changed.");
                    }
                }

                if (droppedPlacements > 0)
                {
                    Debug.LogWarning(
                        $"[{name}] Dropped {droppedPlacements} placement(s) while rebuilding placement store. Check grid topology and slot count settings.");
                }
            }

            return _placementStore;
        }

        private DropPreviewController EnsureDropPreviewController()
        {
            return _dropPreviewController ??= new DropPreviewController(
                this,
                this,
                EnsurePlacementStore);
        }

        private IInventoryTopology CreatePlacementTopology(GridTopology normalizedGrid)
        {
            return _useGridTopology
                ? (IInventoryTopology)new SlotCountLimitedTopology(new RectGridTopology(normalizedGrid), () => _slots?.Count ?? 0)
                : new SlotTopology(() => _slots?.Count ?? 0);
        }

        private IInventoryTopology CreatePlacementTopology(GridTopology normalizedGrid, int slotCount)
        {
            slotCount = Mathf.Max(0, slotCount);
            return _useGridTopology
                ? (IInventoryTopology)new SlotCountLimitedTopology(new RectGridTopology(normalizedGrid), slotCount)
                : new SlotTopology(slotCount);
        }

        private void ResetPlacementState()
        {
            EnsurePlacementStore().Reset();
        }

        private void ClearInitializedPlacementState()
        {
            EnsurePlacementStore().Reset();
        }

        private IReadOnlyList<int> BuildCoveredCells(
            int anchorIndex,
            IPlacementShape shape,
            int orientation)
        {
            return EnsurePlacementStore().GetCoveredIndices(
                anchorIndex,
                shape,
                orientation,
                PlacementBoundsMode.RequireAllInBounds);
        }

        private Vector2Int IndexToCell(int index)
        {
            return EnsurePlacementStore().Topology.ToCell(index);
        }

        private void ShiftPlacementIndicesAfterSlotRemoved(int removedIndex)
        {
            EnsurePlacementStore().ShiftAfterSlotRemoved(removedIndex);
        }

        /// <summary>
        /// Set the stack limit at runtime (for example, from DataBinding).
        /// maxStackSize = 0 means unlimited.
        /// </summary>
        public override void SetMaxStackSize(int maxStackSize, bool allowItemOverride = false)
        {
            EnsureInventoryStrategySettings();
            _inventoryStrategy.SetMaxStackSize(maxStackSize, allowItemOverride);
            _strategy?.SetMaxStackSize(maxStackSize, allowItemOverride);
        }

        public override bool TryAddStack(ItemStack stack, int targetSlotIndex = -1)
        {
            if (stack == null || stack.IsEmpty)
                return false;

            EnsureStrategyInitialized();
            Extensions.DragAndDropLog($"<color=cyan>[{name}] TryAddStack: {stack.DisplayName} x{stack.Count}, targetSlot={targetSlotIndex}, currentSlots={_slots.Count}, strategy={_strategy?.GetType().Name}</color>");

            bool success = TryAddStackViaCandidates(stack, targetSlotIndex);

            if (success)
            {
                Extensions.DragAndDropLog($"<color=green>[{name}] TryAddStack SUCCESS! Now have {_slots.Count} slots</color>");
                UpdateAllVisuals();
            }
            else
            {
                Extensions.DragAndDropLog($"<color=red>[{name}] TryAddStack FAILED!</color>");
            }

            return success;
        }

        public override bool TryAddStackQuiet(ItemStack stack, int targetSlotIndex = -1)
        {
            if (stack == null || stack.IsEmpty)
                return false;

            EnsureStrategyInitialized();
            return TryAddStackViaCandidates(stack, targetSlotIndex);
        }

        private bool TryAddStackViaCandidates(ItemStack stack, int targetSlotIndex)
        {
            var geometry = new InventoryPlacementGeometry(this);

            // Explicit target: try only that slot, no spill into other slots.
            if (targetSlotIndex >= 0)
            {
                var preferredSlot = GetSlot(targetSlotIndex);
                if (preferredSlot == null)
                {
                    // Target slot doesn't exist yet; create dynamic slots until it does.
                    var lifecycle = this as IDynamicSlotLifecycle;
                    while (GetSlot(targetSlotIndex) == null)
                    {
                        if (lifecycle == null || !lifecycle.TryCreateSlot(out _))
                            return false;
                    }
                    preferredSlot = GetSlot(targetSlotIndex);
                    if (preferredSlot == null)
                        return false;
                }
                var request = new InventoryAcceptanceRequest(this, stack.PrimaryAdapter, stack.Count);
                if (!_strategy.TryGetCandidate(geometry, request, preferredSlot, out var candidate))
                    return false;
                int amount = Math.Min(stack.Count, candidate.Capacity);
                if (amount <= 0)
                    return false;
                var subStack = stack.Split(amount);
                if (!TryApplyAddCandidate(candidate, subStack))
                    stack.TryAddToStack(subStack);
                return stack.IsEmpty;
            }

            // No explicit target: distribute across best candidates.
            while (!stack.IsEmpty)
            {
                var request = new InventoryAcceptanceRequest(this, stack.PrimaryAdapter, stack.Count);
                bool progress = false;

                var candidates = _strategy.GetCandidates(geometry, request);
                foreach (var candidate in candidates)
                {
                    int amount = Math.Min(stack.Count, candidate.Capacity);
                    if (amount <= 0)
                        continue;

                    var subStack = stack.Split(amount);
                    if (!TryApplyAddCandidate(candidate, subStack))
                        stack.TryAddToStack(subStack);
                    else
                    {
                        progress = true;
                        break;
                    }
                }

                if (!progress)
                    break;
            }

            return stack.IsEmpty;
        }

        private bool TryApplyAddCandidate(PlacementCandidate candidate, ItemStack subStack)
        {
            switch (candidate.Kind)
            {
                case PlacementCandidateKind.Merge:
                {
                    var anchorSlot = candidate.TargetPlacement != null
                        ? GetSlot(candidate.TargetPlacement.AnchorIndex)
                        : candidate.Anchor;
                    return anchorSlot != null && TryAddToSlotStack(anchorSlot, subStack);
                }
                case PlacementCandidateKind.Create:
                {
                    var anchorSlot = candidate.Anchor;
                    if (anchorSlot == null)
                        return false;
                    var shape = candidate.Shape ?? PlacementShapeUtility.Resolve(subStack.PrimaryAdapter);
                    var placementReq = new PlacementRequest(subStack, anchorSlot.Index, candidate.Orientation, shape);
                    return CanPlace(placementReq) && TryPlace(placementReq);
                }
                case PlacementCandidateKind.NewDynamicSlot:
                {
                    if (!((IDynamicSlotLifecycle)this).TryCreateSlot(out var newSlot) || newSlot == null)
                        return false;
                    return TrySetStackForSlot(newSlot, subStack);
                }
                default:
                    return false;
            }
        }

        public bool CanAcceptByRules(
            BaseSlot baseSlot,
            IItemAdapter itemAdapter,
            int previewCount,
            InventoryAcceptanceRequest request = null,
            bool allowForeignSlot = false)
        {
            if (baseSlot == null || itemAdapter == null || previewCount <= 0)
                return false;

            if (!allowForeignSlot && !ReferenceEquals(baseSlot.Inventory, this))
                return false;

            var previewStack = request?.CreatePreviewStack(previewCount, itemAdapter);
            if (previewStack == null && !ItemStack.TryCreate(new[] { itemAdapter }, out previewStack))
                return false;

            return ValidateRulesForPreview(baseSlot, previewStack, request);
        }

        private bool ValidateRulesForPreview(BaseSlot baseSlot, ItemStack previewStack, InventoryAcceptanceRequest request)
        {
            if (baseSlot == null || previewStack == null || previewStack.IsEmpty)
                return false;

            var context = request?.CreateValidationContext(baseSlot, previewStack.Count, previewStack.PrimaryAdapter)
                ?? new DragContext(previewStack, null, null, baseSlot, this);

            // Force this inventory and slot as the drop target so the shared evaluator consults
            // this inventory's rules, this binding's CanDrop, and this slot's rules — the single
            // source of truth for drop validation (RuleEvaluationService.ValidateEntryDrop).
            context = context.WithTarget(baseSlot, this);

            return new RuleEvaluationService()
                .ValidateEntryDrop(context, context.Entries[0])
                .IsValid;
        }

        public InventorySnapshot CaptureSnapshot()
        {
            var placementStore = EnsurePlacementStore();
            return PlacementSnapshotCodec.Capture(_slots.Count, placementStore.Placements);
        }

        public void RestoreSnapshot(InventorySnapshot snapshot)
        {
            TryRestoreSnapshot(snapshot);
        }

        public bool TryRestoreSnapshot(InventorySnapshot snapshot, bool logFailures = true)
        {
            if (snapshot == null)
                return false;

            int desiredCount = snapshot.SlotCount;
            var topology = CreatePlacementTopology(_gridTopology.Normalized(), desiredCount);
            if (!PlacementSnapshotCodec.TryBuildPlacementRequests(
                    snapshot,
                    topology,
                    out var placementRequests,
                    out var failedPlacement))
            {
                if (logFailures)
                    LogSnapshotRestoreFailure(failedPlacement);

                return false;
            }

            if (_slots == null)
            {
                _slots = new List<BaseSlot>();
            }

            // Destroyed slots still occupy a place in the list, so drop them before the counts
            // below decide how many slots to create or trim.
            _slots.RemoveAll(slot => slot == null);

            // Increase the number of slots to the required amount
            while (_slots.Count < desiredCount)
            {
                CreateSlot();
            }

            // Remove extra slots (if new ones were created during a failed operation)
            while (_slots.Count > desiredCount)
            {
                var slot = _slots[_slots.Count - 1];
                RemovePlacementAt(slot);
                _slots.RemoveAt(_slots.Count - 1);
                if (slot?.Transform != null)
                {
                    DestroySlotObject(slot.Transform.gameObject);
                }
            }

            for (int i = 0; i < _slots.Count; i++)
                _slots[i].SetInventoryIndex(i, this);

            ClearInitializedPlacementState();

            for (int i = 0; i < placementRequests.Count; i++)
            {
                var request = placementRequests[i];
                if (!TryPlace(request))
                {
                    if (logFailures)
                    {
                        Debug.LogError(
                            $"[{nameof(UniversalInventory)}] Failed to restore placement at anchor {request.AnchorIndex} ({request.BoundingSize}, {request.Orientation}).",
                            this);
                    }

                    ClearInitializedPlacementState();
                    UpdateAllVisuals();
                    return false;
                }
            }

            UpdateAllVisuals();
            return true;
        }

        private void LogSnapshotRestoreFailure(InventoryPlacementState placementState)
        {
            Debug.LogError(
                $"[{nameof(UniversalInventory)}] Failed to restore placement at anchor {placementState.AnchorIndex} ({placementState.BoundingSize}, {placementState.Orientation}).",
                this);
        }

        public override void ReInitSlots(int slotCount)
        {
            slotCount = Mathf.Max(0, slotCount);
            _initialSlotCount = slotCount;

            _pointerHoveredBaseSlot = null;
            _lastInteractedBaseSlot = null;

            if (_slots == null)
                _slots = new List<BaseSlot>();

            // Take ownership of strategy initialization before rebuilding the slot list. Left to
            // itself, _strategy stays null and the next entry point (Start, Initialize, or simply
            // reading the Strategy property) falls into InitializeSlots -> CacheSlots, which
            // re-scans the container and undoes the rebuild below.
            if (_strategy == null)
                InitializeStrategy();

            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                BaseSlot baseSlot = _slots[i];
                if (baseSlot?.Transform != null)
                {
                    baseSlot.Transform.gameObject.SetActive(false);
                    DestroySlotObject(baseSlot.Transform.gameObject);
                }
            }

            _slots.Clear();
            ResetPlacementState();

            for (int i = 0; i < slotCount; i++)
                CreateSlot();

            EnsurePlacementStore();
            UpdateAllVisuals();
        }

        public override bool Contains(IItemAdapter itemAdapter)
        {
            if (itemAdapter == null)
                return false;
            foreach (var placement in EnsurePlacementStore().Placements)
            {
                if (placement?.Stack != null && placement.Stack.CanStack(itemAdapter))
                    return true;
            }
            return false;
        }
        
        public override void UpdateAllVisuals()
        {
            // A destroyed slot must never take down the caller: UpdateAllVisuals runs from
            // DragAndDropManager.EndDrag, where an escaping exception would strand the drag state.
            foreach (var slot in _slots)
            {
                if (slot == null)
                    continue;

                slot.UpdateVisuals();
            }
        }

        /// <summary>
        /// Clear the entire inventory
        /// </summary>
        public override void ClearAll()
        {
            EnsureStrategyInitialized();

            foreach (var slot in _slots)
            {
                if (slot == null)
                    continue;

                slot.Clear();
            }

            _pointerHoveredBaseSlot = null;
            _lastInteractedBaseSlot = null;
            ClearInitializedPlacementState();
        }

        /// <summary>
        /// Get all non-empty stacks
        /// </summary>
        public List<ItemStack> GetAllStacks()
        {
            var stacks = new List<ItemStack>();
            foreach (var slot in _slots)
            {
                if (slot != null && !slot.IsEmpty)
                {
                    stacks.Add(slot.Stack.CreateCopy());
                }
            }
            return stacks;
        }

        public IReadOnlyList<IReadOnlyItemStack> GetAllStacksReadOnly()
        {
            var stacks = new List<IReadOnlyItemStack>();
            foreach (var slot in _slots)
            {
                if (slot != null && !slot.IsEmpty)
                    stacks.Add(slot.Stack);
            }

            return stacks;
        }

        /// <summary>
        /// Get all unique items
        /// </summary>
        public List<IItemAdapter> GetUniqueItems()
        {
            var items = new List<IItemAdapter>();
            var addedIds = new HashSet<string>();

            foreach (var slot in _slots)
            {
                if (slot != null && !slot.IsEmpty && !addedIds.Contains(slot.Stack.ID))
                {
                    items.Add(slot.Stack.PrimaryAdapter);
                    addedIds.Add(slot.Stack.ID);
                }
            }

            return items;
        }

        public void NotifyPointerEnter(BaseSlot baseSlot)
        {
            if (baseSlot == null || !ReferenceEquals(baseSlot.Inventory, this))
                return;

            _pointerHoveredBaseSlot = baseSlot;
        }

        public void NotifyPointerExit(BaseSlot baseSlot)
        {
            if (_pointerHoveredBaseSlot == baseSlot)
            {
                _pointerHoveredBaseSlot = null;
            }
        }

        public void NotifySlotInteracted(BaseSlot baseSlot)
        {
            if (baseSlot == null || !ReferenceEquals(baseSlot.Inventory, this))
                return;

            _lastInteractedBaseSlot = baseSlot;
        }

        /// <summary>
        /// Find the active slot for auto-transfer.
        /// Priority: cursor -> selected UI element -> last interacted slot.
        /// </summary>
        public BaseSlot ResolveAutoTransferSlot()
        {
            if (_pointerHoveredBaseSlot != null && ReferenceEquals(_pointerHoveredBaseSlot.Inventory, this))
                return _pointerHoveredBaseSlot;

            var selectedObject = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;

            if (selectedObject != null)
            {
                var selectedSlot = selectedObject.GetComponent<BaseSlot>()
                    ?? selectedObject.GetComponentInParent<BaseSlot>();

                if (selectedSlot != null && ReferenceEquals(selectedSlot.Inventory, this))
                    return selectedSlot;
            }

            if (_lastInteractedBaseSlot != null && ReferenceEquals(_lastInteractedBaseSlot.Inventory, this))
                return _lastInteractedBaseSlot;

            return null;
        }

        /// <summary>
        /// Get the number of items to drag from a slot.
        /// Without parameters it uses inventory settings, with parameters it uses the provided overrides.
        /// </summary>
        public override int GetDragAmount(BaseSlot baseSlot, DragAmount? overrideAmount = null, int? overrideCustom = null)
        {
            if (baseSlot == null || baseSlot.IsEmpty)
                return 0;

            EnsureStrategyInitialized();
            var amount = overrideAmount ?? DragAmount.All;
            var custom = overrideAmount.HasValue ? (overrideCustom ?? 0) : 0;
            var result = _inventoryStrategy.ResolveDragAmount(baseSlot.Stack.Count, amount, custom);

            if (_dragAmountStep > 1)
            {
                result = _dragAmountStepRounding switch
                {
                    DragAmountStepRounding.Ceil    => ((result + _dragAmountStep - 1) / _dragAmountStep) * _dragAmountStep,
                    DragAmountStepRounding.Nearest => ((result + _dragAmountStep / 2) / _dragAmountStep) * _dragAmountStep,
                    _                              => (result / _dragAmountStep) * _dragAmountStep
                };

                result = Math.Min(result, baseSlot.Stack.Count);
            }

            return result;
        }

        private int _dragAmountStep;
        private DragAmountStepRounding _dragAmountStepRounding;

        /// <summary>
        /// Current drag rounding step. 0 or 1 means no rounding.
        /// </summary>
        public int DragAmountStep => _dragAmountStep;

        /// <summary>
        /// Round drag amount to a multiple of step.
        /// step &lt;= 1 means no rounding.
        /// </summary>
        public override void SetDragAmountStep(int step, DragAmountStepRounding rounding = DragAmountStepRounding.Floor)
        {
            _dragAmountStep = step;
            _dragAmountStepRounding = rounding;
        }

        internal int GetMaxStackSizeForItem(IItemAdapter itemAdapter)
        {
            EnsureInventoryStrategySettings();
            return _inventoryStrategy.GetMaxStackSizeForItem(itemAdapter);
        }

        public ResolvedDropPolicy ResolveDropPolicy(DropRequestPolicy? requested, DragContext context)
        {
            return _dropPolicy.Resolve(requested, context);
        }

        /// <summary>
        /// Try to add an item to a specific slot with automatic merge handling
        /// </summary>
        /// <param name="stack">Item stack to add</param>
        /// <param name="targetBaseSlot">Target slot</param>
        /// <param name="sourceInventory">Source inventory (for events)</param>
        /// <param name="sourceSlotIndex">Source slot index (for events)</param>
        public override bool TryAddToSlot(
            ItemStack stack,
            BaseSlot targetBaseSlot,
            IInventory sourceInventory = null,
            int sourceSlotIndex = -1)
        {
            if (stack == null || stack.IsEmpty || targetBaseSlot == null)
                return false;

            EnsureStrategyInitialized();
            var geometry = new InventoryPlacementGeometry(this);
            var request = new InventoryAcceptanceRequest(this, stack.PrimaryAdapter, stack.Count);
            if (!_strategy.TryGetCandidate(geometry, request, targetBaseSlot, out var candidate))
                return false;

            int amount = Math.Min(stack.Count, candidate.Capacity);
            if (amount <= 0)
                return false;

            var subStack = stack.Split(amount);
            if (!TryApplyAddCandidate(candidate, subStack))
            {
                stack.TryAddToStack(subStack);
                return false;
            }

            UpdateAllVisuals();
            return true;
        }

        private void EnsureInventoryStrategySettings()
        {
            _inventoryStrategy ??= new StackableItemStrategy();
        }

        private void EnsureSlotManagementSettings()
        {
            _slotManagementSettings ??= new FixedSlotManagementSettings();
        }

        private void EnsurePlacementSettings()
        {
            _gridTopology = _gridTopology.Normalized();

            if (_useGridTopology && _slotManagementSettings is DynamicSlotManagementSettings)
            {
                Debug.LogError($"[{name}] Grid placement requires fixed slot management. Dynamic slot management was replaced with FixedSlotManagementSettings.");
                _slotManagementSettings = new FixedSlotManagementSettings();
            }
        }

        public override int GetAcceptableCount(InventoryAcceptanceRequest request)
        {
            if (request?.ItemAdapter == null || request.DesiredCount <= 0)
                return 0;

            EnsureStrategyInitialized();
            int result = _strategy.GetAcceptableCount(
                new InventoryPlacementGeometry(this),
                request);
            Extensions.DragAndDropLog($"<color=cyan>[{name}] GetAcceptableCount: itemAdapter={request.ItemAdapter.DisplayName}, desired={request.DesiredCount}, acceptable={result}</color>");
            return result;
        }

        /// <summary>
        /// Ensure the minimum number of free slots (for Dynamic inventory mode)
        /// </summary>
        public void EnsureFreeSlots()
        {
            EnsureSlotManagementSettings();
            _slotManagementSettings.EnsureFreeSlots(this, _initialSlotCount, CountFreeSlots, CreateSlot);
        }

        /// <summary>
        /// Notify the inventory that the specified slot became empty.
        /// Used to dynamically remove extra slots.
        /// </summary>
        public void HandleSlotEmptied(BaseSlot baseSlot)
        {
            if (baseSlot == null)
                return;

            if (!ReferenceEquals(baseSlot.Inventory, this))
                return;

            if (!_slots.Contains(baseSlot))
                return;

            if (_lastInteractedBaseSlot == baseSlot)
            {
                _lastInteractedBaseSlot = null;
            }

            if (_pointerHoveredBaseSlot == baseSlot)
            {
                _pointerHoveredBaseSlot = null;
            }

            if (!baseSlot.IsEmpty)
                return;

            RemovePlacementAt(baseSlot);

            EnsureSlotManagementSettings();
            _slotManagementSettings.HandleSlotEmptied(
                this,
                baseSlot,
                _slots.Count,
                _initialSlotCount,
                CountFreeSlots,
                FindLastEmptySlot,
                TryRemoveSlot,
                UpdateAllVisuals);
        }

        bool IDynamicSlotLifecycle.TryCreateSlot(out BaseSlot newSlot)
        {
            EnsureSlotManagementSettings();
            if (!_slotManagementSettings.CanCreateNewSlot(this, _slots.Count))
            {
                newSlot = null;
                return false;
            }
            newSlot = CreateSlot();
            return newSlot != null;
        }

        private BaseSlot FindLastEmptySlot()
        {
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                var slot = _slots[i];
                if (slot != null && slot.IsEmpty)
                    return slot;
            }

            return null;
        }

        private bool TryRemoveSlot(BaseSlot baseSlot)
        {
            if (baseSlot == null || !baseSlot.IsEmpty)
                return false;

            int index = _slots.IndexOf(baseSlot);
            if (index < 0)
                return false;

            if (_slots.Count - 1 < _initialSlotCount)
                return false;

            var slotTransform = baseSlot.Transform;
            RemovePlacementAt(baseSlot);
            ShiftPlacementIndicesAfterSlotRemoved(index);

            _slots.RemoveAt(index);
            for (int i = index; i < _slots.Count; i++)
            {
                if (_slots[i] == null)
                    continue;

                _slots[i].SetInventoryIndex(i, this);
            }

            if (slotTransform != null)
            {
                DestroySlotObject(slotTransform.gameObject);
            }

            Extensions.DragAndDropLog($"<color=magenta>[{name}] Removed slot {index}. New count: {_slots.Count}</color>");
            return true;
        }

        private static void DestroySlotObject(GameObject slotObject)
        {
            if (slotObject == null)
                return;

            if (Application.isPlaying)
            {
                // Destroy() only lands at the end of the frame, so until then the slot is still a
                // child of the container and GetComponentsInChildren keeps handing it out. Any
                // CacheSlots running later in the same frame would re-adopt the doomed slot, and
                // the entry turns into a "Missing" reference once the destroy actually happens.
                // Detaching makes the removal visible to the hierarchy immediately.
                slotObject.transform.SetParent(null, false);
                Destroy(slotObject);
            }
            else
                DestroyImmediate(slotObject);
        }

        private int CountFreeSlots()
        {
            int freeSlots = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] != null && _slots[i].IsEmpty)
                    freeSlots++;
            }
            return freeSlots;
        }

    }
}
