using UDND.Core;
using UDND.DataBinding;
using UDND.Rules;

namespace UDND.Examples.Minecraft
{
    /// <summary>
    /// DataBinding for the crafting result slot.
    /// Displays the result of the matching recipe.
    /// When the item is taken out, consumes ingredients from the crafting table.
    ///
    /// Consumption is deferred until OnDropCompletedFrom: during batch transfer (split across multiple
    /// target slots), each allocation may be non-multiple of ResultCount,
    /// but their sum is guaranteed to be a multiple (the planner ensures this via DragAmountStep).
    ///
    /// Scene setup:
    /// - UniversalInventory with 1 slot
    /// - DO NOT add InventoryDropArea (incoming drops are forbidden)
    /// - Assign this component
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
            // Result slot is output-only, ignore incoming drops
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
            return RuleResult.Failure("You cannot place an item here. Take the crafting result to start a new craft.");
        }
    }
}
