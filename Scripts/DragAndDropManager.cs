using System;
using System.Collections.Generic;
using CodeUtils;
using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Rules;
using DragAndDropSystem.Selection;
using DragAndDropSystem.Slots;
using DragAndDropSystem.UI;
using UnityEngine;
using Extentions = DragAndDropSystem.Tools.Extentions;

namespace DragAndDropSystem
{
    /// <summary>
    /// Новое поколение менеджера drag-and-drop
    /// Работает через композицию, правила и стратегии
    /// </summary>
    [DisallowMultipleComponent]
    public class DragAndDropManager : MonoSingleton<DragAndDropManager>
    {
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
        private IDropTarget _activeDropTarget;
        private IDropProcessor _currentProcessor;
        private GlobalRuleValidator _globalRules = new GlobalRuleValidator();

        // Стек целей drop операций (для корректной обработки вложенных областей и слотов)
        private List<IDropTarget> _dropTargetStack = new List<IDropTarget>();

        private readonly InventoryTransferService _transferService = new InventoryTransferService();
        private readonly AutoTransferService _autoTransferService = new AutoTransferService();

        public bool IsDragging => _currentContext != null;
        public DragContext CurrentContext => _currentContext;
        public bool HasActiveDropTarget => _currentProcessor != null || _dropTargetStack.Count > 0;
        public bool HasActiveSlotDropTarget => _activeDropTarget?.GetTargetSlot() != null;

        // Exposed for IDropProcessor implementations
        public GlobalRuleValidator GlobalRules => _globalRules;
        public InventoryTransferService TransferService => _transferService;

        // Quick click auto-transfer properties
        public bool IsQuickClickAutoTransferEnabled => _enableQuickClickAutoTransfer;
        public float QuickClickTimeThreshold => _quickClickTimeThreshold;
        public float QuickClickDistanceThreshold => _quickClickDistanceThreshold;

        // События drag-and-drop
        public static event Action<DragContext> OnDragStarting;
        public static event Action<DragContext> OnDragStarted;
        public static event Action<DragContext> OnDragEnterSlot;
        public static event Action<DragContext> OnDragExitSlot;
        public static event Action<DragContext> OnDropAttempting;
        public static event Action<DragContext> OnDropCompleted;
        public static event Action<DragContext> OnDragCancelled;
        public static event Action OnDragEnded;

        // События автопереноса
        public static event Action<DragContext> OnAutoTransferAttempting;
        public static event Action<DragContext> OnAutoTransferCompleted;
        public static event Action<DragContext> OnAutoTransferFailed;

        // События обмена предметов (swap)
        public static event Action<InventorySwapContext> OnSwapAttempting;
        public static event Action<InventorySwapContext> OnSwapCompleted;

