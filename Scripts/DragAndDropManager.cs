using System;
using System.Collections.Generic;
using CodeUtils;
using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Rules;
using DragAndDropSystem.Slots;
using DragAndDropSystem.UI;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Extentions = DragAndDropSystem.Tools.Extentions;

namespace DragAndDropSystem
{
    /// <summary>
    /// Новое поколение менеджера drag-and-drop
    /// Работает через композицию, правила и стратегии
    /// </summary>

#if ENABLE_REFLEX_DI
    public class DragAndDropManager : MonoBehaviour
#else
    public class DragAndDropManager : MonoSingleton<DragAndDropManager>
#endif
    {
        [Header("Visual")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private DefaultDragVisual _defaultDragVisualPrefab;
        [SerializeField] private Transform _visualContainer;

        [Header("Settings")]
        [SerializeField, Tooltip("Автоматически менять предметы местами если целевой слот занят")]
        private bool _autoSwapOnOccupiedSlot = false;

        [Header("Quick Click Auto-Transfer (LMB)")]
        [SerializeField] private bool _enableQuickClickAutoTransfer = true;
        [SerializeField, Range(0.05f, 1f), Tooltip("Максимальная длительность клика для автопереноса (секунды)")]
        private float _quickClickTimeThreshold = 0.2f;
        [SerializeField, Range(1f, 50f), Tooltip("Максимальное смещение мыши для автопереноса (пиксели)")]
        private float _quickClickDistanceThreshold = 5f;

        [Header("Auto-Transfer Animation")]
        [SerializeReference, Tooltip("Стратегия анимации автопереноса. Null = мгновенный перенос")]
        private AutoTransferAnimationStrategy _autoTransferAnimation;

        // Список активных анимационных визуалов для поддержки множественных анимаций
        private List<GameObject> _activeAnimationVisuals = new List<GameObject>();

        private DragContext _currentContext;
        private ISlot _hoveredSlot;
        private IInventory _hoveredInventory;
        private IItemDropHandler _currentHandler;
        private GlobalRuleValidator _globalRules = new GlobalRuleValidator();
        private IDragVisual _currentVisual;

        // Стек целей drop операций (для корректной обработки вложенных областей и слотов)
        private List<IDropTarget> _dropTargetStack = new List<IDropTarget>();

        // Кеш визуалов: префаб → созданный экземпляр
        private Dictionary<MonoBehaviour, IDragVisual> _visualCache = new Dictionary<MonoBehaviour, IDragVisual>();
        private IDragVisual _defaultVisualInstance;
        private readonly InventoryTransferService _transferService = new InventoryTransferService();

        public bool IsDragging => _currentContext != null;
        public DragContext CurrentContext => _currentContext;

        // Exposed for IItemDropHandler implementations
        public GlobalRuleValidator GlobalRules => _globalRules;
        public InventoryTransferService TransferService => _transferService;

        // Quick click auto-transfer properties
        public bool IsQuickClickAutoTransferEnabled => _enableQuickClickAutoTransfer;
        public float QuickClickTimeThreshold => _quickClickTimeThreshold;
        public float QuickClickDistanceThreshold => _quickClickDistanceThreshold;

        // События drag-and-drop
        public event Action<DragContext> OnDragStarting;
        public event Action<DragContext> OnDragStarted;
        public event Action<DragContext> OnDragEnterSlot;
        public event Action<DragContext> OnDragExitSlot;
        public event Action<DragContext> OnDropAttempting;
        public event Action<DragContext> OnDropCompleted;
        public event Action<DragContext> OnDragCancelled;

        // События автопереноса
        public event Action<DragContext> OnAutoTransferAttempting;
        public event Action<DragContext> OnAutoTransferCompleted;
        public event Action<DragContext> OnAutoTransferFailed;

        // События обмена предметов (swap)
        public event Action<InventorySwapContext> OnSwapAttempting;
        public event Action<InventorySwapContext> OnSwapCompleted;

        [Inject]
        private void Initialize()
        {
            _canvas.worldCamera = Camera.main;

            // Добавляем базовые правила
            _globalRules.AddRule(new SameSlotRule());
        }

        protected override void DeInit()
        {
            base.DeInit();
            // Уничтожаем все активные визуалы анимаций
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
        /// Добавить глобальное правило для всех операций drag-and-drop
        /// </summary>
        public void AddGlobalRule(IGlobalRule rule)
        {
            _globalRules.AddRule(rule);
        }

        /// <summary>
        /// Удалить глобальное правило
        /// </summary>
        public void RemoveGlobalRule(IGlobalRule rule)
        {
            _globalRules.RemoveRule(rule);
        }

        /// <summary>
        /// Начать перетаскивание из слота
        /// </summary>
        public bool StartDrag(ISlot sourceSlot)
        {
            if (IsDragging || sourceSlot == null || sourceSlot.IsEmpty) return false;

            if (sourceSlot.Inventory == null)
            {
                Debug.LogError("StartDrag: sourceInventory is null! Slot not initialized?");
                return false;
            }

            // Определяем количество предметов для перетаскивания
            int dragCount = sourceSlot.GetDragAmount();

            Extentions.DragAndDropLog($"<color=cyan>StartDrag: Taking {dragCount} of {sourceSlot.Stack.Count} items</color>");

            // Создаем контекст с независимой копией стака (чтобы он не изменился после переноса)
            var stack = new ItemStack(sourceSlot.Stack.Item, dragCount);
            _currentContext = new DragContext(stack, sourceSlot, sourceSlot.Inventory);

            OnDragStarting?.Invoke(_currentContext);
            
            var entry = _currentContext.Entries[0];

            // Проверяем правила начала перетаскивания
            // Проверяем глобальные правила
            var globalResult = _globalRules.ValidateStartDrag(_currentContext, entry);
            if (!globalResult.IsValid)
            {
                Extentions.DragAndDropLog($"Cannot start drag: {globalResult.FailureReason}");
                _currentContext = null;
                return false;
            }

            // Проверяем правила инвентаря
            if (sourceSlot.Inventory is UniversalInventory universalInventory)
            {
                var inventoryResult = universalInventory.RuleValidator.ValidateStartDrag(_currentContext, entry);
                if (!inventoryResult.IsValid)
                {
                    Extentions.DragAndDropLog($"Cannot start drag: {inventoryResult.FailureReason}");
                    _currentContext = null;
                    return false;
                }
            }

            // Выбираем визуал (кастомный из инвентаря или дефолтный)
            _currentVisual = GetDragVisual(sourceSlot.Inventory);

            if (_currentVisual != null)
            {
                _currentVisual.UpdatePosition(GetMousePosition());
                _currentVisual.Show(_currentContext.Entries);
            }
            else
            {
                Debug.LogWarning("No drag visual available!");
            }

            OnDragStarted?.Invoke(_currentContext);

            Extentions.DragAndDropLog($"<color=green>Started dragging</color>");
            return true;
        }

        /// <summary>
        /// Начать batch-перетаскивание из нескольких слотов
        /// </summary>
        public bool StartDrag(IReadOnlyList<ISlot> sourceSlots)
        {
            if (IsDragging || sourceSlots == null || sourceSlots.Count == 0)
                return false;

            // Single slot — delegate to standard path
            if (sourceSlots.Count == 1)
                return StartDrag(sourceSlots[0]);

            // Build entries
            var entries = new List<DragEntry>(sourceSlots.Count);
            foreach (var slot in sourceSlots)
            {
                if (slot == null || slot.IsEmpty || slot.Inventory == null)
                    continue;

                int dragCount = slot.Inventory.GetDragAmount(slot);
                var stack = new ItemStack(slot.Stack.Item, dragCount);
                entries.Add(new DragEntry(stack, slot, slot.Inventory));
            }

            if (entries.Count == 0)
                return false;

            _currentContext = new DragContext(entries);

            // Event: starting
            OnDragStarting?.Invoke(_currentContext);

            // Validate each entry against rules
            foreach (var entry in _currentContext.Entries)
            {
                var globalResult = _globalRules.ValidateStartDrag(_currentContext, entry);
                if (!globalResult.IsValid)
                {
                    Extentions.DragAndDropLog($"Cannot start batch drag: {globalResult.FailureReason}");
                    _currentContext = null;
                    return false;
                }

                if (entry.SourceInventory is UniversalInventory universalInventory)
                {
                    var inventoryResult = universalInventory.RuleValidator.ValidateStartDrag(_currentContext, entry);
                    if (!inventoryResult.IsValid)
                    {
                        Extentions.DragAndDropLog($"Cannot start batch drag: {inventoryResult.FailureReason}");
                        _currentContext = null;
                        return false;
                    }
                }
            }

            // Visual: use first entry's inventory for visual
            _currentVisual = GetDragVisual(entries[0].SourceInventory);
            if (_currentVisual != null)
            {
                _currentVisual.UpdatePosition(GetMousePosition());
                _currentVisual.Show(_currentContext.Entries);
            }

            OnDragStarted?.Invoke(_currentContext);
            Extentions.DragAndDropLog($"<color=green>Started batch dragging ({entries.Count} entries)</color>");
            return true;
        }

        /// <summary>
        /// Получить визуал для перетаскивания (кастомный или дефолтный)
        /// Использует кеш для переиспользования визуалов
        /// </summary>
        private IDragVisual GetDragVisual(IInventory inventory)
        {
            // Проверяем, есть ли кастомный визуал у инвентаря
            if (inventory is UniversalInventory universalInventory)
            {
                var customPrefab = universalInventory.GetCustomDragVisualPrefab();
                if (customPrefab != null)
                {
                    // Проверяем кеш
                    if (_visualCache.TryGetValue(customPrefab, out var cachedVisual))
                    {
                        Extentions.DragAndDropLog($"<color=cyan>Using cached custom visual from {inventory.GetType().Name}</color>");
                        return cachedVisual;
                    }

                    // Создаем новый экземпляр из префаба
                    var visualInstance = InstantiateVisual(customPrefab);
                    if (visualInstance != null)
                    {
                        _visualCache[customPrefab] = visualInstance;
                        Extentions.DragAndDropLog($"<color=cyan>Created new custom visual from {inventory.GetType().Name}</color>");
                        return visualInstance;
                    }
                }
            }

            // Используем дефолтный (создаем при первом использовании)
            if (_defaultVisualInstance == null && _defaultDragVisualPrefab != null)
            {
                _defaultVisualInstance = InstantiateVisual(_defaultDragVisualPrefab);
                Extentions.DragAndDropLog("<color=cyan>Created default drag visual</color>");
            }

            Extentions.DragAndDropLog("<color=cyan>Using default drag visual</color>");
            return _defaultVisualInstance;
        }

        /// <summary>
        /// Получить префаб визуала для анимации (кастомный от инвентаря или дефолтный)
        /// </summary>
        private MonoBehaviour GetDragVisualPrefab(IInventory inventory)
        {
            // Проверяем, есть ли кастомный визуал у инвентаря
            if (inventory is UniversalInventory universalInventory)
            {
                var customPrefab = universalInventory.GetCustomDragVisualPrefab();
                if (customPrefab != null)
                {
                    return customPrefab;
                }
            }

            // Возвращаем дефолтный
            return _defaultDragVisualPrefab;
        }

        /// <summary>
        /// Создать экземпляр визуала из префаба
        /// </summary>
        private IDragVisual InstantiateVisual(MonoBehaviour prefab)
        {
            if (prefab == null)
                return null;

            var container = _visualContainer != null ? _visualContainer : _canvas.transform;
            var instance = Instantiate(prefab, container);

            if (instance is IDragVisual dragVisual)
            {
                return dragVisual;
            }

            Debug.LogError($"Prefab {prefab.name} does not implement IDragVisual!");
            Destroy(instance.gameObject);
            return null;
        }

        /// <summary>
        /// Установить слот, над которым находится курсор
        /// slot может быть null - это значит дроп в область инвентаря (не в конкретный слот)
        /// </summary>
        public void SetHoveredSlot(ISlot slot, IInventory inventory = null)
        {
            if (!IsDragging)
                return;

            // Если уже наведен на этот слот, ничего не делаем
            if (_hoveredSlot == slot && _hoveredInventory == inventory)
                return;

            // Очищаем предыдущий слот
            if (_hoveredSlot != null && _hoveredSlot is UniversalSlot previousSlot)
            {
                previousSlot.Highlight(false);
            }

            _hoveredSlot = slot;

            // Для дропа в область (slot == null) инвентарь ОБЯЗАТЕЛЕН
            if (slot == null && inventory == null)
            {
                Extentions.DragAndDropLog("<color=red>SetHoveredSlot: slot and inventory are both null!</color>");
                return;
            }

            // Автоматически находим инвентарь если не указан
            if (inventory == null && slot != null)
            {
                inventory = slot.Inventory;
            }

            _hoveredInventory = inventory;

            Extentions.DragAndDropLog($"<color=cyan>SetHoveredSlot: slot={slot?.Index.ToString() ?? "AREA"}, inventory={inventory?.GetType().Name}</color>");

            if (_hoveredInventory != null)
            {
                _currentContext.SetTarget(slot, _hoveredInventory);

                // Проверяем, можно ли сбросить
                if (slot != null)
                {
                    // Дроп в конкретный слот - проверяем правила слота
                    if (CanDropToSlot())
                    {
                        if (slot is UniversalSlot universalSlot)
                        {
                            universalSlot.Highlight(true);
                        }
                    }
                }
                else
                {
                    // Дроп в область - проверяем только правила инвентаря
                    // (CanAcceptItem уже проверил в InventoryDropArea)
                    Extentions.DragAndDropLog("<color=cyan>SetHoveredSlot: Drop to area (no specific slot)</color>");
                }

                OnDragEnterSlot?.Invoke(_currentContext);
            }
            else
            {
                Extentions.DragAndDropLog("<color=red>SetHoveredSlot: Inventory not found!</color>");
            }
        }

        /// <summary>
        /// Clear the hovered slot
        /// </summary>
        public void ClearHoveredSlot(ISlot slot)
        {
            if (_hoveredSlot == slot)
            {
                if (slot is UniversalSlot universalSlot)
                {
                    universalSlot.Highlight(false);
                }

                OnDragExitSlot?.Invoke(_currentContext);

                _hoveredSlot = null;
                _hoveredInventory = null;
                _currentHandler = null;
                _currentContext.ClearTarget();
            }
        }

        /// <summary>
        /// Set hovered slot using the drop handler for validation
        /// </summary>
        private void SetHoveredSlotWithHandler(ISlot slot, IItemDropHandler handler)
        {
            if (!IsDragging)
                return;

            // If already hovering this slot, do nothing
            if (_hoveredSlot == slot && _currentHandler == handler)
                return;

            // Clear previous slot highlight
            if (_hoveredSlot != null && _hoveredSlot is UniversalSlot previousSlot)
            {
                previousSlot.Highlight(false);
            }

            _hoveredSlot = slot;
            _currentHandler = handler;

            // For handler-based drops, we don't require inventory
            // The handler encapsulates everything it needs

            Extentions.DragAndDropLog($"<color=cyan>SetHoveredSlotWithHandler: slot={slot?.Index.ToString() ?? "AREA"}, handler={handler?.GetType().Name}</color>");

            if (handler != null)
            {
                // Validate via handler
                bool canDrop = handler.CanAcceptDrop(_currentContext);

                if (canDrop && slot is UniversalSlot universalSlot)
                {
                    universalSlot.Highlight(true);
                }

                OnDragEnterSlot?.Invoke(_currentContext);
            }
            else
            {
                Extentions.DragAndDropLog("<color=red>SetHoveredSlotWithHandler: Handler is null!</color>");
            }
        }

        /// <summary>
        /// Добавить цель в стек (вызывается при OnPointerEnter)
        /// Автоматически активирует новый верхний target
        /// </summary>
        public void PushDropTarget(IDropTarget target)
        {
            if (!IsDragging || target == null)
                return;

            // Защита от дублей
            if (_dropTargetStack.Contains(target))
            {
                Extentions.DragAndDropLog($"<color=yellow>PushDropTarget: Target already in stack, ignoring</color>");
                return;
            }

            // Деактивируем текущий top
            if (_dropTargetStack.Count > 0)
            {
                var currentTop = _dropTargetStack[_dropTargetStack.Count - 1];
                currentTop.OnBecomeInactiveTarget();
            }

            // Добавляем новый target
            _dropTargetStack.Add(target);

            Extentions.DragAndDropLog($"<color=cyan>PushDropTarget: Added to stack (size={_dropTargetStack.Count})</color>");

            // Активируем новый top
            ActivateTopTarget();
        }

        /// <summary>
        /// Удалить цель из стека (вызывается при OnPointerExit или OnDisable)
        /// Если удаляется top, автоматически активирует предыдущий target
        /// </summary>
        public void PopDropTarget(IDropTarget target)
        {
            if (target == null)
                return;

            int index = _dropTargetStack.IndexOf(target);
            if (index == -1)
            {
                Extentions.DragAndDropLog($"<color=yellow>PopDropTarget: Target not in stack, ignoring</color>");
                return;
            }

            bool wasTop = (index == _dropTargetStack.Count - 1);

            // Деактивируем если это был top
            if (wasTop)
            {
                target.OnBecomeInactiveTarget();
            }

            // Удаляем из стека
            _dropTargetStack.RemoveAt(index);

            Extentions.DragAndDropLog($"<color=cyan>PopDropTarget: Removed from stack (size={_dropTargetStack.Count})</color>");

            // Если убрали top - активируем новый top (или очищаем)
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
                var handler = top.GetDropHandler();

                // Store the handler
                _currentHandler = handler;

                // Update hovered slot and validate via handler
                SetHoveredSlotWithHandler(slot, handler);

                // Activate visual target
                top.OnBecomeActiveTarget();

                Extentions.DragAndDropLog($"<color=green>ActivateTopTarget: slot={slot?.Index.ToString() ?? "AREA"}, handler={handler?.GetType().Name}</color>");
            }
            else
            {
                // Stack empty - clear hovered
                if (_hoveredSlot != null)
                {
                    ClearHoveredSlot(_hoveredSlot);
                }
                _hoveredSlot = null;
                _hoveredInventory = null;
                _currentHandler = null;
                _currentContext?.ClearTarget();

                Extentions.DragAndDropLog("<color=yellow>ActivateTopTarget: Stack empty, cleared hovered</color>");
            }
        }

        /// <summary>
        /// Проверить, можно ли сбросить в текущий наведенный слот
        /// Проверяет все entries контекста
        /// </summary>
        private bool CanDropToSlot()
        {
            if (!IsDragging || !_currentContext.HasTarget)
            {
                Extentions.DragAndDropLog("<color=red>CanDropToSlot: No dragging or target</color>");
                return false;
            }

            foreach (var entry in _currentContext.Entries)
            {
                // Глобальные правила
                var globalResult = _globalRules.ValidateDrop(_currentContext, entry);
                if (!globalResult.IsValid)
                {
                    Extentions.DragAndDropLog($"<color=red>CanDropToSlot: Global rule failed: {globalResult.FailureReason}</color>");
                    return false;
                }

                // Правила целевого инвентаря
                if (_currentContext.TargetInventory is UniversalInventory targetInventory)
                {
                    var inventoryResult = targetInventory.RuleValidator.ValidateDrop(_currentContext, entry);
                    if (!inventoryResult.IsValid)
                    {
                        Extentions.DragAndDropLog($"<color=red>CanDropToSlot: Inventory rule failed: {inventoryResult.FailureReason}</color>");
                        return false;
                    }
                }

                // Правила конкретного слота
                if (_currentContext.TargetSlot?.SlotRuleValidator != null)
                {
                    var slotResult = _currentContext.TargetSlot.SlotRuleValidator.ValidateDrop(_currentContext, entry);
                    if (!slotResult.IsValid)
                    {
                        Extentions.DragAndDropLog($"<color=red>CanDropToSlot: Slot rule failed: {slotResult.FailureReason}</color>");
                        return false;
                    }
                }
            }

            Extentions.DragAndDropLog("<color=green>CanDropToSlot: Success!</color>");
            return true;
        }

        /// <summary>
        /// Complete the drag operation
        /// </summary>
        public void CompleteDrag()
        {
            if (!IsDragging)
                return;

            bool success = false;

            // Check if we have a handler (handler-based drops don't require inventory)
            if (_currentHandler != null)
            {
                OnDropAttempting?.Invoke(_currentContext);

                // Validate via handler
                bool canDrop = _currentHandler.CanAcceptDrop(_currentContext);

                if (canDrop)
                {
                    // Execute drop via handler
                    var result = _currentHandler.HandleDrop(_currentContext);
                    success = result.Success;

                    if (success)
                    {
                        // Update context with result info for events
                        if (result.TargetSlot != null && result.TargetInventory != null)
                        {
                            _currentContext.SetTarget(result.TargetSlot, result.TargetInventory);
                        }

                        // Dispatch events based on result
                        DispatchDropResultEvents(result);

                        OnDropCompleted?.Invoke(_currentContext);
                    }
                    else
                    {
                        Extentions.DragAndDropLog($"<color=red>CompleteDrag: Handler.HandleDrop failed: {result.FailureReason}</color>");
                    }
                }
                else
                {
                    Extentions.DragAndDropLog("<color=red>CompleteDrag: Handler.CanAcceptDrop returned false</color>");
                }
            }
            // Fallback: try old inventory-based path (for backward compatibility during transition)
            else if (_hoveredInventory != null)
            {
                _currentContext.SetTarget(_hoveredSlot, _hoveredInventory);

                OnDropAttempting?.Invoke(_currentContext);

                bool canDrop = CanDropToSlot();

                if (canDrop)
                {
                    success = PerformTransfer();

                    if (success)
                    {
                        OnDropCompleted?.Invoke(_currentContext);
                    }
                }
            }

            if (!success)
            {
                OnDragCancelled?.Invoke(_currentContext);
            }

            EndDrag();
        }

        /// <summary>
        /// Dispatch events based on drop result (for inventory-based handlers)
        /// </summary>
        private void DispatchDropResultEvents(DropResult result)
        {
            if (!result.Success || result.Item == null)
                return;

            // For batch: events are dispatched per-entry by the handler
            // For single: dispatch source/target events
            var entry = _currentContext.Entries[0];

            // Source inventory events
            if (entry.SourceInventory is UniversalInventory sourceUniversal)
            {
                sourceUniversal.EmitItemRemoved(
                    result.Item,
                    result.Amount,
                    entry.SourceSlot?.Index ?? -1,
                    result.TargetInventory,
                    entry.SourceSlot,
                    result.TargetSlot);

                sourceUniversal.HandleSlotEmptied(entry.SourceSlot);
            }

            // Target inventory events (only for inventory-based drops)
            if (result.TargetInventory is UniversalInventory targetUniversal && result.TargetSlot != null)
            {
                targetUniversal.EmitItemAdded(
                    result.Item,
                    result.Amount,
                    result.TargetSlot.Index,
                    entry.SourceInventory,
                    entry.SourceSlot,
                    result.TargetSlot);
            }
        }

        /// <summary>
        /// Отменить перетаскивание
        /// </summary>
        public void CancelDrag()
        {
            if (!IsDragging)
                return;

            OnDragCancelled?.Invoke(_currentContext);
            EndDrag();
        }

        private bool PerformTransfer()
        {
            // Batch drag: iterate all entries
            if (_currentContext.IsBatchDrag)
            {
                return PerformBatchTransfer();
            }

            var entry = _currentContext.Entries[0];
            var source = entry.SourceInventory;
            var target = _currentContext.TargetInventory;
            var sourceSlot = entry.SourceSlot;
            var targetSlot = _currentContext.TargetSlot;
            var draggedStack = entry.Stack;

            if (source == null || target == null || sourceSlot == null || draggedStack == null)
            {
                Extentions.DragAndDropLog("<color=red>PerformTransfer: Invalid context</color>");
                return false;
            }

            Extentions.DragAndDropLog($"<color=yellow>PerformTransfer: {draggedStack.Count}x {draggedStack.Item.DisplayName} | TargetSlot={targetSlot?.Index.ToString() ?? "AREA"} | SameInventory={entry.SourceInventory == _currentContext.TargetInventory}</color>");

            // Попытка swap до начала транзакции, чтобы не терять состояние источника
            if (_autoSwapOnOccupiedSlot && targetSlot != null && !targetSlot.IsEmpty)
            {
                Extentions.DragAndDropLog("<color=cyan>PerformTransfer: Target slot occupied, evaluating swap...</color>");
                if (TrySwap())
                {
                    Extentions.DragAndDropLog("<color=green>PerformTransfer: Swap succeeded!</color>");
                    return true;
                }
            }

            var request = new InventoryTransferRequest(
                source,
                sourceSlot,
                target,
                targetSlot,
                draggedStack,
                true);

            if (!_transferService.TryExecuteTransfer(request, out var outcome))
            {
                Extentions.DragAndDropLog("<color=red>Transfer failed</color>");
                return false;
            }

            if (outcome.TargetSlot != null)
            {
                _currentContext.SetTarget(outcome.TargetSlot, outcome.TargetInventory);
            }

            DispatchTransferEvents(outcome);

            if (outcome.SourceInventory is UniversalInventory universalSource)
            {
                universalSource.HandleSlotEmptied(outcome.SourceSlot);
            }

            Extentions.DragAndDropLog($"<color=green>Transferred {outcome.Amount} items successfully</color>");
            return true;
        }

        private bool PerformBatchTransfer()
        {
            var target = _currentContext.TargetInventory;
            if (target == null)
            {
                Extentions.DragAndDropLog("<color=red>PerformBatchTransfer: No target inventory</color>");
                return false;
            }

            bool anySuccess = false;

            foreach (var entry in _currentContext.Entries)
            {
                if (entry.SourceInventory == null || entry.SourceSlot == null || entry.Stack == null)
                    continue;

                var request = new InventoryTransferRequest(
                    entry.SourceInventory,
                    entry.SourceSlot,
                    target,
                    null, // batch: let inventory find slots
                    entry.Stack,
                    true);

                if (_transferService.TryExecuteTransfer(request, out var outcome))
                {
                    DispatchTransferEvents(outcome);

                    if (outcome.SourceInventory is UniversalInventory universalSource)
                    {
                        universalSource.HandleSlotEmptied(outcome.SourceSlot);
                    }

                    anySuccess = true;
                }
            }

            return anySuccess;
        }

        /// <summary>
        /// Валидация возможности обмена предметов между слотами
        /// Только для одиночного drag (не batch)
        /// </summary>
        private bool ValidateSwap(DragContext dragContext, ISlot targetSlot, out DragContext reverseContext)
        {
            reverseContext = null;

            if (targetSlot == null || targetSlot.IsEmpty)
            {
                Extentions.DragAndDropLog("<color=red>ValidateSwap: Target slot is null or empty</color>");
                return false;
            }

            var entry = dragContext.Entries[0];
            var sourceSlot = entry.SourceSlot;
            var sourceInventory = entry.SourceInventory;
            var targetInventory = dragContext.TargetInventory;

            // Создаем копию стака из целевого слота для валидации
            var targetStack = new ItemStack(targetSlot.Stack.Item, targetSlot.Stack.Count);

            // 1. Проверяем можно ли вытащить предмет из целевого слота
            reverseContext = new DragContext(targetStack, targetSlot, targetInventory);
            var reverseEntry = reverseContext.Entries[0];

            // Проверяем глобальные правила для вытаскивания из целевого слота
            var globalStartResult = _globalRules.ValidateStartDrag(reverseContext, reverseEntry);
            if (!globalStartResult.IsValid)
            {
                Extentions.DragAndDropLog($"<color=red>ValidateSwap: Cannot start drag from target slot: {globalStartResult.FailureReason}</color>");
                return false;
            }

            // Проверяем правила целевого инвентаря для вытаскивания
            if (targetInventory is UniversalInventory targetUniversal)
            {
                var targetStartResult = targetUniversal.RuleValidator.ValidateStartDrag(reverseContext, reverseEntry);
                if (!targetStartResult.IsValid)
                {
                    Extentions.DragAndDropLog($"<color=red>ValidateSwap: Target inventory rejects start drag: {targetStartResult.FailureReason}</color>");
                    return false;
                }
            }

            // 2. Теперь проверяем можно ли поместить предметы в новые места
            // 2a. Предмет из целевого слота -> исходный слот
            reverseContext.SetTarget(sourceSlot, sourceInventory);

            // Глобальные правила
            var globalDropReverseResult = _globalRules.ValidateDrop(reverseContext, reverseEntry);
            if (!globalDropReverseResult.IsValid)
            {
                Extentions.DragAndDropLog($"<color=red>ValidateSwap: Cannot drop target item to source slot (global): {globalDropReverseResult.FailureReason}</color>");
                return false;
            }

            // Правила исходного инвентаря
            if (sourceInventory is UniversalInventory sourceUniversal)
            {
                var sourceDropResult = sourceUniversal.RuleValidator.ValidateDrop(reverseContext, reverseEntry);
                if (!sourceDropResult.IsValid)
                {
                    Extentions.DragAndDropLog($"<color=red>ValidateSwap: Source inventory rejects target item: {sourceDropResult.FailureReason}</color>");
                    return false;
                }
            }

            // Правила исходного слота
            if (sourceSlot.SlotRuleValidator != null)
            {
                var sourceSlotResult = sourceSlot.SlotRuleValidator.ValidateDrop(reverseContext, reverseEntry);
                if (!sourceSlotResult.IsValid)
                {
                    Extentions.DragAndDropLog($"<color=red>ValidateSwap: Source slot rejects target item: {sourceSlotResult.FailureReason}</color>");
                    return false;
                }
            }

            // 2b. Предмет из исходного слота -> целевой слот (уже проверено в CanDropToSlot, но проверим еще раз)
            var globalDropResult = _globalRules.ValidateDrop(dragContext, entry);
            if (!globalDropResult.IsValid)
            {
                Extentions.DragAndDropLog($"<color=red>ValidateSwap: Cannot drop source item to target slot (global): {globalDropResult.FailureReason}</color>");
                return false;
            }

            if (targetInventory is UniversalInventory targetUniversal2)
            {
                var targetDropResult = targetUniversal2.RuleValidator.ValidateDrop(dragContext, entry);
                if (!targetDropResult.IsValid)
                {
                    Extentions.DragAndDropLog($"<color=red>ValidateSwap: Target inventory rejects source item: {targetDropResult.FailureReason}</color>");
                    return false;
                }
            }

            if (targetSlot.SlotRuleValidator != null)
            {
                var targetSlotResult = targetSlot.SlotRuleValidator.ValidateDrop(dragContext, entry);
                if (!targetSlotResult.IsValid)
                {
                    Extentions.DragAndDropLog($"<color=red>ValidateSwap: Target slot rejects source item: {targetSlotResult.FailureReason}</color>");
                    return false;
                }
            }

            Extentions.DragAndDropLog("<color=green>ValidateSwap: Swap is valid!</color>");
            return true;
        }

        /// <summary>
        /// Выполнить обмен предметов между слотами
        /// Атомарная операция: либо оба предмета обмениваются, либо ничего не происходит
        /// Только для одиночного drag (не batch)
        /// </summary>
        private bool TrySwap()
        {
            // Swap only for single entry
            if (!IsDragging || _currentContext.IsBatchDrag || !_currentContext.HasTarget || _currentContext.TargetSlot == null)
            {
                Extentions.DragAndDropLog("<color=red>TrySwap: Invalid state for swap</color>");
                return false;
            }

            var entry = _currentContext.Entries[0];
            var sourceSlot = entry.SourceSlot;
            var targetSlot = _currentContext.TargetSlot;
            var sourceInventory = entry.SourceInventory;
            var targetInventory = _currentContext.TargetInventory;

            if (targetSlot.IsEmpty)
            {
                Extentions.DragAndDropLog("<color=red>TrySwap: Target slot is empty, no need to swap</color>");
                return false;
            }

            // Валидация swap
            if (!ValidateSwap(_currentContext, targetSlot, out DragContext reverseContext))
            {
                Extentions.DragAndDropLog("<color=red>TrySwap: Validation failed</color>");
                return false;
            }

            // Создаем событие для возможности отмены или кастомной обработки
            var swapEventArgs = new InventorySwapContext(
                entry.Stack,
                reverseContext.Entries[0].Stack,
                sourceSlot,
                targetSlot,
                sourceInventory,
                targetInventory
            );

            OnSwapAttempting?.Invoke(swapEventArgs);

            if (swapEventArgs.Cancel)
            {
                Extentions.DragAndDropLog("<color=yellow>TrySwap: Cancelled by event handler</color>");
                return false;
            }

            // Выполняем swap через метод целевого инвентаря и получаем данные для событий
            if (targetInventory is not UniversalInventory targetUniversal)
            {
                Extentions.DragAndDropLog("<color=red>TrySwap: Target inventory is not UniversalInventory</color>");
                return false;
            }

            bool success = targetUniversal.TrySwapSlots(targetSlot, sourceSlot, out var swapResult);

            if (!success)
            {
                Extentions.DragAndDropLog("<color=red>TrySwap: Inventory swap method failed</color>");
                return false;
            }

            var sourceUniversal = sourceSlot.Inventory as UniversalInventory;
            DispatchSwapEvents(targetUniversal, sourceUniversal, targetSlot, sourceSlot, swapResult);

            Extentions.DragAndDropLog($"<color=green>Swap completed via inventory method</color>");
            OnSwapCompleted?.Invoke(swapEventArgs);
            return true;
        }

        private void EndDrag()
        {
            // Деактивируем все targets в стеке
            foreach (var target in _dropTargetStack)
            {
                target.OnBecomeInactiveTarget();
            }
            _dropTargetStack.Clear();

            if (_hoveredSlot is UniversalSlot hoveredSlot)
            {
                hoveredSlot.Highlight(false);
            }

            if (_currentVisual != null)
            {
                _currentVisual.Hide();
                _currentVisual = null;
            }

            _currentContext = null;
            _hoveredSlot = null;
            _hoveredInventory = null;
            _currentHandler = null;
        }

        private void Update()
        {
            if (IsDragging)
            {
                // Обновляем позицию визуала
                if (_currentVisual != null)
                {
                    _currentVisual.UpdatePosition(GetMousePosition());
                }

                // Проверяем отпускание кнопки мыши
                if (Input.GetMouseButtonUp(0))
                {
                    CompleteDrag();
                }
                // ESC для отмены
                else if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CancelDrag();
                }
            }
        }

        /// <summary>
        /// Выполнить автоперенос предмета из слота в целевой инвентарь
        /// </summary>
        public bool TryAutoTransfer(ISlot sourceSlot, IInventory sourceInventory, IInventory targetInventory)
        {
            if (sourceSlot == null || sourceSlot.IsEmpty || sourceInventory == null || targetInventory == null)
            {
                Extentions.DragAndDropLog("<color=red>TryAutoTransfer: Invalid parameters (null check)</color>");
                return false;
            }
            if (IsDragging && _currentContext.Entries[0].SourceSlot == sourceSlot)
            {
                Extentions.DragAndDropLog($"<color=red>Cannot auto-transfer: slot {sourceSlot.Index} is currently being dragged manually</color>");
                return false;
            }
            if (sourceSlot.Stack == null || sourceSlot.Stack.Item == null)
            {
                Extentions.DragAndDropLog("<color=red>TryAutoTransfer: Source slot has no valid item</color>");
                return false;
            }

            // Определяем количество для переноса
            int transferAmount = sourceInventory.GetDragAmount(sourceSlot);
            var transferStack = new ItemStack(sourceSlot.Stack.Item, transferAmount);

            // Создаем контекст автопереноса
            var context = new DragContext(
                new ItemStack(sourceSlot.Stack.Item, transferAmount),
                sourceSlot,
                sourceInventory
            );
            context.TargetInventory = targetInventory;

            var entry = context.Entries[0];

            // Генерируем событие попытки автопереноса
            OnAutoTransferAttempting?.Invoke(context);

            // Проверяем глобальные правила
            var globalResult = _globalRules.ValidateStartDrag(context, entry);
            if (!globalResult.IsValid)
            {
                Extentions.DragAndDropLog($"<color=red>AutoTransfer failed: {globalResult.FailureReason}</color>");
                OnAutoTransferFailed?.Invoke(context);
                return false;
            }

            // Проверяем правила исходного инвентаря
            if (sourceInventory is UniversalInventory srcUniversal)
            {
                var srcResult = srcUniversal.RuleValidator.ValidateStartDrag(context, entry);
                if (!srcResult.IsValid)
                {
                    Extentions.DragAndDropLog($"<color=red>AutoTransfer failed (source rules): {srcResult.FailureReason}</color>");
                    OnAutoTransferFailed?.Invoke(context);
                    return false;
                }
            }

            // Проверяем правила целевого инвентаря
            if (targetInventory is UniversalInventory tgtUniversal)
            {
                var tgtResult = tgtUniversal.RuleValidator.ValidateDrop(context, entry);
                if (!tgtResult.IsValid)
                {
                    Extentions.DragAndDropLog($"<color=red>AutoTransfer failed (target rules): {tgtResult.FailureReason}</color>");
                    OnAutoTransferFailed?.Invoke(context);
                    return false;
                }
            }

            // Сохраняем ссылку на предмет для логирования (до модификации стака)
            var transferredItem = transferStack.Item;
            string itemName = transferredItem?.DisplayName ?? "Unknown";
            string targetName = targetInventory?.GetType().Name ?? "Unknown";

            // Ищем подходящий слот с учетом правил слотов
            ISlot targetSlot = FindValidAutoTransferSlot(targetInventory, transferStack, sourceSlot);

            if (targetSlot == null)
            {
                Extentions.DragAndDropLog("<color=red>AutoTransfer failed: No valid slot found in target inventory</color>");
                OnAutoTransferFailed?.Invoke(context);
                return false;
            }
            context.TargetSlot = targetSlot;

            var transferRequest = new InventoryTransferRequest(
                sourceInventory,
                sourceSlot,
                targetInventory,
                context.TargetSlot,
                entry.Stack,
                true);

            if (!_transferService.TryExecuteTransfer(transferRequest, out var outcome))
            {
                Extentions.DragAndDropLog("<color=red>AutoTransfer failed: Transfer pipeline rejected</color>");
                OnAutoTransferFailed?.Invoke(context);
                return false;
            }

            if (outcome.TargetSlot != null)
            {
                context.TargetSlot = outcome.TargetSlot;
            }

            DispatchTransferEvents(outcome);

            if (outcome.SourceInventory is UniversalInventory srcUniversalInventory)
            {
                srcUniversalInventory.HandleSlotEmptied(outcome.SourceSlot);
            }

            var finalTargetSlot = outcome.TargetSlot ?? targetSlot;
            bool targetWasEmpty = outcome.TargetWasEmptyBefore;
            int transferred = outcome.Amount;

            Extentions.DragAndDropLog($"<color=green>AutoTransfer success: {transferred}x {itemName} → {targetName} (slot {finalTargetSlot?.Index.ToString() ?? "-"})</color>");

            if (_autoTransferAnimation != null && finalTargetSlot != null)
            {
                if (targetWasEmpty && finalTargetSlot is UniversalSlot universalTargetSlot)
                {
                    universalTargetSlot.SetIconVisibility(false);
                }

                var visualStack = new ItemStack(transferredItem, transferred);
                var visualPrefab = GetDragVisualPrefab(sourceInventory);

                GameObject animationVisual = _autoTransferAnimation.AnimateTransfer(
                    visualStack,
                    sourceSlot,
                    finalTargetSlot,
                    visualPrefab,
                    _visualContainer != null ? _visualContainer : _canvas.transform,
                    _canvas,
                    () =>
                    {
                        if (targetWasEmpty && finalTargetSlot is UniversalSlot slotForVisual)
                        {
                            slotForVisual.SetIconVisibility(true);
                        }

                        OnDropCompleted?.Invoke(context);
                        OnAutoTransferCompleted?.Invoke(context);
                    });

                if (animationVisual != null)
                {
                    _activeAnimationVisuals.Add(animationVisual);
                    StartCoroutine(RemoveAnimationVisualWhenDestroyed(animationVisual));
                }
            }
            else
            {
                OnDropCompleted?.Invoke(context);
                OnAutoTransferCompleted?.Invoke(context);
            }

            return true;
        }

        /// <summary>
        /// Найти валидный слот для автопереноса с учетом правил слотов
        /// </summary>
        private ISlot FindValidAutoTransferSlot(IInventory targetInventory, ItemStack transferStack, ISlot sourceSlot)
        {
            var slots = targetInventory.Slots;
            if (slots == null || slots.Count == 0)
            {
                Extentions.DragAndDropLog("<color=red>FindValidAutoTransferSlot: Target inventory has no slots</color>");
                return null;
            }

            // Создаем временный DragContext для проверки правил слотов
            var tempContext = new DragContext(transferStack, sourceSlot, sourceSlot.Inventory);
            var tempEntry = tempContext.Entries[0];

            // Проверяем, поддерживает ли целевой инвентарь стакание предметов
            bool shouldTryStacking = true;
            if (targetInventory is UniversalInventory universalTargetInventory)
            {
                // Если инвентарь в режиме Unique - пропускаем попытку стакания
                if (universalTargetInventory.ItemBehavior == UniversalInventory.ItemBehaviorType.Unique)
                {
                    shouldTryStacking = false;
                    Extentions.DragAndDropLog($"<color=cyan>Target inventory [{universalTargetInventory.name}] is in Unique mode - skipping stacking attempt</color>");
                }
            }

            // Сначала ищем слот с таким же предметом (только если инвентарь поддерживает стакание)
            if (shouldTryStacking)
            {
                foreach (var slot in slots)
                {
                    if (slot == null || slot.IsEmpty)
                        continue;

                    // Проверяем, можно ли стакнуть
                    if (slot.Stack.CanStack(transferStack.Item))
                    {
                        // Устанавливаем целевой слот в контекст
                        tempContext.SetTarget(slot, targetInventory);

                        // Проверяем правила слота
                        if (slot.SlotRuleValidator != null)
                        {
                            var slotResult = slot.SlotRuleValidator.ValidateDrop(tempContext, tempEntry);
                            if (!slotResult.IsValid)
                            {
                                Extentions.DragAndDropLog($"<color=yellow>Slot {slot.Index} with same item rejected by slot rules: {slotResult.FailureReason}</color>");
                                continue;
                            }
                        }

                        Extentions.DragAndDropLog($"<color=cyan>Found valid slot {slot.Index} with same item for stacking</color>");
                        return slot;
                    }
                }
            }

            // Теперь ищем пустой слот
            foreach (var slot in slots)
            {
                if (slot == null || !slot.IsEmpty)
                    continue;

                // Устанавливаем целевой слот в контекст
                tempContext.SetTarget(slot, targetInventory);

                // Проверяем правила слота
                if (slot.SlotRuleValidator != null)
                {
                    var slotResult = slot.SlotRuleValidator.ValidateDrop(tempContext, tempEntry);
                    if (!slotResult.IsValid)
                    {
                        Extentions.DragAndDropLog($"<color=yellow>Empty slot {slot.Index} rejected by slot rules: {slotResult.FailureReason}</color>");
                        continue;
                    }
                }

                Extentions.DragAndDropLog($"<color=cyan>Found valid empty slot {slot.Index}</color>");
                return slot;
            }

            Extentions.DragAndDropLog("<color=red>No valid slot found (all slots are full or rejected by rules)</color>");
            return null;
        }

        /// <summary>
        /// Корутина для автоматического удаления визуала из списка после уничтожения
        /// </summary>
        private System.Collections.IEnumerator RemoveAnimationVisualWhenDestroyed(GameObject visual)
        {
            // Ждем пока визуал существует
            while (visual != null)
            {
                yield return null;
            }

            // Визуал уничтожен - удаляем из списка
            _activeAnimationVisuals.Remove(visual);
        }

        private void DispatchTransferEvents(InventoryTransferResult outcome)
        {
            if (outcome.SourceInventory is UniversalInventory sourceUniversal && outcome.Item != null)
            {
                sourceUniversal.EmitItemRemoved(
                    outcome.Item,
                    outcome.Amount,
                    outcome.SourceSlot?.Index ?? -1,
                    outcome.TargetInventory,
                    outcome.SourceSlot,
                    outcome.TargetSlot);
            }

            if (outcome.TargetInventory is UniversalInventory targetUniversal && outcome.TargetSlot != null && outcome.Item != null)
            {
                targetUniversal.EmitItemAdded(
                    outcome.Item,
                    outcome.Amount,
                    outcome.TargetSlot.Index,
                    outcome.SourceInventory,
                    outcome.SourceSlot,
                    outcome.TargetSlot);
            }
        }

        private void DispatchSwapEvents(
            UniversalInventory targetInventory,
            UniversalInventory sourceInventory,
            ISlot targetSlot,
            ISlot sourceSlot,
            SwapOperationResult swapResult)
        {
            if (targetInventory != null && swapResult.TargetStackBefore != null && !swapResult.TargetStackBefore.IsEmpty)
            {
                targetInventory.EmitItemRemoved(
                    swapResult.TargetStackBefore.Item,
                    swapResult.TargetStackBefore.Count,
                    targetSlot.Index,
                    sourceInventory,
                    targetSlot,
                    sourceSlot);

                targetInventory.EmitItemAdded(
                    swapResult.SourceStackBefore.Item,
                    swapResult.SourceStackBefore.Count,
                    targetSlot.Index,
                    sourceInventory,
                    sourceSlot,
                    targetSlot);
            }

            if (sourceInventory != null && swapResult.SourceStackBefore != null && !swapResult.SourceStackBefore.IsEmpty)
            {
                sourceInventory.EmitItemRemoved(
                    swapResult.SourceStackBefore.Item,
                    swapResult.SourceStackBefore.Count,
                    sourceSlot.Index,
                    targetInventory,
                    sourceSlot,
                    targetSlot);

                sourceInventory.EmitItemAdded(
                    swapResult.TargetStackBefore.Item,
                    swapResult.TargetStackBefore.Count,
                    sourceSlot.Index,
                    targetInventory,
                    targetSlot,
                    sourceSlot);
            }
        }

        Vector3 GetMousePosition()
        {
            // Проверяем тип Canvas
            if (_canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                // Для Screen Space - Overlay просто используем позицию мыши
                return Input.mousePosition;
            }
            else if (_canvas.renderMode == RenderMode.ScreenSpaceCamera || _canvas.renderMode == RenderMode.WorldSpace)
            {
                // Для Screen Space - Camera или World Space используем RectTransformUtility
                RectTransform canvasRect = _canvas.GetComponent<RectTransform>();
                Vector2 localPoint;

                Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceCamera ? _canvas.worldCamera : Camera.main;

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvasRect,
                        Input.mousePosition,
                        cam,
                        out localPoint))
                {
                    return canvasRect.TransformPoint(localPoint);
                }
            }

            return Input.mousePosition;
        }
    }
}
