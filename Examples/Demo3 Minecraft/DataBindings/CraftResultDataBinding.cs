using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Rules;

namespace DragAndDropSystem.Examples.Minecraft
{
    /// <summary>
    /// DataBinding для слота результата крафта.
    /// Показывает результат подходящего рецепта.
    /// При вытаскивании предмета — потребляет ингредиенты со стола крафта.
    ///
    /// Потребление отложено до OnDropCompletedFrom: при batch-трансфере (разбивка по нескольким
    /// target слотам) каждая аллокация может быть не кратна ResultCount,
    /// но их сумма — кратна (планировщик это гарантирует через DragAmountStep).
    ///
    /// Настройка в сцене:
    /// - UniversalInventory с 1 слотом
    /// - НЕ добавлять InventoryDropArea (запрет на входящие дропы)
    /// - Назначить этот компонент
    /// </summary>
    public class CraftResultDataBinding : InventoryDataBindingBase
    {
        private int _pendingRemovedItems;

        protected override void OnEnable()
        {
            base.OnEnable();
            CraftingManager.AutoCreateInstance.OnCraftResultChanged += ReloadUI;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (CraftingManager.IsInstanceExist)
                CraftingManager.Instance.OnCraftResultChanged -= ReloadUI;
        }

        protected override void OnReloadUI()
        {
            var recipe = CraftingManager.Instance?.CurrentRecipe;

            if (recipe != null && recipe.Result != null)
            {
                int multiplier = CraftingManager.AutoCreateInstance.CraftMultiplier;
                AddToUIQuiet(() => new MinecraftItemAdapterAdapter(recipe.Result), multiplier * recipe.ResultCount, 0);
                _inventory.SetDragAmountStep(recipe.ResultCount, DragAmountStepRounding.Ceil);
            }
            else
            {
                _inventory.SetDragAmountStep(0);
            }
        }

        protected override void OnItemAddedToUI(InventoryItemEventContext context)
        {
            // Слот результата — только на вытаскивание, входящие дропы игнорируем
        }

        protected override void OnItemRemovedFromUI(InventoryItemEventContext context)
        {
            _pendingRemovedItems += context.Stack.Count;
        }

        protected override void OnDropCompletedFrom(DragContext context)
        {
            if (_pendingRemovedItems <= 0)
                return;

            var manager = CraftingManager.AutoCreateInstance;
            if (manager == null || manager.CurrentRecipe == null)
            {
                _pendingRemovedItems = 0;
                return;
            }

            int resultCount = manager.CurrentRecipe.ResultCount;
            int craftsConsumed = resultCount > 0 ? _pendingRemovedItems / resultCount : 0;
            _pendingRemovedItems = 0;

            if (craftsConsumed > 0)
                manager.ConsumeCraftIngredients(craftsConsumed);
        }

        protected override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            return RuleResult.Failure("Вы не можете положить предмет сюда! Заберите результат крафта, чтобы начать новый.");
        }
    }
}
