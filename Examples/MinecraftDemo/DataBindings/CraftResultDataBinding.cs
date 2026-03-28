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
    /// Настройка в сцене:
    /// - UniversalInventory с 1 слотом
    /// - НЕ добавлять InventoryDropArea (запрет на входящие дропы)
    /// - Назначить этот компонент
    /// </summary>
    public class CraftResultDataBinding : InventoryDataBindingBase
    {
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
                AddToUIQuiet(new MinecraftItemAdapter(recipe.Result), multiplier * recipe.ResultCount, 0);
            }
        }

        protected override void OnItemAddedToUI(InventoryItemEventContext context)
        {
            // Слот результата — только на вытаскивание, входящие дропы игнорируем
        }

        protected override void OnItemRemovedFromUI(InventoryItemEventContext context)
        {
            // Игрок забрал результат — потребляем ингредиенты
            if (CraftingManager.AutoCreateInstance != null)
                CraftingManager.AutoCreateInstance.ConsumeCraftIngredients();
        }

        protected override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            return RuleResult.Failure("Вы не можете положить предмет сюда! Заберите результат крафта, чтобы начать новый.");
        }
    }
}
