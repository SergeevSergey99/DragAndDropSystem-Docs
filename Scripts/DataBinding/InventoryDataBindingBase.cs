using System;
using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Rules;
using DragAndDropSystem.Tools;
using Sirenix.OdinInspector;
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

        protected bool _isSyncing = false; // Флаг для предотвращения циклической синхронизации
        private DataBindingInventoryRule _internalRule;

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
            }

            // Подписываемся на события swap от DragAndDropManager
            SubscribeToSwapEvents();

            ReloadUI();
        }

        protected virtual void OnDisable()
        {
            if (_inventory != null)
            {
                _inventory.OnItemAdded -= OnInventoryItemAdded;
                _inventory.OnItemRemoved -= OnInventoryItemRemoved;
            }

            // Отписываемся от событий swap
            UnsubscribeFromSwapEvents();
        }

        /// <summary>
        /// Подписаться на события swap от DragAndDropManager
        /// </summary>
        private void SubscribeToSwapEvents()
        {
            var manager = DragAndDropManager.Instance;
            if (manager != null)
            {
                manager.OnSwapAttempting += HandleSwapAttempting;
                manager.OnSwapCompleted += HandleSwapCompleted;
            }
        }

        /// <summary>
        /// Отписаться от событий swap
        /// </summary>
        private void UnsubscribeFromSwapEvents()
        {
            // Проверяем существование инстанса через статическое свойство
            if (DragAndDropManager.IsInstanceExist)
            {
                var manager = DragAndDropManager.Instance;
                if (manager != null)
                {
                    manager.OnSwapAttempting -= HandleSwapAttempting;
                    manager.OnSwapCompleted -= HandleSwapCompleted;
                }
            }
        }

        /// <summary>
        /// Обработчик события попытки swap
        /// Вызывает CanSwapInternal если swap затрагивает этот инвентарь
        /// </summary>
        private void HandleSwapAttempting(object sender, InventorySwapEventArgs args)
        {
            if (args.Cancel)
                return;

            // Проверяем участвует ли наш инвентарь в swap
            bool isSourceInventory = ReferenceEquals(args.SourceInventory, _inventory);
            bool isTargetInventory = ReferenceEquals(args.TargetInventory, _inventory);

            if (!isSourceInventory && !isTargetInventory)
                return;

            // Вызываем кастомную валидацию
            var result = CanSwapInternal(args);
            if (!result.IsValid)
            {
                Extentions.DragAndDropLog($"[{GetType().Name}] CanSwapInternal rejected: {result.FailureReason}");
                args.Cancel = true;
            }
        }

        /// <summary>
        /// Обработчик события успешного swap
        /// Вызывает OnSwapCompleted если swap затрагивает этот инвентарь
        /// </summary>
        private void HandleSwapCompleted(object sender, InventorySwapEventArgs args)
        {
            // Проверяем участвует ли наш инвентарь в swap
            bool isSourceInventory = ReferenceEquals(args.SourceInventory, _inventory);
            bool isTargetInventory = ReferenceEquals(args.TargetInventory, _inventory);

            if (!isSourceInventory && !isTargetInventory)
                return;

            // Вызываем кастомную обработку
            OnSwapCompleted(args);
        }

        #region Inventory Events

        /// <summary>
        /// Обработчик события добавления предмета в инвентарь
        /// </summary>
        private void OnInventoryItemAdded(object sender, InventoryItemEventArgs args)
        {
            if (_isSyncing) return;

            Extentions.DragAndDropLog($"[{GetType().Name}] Item added: {args.Item.DisplayName} x{args.Count} (from: {args.SourceInventory?.GetType().Name ?? "null"})");
            OnItemAddedToUI(args);
        }

        /// <summary>
        /// Обработчик события удаления предмета из инвентаря
        /// </summary>
        private void OnInventoryItemRemoved(object sender, InventoryItemEventArgs args)
        {
            if (_isSyncing) return;

            Extentions.DragAndDropLog($"[{GetType().Name}] Item removed: {args.Item.DisplayName} x{args.Count} (to: {args.TargetInventory?.GetType().Name ?? "null"})");

            OnItemRemovedFromUI(args);
        }

        #endregion

        #region Abstract Methods

        /// <summary>
        /// Вызывается когда в UI добавили предмет
        /// Здесь нужно обновить внешние данные (добавить в список GameManager)
        /// </summary>
        /// <param name="args">Аргументы события с информацией о предмете, количестве и контексте переноса (SourceInventory, TargetInventory)</param>
        protected abstract void OnItemAddedToUI(InventoryItemEventArgs args);

        /// <summary>
        /// Вызывается когда из UI убрали предмет
        /// Здесь нужно обновить внешние данные (убрать из списка GameManager)
        /// </summary>
        /// <param name="args">Аргументы события с информацией о предмете, количестве и контексте переноса (SourceInventory, TargetInventory)</param>
        protected abstract void OnItemRemovedFromUI(InventoryItemEventArgs args);

        /// <summary>
        /// Синхронизировать UI с внешними данными
        /// Читает данные из GameManager и обновляет UI
        /// </summary>
        public abstract void ReloadUI();

        #endregion

        #region Helper Methods

        /// <summary>
        /// Очистить UI инвентарь
        /// </summary>
        protected void ClearUI()
        {
            if (_inventory != null)
            {
                _inventory.ClearAll();
            }
        }

        /// <summary>
        /// Добавить предмет в UI без триггера событий
        /// </summary>
        protected void AddToUIQuiet(IInventoryItem item, int count)
        {
            if (_inventory == null) return;

            _isSyncing = true;
            _inventory.TryAddItem(item, count);
            _isSyncing = false;
        }

        /// <summary>
        /// Удалить предмет из UI без триггера событий
        /// </summary>
        protected void RemoveFromUIQuiet(IInventoryItem item, int count)
        {
            if (_inventory == null) return;

            _isSyncing = true;
            _inventory.TryRemoveItem(item, count);
            _isSyncing = false;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Принудительно синхронизировать UI (можно вызвать из внешнего кода)
        /// </summary>
        [ContextMenu("Force Sync To UI")]
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
        protected virtual RuleResult CanStartDragInternal(DragContext context, DragEntry entry)
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
        protected virtual RuleResult CanDropInternal(DragContext context, DragEntry entry)
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
        protected virtual RuleResult CanSwapInternal(InventorySwapEventArgs args)
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
        protected virtual void OnSwapCompleted(InventorySwapEventArgs args)
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

                return _owner.CanStartDragInternal(context, entry);
            }

            public RuleResult CanDrop(DragContext context, DragEntry entry)
            {
                if (_owner == null)
                    return RuleResult.Success();

                return _owner.CanDropInternal(context, entry);
            }
        }

        #endregion
    }
}
