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
    public class UniversalInventory : MonoBehaviour, IInventory, IInventorySnapshotProvider
    {
        [FoldoutGroup("Slot Setup", expanded: true)]
        [SerializeField, Required, Tooltip("Контейнер для слотов")]
        private Transform _slotContainer;

        [FoldoutGroup("Slot Setup")]
        [SerializeField, Required, Tooltip("Префаб слота")]
        private UniversalSlot _slotPrefab;

        [FoldoutGroup("Slot Setup")]
        [SerializeField, Range(0, 100), Tooltip("Количество слотов при инициализации")]
        private int _initialSlotCount = 10;

        [FoldoutGroup("Strategy", expanded: true)]
        [InfoBox("Item Behavior: как предметы размещаются | Slot Management: управление количеством слотов", InfoMessageType.Info)]
        [SerializeField, EnumToggleButtons, LabelText("Item Behavior")]
        private ItemBehaviorType _itemBehavior = ItemBehaviorType.Stackable;

        [FoldoutGroup("Strategy")]
        [SerializeField, EnumToggleButtons, LabelText("Drag Amount")]
        [Tooltip("Сколько предметов брать при перетаскивании из стака")]
        [ShowIf(nameof(ShowDragAmountSettings))]
        private DragAmountType _dragAmount = DragAmountType.All;

        [FoldoutGroup("Strategy")]
        [SerializeField, Range(1, 100), Tooltip("Количество предметов при Custom")]
        [ShowIf(nameof(ShowCustomDragAmount))]
        private int _customDragAmount = 1;

        [FoldoutGroup("Strategy")]
        [SerializeField, Tooltip("Автоматически объединять предметы при дропе в пустой слот")]
        [ShowIf(nameof(_itemBehavior), nameof(ItemBehaviorType.Stackable))]
        private bool _autoMergeOnDrop = true;

        [FoldoutGroup("Strategy")]
        [SerializeField, Tooltip("Объединять предметы при дропе на такой же предмет")]
        [ShowIf(nameof(_itemBehavior), nameof(ItemBehaviorType.SeparableStacks))]
        private bool _allowMergeOnDrop = true;

        [FoldoutGroup("Strategy")]
        [SerializeField, EnumToggleButtons, LabelText("Slot Management")]
        private SlotManagementType _slotManagement = SlotManagementType.Fixed;

        [FoldoutGroup("Strategy")]
        [SerializeField, Range(1, 200), Tooltip("Максимальное количество слотов (для Dynamic)")]
        [ShowIf(nameof(_slotManagement), nameof(SlotManagementType.Dynamic))]
        private int _maxDynamicSlots = 100;

        [FoldoutGroup("Strategy")]
        [SerializeField, Range(0, 20), Tooltip("Минимальное количество свободных слотов (для Dynamic). 0 = создавать только при TryAddItem, не при переносе в слоты")]
        [ShowIf(nameof(_slotManagement), nameof(SlotManagementType.Dynamic))]
        private int _maxFreeSlots = 1;

        private bool ShowDragAmountSettings => _itemBehavior == ItemBehaviorType.Stackable || _itemBehavior == ItemBehaviorType.SeparableStacks;
        private bool ShowCustomDragAmount => ShowDragAmountSettings && _dragAmount == DragAmountType.Custom;

        [FoldoutGroup("Rules")]
        [SerializeField, HideLabel]
        private InventoryRuleValidator _ruleValidator = new InventoryRuleValidator();

        [FoldoutGroup("Drop Policy", expanded: false)]
        [SerializeField, HideLabel]
        private DropPolicySettings _dropPolicySettings = new DropPolicySettings();

        [FoldoutGroup("Slot Setup", expanded: true)]
        [SerializeField, Tooltip("Слоты, созданные в сцене. Можно задать вручную в инспекторе. Если пусто — будут найдены автоматически.")]
        private List<ISlot> _slots = new List<ISlot>();

        [FoldoutGroup("Debug")]
        [ReadOnly, ShowInInspector]
        private IInventoryStrategy _strategy;

        [FoldoutGroup("Debug")]
        [ShowInInspector, ReadOnly]
        private UniversalSlot _pointerHoveredSlot;

        [FoldoutGroup("Debug")]
        [ShowInInspector, ReadOnly]
        private UniversalSlot _lastInteractedSlot;

        public IReadOnlyList<ISlot> Slots => _slots.AsReadOnly();
        public int SlotCount => _slots.Count;
        public InventoryRuleValidator RuleValidator => _ruleValidator;
        public bool AutoMergeOnDrop => _autoMergeOnDrop;
        public ItemBehaviorType ItemBehavior => _itemBehavior;
        public SlotManagementType SlotManagement => _slotManagement;
        public UniversalSlot SlotPrefab => _slotPrefab;
        
        public InventoryDataBindingBase DataBinding { get; private set; }

        /// <summary>
        /// Effective drop policy for this inventory.
        /// If custom settings are disabled, returns system defaults by drag type.
        /// </summary>
        public DropPolicy GetDropPolicy(bool isBatchDrag)
        {
            var configured = _dropPolicySettings?.BuildOrNull();
            if (configured != null)
                return configured;

            return isBatchDrag ? DropPolicy.BatchAtomic : DropPolicy.SingleDefault;
        }

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

        internal void EmitItemAdded(IInventoryItem item, int count, int slotIndex, IInventory sourceInventory, ISlot sourceSlot, ISlot targetSlot)
        {
            var context = new InventoryItemEventContext(
                item, count, slotIndex, sourceInventory, this, sourceSlot, targetSlot);

            DataBinding?.HandleItemAdded(context);
            OnItemAdded?.Invoke(context);
        }

        internal void EmitItemRemoved(IInventoryItem item, int count, int slotIndex, IInventory targetInventory, ISlot sourceSlot, ISlot targetSlot)
        {
            var context = new InventoryItemEventContext(
                item, count, slotIndex, this, targetInventory, sourceSlot, targetSlot);

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

        public enum DragAmountType
        {
            One,         // Брать по 1 предмету
            Half,        // Брать половину стака
            All,         // Брать весь стак
            Custom       // Указанное количество
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
            Extentions.DragAndDropLog($"<color=magenta>[{name}] CacheSlots completed! Found {_slots.Count} slots.</color>");
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
            Extentions.DragAndDropLog($"<color=magenta>[{name}] CreateSlot called! Current count: {_slots.Count}</color>");

            if (_slotPrefab == null)
            {
                Extentions.DragAndDropLog($"[{name}] CreateSlot: _slotPrefab is NULL!");
                return null;
            }

            if (_slotContainer == null)
            {
                Extentions.DragAndDropLog($"[{name}] CreateSlot: _slotContainer is NULL!");
                return null;
            }

            var slotGO = Instantiate(_slotPrefab, _slotContainer);
            slotGO.Initialize(_slots.Count, this);
            _slots.Add(slotGO);

            Extentions.DragAndDropLog($"<color=magenta>[{name}] CreateSlot SUCCESS! New count: {_slots.Count}</color>");
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
                    Extentions.DragAndDropLog($"<color=yellow>[{name}] Strategy: UniqueItemStrategy</color>");
                    break;
                case ItemBehaviorType.Stackable:
                    baseStrategy = new StackableItemStrategy(_autoMergeOnDrop);
                    Extentions.DragAndDropLog($"<color=yellow>[{name}] Strategy: StackableItemStrategy</color>");
                    break;
                case ItemBehaviorType.SeparableStacks:
                    baseStrategy = new SeparableStacksStrategy(_allowMergeOnDrop);
                    Extentions.DragAndDropLog($"<color=yellow>[{name}] Strategy: SeparableStacksStrategy (allowMerge: {_allowMergeOnDrop})</color>");
                    break;
                default:
                    baseStrategy = new StackableItemStrategy(_autoMergeOnDrop);
                    Extentions.DragAndDropLog($"<color=yellow>[{name}] Strategy: StackableItemStrategy (default)</color>");
                    break;
            }

            // Оборачиваем в декоратор для динамических слотов, если нужно
            if (_slotManagement == SlotManagementType.Dynamic)
            {
                _strategy = new DynamicSlotDecorator(baseStrategy, CreateSlot, _maxDynamicSlots, _maxFreeSlots, () => _slots, EnsureFreeSlots);
                Extentions.DragAndDropLog($"<color=yellow>[{name}] Strategy wrapped in DynamicSlotDecorator (max: {_maxDynamicSlots}, maxFree: {_maxFreeSlots})</color>");
            }
            else
            {
                _strategy = baseStrategy;
                Extentions.DragAndDropLog($"<color=yellow>[{name}] Strategy: Fixed slots</color>");
            }
        }

        /// <summary>
        /// Изменить стратегию инвентаря в рантайме
        /// </summary>
        public void SetStrategy(IInventoryStrategy strategy)
        {
            _strategy = strategy;
        }

        private void EnsureStrategyInitialized()
        {
            if (_strategy != null)
                return;

            Extentions.DragAndDropLog($"<color=yellow>[{name}] Strategy not initialized, initializing now...</color>");
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

        public bool TryAddItem(IInventoryItem item, int count = 1, int targetSlotIndex = -1)
        {
            if (item == null) return false;

            var stack = new ItemStack(item, count);
            return TryAddStack(stack, targetSlotIndex);
        }

        internal bool TryPreviewIncomingItem(IInventoryItem item, out IInventoryItem converted)
        {
            converted = item;

            if (item == null)
                return false;

            if (DataBinding == null)
                return true;

            converted = DataBinding.ConvertIncomingItem(item);
            return converted != null;
        }

        internal bool TryPreviewOutgoingItem(IInventoryItem item, out IInventoryItem converted)
        {
            converted = item;

            if (item == null)
                return false;

            if (DataBinding == null)
                return true;

            converted = DataBinding.ConvertOutgoingItem(item);
            return converted != null;
        }

        bool TryConvertIncomingItem(ItemStack stack)
        {
            if (!TryPreviewIncomingItem(stack.Item, out var converted))
                return false;

            if (!ReferenceEquals(converted, stack.Item))
                stack.ReplaceItem(converted);

            return true;
        }

        internal bool TryConvertOutgoingItem(ItemStack stack)
        {
            if (!TryPreviewOutgoingItem(stack.Item, out var converted))
                return false;

            if (!ReferenceEquals(converted, stack.Item))
                stack.ReplaceItem(converted);

            return true;
        }

        internal bool RequiresStrategyPlacement(ItemStack stack)
        {
            EnsureStrategyInitialized();
            return _strategy.RequiresStrategyPlacement(stack);
        }

        internal bool CanUseAlternativeSlot(ISlot slot, IInventoryItem item)
        {
            EnsureStrategyInitialized();
            return _strategy.CanUseAlternativeSlot(slot, item);
        }

        internal bool UsesPerItemSlotPlanning()
        {
            EnsureStrategyInitialized();
            return _strategy.UsesPerItemSlotPlanning;
        }

        public bool TryAddStack(ItemStack stack, int targetSlotIndex = -1)
        {
            if (stack == null || stack.IsEmpty)
                return false;

            if (!TryConvertIncomingItem(stack))
                return false;

            EnsureStrategyInitialized();

            Extentions.DragAndDropLog($"<color=cyan>[{name}] TryAddStack: {stack.Item.DisplayName} x{stack.Count}, targetSlot={targetSlotIndex}, currentSlots={_slots.Count}, strategy={_strategy?.GetType().Name}</color>");

            bool success = _strategy.TryAdd(_slots as List<ISlot>, stack, targetSlotIndex);
            bool stackConsumed = stack == null || stack.IsEmpty;

            if ((!success || !stackConsumed) && stack != null && !stack.IsEmpty)
            {
                success = TryRelocateAndRetry(stack, targetSlotIndex);
                stackConsumed = stack == null || stack.IsEmpty;
            }

            if (success && stackConsumed)
            {
                Extentions.DragAndDropLog($"<color=green>[{name}] TryAddStack SUCCESS! Now have {_slots.Count} slots</color>");
                UpdateAllVisuals();
            }
            else
            {
                Extentions.DragAndDropLog($"<color=red>[{name}] TryAddStack FAILED!</color>");
                success = false;
            }

            return success;
        }

        internal bool CanAcceptByRules(ISlot slot, IInventoryItem item, int previewCount)
        {
            if (slot == null || item == null || previewCount <= 0)
                return false;

            if (!ReferenceEquals(slot.Inventory, this))
                return false;

            var previewStack = new ItemStack(item, previewCount);
            return ValidateRulesForPreview(slot, previewStack);
        }

        private bool ValidateRulesForPreview(ISlot slot, ItemStack previewStack)
        {
            if (slot == null || previewStack == null || previewStack.IsEmpty)
                return false;

            var context = new DragContext(previewStack, null, null);
            context.SetTarget(slot, this);
            var entry = context.Entries[0];

            if (_ruleValidator != null)
            {
                var inventoryResult = _ruleValidator.ValidateDrop(context, entry);
                if (!inventoryResult.IsValid)
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

        // Простая эвристика: пытаемся переупаковать предметы, чтобы освободить подходящий слот.
        // При необходимости можно заменить на более сложный перебор (например, bipartite matching).
        private bool TryRelocateAndRetry(ItemStack stack, int targetSlotIndex)
        {
            if (stack == null || stack.IsEmpty)
                return false;

            var snapshot = CaptureSnapshot();
            var candidates = BuildRelocationCandidates(stack, targetSlotIndex);

            bool isFirstAttempt = true;
            foreach (var slot in candidates)
            {
                if (!isFirstAttempt)
                {
                    RestoreSnapshot(snapshot);
                }
                else
                {
                    isFirstAttempt = false;
                }

                if (!TryRelocateOccupant(slot, stack))
                    continue;

                // Если нужно больше гибкости — можно заменить на полный пересчет (matching).
                if (_strategy.TryAdd(_slots as List<ISlot>, stack, targetSlotIndex))
                {
                    Extentions.DragAndDropLog($"<color=green>[{name}] TryAddStack recovered via relocation</color>");
                    return true;
                }
            }

            RestoreSnapshot(snapshot);
            return false;
        }

        private List<ISlot> BuildRelocationCandidates(ItemStack stack, int targetSlotIndex)
        {
            var result = new List<ISlot>();

            if (targetSlotIndex >= 0 && targetSlotIndex < _slots.Count)
            {
                var targetedSlot = _slots[targetSlotIndex];
                if (!result.Contains(targetedSlot))
                    result.Add(targetedSlot);
            }

            foreach (var slot in _slots)
            {
                if (result.Contains(slot))
                    continue;

                if (slot.IsEmpty)
                    continue;

                if (CanAcceptByRules(slot, stack.Item, stack.Count))
                {
                    result.Add(slot);
                }
            }

            foreach (var slot in _slots)
            {
                if (result.Contains(slot))
                    continue;

                if (!slot.IsEmpty)
                    result.Add(slot);
            }

            return result;
        }

        private bool TryRelocateOccupant(ISlot slotToFree, ItemStack incomingStack)
        {
            if (slotToFree == null || slotToFree.IsEmpty)
                return false;

            var occupantStack = slotToFree.Stack;
            if (occupantStack == null || occupantStack.IsEmpty)
                return false;

            var destinations = new List<ISlot>();
            foreach (var slot in _slots)
            {
                if (slot == slotToFree)
                    continue;

                if (!slot.IsEmpty)
                {
                    if (!slot.Stack.CanStack(occupantStack.Item))
                        continue;

                    if (!CanAcceptByRules(slot, occupantStack.Item, occupantStack.Count))
                        continue;

                    destinations.Add(slot);
                }
                else
                {
                    if (!CanAcceptByRules(slot, occupantStack.Item, occupantStack.Count))
                        continue;

                    destinations.Add(slot);
                }
            }

            destinations.Sort((a, b) => CompareDestinations(a, b, incomingStack));

            foreach (var destination in destinations)
            {
                if (TryMoveStack(slotToFree, destination))
                    return true;
            }

            return false;
        }

        private int CompareDestinations(ISlot a, ISlot b, ItemStack incomingStack)
        {
            bool aAllowsIncoming = CanAcceptByRules(a, incomingStack.Item, 1);
            bool bAllowsIncoming = CanAcceptByRules(b, incomingStack.Item, 1);

            if (aAllowsIncoming != bAllowsIncoming)
                return aAllowsIncoming ? 1 : -1;

            return a.Index.CompareTo(b.Index);
        }

        private bool TryMoveStack(ISlot sourceSlot, ISlot destinationSlot)
        {
            if (sourceSlot == null || destinationSlot == null)
                return false;

            var sourceStack = sourceSlot.Stack;
            if (sourceStack == null || sourceStack.IsEmpty)
                return false;

            int amountToMove = sourceStack.Count;
            var itemToMove = sourceStack.Item;

            if (!destinationSlot.IsEmpty)
            {
                if (!destinationSlot.Stack.CanStack(itemToMove))
                    return false;

                if (!CanAcceptByRules(destinationSlot, itemToMove, amountToMove))
                    return false;

                destinationSlot.Stack.AddToStack(amountToMove);
                destinationSlot.UpdateVisuals();
                sourceSlot.Clear();
                return true;
            }

            if (!CanAcceptByRules(destinationSlot, itemToMove, amountToMove))
                return false;

            var movedStack = new ItemStack(itemToMove, amountToMove);
            destinationSlot.SetStack(movedStack);
            destinationSlot.UpdateVisuals();
            sourceSlot.Clear();
            return true;
        }

        public InventorySnapshot CaptureSnapshot()
        {
            var slotsSnapshot = new List<InventorySlotState>(_slots.Count);
            foreach (var slot in _slots)
            {
                if (slot != null && !slot.IsEmpty)
                {
                    slotsSnapshot.Add(new InventorySlotState(slot.Stack.Item, slot.Stack.Count));
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
                    slot.SetStack(new ItemStack(state.Item, state.Count));
                }

                slot.UpdateVisuals();
                slot.Initialize(i, this);
            }
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
                if (previous.Count <= 0 || previous.Item == null)
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

        public bool TryRemoveItem(IInventoryItem item, int count = 1, int sourceSlotIndex = -1)
        {
            if (item == null) return false;

            InventorySnapshot snapshot = null;
            if (_slotManagement == SlotManagementType.Dynamic)
            {
                snapshot = CaptureSnapshot();
            }

            bool success = _strategy.TryRemove(_slots as List<ISlot>, item, count, sourceSlotIndex);

            if (success)
            {
                UpdateAllVisuals();
                if (_slotManagement == SlotManagementType.Dynamic)
                {
                    ProcessEmptiedSlots(snapshot);
                }
            }

            return success;
        }

        public bool Contains(IInventoryItem item)
        {
            return _strategy.Contains(_slots as List<ISlot>, item);
        }

        public int GetItemCount(IInventoryItem item)
        {
            return _strategy.GetItemCount(_slots as List<ISlot>, item);
        }

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
        public List<IInventoryItem> GetUniqueItems()
        {
            var items = new List<IInventoryItem>();
            var addedIds = new HashSet<string>();

            foreach (var slot in _slots)
            {
                if (!slot.IsEmpty && !addedIds.Contains(slot.Stack.Item.ItemId))
                {
                    items.Add(slot.Stack.Item);
                    addedIds.Add(slot.Stack.Item.ItemId);
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
        /// Получить количество предметов для перетаскивания из слота
        /// </summary>
        public int GetDragAmount(ISlot slot)
        {
            if (slot == null || slot.IsEmpty)
                return 0;

            EnsureStrategyInitialized();
            return _strategy.ResolveDragAmount(slot.Stack.Count, _dragAmount, _customDragAmount);
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
            return _strategy.TryAddToSlot(_slots, stack, targetSlot, EnsureFreeSlots, operationContext);
        }

        /// <summary>
        /// Проверить, может ли инвентарь принять предмет (без привязки к конкретному слоту)
        /// Проверяет правила инвентаря + наличие подходящих слотов или возможность создания нового
        /// </summary>
        public bool CanAcceptItem(IInventoryItem item, int count, out ISlot suggestedSlot)
        {
            suggestedSlot = null;

            if (item == null || count <= 0)
                return false;

            // Создаем временный стак для проверки
            var previewStack = new ItemStack(item, count);

            // 1. Проверяем правила инвентаря (без привязки к слоту)
            // Создаем контекст без целевого слота
            var context = new DragContext(previewStack, null, null);
            context.TargetInventory = this;
            var entry = context.Entries[0];

            if (_ruleValidator != null)
            {
                var inventoryResult = _ruleValidator.ValidateDrop(context, entry);
                if (!inventoryResult.IsValid)
                {
                    Extentions.DragAndDropLog($"<color=red>[{name}] CanAcceptItem: Inventory rules rejected: {inventoryResult.FailureReason}</color>");
                    return false;
                }
            }

            EnsureStrategyInitialized();

            bool canCreateNewSlot = _slotManagement == SlotManagementType.Dynamic && _slots.Count < _maxDynamicSlots;
            int potentialNewSlots = Mathf.Max(0, _maxDynamicSlots - _slots.Count);
            bool canAccept = _strategy.CanAcceptItem(_slots, item, count, canCreateNewSlot, potentialNewSlots, _slotPrefab, out suggestedSlot);

            if (canAccept)
                Extentions.DragAndDropLog($"<color=green>[{name}] CanAcceptItem: success via strategy</color>");
            else
                Extentions.DragAndDropLog($"<color=red>[{name}] CanAcceptItem: No suitable slots and cannot create new</color>");

            return canAccept;
        }

        /// <summary>
        /// Получить количество предметов данного типа, которое инвентарь может принять.
        /// Учитывает стратегию инвентаря (Unique, Stackable, SeparableStacks),
        /// свободные слоты и правила валидации.
        /// </summary>
        public int GetAcceptableCount(IInventoryItem item, int desiredCount)
        {
            if (item == null || desiredCount <= 0)
                return 0;

            // Проверяем правила инвентаря (без привязки к слоту)
            var previewStack = new ItemStack(item, 1);
            var context = new DragContext(previewStack, null, null);
            context.TargetInventory = this;
            var entry = context.Entries[0];

            if (_ruleValidator != null)
            {
                var inventoryResult = _ruleValidator.ValidateDrop(context, entry);
                if (!inventoryResult.IsValid)
                {
                    Extentions.DragAndDropLog($"<color=red>[{name}] GetAcceptableCount: Inventory rules rejected</color>");
                    return 0;
                }
            }

            EnsureStrategyInitialized();

            bool canCreateNewSlot = _slotManagement == SlotManagementType.Dynamic && _slots.Count < _maxDynamicSlots;
            int potentialNewSlots = Mathf.Max(0, _maxDynamicSlots - _slots.Count);
            int result = _strategy.GetAcceptableCount(_slots, item, desiredCount, canCreateNewSlot, potentialNewSlots, _slotPrefab);
            Extentions.DragAndDropLog($"<color=cyan>[{name}] GetAcceptableCount: item={item.DisplayName}, desired={desiredCount}, acceptable={result}</color>");
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
                Extentions.DragAndDropLog($"<color=green>[{name}] EnsureFreeSlots: Created slot {_slots.Count}, free slots now: {freeSlots + i + 1}/{_maxFreeSlots}</color>");
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

            Extentions.DragAndDropLog($"<color=magenta>[{name}] Removed slot {index}. New count: {_slots.Count}</color>");
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
                Extentions.DragAndDropLog("<color=red>[TrySwapSlots] Slot is null</color>");
                return false;
            }

            if (targetSlot.IsEmpty || sourceSlot.IsEmpty)
            {
                Extentions.DragAndDropLog("<color=red>[TrySwapSlots] One of the slots is empty</color>");
                return false;
            }

            // Проверяем что targetSlot принадлежит этому инвентарю
            if (!ReferenceEquals(targetSlot.Inventory, this))
            {
                Extentions.DragAndDropLog("<color=red>[TrySwapSlots] Target slot doesn't belong to this inventory</color>");
                return false;
            }

            // Сохраняем копии стаков для событий
            var targetStackBackup = new ItemStack(targetSlot.Stack.Item, targetSlot.Stack.Count);
            var sourceStackBackup = new ItemStack(sourceSlot.Stack.Item, sourceSlot.Stack.Count);

            try
            {
                // Выполняем обмен
                var tempStack = targetSlot.Stack;
                targetSlot.SetStack(sourceSlot.Stack);
                sourceSlot.SetStack(tempStack);

                targetSlot.UpdateVisuals();
                sourceSlot.UpdateVisuals();

                Extentions.DragAndDropLog($"<color=green>[{name}] Swap completed: slot {targetSlot.Index} ↔ slot {sourceSlot.Index}</color>");
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
