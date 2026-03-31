using System;
using System.Collections.Generic;
using System.Linq;
using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Inspector;
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
        [SerializeField, Required, Tooltip("Контейнер для слотов")]
        private Transform _slotContainer;

        [FoldoutGroup("Slot Setup")]
        [SerializeField, Required, Tooltip("Префаб слота")]
        private UniversalSlot _slotPrefab;

        [FoldoutGroup("Slot Setup")]
        [SerializeField, Tooltip("Количество слотов при инициализации")]
        private int _initialSlotCount = 10;

        [FoldoutGroup("Strategy", expanded: true)]
        [InfoBox("PrimaryAdapter Behavior: как предметы размещаются | Slot Management: управление количеством слотов", InfoMessageType.Info)]
        [SerializeField, LabelText("PrimaryAdapter Behavior")]
        private ItemBehaviorType _itemBehavior = ItemBehaviorType.Stackable;

        [FoldoutGroup("Strategy")]
        [SerializeField, LabelText("Drag Amount")]
        [Tooltip("Сколько предметов брать при перетаскивании из стака")]
        [ShowIf(nameof(ShowDragAmountSettings))]
        private DragAmount _dragAmount = DragAmount.All;

        [FoldoutGroup("Strategy")]
        [SerializeField, Tooltip("Количество предметов при Custom")]
        [ShowIf(nameof(ShowCustomDragAmount))]
        private int _customDragAmount = 1;

        [FoldoutGroup("Strategy")]
        [SerializeField, Tooltip("Максимальный размер стака. 0 или меньше = без ограничений.")]
        [ShowIf(nameof(ShowDragAmountSettings))]
        private int _maxStackSize = 0;

        [FoldoutGroup("Strategy")]
        [SerializeField, Tooltip("Разрешить предметам переопределять лимит стака через IStackSizeLimitable. " +
                                 "Если true — предмет использует свой лимит. Если false — _maxStackSize является жёстким потолком.")]
        [ShowIf(nameof(ShowMaxStackOverride))]
        private bool _allowItemStackOverride = false;

        [FoldoutGroup("Strategy")]
        [SerializeField, LabelText("Slot Management")]
        private SlotManagementType _slotManagement = SlotManagementType.Fixed;

        [FoldoutGroup("Strategy")]
        [SerializeField, Tooltip("Максимальное количество слотов (для Dynamic)")]
        [ShowIf(nameof(_slotManagement), nameof(SlotManagementType.Dynamic))]
        private int _maxDynamicSlots = 100;

        [FoldoutGroup("Strategy")]
        [SerializeField, Tooltip("Минимальное количество свободных слотов (для Dynamic). 0 = создавать только при TryAddItem, не при переносе в слоты")]
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
        [SerializeField, Tooltip("Слоты, созданные в сцене. Можно задать вручную в инспекторе. Если пусто — будут найдены автоматически.")]
        private List<ISlot> _slots = new List<ISlot>();

        private IInventoryStrategy _strategy;
        private IPlacementStrategy _placementStrategy;
        private IAcceptanceStrategy _acceptanceStrategy;
        private IDragPolicy _dragPolicy;
        private IInventoryQueryStrategy _queryStrategy;
        private IItemAdapterConverter _itemAdapterConverter = IdentityItemAdapterConverter.Instance;
        private UniversalSlot _pointerHoveredSlot;
        private UniversalSlot _lastInteractedSlot;

        public IReadOnlyList<ISlot> Slots => _slots.AsReadOnly();
        public int SlotCount => _slots.Count;
        public InventoryRuleValidator RuleValidator => _ruleValidator;
        public ItemBehaviorType ItemBehavior => _itemBehavior;
        public SlotManagementType SlotManagement => _slotManagement;
        public UniversalSlot SlotPrefab => _slotPrefab;

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

        public IItemAdapterConverter ItemAdapterConverter => _itemAdapterConverter ?? IdentityItemAdapterConverter.Instance;
        
        public InventoryDataBindingBase DataBinding { get; private set; }

        internal bool CheckOccupiedSlotDrop(DragEntry entry, ISlot occupiedSlot)
            => DataBinding != null && DataBinding.CheckOccupiedSlotDrop(entry, occupiedSlot);

        internal bool ExecuteOccupiedSlotDrop(DragEntry entry, ISlot occupiedSlot)
            => DataBinding != null && DataBinding.DoOccupiedSlotDrop(entry, occupiedSlot);

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

        internal void EmitItemAdded(IItemAdapter itemAdapter, int count, int slotIndex, IInventory sourceInventory, ISlot sourceSlot, ISlot targetSlot)
        {
            var context = new InventoryItemEventContext(
                itemAdapter, count, slotIndex, sourceInventory, this, sourceSlot, targetSlot);

            DataBinding?.HandleItemAdded(context);
            OnItemAdded?.Invoke(context);
        }

        internal void EmitItemRemoved(IItemAdapter itemAdapter, int count, int slotIndex, IInventory targetInventory, ISlot sourceSlot, ISlot targetSlot)
        {
            var context = new InventoryItemEventContext(
                itemAdapter, count, slotIndex, this, targetInventory, sourceSlot, targetSlot);

            DataBinding?.HandleItemRemoved(context);
            OnItemRemoved?.Invoke(context);
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

        public void SetItemConverter(IItemAdapterConverter itemAdapterConverter)
        {
            _itemAdapterConverter = itemAdapterConverter ?? IdentityItemAdapterConverter.Instance;
        }

        private void OnValidate()
        {
            // Сортируем правила при изменении в Inspector
            _ruleValidator?.OnValidate();
        }

        private void OnDisable()
        {
            _pointerHoveredSlot = null;
            _lastInteractedSlot = null;
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
                _slots = new List<ISlot>();
            }

            // Удаляем возможные пустые ссылки
            _slots.RemoveAll(slot => slot == null);


            var autoSlots = _slotContainer.GetComponentsInChildren<ISlot>(includeInactive: true).ToList();
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

        private ISlot CreateSlot()
        {
            Extensions.DragAndDropLog($"<color=magenta>[{name}] CreateSlot called! Current count: {_slots.Count}</color>");

            if (_slotPrefab == null)
            {
                Extensions.DragAndDropLog($"[{name}] CreateSlot: _slotPrefab is NULL!");
                return null;
            }

            if (_slotContainer == null)
            {
                Extensions.DragAndDropLog($"[{name}] CreateSlot: _slotContainer is NULL!");
                return null;
            }

            var slotGO = Instantiate(_slotPrefab, _slotContainer);
            slotGO.Initialize(_slots.Count, this);
            _slots.Add(slotGO);

            Extensions.DragAndDropLog($"<color=magenta>[{name}] CreateSlot SUCCESS! New count: {_slots.Count}</color>");
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

        public ISlot GetSlot(int index)
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

        internal bool TryPreviewIncomingItem(IItemAdapter itemAdapter, out IItemAdapter converted)
        {
            converted = itemAdapter;

            if (itemAdapter == null)
                return false;

            return ItemAdapterConverter.TryConvertIncoming(itemAdapter, out converted);
        }

        internal bool TryPreviewOutgoingItem(IItemAdapter itemAdapter, out IItemAdapter converted)
        {
            converted = itemAdapter;

            if (itemAdapter == null)
                return false;

            return ItemAdapterConverter.TryConvertOutgoing(itemAdapter, out converted);
        }

        bool TryConvertIncomingItem(ItemStack stack)
        {
            if (!TryPreviewIncomingItem(stack.PrimaryAdapter, out var converted))
                return false;

            if (!ReferenceEquals(converted, stack.PrimaryAdapter))
                stack.ReplaceItem(converted);

            return true;
        }

        internal bool TryConvertOutgoingItem(ItemStack stack)
        {
            if (!TryPreviewOutgoingItem(stack.PrimaryAdapter, out var converted))
                return false;

            if (!ReferenceEquals(converted, stack.PrimaryAdapter))
                stack.ReplaceItem(converted);

            return true;
        }

        public bool TryAddStack(ItemStack stack, int targetSlotIndex = -1)
        {
            if (stack == null || stack.IsEmpty)
                return false;

            if (!TryConvertIncomingItem(stack))
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

            if (!TryConvertIncomingItem(stack))
                return false;

            EnsureStrategyInitialized();
            return _placementStrategy.TryAddQuite(_slots, stack, targetSlotIndex);
        }

        internal bool CanAcceptByRules(
            ISlot slot,
            IItemAdapter itemAdapter,
            int previewCount,
            InventoryAcceptanceRequest request = null,
            bool allowForeignSlot = false)
        {
            if (slot == null || itemAdapter == null || previewCount <= 0)
                return false;

            if (!allowForeignSlot && !ReferenceEquals(slot.Inventory, this))
                return false;

            var previewStack = new ItemStack(itemAdapter, previewCount);
            return ValidateRulesForPreview(slot, previewStack, request);
        }

        private bool ValidateRulesForPreview(ISlot slot, ItemStack previewStack, InventoryAcceptanceRequest request)
        {
            if (slot == null || previewStack == null || previewStack.IsEmpty)
                return false;

            var context = request?.CreateValidationContext(slot, previewStack.Count, previewStack.PrimaryAdapter)
                ?? new DragContext(previewStack, null, null, slot, this);
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

            if (slot.SlotRuleValidator != null)
            {
                var slotResult = slot.SlotRuleValidator.ValidateDrop(context, entry);
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
                    slotsSnapshot.Add(new InventorySlotState(slot.Stack.PrimaryAdapter, slot.Stack.Count));
                }
                else
                {
                    slotsSnapshot.Add(new InventorySlotState(null, 0));
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
                _slots = new List<ISlot>();
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
                    slot.SetStack(new ItemStack(state.ItemAdapter, state.Count));
                }

                slot.UpdateVisuals();
                slot.Initialize(i, this);
            }
        }

        public void ReInitSlots(int slotCount)
        {
            slotCount = Mathf.Max(0, slotCount);
            _initialSlotCount = slotCount;

            _pointerHoveredSlot = null;
            _lastInteractedSlot = null;

            if (_slots == null)
                _slots = new List<ISlot>();

            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                ISlot slot = _slots[i];
                if (slot?.Transform != null)
                {
                    slot.Transform.gameObject.SetActive(false);
                    Destroy(slot.Transform.gameObject);
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

            var emptiedSlots = new List<ISlot>();
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

            _pointerHoveredSlot = null;
            _lastInteractedSlot = null;
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

        internal void NotifyPointerEnter(UniversalSlot slot)
        {
            if (slot == null || !ReferenceEquals(slot.Inventory, this))
                return;

            _pointerHoveredSlot = slot;
        }

        internal void NotifyPointerExit(UniversalSlot slot)
        {
            if (_pointerHoveredSlot == slot)
            {
                _pointerHoveredSlot = null;
            }
        }

        internal void NotifySlotInteracted(UniversalSlot slot)
        {
            if (slot == null || !ReferenceEquals(slot.Inventory, this))
                return;

            _lastInteractedSlot = slot;
        }

        /// <summary>
        /// Найти активный слот для автопереноса.
        /// Приоритет: курсор → выбранный UI элемент → последний взаимодействовавший слот.
        /// </summary>
        public UniversalSlot ResolveAutoTransferSlot()
        {
            if (_pointerHoveredSlot != null && ReferenceEquals(_pointerHoveredSlot.Inventory, this))
                return _pointerHoveredSlot;

            var selectedObject = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;

            if (selectedObject != null)
            {
                var selectedSlot = selectedObject.GetComponent<UniversalSlot>()
                    ?? selectedObject.GetComponentInParent<UniversalSlot>();

                if (selectedSlot != null && ReferenceEquals(selectedSlot.Inventory, this))
                    return selectedSlot;
            }

            if (_lastInteractedSlot != null && ReferenceEquals(_lastInteractedSlot.Inventory, this))
                return _lastInteractedSlot;

            return null;
        }

        /// <summary>
        /// Получить количество предметов для перетаскивания из слота.
        /// Без параметров использует настройки инвентаря, с параметрами — переданные override.
        /// </summary>
        public int GetDragAmount(ISlot slot, DragAmount? overrideAmount = null, int? overrideCustom = null)
        {
            if (slot == null || slot.IsEmpty)
                return 0;

            EnsureStrategyInitialized();
            var amount = overrideAmount ?? _dragAmount;
            var custom = overrideAmount.HasValue ? (overrideCustom ?? 0) : _customDragAmount;
            var result = _dragPolicy.ResolveDragAmount(slot.Stack.Count, amount, custom);

            if (_dragAmountStep > 1)
            {
                result = _dragAmountStepRounding switch
                {
                    DragAmountStepRounding.Ceil    => ((result + _dragAmountStep - 1) / _dragAmountStep) * _dragAmountStep,
                    DragAmountStepRounding.Nearest => ((result + _dragAmountStep / 2) / _dragAmountStep) * _dragAmountStep,
                    _                              => (result / _dragAmountStep) * _dragAmountStep
                };

                result = Math.Min(result, slot.Stack.Count);
            }

            return result;
        }

        private int _dragAmountStep;
        private DragAmountStepRounding _dragAmountStepRounding;

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
        /// <param name="targetSlot">Целевой слот</param>
        /// <param name="sourceInventory">Инвентарь-источник (для событий)</param>
        /// <param name="sourceSlotIndex">Индекс исходного слота (для событий)</param>
        public bool TryAddToSlot(
            ItemStack stack,
            ISlot targetSlot,
            IInventory sourceInventory = null,
            int sourceSlotIndex = -1,
            SlotOperationContext operationContext = null)
        {
            if (stack == null || stack.IsEmpty || targetSlot == null)
                return false;

            // Цепочка конвертации: source.ConvertOutgoing → target.ConvertIncoming
            if (sourceInventory is UniversalInventory srcInv && !srcInv.TryConvertOutgoingItem(stack))
                return false;

            if (!TryConvertIncomingItem(stack))
                return false;

            EnsureStrategyInitialized();
            return _placementStrategy.TryAddToSlot(_slots, stack, targetSlot, EnsureFreeSlots, operationContext);
        }

        /// <summary>
        /// Проверить, может ли инвентарь принять предмет (без привязки к конкретному слоту)
        /// Проверяет правила инвентаря + наличие подходящих слотов или возможность создания нового
        /// </summary>
        public bool CanAcceptItem(IItemAdapter itemAdapter, int count, out ISlot suggestedSlot)
            => CanAcceptItem(new InventoryAcceptanceRequest(this, itemAdapter, count), out suggestedSlot);

        /// <summary>
        /// Проверить, может ли инвентарь принять предмет в контексте текущей drag/drop операции.
        /// </summary>
        public bool CanAcceptItem(InventoryAcceptanceRequest request, out ISlot suggestedSlot)
        {
            suggestedSlot = null;

            if (request?.ItemAdapter == null || request.DesiredCount <= 0)
                return false;

            EnsureStrategyInitialized();

            bool canCreateNewSlot = _slotManagement == SlotManagementType.Dynamic && _slots.Count < _maxDynamicSlots;
            int potentialNewSlots = Mathf.Max(0, _maxDynamicSlots - _slots.Count);
            bool canAccept = _acceptanceStrategy.CanAcceptItem(_slots, request, canCreateNewSlot, potentialNewSlots, _slotPrefab, out suggestedSlot);

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
            int result = _acceptanceStrategy.GetAcceptableCount(_slots, request, canCreateNewSlot, potentialNewSlots, _slotPrefab);
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
        public void HandleSlotEmptied(ISlot slot)
        {
            if (_slotManagement != SlotManagementType.Dynamic || slot == null)
                return;

            if (!ReferenceEquals(slot.Inventory, this))
                return;

            if (!_slots.Contains(slot))
                return;

            if (_lastInteractedSlot == slot)
            {
                _lastInteractedSlot = null;
            }

            if (_pointerHoveredSlot == slot)
            {
                _pointerHoveredSlot = null;
            }

            if (!slot.IsEmpty)
                return;

            TrimExcessFreeSlots(slot);
        }

        private void TrimExcessFreeSlots(ISlot preferredSlot)
        {
            if (_slotManagement != SlotManagementType.Dynamic)
                return;

            bool removedAny = false;
            if (preferredSlot != null && preferredSlot.IsEmpty && CanRemoveAnotherSlot())
            {
                removedAny |= TryRemoveSlot(preferredSlot);
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

        private ISlot FindLastEmptySlot()
        {
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                var slot = _slots[i];
                if (slot.IsEmpty)
                    return slot;
            }

            return null;
        }

        private bool TryRemoveSlot(ISlot slot)
        {
            if (slot == null || !slot.IsEmpty)
                return false;

            int index = _slots.IndexOf(slot);
            if (index < 0)
                return false;

            if (_slots.Count - 1 < _initialSlotCount)
                return false;

            var slotTransform = slot.Transform;
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
        /// <param name="targetSlot">Целевой слот (из этого инвентаря)</param>
        /// <param name="sourceSlot">Исходный слот (может быть из другого инвентаря)</param>
        /// <param name="result">Результат обмена для генерации событий</param>
        /// <returns>True если swap успешен</returns>
        public bool TrySwapSlots(ISlot targetSlot, ISlot sourceSlot, out SwapOperationResult result)
        {
            result = default;
            if (targetSlot == null || sourceSlot == null)
            {
                Extensions.DragAndDropLog("<color=red>[TrySwapSlots] Slot is null</color>");
                return false;
            }

            if (targetSlot.IsEmpty || sourceSlot.IsEmpty)
            {
                Extensions.DragAndDropLog("<color=red>[TrySwapSlots] One of the slots is empty</color>");
                return false;
            }

            // Проверяем что targetSlot принадлежит этому инвентарю
            if (!ReferenceEquals(targetSlot.Inventory, this))
            {
                Extensions.DragAndDropLog("<color=red>[TrySwapSlots] Target slot doesn't belong to this inventory</color>");
                return false;
            }

            // Сохраняем копии стаков для событий
            var targetStackBackup = ItemStack.TryCreate(targetSlot.Stack.Adapters, out var targetBackup)
                ? targetBackup
                : ItemStack.Empty();
            var sourceStackBackup = ItemStack.TryCreate(sourceSlot.Stack.Adapters, out var sourceBackup)
                ? sourceBackup
                : ItemStack.Empty();

            try
            {
                // Выполняем обмен
                var tempStack = targetSlot.Stack;
                targetSlot.SetStack(sourceSlot.Stack);
                sourceSlot.SetStack(tempStack);

                targetSlot.UpdateVisuals();
                sourceSlot.UpdateVisuals();

                Extensions.DragAndDropLog($"<color=green>[{name}] Swap completed: slot {targetSlot.Index} ↔ slot {sourceSlot.Index}</color>");
                result = new SwapOperationResult(targetStackBackup, sourceStackBackup);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{name}] TrySwapSlots exception: {ex.Message}");

                // Откатываем изменения
                targetSlot.SetStack(targetStackBackup);
                sourceSlot.SetStack(sourceStackBackup);
                targetSlot.UpdateVisuals();
                sourceSlot.UpdateVisuals();

                return false;
            }
        }
    }
}
