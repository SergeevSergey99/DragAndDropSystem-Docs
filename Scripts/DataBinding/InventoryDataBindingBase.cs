using System;
using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Inspector;
using DragAndDropSystem.Interaction;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Rules;
using DragAndDropSystem.Slots;
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
        [SerializeField, Tooltip("Inventory UI representation")]
        protected UniversalInventory _inventory;

        /*
        [FoldoutGroup("Rules", false)]
        [SerializeField, HideLabel]
        [Tooltip("Rules for validating whether items can be transferred into this inventory")]
        private InventoryRuleValidator _ruleValidator = new();
        */

        private int _syncDepth = 0;

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

        protected virtual void Awake()
        {
            // Инициализируем инвентарь
            _inventory.Initialize(this);
        }

        /*private void OnValidate()
        {
            // Сортируем правила при изменении в Inspector
            _ruleValidator?.OnValidate();
        }*/

        protected virtual void OnEnable()
        {
            if (_inventory != null)
            {
                _inventory.OnSwapAttempting += HandleSwapAttempting;
                _inventory.OnSwapCompleted += HandleSwapCompleted;
            }

            DragAndDropManager.OnDropCompleted += HandleDropCompleted;
            ReloadUI();
        }

        protected virtual void OnDisable()
        {
            if (_inventory != null)
            {
                _inventory.OnSwapAttempting -= HandleSwapAttempting;
                _inventory.OnSwapCompleted -= HandleSwapCompleted;
            }

            DragAndDropManager.OnDropCompleted -= HandleDropCompleted;
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
                Extensions.DragAndDropLog($"[{GetType().Name}] CanSwap rejected: {result.FailureReason}");
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

        private void HandleDropCompleted(DragContext context)
        {
            if (context == null)
                return;

            bool isSource = false;
            bool isTarget = ReferenceEquals(context.TargetInventory, _inventory);

            for (int i = 0; i < context.Entries.Count; i++)
            {
                if (ReferenceEquals(context.Entries[i].SourceInventory, _inventory))
                {
                    isSource = true;
                    break;
                }
            }

            if (isSource)
                OnDropCompletedFrom(context);
            if (isTarget)
                OnDropCompletedTo(context);
        }

        /// <summary>
        /// Вызывается напрямую из UniversalInventory при добавлении предмета.
        /// </summary>
        internal void HandleItemAdded(InventoryItemEventContext context)
        {
            if (IsSyncing) return;

            Extensions.DragAndDropLog($"[{GetType().Name}] PrimaryAdapter added: {context.Stack.PrimaryAdapter?.DisplayName} x{context.Stack.Count} (from: {context.SourceInventory?.GetType().Name ?? "null"})");
            OnItemAddedToUI(context);
        }

        /// <summary>
        /// Вызывается напрямую из UniversalInventory при удалении предмета.
        /// </summary>
        internal void HandleItemRemoved(InventoryItemEventContext context)
        {
            if (IsSyncing) return;

            Extensions.DragAndDropLog($"[{GetType().Name}] PrimaryAdapter removed: {context.Stack.PrimaryAdapter?.DisplayName} x{context.Stack.Count} (to: {context.TargetInventory?.GetType().Name ?? "null"})");
            OnItemRemovedFromUI(context);
        }

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
        /// Вызывается после завершения drop-операции, если этот инвентарь был источником.
        /// Полезно для отложенной обработки (напр. потребление ингредиентов крафта).
        /// </summary>
        protected virtual void OnDropCompletedFrom(DragContext context) { }

        /// <summary>
        /// Вызывается после завершения drop-операции, если этот инвентарь был целью.
        /// </summary>
        protected virtual void OnDropCompletedTo(DragContext context) { }

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
        /// Добавить предметы в UI без триггера событий.
        /// Каждый элемент списка должен быть отдельным экземпляром адаптера.
        /// </summary>
        protected void AddToUIQuiet(IEnumerable<IItemAdapter> adapters, int targetSlotIndex = -1)
        {
            if (_inventory == null || adapters == null)
                return;

            if (!ItemStack.TryCreate(adapters, out var stack))
                return;

            if (targetSlotIndex < 0)
            {
                _inventory.TryAddStackQuiet(stack, -1);
                return;
            }

            var slot = _inventory.GetSlot(targetSlotIndex);
            if (slot != null)
                slot.SetStack(stack);
        }

        /// <summary>
        /// Добавить несколько предметов в UI через фабрику, создающую уникальный адаптер на каждый элемент стека.
        /// </summary>
        protected void AddToUIQuiet(Func<IItemAdapter> createAdapter, int count, int targetSlotIndex = -1)
        {
            if (_inventory == null || createAdapter == null || count <= 0)
                return;

            var adapters = new List<IItemAdapter>(count);
            for (int i = 0; i < count; i++)
            {
                var adapter = createAdapter();
                if (adapter == null)
                    return;

                if (ContainsReference(adapters, adapter))
                {
                    Debug.LogWarning($"[{GetType().Name}] AddToUIQuiet received the same adapter instance more than once. Aborting stack creation.");
                    return;
                }

                adapters.Add(adapter);
            }

            AddToUIQuiet(adapters, targetSlotIndex);
        }

        private static bool ContainsReference(List<IItemAdapter> adapters, IItemAdapter candidate)
        {
            foreach (var adapter in adapters)
            {
                if (ReferenceEquals(adapter, candidate))
                    return true;
            }

            return false;
        }

        #endregion

        #region Public API

        public UniversalInventory Inventory => _inventory;

        /// <summary>
        /// Принудительно синхронизировать UI (можно вызвать из внешнего кода)
        /// </summary>
        [Button("Force Sync To UI")]
        public void ForceSyncToUI()
        {
            ReloadUI();
        }

        /// <summary>
        /// Проверяет условия старта драга DataBinding
        /// </summary>
        internal RuleResult ValidateStartDragRules(DragContext context, DragEntry entry)
        {
            /*var rulesResult = _ruleValidator.ValidateStartDrag(context, entry);
            if (!rulesResult.IsValid)
                return rulesResult;*/

            return CanStartDrag(context, entry);
        }

        internal bool CheckOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedBaseSlot)
            => CanHandleOccupiedSlotDrop(entry, occupiedBaseSlot);

        internal bool DoOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedBaseSlot)
            => ExecuteOccupiedSlotDrop(entry, occupiedBaseSlot);

        /// <summary>
        /// Проверяет условия дропа DataBinding
        /// </summary>
        internal RuleResult ValidateDropRules(DragContext context, DragEntry entry)
        {
            /*var rulesResult = _ruleValidator.ValidateDrop(context, entry);
            if (!rulesResult.IsValid)
                return rulesResult;*/

            return CanDrop(context, entry);
        }

        #endregion

        #region Virtual Methods for Transfer Validation

        private IItemAdapterConverter _itemConverter;
        public IItemAdapterConverter ItemConverter => _itemConverter ??= CreateItemConverter();
        
        /// <summary>
        /// Создать converter для преобразования предметов при входе/выходе из инвентаря.
        /// Верните null если конвертация не нужна.
        /// </summary>
        protected virtual IItemAdapterConverter CreateItemConverter() => IdentityItemAdapterConverter.Instance;

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

        /// <summary>
        /// Вызывается планировщиком когда предмет бросают на занятый слот, ДО проверки swap/findAlternative.
        /// Верните true если этот DataBinding может обработать такой дроп (например, добавить предмет внутрь контейнера).
        /// Если false — pipeline продолжит стандартную логику (swap, findAlternative, reject).
        /// </summary>
        protected virtual bool CanHandleOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedBaseSlot) => false;

        /// <summary>
        /// Выполняет дроп на занятый слот. Вызывается executor-ом если CanHandleOccupiedSlotDrop вернул true.
        /// Реализация должна обработать перенос полностью: добавить предмет в целевое место,
        /// очистить source слот и обновить данные.
        /// </summary>
        protected virtual bool ExecuteOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedBaseSlot) => false;

        #endregion
    }
}
