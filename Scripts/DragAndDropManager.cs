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
        public bool StartDrag(ISlot sourceSlot) => sourceSlot != null && StartDrag(new List<ISlot> { sourceSlot });

        /// <summary>
        /// Начать перетаскивание из одного или нескольких слотов
        /// </summary>
        public bool StartDrag(IReadOnlyList<ISlot> sourceSlots)
        {
            if (IsDragging || sourceSlots == null || sourceSlots.Count == 0)
                return false;

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

            // Per-entry validation
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
            Extentions.DragAndDropLog($"<color=green>Started dragging ({entries.Count} entries)</color>");
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

            if (slot == null && inventory == null)
            {
                Extentions.DragAndDropLog("<color=red>SetHoveredSlot: slot and inventory are both null!</color>");
                return;
            }

            if (inventory == null && slot != null)
            {
                inventory = slot.Inventory;
            }

            if (inventory == null)
            {
                Extentions.DragAndDropLog("<color=red>SetHoveredSlot: Inventory not found!</color>");
                return;
            }

            _hoveredInventory = inventory;
            var handler = new InventoryDropHandler(
                slot,
                inventory,
                _globalRules,
                _transferService,
                policyOverride: null,
                swapAttempting: RaiseSwapAttempting,
                swapCompleted: RaiseSwapCompleted);
            SetHoveredSlotWithHandler(slot, handler);
        }

        /// <summary>
        /// Clear the hovered slot
        /// </summary>
        public void ClearHoveredSlot(ISlot slot)
        {
            if (_hoveredSlot != slot)
                return;

            if (slot is UniversalSlot universalSlot)
            {
                universalSlot.Highlight(false);
            }

            OnDragExitSlot?.Invoke(_currentContext);

            _hoveredSlot = null;
            _hoveredInventory = null;
            _currentHandler = null;
            _currentContext?.ClearTarget();
        }

        /// <summary>
        /// Set hovered slot using the drop handler for validation.
        /// </summary>
        private void SetHoveredSlotWithHandler(ISlot slot, IItemDropHandler handler)
        {
            if (!IsDragging)
                return;

            if (_hoveredSlot == slot && _currentHandler == handler)
                return;

            if (_hoveredSlot is UniversalSlot previousSlot)
            {
                previousSlot.Highlight(false);
            }

            _hoveredSlot = slot;
            _currentHandler = handler;

            if (handler == null)
            {
                Extentions.DragAndDropLog("<color=red>SetHoveredSlotWithHandler: Handler is null!</color>");
                return;
            }

            bool canDrop = handler.CanAcceptDrop(_currentContext);
            if (canDrop && slot is UniversalSlot universalSlot)
            {
                universalSlot.Highlight(true);
            }

            OnDragEnterSlot?.Invoke(_currentContext);
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
        /// Complete the drag operation
        /// </summary>
        public void CompleteDrag()
        {
            if (!IsDragging)
                return;

            bool success = false;
            DropResult result = default;
            IItemDropHandler handlerToUse = _currentHandler;

            // Fallback: for legacy hover path, wrap inventory target into handler so
            // both paths use the same planner/executor pipeline.
            if (handlerToUse == null && _hoveredInventory != null)
            {
                handlerToUse = new InventoryDropHandler(
                    _hoveredSlot,
                    _hoveredInventory,
                    _globalRules,
                    _transferService,
                    policyOverride: null,
                    swapAttempting: RaiseSwapAttempting,
                    swapCompleted: RaiseSwapCompleted);
            }

            if (handlerToUse != null)
            {
                OnDropAttempting?.Invoke(_currentContext);

                bool canDrop = handlerToUse.CanAcceptDrop(_currentContext);

                if (canDrop)
                {
                    result = handlerToUse.HandleDrop(_currentContext);
                    success = result.Success;

                    if (success)
                    {
                        // Update context with result info for events
                        if (result.TargetSlot != null && result.TargetInventory != null)
                        {
                            _currentContext.SetTarget(result.TargetSlot, result.TargetInventory);
                        }

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

            if (!success)
            {
                OnDragCancelled?.Invoke(_currentContext);
            }

            EndDrag();
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

        public bool RaiseSwapAttempting(InventorySwapContext context)
        {
            OnSwapAttempting?.Invoke(context);
            return context != null && !context.Cancel;
        }

        public void RaiseSwapCompleted(InventorySwapContext context)
        {
            OnSwapCompleted?.Invoke(context);
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
