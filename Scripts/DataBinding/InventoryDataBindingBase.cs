using System;
using DragAndDropSystem.Core;
using DragAndDropSystem.Inspector;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Rules;
using DragAndDropSystem.Tools;
using UnityEngine;

namespace DragAndDropSystem.DataBinding
{
    /// <summary>
    /// Базовый класс для связи UniversalInventory (UI) с внешними данными (например, GameManager)
    /// Использует паттерн Adapter для двусторонней синхронизации:
    /// - UI изменения → внешние данные (через OnItemAddedToUI / OnItemRemovedFromUI)
    /// - Внешние данные → UI (через SyncToUI)
    /// </summary>
    public abstract class InventoryDataBindingBase : MonoBehaviour
    {
        [Header("UI Reference")]
        [SerializeField, Tooltip("UI представление инвентаря")]
        protected UniversalInventory _inventory;

        [FoldoutGroup("Rules", expanded: true)]
        [SerializeField, HideLabel]
        [Tooltip("Правила для проверки возможности переноса предметов в этот инвентарь")]
        private InventoryRuleValidator _ruleValidator = new InventoryRuleValidator();

        private int _syncDepth = 0;
        private DataBindingInventoryRule _internalRule;

        /// <summary>
        /// Находимся ли мы в режиме синхронизации (Data → UI).
        /// Когда true, события OnItemAddedToUI/OnItemRemovedFromUI не вызываются.
        /// </summary>
        protected bool IsSyncing => _syncDepth > 0;

        /// <summary>
        /// Начать scope синхронизации. Используйте с using:
        /// using (BeginSync()) { ... }
        /// Exception-safe и поддерживает вложенность.
        /// </summary>
        protected SyncScope BeginSync() => new SyncScope(this);

        protected readonly struct SyncScope : IDisposable
        {
            private readonly InventoryDataBindingBase _owner;

            public SyncScope(InventoryDataBindingBase owner)
            {
                _owner = owner;
                _owner._syncDepth++;
            }

            public void Dispose()
            {
                _owner._syncDepth--;
            }
        }

        private void Awake()
        {
            // Создаем внутреннее правило, которое будет делегировать вызовы виртуальным методам
            _internalRule = new DataBindingInventoryRule(this);

            // Инициализируем инвентарь
            _inventory.Initialize(this);

            // Добавляем правила DataBinding в инвентарь
            IntegrateRulesWithInventory();
        }

        private void OnValidate()
        {
            // Сортируем правила при изменении в Inspector
            _ruleValidator?.OnValidate();
        }

        /// <summary>
        /// Интегрирует правила DataBinding с правилами UniversalInventory
        /// </summary>
        private void IntegrateRulesWithInventory()
        {
            if (_inventory == null) return;

            // Добавляем внутреннее правило в инвентарь
            if (_internalRule != null)
            {
                _inventory.RuleValidator.AddRule(_internalRule);
                Extentions.DragAndDropLog($"[{GetType().Name}] Added internal rule to inventory");
            }

            // Копируем все правила из DataBinding в инвентарь
            var rules = _ruleValidator.GetRules();
            foreach (var rule in rules)
            {
                if (rule != null)
                {
                    _inventory.RuleValidator.AddRule(rule);
                    Extentions.DragAndDropLog($"[{GetType().Name}] Added rule '{rule.RuleName}' to inventory");
                }
            }
        }

        protected virtual void OnEnable()
        {
            if (_inventory != null)
            {
                _inventory.OnItemAdded += OnInventoryItemAdded;
                _inventory.OnItemRemoved += OnInventoryItemRemoved;
                _inventory.OnSwapAttempting += HandleSwapAttempting;
                _inventory.OnSwapCompleted += HandleSwapCompleted;
            }

            ReloadUI();
        }

        protected virtual void OnDisable()
        {
            if (_inventory != null)
            {
                _inventory.OnItemAdded -= OnInventoryItemAdded;
                _inventory.OnItemRemoved -= OnInventoryItemRemoved;
                _inventory.OnSwapAttempting -= HandleSwapAttempting;
                _inventory.OnSwapCompleted -= HandleSwapCompleted;
            }
        }

