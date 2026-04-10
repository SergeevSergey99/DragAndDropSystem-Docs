using System;
using System.Collections.Generic;
using System.Linq;
using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Tools.Inspector;
using DragAndDropSystem.Rules;
using DragAndDropSystem.Slots;
using DragAndDropSystem.Tools;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Универсальный инвентарь, работающий через композицию
    /// Не требует наследования - настраивается через стратегии и правила
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
        [InfoBox("PrimaryAdapter Behavior: как предметы размещаются | Slot Management: управление количеством слотов", InfoMessageType.Info)]
        [SerializeField, LabelText("PrimaryAdapter Behavior")]
        private ItemBehaviorType _itemBehavior = ItemBehaviorType.Stackable;

        [FoldoutGroup("Strategy")]
        [SerializeField, LabelText("Drag Amount")]
        [Tooltip("How many items to take when dragging from a stack")]
        [ShowIf(nameof(ShowDragAmountSettings))]
        private DragAmount _dragAmount = DragAmount.All;

        [FoldoutGroup("Strategy")]
        [SerializeField, Tooltip("Item amount for Custom")]
        [ShowIf(nameof(ShowCustomDragAmount))]
        private int _customDragAmount = 1;

        [FoldoutGroup("Strategy")]
        [SerializeField, Tooltip("Maximum stack size. 0 or less = unlimited.")]
        [ShowIf(nameof(ShowDragAmountSettings))]
        private int _maxStackSize = 0;

        [FoldoutGroup("Strategy")]
        [SerializeField, Tooltip("Allow items to override the stack limit via IStackSizeLimitable. " +
                                 "If true, the item's MaxStackSize fully replaces _maxStackSize. " +
                                 "If false, IStackSizeLimitable is ignored and only _maxStackSize is used.")]
        [ShowIf(nameof(ShowMaxStackOverride))]
        private bool _allowItemStackOverride = false;

        [FoldoutGroup("Strategy")]
        [SerializeField, LabelText("Slot Management")]
        private SlotManagementType _slotManagement = SlotManagementType.Fixed;

        [FoldoutGroup("Strategy")]
        [SerializeField, Tooltip("Maximum number of slots (for Dynamic)")]
        [ShowIf(nameof(_slotManagement), nameof(SlotManagementType.Dynamic))]
        private int _maxDynamicSlots = 100;

        [FoldoutGroup("Strategy")]
        [SerializeField, Tooltip("Minimum number of free slots (for Dynamic). 0 = create only on TryAddItem, not when moving into slots")]
        [ShowIf(nameof(_slotManagement), nameof(SlotManagementType.Dynamic))]
        private int _maxFreeSlots = 1;

        private bool ShowDragAmountSettings => _itemBehavior == ItemBehaviorType.Stackable || _itemBehavior == ItemBehaviorType.SeparableStacks;
        private bool ShowCustomDragAmount => ShowDragAmountSettings && _dragAmount == DragAmount.Custom;
        private bool ShowMaxStackOverride => ShowDragAmountSettings && _maxStackSize > 0;

        [FoldoutGroup("Rules")]
        [SerializeField, HideLabel]
        private InventoryRuleValidator _ruleValidator = new InventoryRuleValidator();

        [FoldoutGroup("Drop Policy")]
        [SerializeField, HideLabel]
        private DropPolicySettings _dropPolicy = new DropPolicySettings();

        [FoldoutGroup("Slot Setup", expanded: true)]
        [SerializeField, Tooltip("Slots created in the scene. Can be assigned manually in the Inspector. If empty, they will be found automatically.")]
        private List<BaseSlot> _slots = new List<BaseSlot>();

        private IInventoryStrategy _strategy;
        private IPlacementStrategy _placementStrategy;
        private IAcceptanceStrategy _acceptanceStrategy;
        private IDragPolicy _dragPolicy;
        private IInventoryQueryStrategy _queryStrategy;
        private BaseSlot _pointerHoveredBaseSlot;
        private BaseSlot _lastInteractedBaseSlot;
        private StrategyConfiguration _appliedStrategyConfiguration;

        public IReadOnlyList<BaseSlot> Slots => _slots.AsReadOnly();
        public int SlotCount => _slots.Count;
        public InventoryRuleValidator RuleValidator => _ruleValidator;
        public ItemBehaviorType ItemBehavior => _itemBehavior;
        public SlotManagementType SlotManagement => _slotManagement;
        public BaseSlot BaseSlotPrefab => baseSlotPrefab;
        public Transform SlotContainer => _slotContainer;

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
        /// Событие создания нового слота (после Instantiate + Initialize).
        /// Используется FreeFormSlotLayout для позиционирования динамически создаваемых слотов.
        /// </summary>
        public event Action<BaseSlot> OnSlotCreated;

        /// <summary>
        /// Событие добавления предмета в этот инвентарь
        /// </summary>
        public event Action<InventoryItemEventContext> OnItemAdded;

        /// <summary>
        /// Событие удаления предмета из этого инвентаря
        /// </summary>
        public event Action<InventoryItemEventContext> OnItemRemoved;

        /// <summary>
        /// Событие попытки обмена предметов, затрагивающего этот инвентарь.
        /// Подписчик может отменить swap через context.Cancel = true.
        /// </summary>
        public event Action<InventorySwapContext> OnSwapAttempting;

        /// <summary>
        /// Событие успешного обмена предметов, затрагивающего этот инвентарь.
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
        /// Удалить предметы из слота этого инвентаря с эмиссией событий.
        /// Используется внешними drop processor'ами (WorldDropZone и т.п.),
        /// которые не проходят через TransferPlanExecutor.
        /// </summary>
        /// <returns>Стак удалённых предметов, или пустой стак если ничего не удалено</returns>
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

        public enum ItemBehaviorType
        {
            Unique,            // Каждый предмет в отдельном слоте (не стакается)
            Stackable,         // Предметы стакаются и автоматически мержатся
            SeparableStacks    // HoMM style: можно иметь несколько стаков одного предмета, мерж только при дропе
        }

        public enum SlotManagementType
        {
            Fixed,       // Фиксированное количество слотов
            Dynamic      // Динамическое добавление слотов
        }

        private void Awake()
        {
            // Проверяем что не было ленивой инициализации
            if (_strategy == null)
            {
                InitializeSlots();
                InitializeStrategy();
                EnsureFreeSlots(); // Создаем начальные свободные слоты для Dynamic
            }
        }

        public void Initialize(InventoryDataBindingBase inventoryDataBindingBase)
        {
            DataBinding = inventoryDataBindingBase;
        }

        private void OnValidate()
        {
            // Сортируем правила при изменении в Inspector
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

            // Удаляем возможные пустые ссылки
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
            // Создаем базовую стратегию на основе поведения предметов
            IInventoryStrategy baseStrategy;
            switch (_itemBehavior)
            {
                case ItemBehaviorType.Unique:
                    baseStrategy = new UniqueItemStrategy();
                    Extensions.DragAndDropLog($"<color=yellow>[{name}] Strategy: UniqueItemStrategy</color>");
                    break;
                case ItemBehaviorType.Stackable:
                    baseStrategy = new StackableItemStrategy(_dropPolicy.AllowMergeOnDrop, _maxStackSize, _allowItemStackOverride);
                    Extensions.DragAndDropLog($"<color=yellow>[{name}] Strategy: StackableItemStrategy (maxStack: {_maxStackSize}, itemOverride: {_allowItemStackOverride})</color>");
                    break;
                case ItemBehaviorType.SeparableStacks:
                    baseStrategy = new SeparableStacksStrategy(_dropPolicy.AllowMergeOnDrop, _maxStackSize, _allowItemStackOverride);
                    Extensions.DragAndDropLog($"<color=yellow>[{name}] Strategy: SeparableStacksStrategy (allowMerge: {_dropPolicy.AllowMergeOnDrop}, maxStack: {_maxStackSize}, itemOverride: {_allowItemStackOverride})</color>");
                    break;
                default:
                    baseStrategy = new StackableItemStrategy(_dropPolicy.AllowMergeOnDrop, _maxStackSize, _allowItemStackOverride);
                    Extensions.DragAndDropLog($"<color=yellow>[{name}] Strategy: StackableItemStrategy (default, maxStack: {_maxStackSize}, itemOverride: {_allowItemStackOverride})</color>");
                    break;
            }

            // Оборачиваем в декоратор для динамических слотов, если нужно
            if (_slotManagement == SlotManagementType.Dynamic)
            {
                SetStrategy(new DynamicSlotDecorator(baseStrategy, CreateSlot, _maxDynamicSlots, _maxFreeSlots, () => _slots, EnsureFreeSlots));
                Extensions.DragAndDropLog($"<color=yellow>[{name}] Strategy wrapped in DynamicSlotDecorator (max: {_maxDynamicSlots}, maxFree: {_maxFreeSlots})</color>");
            }
            else
            {
                SetStrategy(baseStrategy);
                Extensions.DragAndDropLog($"<color=yellow>[{name}] Strategy: Fixed slots</color>");
            }
        }

        /// <summary>
        /// Изменить стратегию инвентаря в рантайме
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

            InitializeStrategy();

            if (Application.isPlaying && DataBinding != null)
            {
                DataBinding.ReloadUI();
            }

            // EnsureFreeSlots/Trim должны выполняться ПОСЛЕ ReloadUI,
            // иначе слоты, освободившиеся при переупаковке (например Unique→Stackable),
            // не будут удалены.
            EnsureFreeSlots();
            TrimExcessFreeSlots(null);
            UpdateAllVisuals();
        }

        private void EnsureStrategyInitialized()
        {
            if (_strategy != null)
                return;

            Extensions.DragAndDropLog($"<color=yellow>[{name}] Strategy not initialized, initializing now...</color>");
            InitializeSlots();
            InitializeStrategy();
            EnsureFreeSlots();
        }

        private StrategyConfiguration CaptureStrategyConfiguration()
        {
            return new StrategyConfiguration(
                _itemBehavior,
                _maxStackSize,
                _allowItemStackOverride,
                _slotManagement,
                _maxDynamicSlots,
                _maxFreeSlots,
                _dropPolicy.AllowMergeOnDrop);
        }

        /// <summary>
        /// Добавить правило к инвентарю
        /// </summary>
        public void AddRule(IInventoryRule rule)
        {
            _ruleValidator.AddRule(rule);
        }

        /// <summary>
        /// Удалить правило
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

        /// <summary>
        /// Задать лимит стака в рантайме (например, из DataBinding).
        /// maxStackSize = 0 означает без ограничений.
        /// </summary>
        public void SetMaxStackSize(int maxStackSize, bool allowItemOverride = false)
        {
            _maxStackSize = maxStackSize;
            _allowItemStackOverride = allowItemOverride;
            _strategy?.SetMaxStackSize(maxStackSize, allowItemOverride);
        }

        public bool TryAddStack(ItemStack stack, int targetSlotIndex = -1)
        {
            if (stack == null || stack.IsEmpty)
                return false;

            EnsureStrategyInitialized();

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

            return new InventorySnapshot(slotsSnapshot);
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

            // Увеличиваем количество слотов до нужного значения
            while (_slots.Count < desiredCount)
            {
                CreateSlot();
            }

            // Удаляем лишние слоты (если были созданы новые в процессе неуспешной операции)
            while (_slots.Count > desiredCount)
            {
                var slot = _slots[_slots.Count - 1];
                _slots.RemoveAt(_slots.Count - 1);
                if (slot?.Transform != null)
                {
                    Destroy(slot.Transform.gameObject);
                }
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

                slot.UpdateVisuals();
                slot.Initialize(i, this);
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

            for (int i = 0; i < slotCount; i++)
                CreateSlot();

            UpdateAllVisuals();
        }

        private void ProcessEmptiedSlots(InventorySnapshot beforeSnapshot)
        {
            if (beforeSnapshot == null || beforeSnapshot.Slots.Count == 0)
                return;

            var emptiedSlots = new List<BaseSlot>();
            int limit = Math.Min(beforeSnapshot.Slots.Count, _slots.Count);

            for (int i = 0; i < limit; i++)
            {
                var previous = beforeSnapshot.Slots[i];
                if (previous.Count <= 0 || previous.ItemAdapter == null)
                    continue;

                var slot = _slots[i];
                if (slot != null && slot.IsEmpty)
                {
                    emptiedSlots.Add(slot);
                }
            }

            if (emptiedSlots.Count == 0)
                return;

            foreach (var slot in emptiedSlots)
            {
                HandleSlotEmptied(slot);
            }
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
        /// Очистить весь инвентарь
        /// </summary>
        public void ClearAll()
        {
            foreach (var slot in _slots)
            {
                slot.Clear();
            }

            _pointerHoveredBaseSlot = null;
            _lastInteractedBaseSlot = null;
        }

        /// <summary>
        /// Получить все непустые стаки
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
        /// Получить все уникальные предметы
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
        /// Найти активный слот для автопереноса.
        /// Приоритет: курсор → выбранный UI элемент → последний взаимодействовавший слот.
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
        /// Получить количество предметов для перетаскивания из слота.
        /// Без параметров использует настройки инвентаря, с параметрами — переданные override.
        /// </summary>
        public int GetDragAmount(BaseSlot baseSlot, DragAmount? overrideAmount = null, int? overrideCustom = null)
        {
            if (baseSlot == null || baseSlot.IsEmpty)
                return 0;

            EnsureStrategyInitialized();
            var amount = overrideAmount ?? _dragAmount;
            var custom = overrideAmount.HasValue ? (overrideCustom ?? 0) : _customDragAmount;
            var result = _dragPolicy.ResolveDragAmount(baseSlot.Stack.Count, amount, custom);

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
        /// Текущий шаг округления драга. 0 или 1 — без округления.
        /// </summary>
        public int DragAmountStep => _dragAmountStep;

        /// <summary>
        /// Округлять количество драга до кратного step.
        /// step &lt;= 1 — без округления.
        /// </summary>
        public void SetDragAmountStep(int step, DragAmountStepRounding rounding = DragAmountStepRounding.Floor)
        {
            _dragAmountStep = step;
            _dragAmountStepRounding = rounding;
        }

        internal int GetMaxStackSizeForItem(IItemAdapter itemAdapter)
        {
            if (itemAdapter == null)
                return 0;

            if (_allowItemStackOverride && itemAdapter is IStackSizeLimitable limitable)
                return Mathf.Max(1, limitable.MaxStackSize);

            return _maxStackSize > 0 ? _maxStackSize : int.MaxValue;
        }

        public ResolvedDropPolicy ResolveDropPolicy(DropRequestPolicy? requested, DragContext context)
        {
            return _dropPolicy.Resolve(requested, context);
        }

        /// <summary>
        /// Попытаться добавить предмет в конкретный слот с учетом автоматического объединения
        /// </summary>
        /// <param name="stack">Стак предметов для добавления</param>
        /// <param name="targetBaseSlot">Целевой слот</param>
        /// <param name="sourceInventory">Инвентарь-источник (для событий)</param>
        /// <param name="sourceSlotIndex">Индекс исходного слота (для событий)</param>
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
            return _placementStrategy.TryAddToSlot(_slots, stack, targetBaseSlot, EnsureFreeSlots, operationContext);
        }

        /// <summary>
        /// Проверить, может ли инвентарь принять предмет (без привязки к конкретному слоту)
        /// Проверяет правила инвентаря + наличие подходящих слотов или возможность создания нового
        /// </summary>
        public bool CanAcceptItem(IItemAdapter itemAdapter, int count, out BaseSlot suggestedBaseSlot)
            => CanAcceptItem(new InventoryAcceptanceRequest(this, itemAdapter, count), out suggestedBaseSlot);

        /// <summary>
        /// Проверить, может ли инвентарь принять предмет в контексте текущей drag/drop операции.
        /// </summary>
        public bool CanAcceptItem(InventoryAcceptanceRequest request, out BaseSlot suggestedBaseSlot)
        {
            suggestedBaseSlot = null;

            if (request?.ItemAdapter == null || request.DesiredCount <= 0)
                return false;

            EnsureStrategyInitialized();

            bool canCreateNewSlot = _slotManagement == SlotManagementType.Dynamic && _slots.Count < _maxDynamicSlots;
            int potentialNewSlots = Mathf.Max(0, _maxDynamicSlots - _slots.Count);
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

            bool canCreateNewSlot = _slotManagement == SlotManagementType.Dynamic && _slots.Count < _maxDynamicSlots;
            int potentialNewSlots = Mathf.Max(0, _maxDynamicSlots - _slots.Count);
            int result = _acceptanceStrategy.GetAcceptableCount(_slots, request, canCreateNewSlot, potentialNewSlots, baseSlotPrefab);
            Extensions.DragAndDropLog($"<color=cyan>[{name}] GetAcceptableCount: itemAdapter={request.ItemAdapter.DisplayName}, desired={request.DesiredCount}, acceptable={result}</color>");
            return result;
        }

        /// <summary>
        /// Обеспечить минимальное количество свободных слотов (для Dynamic инвентаря)
        /// </summary>
        public void EnsureFreeSlots()
        {
            if (_slotManagement != SlotManagementType.Dynamic)
                return;

            int freeSlots = CountFreeSlots();

            // Создаем недостающие слоты
            int slotsToCreate = _maxFreeSlots - freeSlots;
            for (int i = 0; i < slotsToCreate && _slots.Count < _maxDynamicSlots; i++)
            {
                CreateSlot();
                Extensions.DragAndDropLog($"<color=green>[{name}] EnsureFreeSlots: Created slot {_slots.Count}, free slots now: {freeSlots + i + 1}/{_maxFreeSlots}</color>");
            }
        }

        /// <summary>
        /// Уведомить инвентарь о том, что указанный слот стал пустым.
        /// Используется для динамического удаления лишних слотов.
        /// </summary>
        public void HandleSlotEmptied(BaseSlot baseSlot)
        {
            if (_slotManagement != SlotManagementType.Dynamic || baseSlot == null)
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

            TrimExcessFreeSlots(baseSlot);
        }

        private void TrimExcessFreeSlots(BaseSlot preferredBaseSlot)
        {
            if (_slotManagement != SlotManagementType.Dynamic)
                return;

            bool removedAny = false;
            if (preferredBaseSlot != null && preferredBaseSlot.IsEmpty && CanRemoveAnotherSlot())
            {
                removedAny |= TryRemoveSlot(preferredBaseSlot);
            }

            while (CanRemoveAnotherSlot())
            {
                var slotToRemove = FindLastEmptySlot();
                if (slotToRemove == null)
                    break;

                if (!TryRemoveSlot(slotToRemove))
                    break;

                removedAny = true;
            }

            if (removedAny)
            {
                UpdateAllVisuals();
            }
        }

        private bool CanRemoveAnotherSlot()
        {
            if (_slotManagement != SlotManagementType.Dynamic)
                return false;

            if (_slots.Count <= _initialSlotCount)
                return false;

            int freeSlots = CountFreeSlots();
            if (freeSlots <= _maxFreeSlots)
                return false;

            return true;
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
            _slots.RemoveAt(index);

            for (int i = index; i < _slots.Count; i++)
            {
                _slots[i].Initialize(i, this);
            }

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
        /// Выполнить обмен предметов между двумя слотами (возможно из разных инвентарей).
        /// События не генерируются напрямую, информация возвращается через SwapOperationResult.
        /// </summary>
        /// <param name="targetBaseSlot">Целевой слот (из этого инвентаря)</param>
        /// <param name="sourceBaseSlot">Исходный слот (может быть из другого инвентаря)</param>
        /// <param name="result">Результат обмена для генерации событий</param>
        /// <returns>True если swap успешен</returns>
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

            // Проверяем что targetSlot принадлежит этому инвентарю
            if (!ReferenceEquals(targetBaseSlot.Inventory, this))
            {
                Extensions.DragAndDropLog("<color=red>[TrySwapSlots] Target slot doesn't belong to this inventory</color>");
                return false;
            }

            // Сохраняем копии стаков для событий
            var targetStackBackup = ItemStack.TryCreate(targetBaseSlot.Stack.Adapters, out var targetBackup)
                ? targetBackup
                : ItemStack.Empty();
            var sourceStackBackup = ItemStack.TryCreate(sourceBaseSlot.Stack.Adapters, out var sourceBackup)
                ? sourceBackup
                : ItemStack.Empty();

            try
            {
                // Выполняем обмен
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

                // Откатываем изменения
                targetBaseSlot.SetStack(targetStackBackup);
                sourceBaseSlot.SetStack(sourceStackBackup);
                targetBaseSlot.UpdateVisuals();
                sourceBaseSlot.UpdateVisuals();

                return false;
            }
        }
    }
}
