using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.DataBinding;
using UniversalDragAndDrop.Rules;
using UniversalDragAndDrop.Slots;
using UniversalDragAndDrop.Tools;
using UniversalDragAndDrop.Tools.Inspector;

namespace UniversalDragAndDrop.Inventories
{
    /// <summary>
    /// Universal inventory built around composition
    /// Does not require inheritance and is configured through strategies and rules
    /// </summary>
    public class UniversalInventory : MonoBehaviour, IInventory, IInventorySnapshotProvider, IDropPolicyProvider
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
        [SerializeField, Tooltip("Grid dimensions used when grid topology is enabled.")]
        private GridTopology _gridTopology = new GridTopology(1, 1);

        [FoldoutGroup("Placement")]
        [SerializeField, Tooltip("Policy for shaped items in non-grid slot inventories.")]
        private SlotShapedItemPolicy _slotShapedItemPolicy = SlotShapedItemPolicy.Accept;

        [FoldoutGroup("Placement")]
        [SerializeReference, ManagedReferencePicker, InlineProperty, HideLabel, Tooltip("Controls how a hovered grid slot is converted to a shaped-item placement anchor.")]
        private ShapedPlacementAnchorStrategyBase _shapedPlacementAnchorStrategy = new RotatedGrabOffsetAnchorStrategy();

        private IInventoryStrategy _strategy;
        private IPlacementStrategy _placementStrategy;
        private IAcceptanceStrategy _acceptanceStrategy;
        private IDragPolicy _dragPolicy;
        private IInventoryQueryStrategy _queryStrategy;
        private BaseSlot _pointerHoveredBaseSlot;
        private BaseSlot _lastInteractedBaseSlot;
        private StrategyConfiguration _appliedStrategyConfiguration;
        
        private readonly Dictionary<int, Placement> _cellToPlacement = new Dictionary<int, Placement>();
        private readonly List<BaseSlot> _dropPreviewSlots = new List<BaseSlot>();
        private bool _placementStateInitialized;

        public IReadOnlyList<BaseSlot> Slots => _slots.AsReadOnly();
        public int SlotCount => _slots.Count;
        public HashSet<Placement> Placements
        {
            get
            {
                EnsurePlacementStateInitialized();
                PruneEmptyPlacements();
                return new HashSet<Placement>(_cellToPlacement.Values);
            }
        }

        public GridTopology? Grid => _useGridTopology ? _gridTopology.Normalized() : (GridTopology?)null;
        public SlotShapedItemPolicy ShapedItemPolicy => _slotShapedItemPolicy;
        public IShapedPlacementAnchorStrategy ShapedPlacementAnchorStrategy => ResolveShapedPlacementAnchorStrategy();
        public InventoryRuleValidator RuleValidator => _ruleValidator;
        public BaseSlot BaseSlotPrefab => baseSlotPrefab;
        public Transform SlotContainer => _slotContainer;
        internal bool AllowMergeOnDrop => _dropPolicy != null && _dropPolicy.AllowMergeOnDrop;

        public IInventoryStrategy Strategy
        {
            get
            {
                EnsureStrategyInitialized();
                return _strategy;
            }
        }

        public IPlacementStrategy PlacementStrategy
        {
            get
            {
                EnsureStrategyInitialized();
                return _placementStrategy;
            }
        }

        public IAcceptanceStrategy AcceptanceStrategy
        {
            get
            {
                EnsureStrategyInitialized();
                return _acceptanceStrategy;
            }
        }

        public IDragPolicy DragPolicy
        {
            get
            {
                EnsureStrategyInitialized();
                return _dragPolicy;
            }
        }

        public IInventoryQueryStrategy QueryStrategy
        {
            get
            {
                EnsureStrategyInitialized();
                return _queryStrategy;
            }
        }
        
        public InventoryDataBindingBase DataBinding { get; private set; }