        /// <summary>
        /// Обработчик события попытки swap (inventory-scoped — фильтрация не нужна)
        /// </summary>
        private void HandleSwapAttempting(InventorySwapContext context)
        {
            if (context.Cancel)
                return;

            var result = CanSwap(context);
            if (!result.IsValid)
            {
                Extentions.DragAndDropLog($"[{GetType().Name}] CanSwap rejected: {result.FailureReason}");
                context.Cancel = true;
            }
        }

        /// <summary>
        /// Обработчик события успешного swap (inventory-scoped — фильтрация не нужна)
        /// </summary>
        private void HandleSwapCompleted(InventorySwapContext context)
        {
            OnSwapCompleted(context);
        }

        #region Inventory Events

        /// <summary>
        /// Обработчик события добавления предмета в инвентарь
        /// </summary>
        private void OnInventoryItemAdded(InventoryItemEventContext context)
        {
            if (IsSyncing) return;

            Extentions.DragAndDropLog($"[{GetType().Name}] Item added: {context.Item.DisplayName} x{context.Count} (from: {context.SourceInventory?.GetType().Name ?? "null"})");
            OnItemAddedToUI(context);
        }

        /// <summary>
        /// Обработчик события удаления предмета из инвентаря
        /// </summary>
        private void OnInventoryItemRemoved(InventoryItemEventContext context)
        {
            if (IsSyncing) return;

            Extentions.DragAndDropLog($"[{GetType().Name}] Item removed: {context.Item.DisplayName} x{context.Count} (to: {context.TargetInventory?.GetType().Name ?? "null"})");

            OnItemRemovedFromUI(context);
        }

        #endregion

        #region Abstract Methods

        /// <summary>
        /// Вызывается когда в UI добавили предмет
        /// Здесь нужно обновить внешние данные (добавить в список GameManager)
        /// </summary>
        /// <param name="context">Аргументы события с информацией о предмете, количестве и контексте переноса (SourceInventory, TargetInventory)</param>
        protected abstract void OnItemAddedToUI(InventoryItemEventContext context);

        /// <summary>
        /// Вызывается когда из UI убрали предмет
        /// Здесь нужно обновить внешние данные (убрать из списка GameManager)
        /// </summary>
        /// <param name="context">Аргументы события с информацией о предмете, количестве и контексте переноса (SourceInventory, TargetInventory)</param>
        protected abstract void OnItemRemovedFromUI(InventoryItemEventContext context);

        /// <summary>
        /// Синхронизировать UI с внешними данными.
        /// Очищает UI и вызывает OnReloadUI() внутри sync scope.
        /// </summary>
        public void ReloadUI()
        {
            if (!Application.isPlaying || _inventory == null) return;

            using (BeginSync())
            {
                _inventory.ClearAll();
                OnReloadUI();
            }
        }

        /// <summary>
        /// Заполнить UI данными из внешнего источника.
        /// Вызывается внутри sync scope — события подавлены, UI уже очищен.
        /// Используйте AddToUIQuiet() для добавления предметов.
        /// </summary>
        protected abstract void OnReloadUI();

        #endregion

        #region Helper Methods

        /// <summary>
        /// Очистить UI инвентарь
        /// </summary>
        protected void ClearUI()
        {
            if (_inventory != null)
            {
                using (BeginSync())
                    _inventory.ClearAll();
            }
        }

        /// <summary>
        /// Добавить предмет в UI без триггера событий
        /// </summary>
        protected void AddToUIQuiet(IInventoryItem item, int count, int targetSlotIndex = -1)
        {
            if (_inventory == null) return;

            using (BeginSync())
                _inventory.TryAddItem(item, count, targetSlotIndex);
        }