        protected override void Init()
        {
            base.Init();

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

            OnDragStarted?.Invoke(_currentContext);
            Extentions.DragAndDropLog($"<color=green>Started dragging ({entries.Count} entries)</color>");
            return true;
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

                Extentions.DragAndDropLog($"<color=green>ActivateTopTarget: slot={slot?.Index.ToString() ?? "AREA"}, processor={processor?.GetType().Name}</color>");
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

                Extentions.DragAndDropLog("<color=yellow>ActivateTopTarget: Stack empty, cleared active target</color>");
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
            IDropProcessor processorToUse = _currentProcessor;

            if (processorToUse != null)
            {
                OnDropAttempting?.Invoke(_currentContext);

                bool canDrop = processorToUse.CanAcceptDrop(_currentContext);

                if (canDrop)
                {
                    result = processorToUse.ProcessDrop(_currentContext);
                    success = result.Success;

                    if (success)
                    {
                        // Update context with result info for events
                        if (result.TargetSlot != null && result.TargetInventory != null)
                        {
                            _currentContext.SetTarget(result.TargetSlot, result.TargetInventory);
                        }

                        if (_currentContext.IsBatchDrag && SelectionManager.IsInstanceExist)
                            SelectionManager.Instance.Clear();

                        OnDropCompleted?.Invoke(_currentContext);
                    }
                    else
                    {
                        Extentions.DragAndDropLog($"<color=red>CompleteDrag: Handler.ProcessDrop failed: {result.FailureReason}</color>");
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

            if (_activeDropTarget != null)
            {
                OnDragExitSlot?.Invoke(_currentContext);
            }

            _currentContext = null;
            _activeDropTarget = null;
            _currentProcessor = null;
            OnDragEnded?.Invoke();
        }

        /// <summary>
        /// Выполнить автоперенос предмета из слота в целевой инвентарь
        /// </summary>
        public bool TryAutoTransfer(ISlot sourceSlot, IInventory sourceInventory, IInventory targetInventory)
        {
            if (sourceSlot == null)
                return false;

            return TryAutoTransfer(
                new[] { sourceSlot },
                sourceInventory,
                targetInventory);
        }

        /// <summary>
        /// Выполнить автоперенос одного или нескольких слотов в целевой инвентарь через общий transfer pipeline.
        /// </summary>
        public bool TryAutoTransfer(IReadOnlyList<ISlot> sourceSlots, IInventory sourceInventory, IInventory targetInventory)
        {
            if (sourceSlots == null || sourceSlots.Count == 0 || sourceInventory == null || targetInventory == null)
            {
                Extentions.DragAndDropLog("<color=red>TryAutoTransfer(batch): Invalid parameters</color>");
                return false;
            }

            if (IsDragging && _currentContext != null && _currentContext.Entries.Count > 0)
            {
                for (int i = 0; i < sourceSlots.Count; i++)
                {
                    if (sourceSlots[i] != null && sourceSlots[i] == _currentContext.Entries[0].SourceSlot)
                    {
                        Extentions.DragAndDropLog("<color=red>TryAutoTransfer(batch): Source slot is currently dragged manually</color>");
                        return false;
                    }
                }
            }

            if (!_autoTransferService.TryCreateContext(sourceSlots, sourceInventory, targetInventory, out var context, out var createFailure))
            {
                Extentions.DragAndDropLog($"<color=red>TryAutoTransfer(batch): {createFailure}</color>");
                return false;
            }

            OnAutoTransferAttempting?.Invoke(context);

            var dropResult = _autoTransferService.Execute(
                context,
                targetInventory,
                _globalRules,
                _transferService,
                RaiseSwapAttempting,
                RaiseSwapCompleted,
                out var executionSummary);

            if (!dropResult.Success)
            {
                Extentions.DragAndDropLog($"<color=red>AutoTransfer failed: {dropResult.FailureReason}</color>");
                OnAutoTransferFailed?.Invoke(context);
                return false;
            }

            NotifyAutoTransferSourceSlots(context);

            var transferredItem = dropResult.Item;
            int transferredAmount = dropResult.Amount;
            var finalTargetSlot = dropResult.TargetSlot;
            var executedEntries = executionSummary?.ExecutedEntries;
            bool canAnimate = _autoTransferAnimation != null
                              && executedEntries != null
                              && executedEntries.Count > 0;

            string itemName = transferredItem?.DisplayName ?? "Unknown";
            string targetName = targetInventory?.GetType().Name ?? "Unknown";
            Extentions.DragAndDropLog($"<color=green>AutoTransfer success: {transferredAmount}x {itemName} → {targetName} (slot {finalTargetSlot?.Index.ToString() ?? "-"})</color>");

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
                    if (entry.SourceSlot == null || entry.TargetSlot == null || entry.Item == null || entry.Amount <= 0)
                        continue;

                    if (entry.TargetSlot is UniversalSlot targetUniversalSlot)
                        targetUniversalSlot.SetIconVisibility(false);

                    var visualStack = new ItemStack(entry.Item, entry.Amount);
                    var presenter = DragVisualPresenter.Instance;
                    var visualPrefab = presenter.ResolveVisualPrefab(entry.SourceSlot.Inventory);

                    pendingAnimations++;

                    GameObject animationVisual = _autoTransferAnimation.AnimateTransfer(
                        visualStack,
                        entry.SourceSlot,
                        entry.TargetSlot,
                        visualPrefab,
                        presenter.VisualContainer,
                        presenter.PresentationCanvas,
                        () =>
                        {
                            if (entry.TargetSlot is UniversalSlot slotForVisual)
                                slotForVisual.SetIconVisibility(true);
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

            return true;
        }

        private static void NotifyAutoTransferSourceSlots(DragContext context)
        {
            if (context?.Entries == null)
                return;

            for (int i = 0; i < context.Entries.Count; i++)
            {
                var entry = context.Entries[i];
                if (entry.SourceInventory is UniversalInventory sourceUniversal &&
                    entry.SourceSlot != null &&
                    entry.SourceSlot.IsEmpty)
                {
                    sourceUniversal.HandleSlotEmptied(entry.SourceSlot);
                }
            }
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

        public bool RaiseSwapAttempting(InventorySwapContext context)
        {
            OnSwapAttempting?.Invoke(context);
            return context != null && !context.Cancel;
        }

        public void RaiseSwapCompleted(InventorySwapContext context)
        {
            OnSwapCompleted?.Invoke(context);
        }
    }
}