        internal bool CheckOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedBaseSlot)
            => DataBinding != null && DataBinding.CheckOccupiedSlotDrop(entry, occupiedBaseSlot);

        internal bool ExecuteOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedBaseSlot)
            => DataBinding != null && DataBinding.DoOccupiedSlotDrop(entry, occupiedBaseSlot);

        /// <summary>
        /// Event raised when a new slot is created (after Instantiate + Initialize).
        /// Used by FreeFormSlotLayout to position dynamically created slots.
        /// </summary>
        public event Action<BaseSlot> OnSlotCreated;

        /// <summary>
        /// Event raised when an item is added to this inventory
        /// </summary>
        public event Action<InventoryItemEventContext> OnItemAdded;

        /// <summary>
        /// Event raised when an item is removed from this inventory
        /// </summary>
        public event Action<InventoryItemEventContext> OnItemRemoved;

        /// <summary>
        /// Event raised when inventory content is refreshed in bulk (for example, after DataBinding.ReloadUI).
        /// Useful when items are changed via quiet APIs and item-level events are suppressed.
        /// </summary>
        public event Action OnContentRefreshed;

        /// <summary>
        /// Event raised when an item swap affecting this inventory is attempted.
        /// A subscriber can cancel the swap via context.Cancel = true.
        /// </summary>
        public event Action<InventorySwapContext> OnSwapAttempting;

        /// <summary>
        /// Event raised when an item swap affecting this inventory completes successfully.
        /// </summary>
        public event Action<InventorySwapContext> OnSwapCompleted;

        internal void EmitItemAdded(ItemStack stack, int slotIndex, IInventory sourceInventory, BaseSlot sourceBaseSlot, BaseSlot targetBaseSlot)
        {
            var context = new InventoryItemEventContext(
                stack, slotIndex, sourceInventory, this, sourceBaseSlot, targetBaseSlot);

            DataBinding?.HandleItemAdded(context);
            OnItemAdded?.Invoke(context);
        }

        internal void EmitItemRemoved(ItemStack stack, int slotIndex, IInventory targetInventory, BaseSlot sourceBaseSlot, BaseSlot targetBaseSlot)
        {
            var context = new InventoryItemEventContext(
                stack, slotIndex, this, targetInventory, sourceBaseSlot, targetBaseSlot);

            DataBinding?.HandleItemRemoved(context);
            OnItemRemoved?.Invoke(context);
        }

        /// <summary>
        /// Remove items from a slot in this inventory and emit events.
        /// Used by external drop processors (WorldDropZone and similar)
        /// that do not go through TransferPlanExecutor.
        /// </summary>
        /// <returns>Number of removed items, or 0 if nothing was removed</returns>
        internal int RemoveItemsFromSlot(BaseSlot sourceBaseSlot, ItemStack stackToRemove, IInventory targetInventory = null, BaseSlot targetBaseSlot = null)
        {
            if (sourceBaseSlot?.Stack == null || sourceBaseSlot.Stack.IsEmpty || stackToRemove == null || stackToRemove.IsEmpty)
                return 0;

            int removed = sourceBaseSlot.Stack.RemoveAdapters(stackToRemove.Adapters);
            if (removed <= 0)
                return 0;

            sourceBaseSlot.UpdateVisuals();

            EmitItemRemoved(stackToRemove, sourceBaseSlot.Index, targetInventory, sourceBaseSlot, targetBaseSlot);
            HandleSlotEmptied(sourceBaseSlot);

            return removed;
        }

        internal void EmitSwapAttempting(InventorySwapContext context)
        {
            OnSwapAttempting?.Invoke(context);
        }

        internal void EmitSwapCompleted(InventorySwapContext context)
        {
            OnSwapCompleted?.Invoke(context);
        }

        public void NotifyContentRefreshed()
        {
            OnContentRefreshed?.Invoke();
        }

        private void Start()
        {
            EnsureStrategyInitialized();
        }

        public void Initialize(InventoryDataBindingBase inventoryDataBindingBase)
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
            
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot == null)
                    continue;

                slot.Initialize(i, this);
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

            _inventoryStrategy.BindInventory(this);
            _slotManagementSettings.BindInventory(this);
            IInventoryStrategy baseStrategy = _inventoryStrategy;
            Extensions.DragAndDropLog($"<color=yellow>[{name}] Strategy: {baseStrategy.GetType().Name}</color>");

            // Wrap it in a decorator for dynamic slots if needed
            IInventoryStrategy configuredStrategy = _slotManagementSettings.WrapRuntimeStrategy(this, baseStrategy, CreateSlot, () => _slots, EnsureFreeSlots);
            if (!ReferenceEquals(configuredStrategy, baseStrategy))
            {
                SetStrategy(configuredStrategy);
                Extensions.DragAndDropLog($"<color=yellow>[{name}] Strategy wrapped by {_slotManagementSettings.GetType().Name}</color>");
            }
            else
            {
                SetStrategy(baseStrategy);
                Extensions.DragAndDropLog($"<color=yellow>[{name}] Strategy: Fixed slots</color>");
            }
        }

        /// <summary>
        /// Change the inventory strategy at runtime
        /// </summary>
        public void SetStrategy(IInventoryStrategy strategy)
        {
            _strategy = strategy;
            _placementStrategy = strategy;
            _acceptanceStrategy = strategy;
            _dragPolicy = strategy;
            _queryStrategy = strategy;
            _appliedStrategyConfiguration = CaptureStrategyConfiguration();
        }

        public void RefreshStrategy()
        {
            Debug.Log($"[{name}] RefreshStrategy called! Current strategy: {_strategy?.GetType().Name}, current config: {_appliedStrategyConfiguration}");
            if (_slots == null)
                _slots = new List<BaseSlot>();

            if (_slots.Count == 0)
                InitializeSlots();

            EnsurePlacementStateInitialized();
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
            EnsurePlacementStateInitialized();
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
                _dropPolicy.AllowMergeOnDrop,
                _useGridTopology,
                _gridTopology.Normalized().ToString(),
                _slotShapedItemPolicy.ToString());
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

        public BaseSlot GetSlot(int index)
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
            EnsurePlacementStateInitialized();
            PruneEmptyPlacements();
            return GetPlacementAtInitialized(cellIndex);
        }

        public IReadOnlyList<int> GetCoveredCells(
            int anchorIndex,
            Footprint footprint,
            PlacementOrientation orientation = PlacementOrientation.Rot0)
        {
            EnsurePlacementStateInitialized();
            return BuildCoveredCells(anchorIndex, footprint, orientation);
        }

        public Vector2Int GetCellForIndex(int index)
        {
            EnsurePlacementSettings();
            return IndexToCell(index);
        }

        public bool TryGetIndexForCell(Vector2Int cell, out int index)
        {
            EnsurePlacementSettings();

            if (_useGridTopology)
            {
                var topology = _gridTopology.Normalized();
                if (!topology.Contains(cell))
                {
                    index = -1;
                    return false;
                }

                index = topology.ToIndex(cell);
                return index >= 0 && index < _slots.Count;
            }

            index = cell.y == 0 ? cell.x : -1;
            return index >= 0 && index < _slots.Count;
        }

        public Vector2Int GetGrabOffset(Placement placement, BaseSlot baseSlot)
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
            Footprint footprint,
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
                footprint,
                entry.Orientation,
                targetItemAdapter);
            return ResolveShapedPlacementAnchorStrategy().TryResolveAnchorCell(strategyContext, out anchorCell);
        }

        public bool TryResolveShapedPlacementAnchor(
            BaseSlot targetBaseSlot,
            DragContext context,
            DragEntry entry,
            Footprint footprint,
            IItemAdapter targetItemAdapter,
            out Vector2Int anchorCell,
            out int anchorIndex)
        {
            anchorIndex = -1;
            if (!TryResolveShapedPlacementAnchorCell(
                    targetBaseSlot,
                    context,
                    entry,
                    footprint,
                    targetItemAdapter,
                    out anchorCell))
                return false;

            return TryGetIndexForCell(anchorCell, out anchorIndex);
        }

        public bool CanPlace(PlacementRequest request)
        {
            EnsurePlacementStateInitialized();
            PruneEmptyPlacements();
            return CanPlaceInitialized(request);
        }

        public bool CanPlace(PlacementRequest request, Placement ignoredPlacement)
        {
            EnsurePlacementStateInitialized();
            PruneEmptyPlacements();
            return CanPlaceInitialized(request, ignoredPlacement);
        }

        public bool TryGetDropPreviewSlots(
            BaseSlot targetBaseSlot,
            DragContext context,
            out IReadOnlyList<BaseSlot> previewSlots,
            out bool canPlace)
        {
            previewSlots = Array.Empty<BaseSlot>();
            canPlace = false;

            if (targetBaseSlot == null ||
                !ReferenceEquals(targetBaseSlot.Inventory, this) ||
                context == null ||
                context.Entries == null ||
                context.Entries.Count == 0)
                return false;

            var entry = context.Entries[0];
            if (entry.Stack == null || entry.Stack.IsEmpty || entry.Stack.PrimaryAdapter == null)
                return false;

            if (!TransferItemConversionUtility.TryResolveTargetItem(
                    entry.SourceInventory,
                    this,
                    entry.Stack.PrimaryAdapter,
                    out var targetItem))
                return false;

            var footprint = Footprint.Resolve(targetItem);
            if (!_useGridTopology || footprint.IsSingleCell)
            {
                previewSlots = new[] { targetBaseSlot };
                canPlace = true;
                return true;
            }

            if (!TryResolveShapedPlacementAnchorCell(
                    targetBaseSlot,
                    context,
                    entry,
                    footprint,
                    targetItem,
                    out var anchorCell))
                return true;

            var hasValidAnchor = TryGetIndexForCell(anchorCell, out int anchorIndex);
            var coveredIndices = GetPreviewCoveredCells(anchorCell, footprint, entry.Orientation);
            if (coveredIndices == null || coveredIndices.Count == 0)
                return true;

            var slots = new List<BaseSlot>(coveredIndices.Count);
            for (int i = 0; i < coveredIndices.Count; i++)
            {
                var slot = GetSlot(coveredIndices[i]);
                if (slot != null)
                    slots.Add(slot);
            }

            previewSlots = slots;

            var acceptanceRequest = new InventoryAcceptanceRequest(
                this,
                targetItem,
                entry.Stack.Count,
                context,
                entry);
            var previewStack = acceptanceRequest.CreatePreviewStack(entry.Stack.Count, targetItem);
            if (previewStack == null)
                return true;

            if (!hasValidAnchor)
                return true;

            var ignoredPlacement = ReferenceEquals(entry.SourceInventory, this)
                ? entry.SourcePlacement
                : null;
            canPlace = CanPlace(
                new PlacementRequest(previewStack, anchorIndex, entry.Orientation, footprint),
                ignoredPlacement);
            return true;
        }

        public bool ShowDropPreview(BaseSlot targetBaseSlot, DragContext context)
        {
            ClearDropPreview();

            if (!TryGetDropPreviewSlots(targetBaseSlot, context, out var previewSlots, out _) ||
                previewSlots == null ||
                previewSlots.Count == 0)
                return false;

            for (int i = 0; i < previewSlots.Count; i++)
            {
                var slot = previewSlots[i];
                if (slot == null)
                    continue;

                slot.Highlight(true);
                _dropPreviewSlots.Add(slot);
            }

            return _dropPreviewSlots.Count > 0;
        }

        public void ClearDropPreview()
        {
            for (int i = 0; i < _dropPreviewSlots.Count; i++)
                _dropPreviewSlots[i]?.Highlight(false);

            _dropPreviewSlots.Clear();
        }

        public bool TryPlace(PlacementRequest request)
        {
            return TryPlace(request, out _);
        }

        public bool TryPlace(PlacementRequest request, out Placement placement)
        {
            EnsurePlacementStateInitialized();
            PruneEmptyPlacements();
            return TryPlaceInitialized(request, out placement);
        }

        public bool RemovePlacement(Placement placement)
        {
            if (placement == null)
                return false;

            EnsurePlacementStateInitialized();
            return UnregisterPlacement(placement);
        }

        public bool RemovePlacementAt(BaseSlot baseSlot)
        {
            if (baseSlot == null || !ReferenceEquals(baseSlot.Inventory, this))
                return false;

            return RemovePlacementAt(baseSlot.Index);
        }

        public bool RemovePlacementAt(int cellIndex)
        {
            EnsurePlacementStateInitialized();
            var placement = GetPlacementAtInitialized(cellIndex);
            return placement != null && UnregisterPlacement(placement);
        }

        internal bool TryGetPlacementStackForSlot(BaseSlot baseSlot, out ItemStack stack)
        {
            stack = null;
            if (baseSlot == null || !ReferenceEquals(baseSlot.Inventory, this))
                return false;

            if (!_placementStateInitialized)
                return false;

            PruneEmptyPlacements();
            stack = GetPlacementAtInitialized(baseSlot.Index)?.Stack ?? ItemStack.Empty();
            return true;
        }

        internal bool TrySetPlacementStackFromSlot(BaseSlot baseSlot, ItemStack stack)
        {
            if (baseSlot == null || !ReferenceEquals(baseSlot.Inventory, this))
                return false;

            EnsurePlacementStateInitialized();
            PruneEmptyPlacements();

            var existingPlacement = GetPlacementAtInitialized(baseSlot.Index);

            if (stack == null || stack.IsEmpty)
            {
                if (existingPlacement != null)
                    UnregisterPlacement(existingPlacement);

                baseSlot.SetLocalStackForPlacementMigration(ItemStack.Empty());
                return true;
            }

            var request = new PlacementRequest(
                stack,
                baseSlot.Index,
                PlacementOrientation.Rot0,
                Footprint.Resolve(stack.PrimaryAdapter));

            if (existingPlacement != null)
                UnregisterPlacement(existingPlacement);

            if (!TryPlaceInitialized(request, out _))
            {
                if (existingPlacement != null)
                    RegisterPlacement(existingPlacement);

                return true;
            }

            baseSlot.SetLocalStackForPlacementMigration(ItemStack.Empty());
            return true;
        }

        private void EnsurePlacementStateInitialized()
        {
            if (_placementStateInitialized)
                return;

            _cellToPlacement.Clear();

            if (_slots != null)
            {
                for (int i = 0; i < _slots.Count; i++)
                {
                    var slot = _slots[i];
                    var stack = slot?.GetLocalStackForPlacementMigration();
                    if (stack == null || stack.IsEmpty)
                        continue;

                    var request = new PlacementRequest(
                        stack,
                        i,
                        PlacementOrientation.Rot0,
                        Footprint.Resolve(stack.PrimaryAdapter));

                    if (CanPlaceInitialized(request))
                    {
                        var coveredIndices = BuildCoveredCells(request.AnchorIndex, request.Footprint, request.Orientation);
                        RegisterPlacement(new Placement(
                            IndexToCell(i),
                            i,
                            request.Orientation,
                            request.Footprint,
                            stack,
                            coveredIndices));
                        slot.SetLocalStackForPlacementMigration(ItemStack.Empty());
                    }
                }
            }

            _placementStateInitialized = true;
        }

        private void ResetPlacementState()
        {
            _cellToPlacement.Clear();
            _placementStateInitialized = false;
        }

        private void ClearInitializedPlacementState()
        {
            _cellToPlacement.Clear();
            _placementStateInitialized = true;
        }

        private bool CanPlaceInitialized(PlacementRequest request, Placement ignoredPlacement = null)
        {
            if (request.Stack == null || request.Stack.IsEmpty)
                return false;

            if (!_useGridTopology &&
                _slotShapedItemPolicy == SlotShapedItemPolicy.Reject &&
                !request.Footprint.IsSingleCell)
                return false;

            if (!request.Footprint.IsSingleCell && request.Stack.Count > 1)
                return false;

            var coveredIndices = BuildCoveredCells(request.AnchorIndex, request.Footprint, request.Orientation);
            if (coveredIndices == null || coveredIndices.Count == 0)
                return false;

            for (int i = 0; i < coveredIndices.Count; i++)
            {
                if (_cellToPlacement.TryGetValue(coveredIndices[i], out var existing) &&
                    !ReferenceEquals(existing, ignoredPlacement))
                    return false;
            }

            return true;
        }

        private void PruneEmptyPlacements()
        {
            var toPrune = new HashSet<Placement>();
            foreach (var placement in _cellToPlacement.Values)
            {
                if (placement == null || placement.Stack == null || placement.Stack.IsEmpty)
                    toPrune.Add(placement);
            }

            foreach (var placement in toPrune)
                UnregisterPlacement(placement);
        }

        private bool TryPlaceInitialized(PlacementRequest request, out Placement placement)
        {
            placement = null;
            if (!CanPlaceInitialized(request))
                return false;

            var coveredIndices = BuildCoveredCells(request.AnchorIndex, request.Footprint, request.Orientation);
            var anchorCell = IndexToCell(request.AnchorIndex);
            placement = new Placement(
                anchorCell,
                request.AnchorIndex,
                request.Orientation,
                request.Footprint,
                request.Stack,
                coveredIndices);

            RegisterPlacement(placement);
            return true;
        }

        private Placement GetPlacementAtInitialized(int cellIndex)
        {
            return _cellToPlacement.TryGetValue(cellIndex, out var placement) ? placement : null;
        }

        private bool RemovePlacementAtInitialized(int cellIndex)
        {
            var placement = GetPlacementAtInitialized(cellIndex);
            return placement != null && UnregisterPlacement(placement);
        }

        private void RegisterPlacement(Placement placement)
        {
            if (placement == null)
                return;

            for (int i = 0; i < placement.CoveredIndices.Count; i++)
                _cellToPlacement[placement.CoveredIndices[i]] = placement;
        }

        private bool UnregisterPlacement(Placement placement)
        {
            if (placement == null)
                return false;

            bool removed = false;
            for (int i = 0; i < placement.CoveredIndices.Count; i++)
            {
                int cellIndex = placement.CoveredIndices[i];
                if (_cellToPlacement.TryGetValue(cellIndex, out var existing) &&
                    ReferenceEquals(existing, placement))
                {
                    _cellToPlacement.Remove(cellIndex);
                    removed = true;
                }
            }

            return removed;
        }

        private IReadOnlyList<int> BuildCoveredCells(
            int anchorIndex,
            Footprint footprint,
            PlacementOrientation orientation)
        {
            if (_useGridTopology)
                return BuildGridCoveredCells(anchorIndex, footprint, orientation);

            return anchorIndex >= 0 && anchorIndex < _slots.Count
                ? new[] { anchorIndex }
                : Array.Empty<int>();
        }

        private IReadOnlyList<int> BuildGridCoveredCells(
            int anchorIndex,
            Footprint footprint,
            PlacementOrientation orientation)
        {
            var topology = _gridTopology.Normalized();
            if (!topology.IsValidIndex(anchorIndex))
                return Array.Empty<int>();

            var anchorCell = topology.ToCell(anchorIndex);
            var size = footprint.GetSize(orientation);
            var result = new List<int>(size.x * size.y);

            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    var cell = new Vector2Int(anchorCell.x + x, anchorCell.y + y);
                    if (!topology.Contains(cell))
                        return Array.Empty<int>();

                    int index = topology.ToIndex(cell);
                    if (index < 0 || index >= _slots.Count)
                        return Array.Empty<int>();

                    result.Add(index);
                }
            }

            return result;
        }

        private IReadOnlyList<int> GetPreviewCoveredCells(
            Vector2Int anchorCell,
            Footprint footprint,
            PlacementOrientation orientation)
        {
            if (!_useGridTopology)
                return TryGetIndexForCell(anchorCell, out int anchorIndex)
                    ? GetCoveredCells(anchorIndex, footprint, orientation)
                    : Array.Empty<int>();

            var topology = _gridTopology.Normalized();
            var size = footprint.GetSize(orientation);
            var result = new List<int>(size.x * size.y);

            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    var cell = new Vector2Int(anchorCell.x + x, anchorCell.y + y);
                    if (!topology.Contains(cell))
                        continue;

                    int index = topology.ToIndex(cell);
                    if (index >= 0 && index < _slots.Count)
                        result.Add(index);
                }
            }

            return result;
        }

        private Vector2Int IndexToCell(int index)
        {
            if (_useGridTopology)
                return _gridTopology.Normalized().ToCell(index);

            return new Vector2Int(index, 0);
        }

        private void ShiftPlacementIndicesAfterSlotRemoved(int removedIndex)
        {
            if (!_placementStateInitialized || removedIndex < 0)
                return;

            var uniquePlacements = new HashSet<Placement>(_cellToPlacement.Values);
            _cellToPlacement.Clear();
            foreach (var placement in uniquePlacements)
            {
                int anchorIndex = placement.AnchorIndex > removedIndex
                    ? placement.AnchorIndex - 1
                    : placement.AnchorIndex;
                var covered = BuildCoveredCells(anchorIndex, placement.Footprint, placement.Orientation);
                placement.MoveAnchor(IndexToCell(anchorIndex), anchorIndex, covered);
                for (int c = 0; c < placement.CoveredIndices.Count; c++)
                    _cellToPlacement[placement.CoveredIndices[c]] = placement;
            }
        }

        /// <summary>
        /// Set the stack limit at runtime (for example, from DataBinding).
        /// maxStackSize = 0 means unlimited.
        /// </summary>
        public void SetMaxStackSize(int maxStackSize, bool allowItemOverride = false)
        {
            EnsureInventoryStrategySettings();
            _inventoryStrategy.SetMaxStackSize(maxStackSize, allowItemOverride);
            _strategy?.SetMaxStackSize(maxStackSize, allowItemOverride);
        }

        public bool TryAddStack(ItemStack stack, int targetSlotIndex = -1)
        {
            if (stack == null || stack.IsEmpty)
                return false;

            EnsureStrategyInitialized();
            if (!CanAcceptStackByPlacementPolicy(stack))
                return false;

            Extensions.DragAndDropLog($"<color=cyan>[{name}] TryAddStack: {stack.DisplayName} x{stack.Count}, targetSlot={targetSlotIndex}, currentSlots={_slots.Count}, strategy={_strategy?.GetType().Name}</color>");

            bool success = _placementStrategy.TryAdd(_slots, stack, targetSlotIndex);
            bool stackConsumed = stack.IsEmpty;

            if ((!success || !stackConsumed) && stack != null && !stack.IsEmpty)
            {
                success = SlotRelocationService.TryRelocateAndRetry(
                    _slots, stack, targetSlotIndex, _placementStrategy,
                    (slot, item, count) => CanAcceptByRules(slot, item, count),
                    CaptureSnapshot, RestoreSnapshot);
                stackConsumed = stack.IsEmpty;
            }

            if (success && stackConsumed)
            {
                Extensions.DragAndDropLog($"<color=green>[{name}] TryAddStack SUCCESS! Now have {_slots.Count} slots</color>");
                UpdateAllVisuals();
            }
            else
            {
                Extensions.DragAndDropLog($"<color=red>[{name}] TryAddStack FAILED!</color>");
                success = false;
            }

            return success;
        }

        internal bool TryAddStackQuiet(ItemStack stack, int targetSlotIndex = -1)
        {
            if (stack == null || stack.IsEmpty)
                return false;

            EnsureStrategyInitialized();
            if (!CanAcceptStackByPlacementPolicy(stack))
                return false;

            return _placementStrategy.TryAddQuite(_slots, stack, targetSlotIndex);
        }

        internal bool CanAcceptByRules(
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
            var entry = context.Entries[0];

            if (_ruleValidator != null)
            {
                var inventoryResult = _ruleValidator.ValidateDrop(context, entry);
                if (!inventoryResult.IsValid)
                    return false;
            }

            var binding = DataBinding;
            if (binding != null)
            {
                var bindingResult = binding.ValidateDropRules(context, entry);
                if (!bindingResult.IsValid)
                    return false;
            }

            if (baseSlot.SlotRuleValidator != null)
            {
                var slotResult = baseSlot.SlotRuleValidator.ValidateDrop(context, entry);
                if (!slotResult.IsValid)
                    return false;
            }

            return true;
        }

        public InventorySnapshot CaptureSnapshot()
        {
            EnsurePlacementStateInitialized();

            var slotsSnapshot = new List<InventorySlotState>(_slots.Count);
            foreach (var slot in _slots)
            {
                if (slot != null && !slot.IsEmpty)
                {
                    slotsSnapshot.Add(new InventorySlotState(slot.Stack.Adapters));
                }
                else
                {
                    slotsSnapshot.Add(new InventorySlotState(null));
                }
            }

            var placementSnapshot = new List<InventoryPlacementState>();
            var seen = new HashSet<Placement>();
            foreach (var placement in _cellToPlacement.Values)
            {
                if (!seen.Add(placement))
                    continue;

                if (placement == null || placement.Stack == null || placement.Stack.IsEmpty)
                    continue;

                placementSnapshot.Add(new InventoryPlacementState(
                    placement.AnchorIndex,
                    placement.Stack.Adapters,
                    placement.Orientation,
                    placement.Footprint,
                    placement.CoveredIndices));
            }

            return new InventorySnapshot(slotsSnapshot, placementSnapshot);
        }

        public void RestoreSnapshot(InventorySnapshot snapshot)
        {
            if (snapshot == null)
                return;

            if (_slots == null)
            {
                _slots = new List<BaseSlot>();
            }

            int desiredCount = snapshot.Slots.Count;

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
                    Destroy(slot.Transform.gameObject);
                }
            }

            for (int i = 0; i < _slots.Count; i++)
                _slots[i].SetInventoryIndex(i, this);

            if (snapshot.Placements != null && snapshot.Placements.Count > 0)
            {
                ClearInitializedPlacementState();
                for (int i = 0; i < _slots.Count; i++)
                    _slots[i].SetLocalStackForPlacementMigration(ItemStack.Empty());

                for (int i = 0; i < snapshot.Placements.Count; i++)
                {
                    var placementState = snapshot.Placements[i];
                    if (placementState.IsEmpty)
                        continue;

                    if (!ItemStack.TryCreate(placementState.Adapters, out var restoredStack))
                        continue;

                    TryPlace(new PlacementRequest(
                        restoredStack,
                        placementState.AnchorIndex,
                        placementState.Orientation,
                        placementState.Footprint));
                }

                UpdateAllVisuals();
                return;
            }

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                var state = snapshot.Slots[i];

                if (state.IsEmpty)
                {
                    slot.Clear();
                }
                else
                {
                    if (ItemStack.TryCreate(state.Adapters, out var restoredStack))
                        slot.SetStack(restoredStack);
                }

                slot.SetInventoryIndex(i, this);
            }
        }

        public void ReInitSlots(int slotCount)
        {
            slotCount = Mathf.Max(0, slotCount);
            _initialSlotCount = slotCount;

            _pointerHoveredBaseSlot = null;
            _lastInteractedBaseSlot = null;

            if (_slots == null)
                _slots = new List<BaseSlot>();

            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                BaseSlot baseSlot = _slots[i];
                if (baseSlot?.Transform != null)
                {
                    baseSlot.Transform.gameObject.SetActive(false);
                    Destroy(baseSlot.Transform.gameObject);
                }
            }

            _slots.Clear();
            ResetPlacementState();

            for (int i = 0; i < slotCount; i++)
                CreateSlot();

            EnsurePlacementStateInitialized();
            UpdateAllVisuals();
        }

        public bool Contains(IItemAdapter itemAdapter) => _queryStrategy.Contains(_slots, itemAdapter);
        
        public void UpdateAllVisuals()
        {
            foreach (var slot in _slots)
            {
                slot.UpdateVisuals();
            }
        }

        /// <summary>
        /// Clear the entire inventory
        /// </summary>
        public void ClearAll()
        {
            EnsureStrategyInitialized();

            foreach (var slot in _slots)
            {
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
                if (!slot.IsEmpty)
                {
                    stacks.Add(slot.Stack);
                }
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
                if (!slot.IsEmpty && !addedIds.Contains(slot.Stack.ID))
                {
                    items.Add(slot.Stack.PrimaryAdapter);
                    addedIds.Add(slot.Stack.ID);
                }
            }

            return items;
        }

        internal void NotifyPointerEnter(BaseSlot baseSlot)
        {
            if (baseSlot == null || !ReferenceEquals(baseSlot.Inventory, this))
                return;

            _pointerHoveredBaseSlot = baseSlot;
        }

        internal void NotifyPointerExit(BaseSlot baseSlot)
        {
            if (_pointerHoveredBaseSlot == baseSlot)
            {
                _pointerHoveredBaseSlot = null;
            }
        }

        internal void NotifySlotInteracted(BaseSlot baseSlot)
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
        public int GetDragAmount(BaseSlot baseSlot, DragAmount? overrideAmount = null, int? overrideCustom = null)
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
        public void SetDragAmountStep(int step, DragAmountStepRounding rounding = DragAmountStepRounding.Floor)
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
        public bool TryAddToSlot(
            ItemStack stack,
            BaseSlot targetBaseSlot,
            IInventory sourceInventory = null,
            int sourceSlotIndex = -1,
            SlotOperationContext operationContext = null)
        {
            if (stack == null || stack.IsEmpty || targetBaseSlot == null)
                return false;

            EnsureStrategyInitialized();
            if (!CanAcceptStackByPlacementPolicy(stack))
                return false;

            return _placementStrategy.TryAddToSlot(_slots, stack, targetBaseSlot, EnsureFreeSlots, operationContext);
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

        private bool CanAcceptStackByPlacementPolicy(ItemStack stack)
        {
            return stack != null && !stack.IsEmpty && CanAcceptFootprint(stack.PrimaryAdapter, stack.Count);
        }

        private bool CanAcceptFootprint(IItemAdapter itemAdapter, int count)
        {
            if (itemAdapter == null || count <= 0)
                return false;

            var footprint = Footprint.Resolve(itemAdapter);
            if (_useGridTopology && !footprint.IsSingleCell)
                return false;

            if (!_useGridTopology &&
                _slotShapedItemPolicy == SlotShapedItemPolicy.Reject &&
                !footprint.IsSingleCell)
                return false;

            return footprint.IsSingleCell || count <= 1;
        }

        /// <summary>
        /// Check whether the inventory can accept an item (without targeting a specific slot)
        /// Checks inventory rules plus matching slot availability or the ability to create a new slot
        /// </summary>
        public bool CanAcceptItem(IItemAdapter itemAdapter, int count, out BaseSlot suggestedBaseSlot)
            => CanAcceptItem(new InventoryAcceptanceRequest(this, itemAdapter, count), out suggestedBaseSlot);

        /// <summary>
        /// Check whether the inventory can accept an item in the context of the current drag/drop operation.
        /// </summary>
        public bool CanAcceptItem(InventoryAcceptanceRequest request, out BaseSlot suggestedBaseSlot)
        {
            suggestedBaseSlot = null;

            if (request?.ItemAdapter == null || request.DesiredCount <= 0)
                return false;

            EnsureStrategyInitialized();
            if (!CanAcceptFootprint(request.ItemAdapter, request.DesiredCount))
                return false;

            bool canCreateNewSlot = _slotManagementSettings.CanCreateNewSlot(this, _slots.Count);
            int potentialNewSlots = _slotManagementSettings.GetPotentialNewSlots(this, _slots.Count);
            bool canAccept = _acceptanceStrategy.CanAcceptItem(_slots, request, canCreateNewSlot, potentialNewSlots, baseSlotPrefab, out suggestedBaseSlot);

            if (canAccept)
                Extensions.DragAndDropLog($"<color=green>[{name}] CanAcceptItem: success via strategy</color>");
            else
                Extensions.DragAndDropLog($"<color=red>[{name}] CanAcceptItem: No suitable slots and cannot create new</color>");

            return canAccept;
        }
        
        public int GetAcceptableCount(InventoryAcceptanceRequest request)
        {
            if (request?.ItemAdapter == null || request.DesiredCount <= 0)
                return 0;

            EnsureStrategyInitialized();
            if (!CanAcceptFootprint(request.ItemAdapter, request.DesiredCount))
                return 0;

            bool canCreateNewSlot = _slotManagementSettings.CanCreateNewSlot(this, _slots.Count);
            int potentialNewSlots = _slotManagementSettings.GetPotentialNewSlots(this, _slots.Count);
            int result = _acceptanceStrategy.GetAcceptableCount(_slots, request, canCreateNewSlot, potentialNewSlots, baseSlotPrefab);
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

        private BaseSlot FindLastEmptySlot()
        {
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                var slot = _slots[i];
                if (slot.IsEmpty)
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
            _slots.RemoveAt(index);

            for (int i = index; i < _slots.Count; i++)
            {
                _slots[i].SetInventoryIndex(i, this);
            }

            ShiftPlacementIndicesAfterSlotRemoved(index);

            if (slotTransform != null)
            {
                Destroy(slotTransform.gameObject);
            }

            Extensions.DragAndDropLog($"<color=magenta>[{name}] Removed slot {index}. New count: {_slots.Count}</color>");
            return true;
        }

        private int CountFreeSlots()
        {
            int freeSlots = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsEmpty)
                    freeSlots++;
            }
            return freeSlots;
        }

        /// <summary>
        /// Perform an item swap between two slots (possibly from different inventories).
        /// Events are not emitted directly; information is returned through SwapOperationResult.
        /// </summary>
        /// <param name="targetBaseSlot">Target slot (from this inventory)</param>
        /// <param name="sourceBaseSlot">Source slot (may belong to another inventory)</param>
        /// <param name="result">Swap result used for event generation</param>
        /// <returns>True if the swap succeeds</returns>
        public bool TrySwapSlots(BaseSlot targetBaseSlot, BaseSlot sourceBaseSlot, out SwapOperationResult result)
        {
            result = default;
            if (targetBaseSlot == null || sourceBaseSlot == null)
            {
                Extensions.DragAndDropLog("<color=red>[TrySwapSlots] Slot is null</color>");
                return false;
            }

            if (targetBaseSlot.IsEmpty || sourceBaseSlot.IsEmpty)
            {
                Extensions.DragAndDropLog("<color=red>[TrySwapSlots] One of the slots is empty</color>");
                return false;
            }

            // Check that targetSlot belongs to this inventory
            if (!ReferenceEquals(targetBaseSlot.Inventory, this))
            {
                Extensions.DragAndDropLog("<color=red>[TrySwapSlots] Target slot doesn't belong to this inventory</color>");
                return false;
            }

            // Save stack copies for events
            var targetStackBackup = ItemStack.TryCreate(targetBaseSlot.Stack.Adapters, out var targetBackup)
                ? targetBackup
                : ItemStack.Empty();
            var sourceStackBackup = ItemStack.TryCreate(sourceBaseSlot.Stack.Adapters, out var sourceBackup)
                ? sourceBackup
                : ItemStack.Empty();

            try
            {
                // Perform the swap
                var tempStack = targetBaseSlot.Stack;
                targetBaseSlot.SetStack(sourceBaseSlot.Stack);
                sourceBaseSlot.SetStack(tempStack);

                targetBaseSlot.UpdateVisuals();
                sourceBaseSlot.UpdateVisuals();

                Extensions.DragAndDropLog($"<color=green>[{name}] Swap completed: slot {targetBaseSlot.Index} ↔ slot {sourceBaseSlot.Index}</color>");
                var targetStackAfter = ItemStack.TryCreate(targetBaseSlot.Stack.Adapters, out var targetAfter)
                    ? targetAfter
                    : ItemStack.Empty();
                var sourceStackAfter = ItemStack.TryCreate(sourceBaseSlot.Stack.Adapters, out var sourceAfter)
                    ? sourceAfter
                    : ItemStack.Empty();

                result = new SwapOperationResult(targetStackBackup, sourceStackBackup, targetStackAfter, sourceStackAfter);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{name}] TrySwapSlots exception: {ex.Message}");

                // Roll back changes
                targetBaseSlot.SetStack(targetStackBackup);
                sourceBaseSlot.SetStack(sourceStackBackup);
                targetBaseSlot.UpdateVisuals();
                sourceBaseSlot.UpdateVisuals();

                return false;
            }
        }
    }
}