        /// <summary>
        /// Удалить предмет из UI без триггера событий
        /// </summary>
        protected void RemoveFromUIQuiet(IInventoryItem item, int count, int sourceSlotIndex = -1)
        {
            if (_inventory == null) return;

            using (BeginSync())
                _inventory.TryRemoveItem(item, count, sourceSlotIndex);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Принудительно синхронизировать UI (можно вызвать из внешнего кода)
        /// </summary>
        [Button("Force Sync To UI")]
        public void ForceSyncToUI()
        {
            ReloadUI();
        }

        /// <summary>
        /// Получить валидатор правил этого DataBinding
        /// </summary>
        public InventoryRuleValidator RuleValidator => _ruleValidator;

        #endregion

        #region Virtual Methods for Transfer Validation

        /// <summary>
        /// Проверить, можно ли начать перетаскивание из этого инвентаря
        /// Переопределите этот метод для добавления кастомной логики проверки
        /// </summary>
        /// <param name="context">Контекст перетаскивания</param>
        /// <returns>Результат валидации</returns>
        protected virtual RuleResult CanStartDrag(DragContext context, DragEntry entry)
        {
            // По умолчанию разрешаем
            return RuleResult.Success();
        }

        /// <summary>
        /// Проверить, можно ли бросить предмет в этот инвентарь
        /// Переопределите этот метод для добавления кастомной логики проверки
        /// </summary>
        /// <param name="context">Контекст перетаскивания</param>
        /// <param name="entry">Конкретный элемент перетаскивания</param>
        /// <returns>Результат валидации</returns>
        protected virtual RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            // По умолчанию разрешаем
            return RuleResult.Success();
        }

        /// <summary>
        /// Проверить, можно ли выполнить swap (обмен предметов)
        /// Переопределите этот метод для добавления кастомной логики проверки swap
        ///
        /// ПРИМЕЧАНИЕ: Базовая валидация (CanStartDrag и CanDrop для обоих направлений)
        /// уже выполнена в DragAndDropManager.ValidateSwap()
        /// Этот метод предназначен для дополнительной логики специфичной для вашего DataBinding
        /// </summary>
        /// <param name="args">Аргументы события swap с информацией об обоих стаках и слотах</param>
        /// <returns>Результат валидации. Если вернуть Failure - swap будет отменен</returns>
        protected virtual RuleResult CanSwap(InventorySwapContext args)
        {
            // По умолчанию разрешаем (базовая валидация уже выполнена)
            return RuleResult.Success();
        }

        /// <summary>
        /// Вызывается после успешного завершения swap
        /// Переопределите для добавления кастомной логики обработки обмена
        ///
        /// Например:
        /// - Обновление внешних данных (если swap произошел между разными DataBinding)
        /// - Логирование обмена
        /// - Специальная обработка экипировки (если swap с инвентаря на слот экипировки)
        /// </summary>
        /// <param name="args">Аргументы события swap с информацией об обоих стаках и слотах</param>
        protected virtual void OnSwapCompleted(InventorySwapContext args)
        {
            // По умолчанию ничего не делаем
            // События OnItemAdded/OnItemRemoved уже сгенерированы для обоих инвентарей
        }

        #endregion

        #region Internal Rule

        /// <summary>
        /// Внутреннее правило, которое делегирует вызовы виртуальным методам DataBinding
        /// Это позволяет дочерним классам переопределять логику проверки переноса
        /// </summary>
        private class DataBindingInventoryRule : IInventoryRule
        {
            private readonly InventoryDataBindingBase _owner;

            public DataBindingInventoryRule(InventoryDataBindingBase owner)
            {
                _owner = owner;
            }

            public int Priority => 50; // Средний приоритет

            public string RuleName => $"[{Priority}] DataBinding ({_owner.GetType().Name})";

            public RuleResult CanStartDrag(DragContext context, DragEntry entry)
            {
                if (_owner == null)
                    return RuleResult.Success();

                return _owner.CanStartDrag(context, entry);
            }

            public RuleResult CanDrop(DragContext context, DragEntry entry)
            {
                if (_owner == null)
                    return RuleResult.Success();

                return _owner.CanDrop(context, entry);
            }
        }

        #endregion
    }
}
