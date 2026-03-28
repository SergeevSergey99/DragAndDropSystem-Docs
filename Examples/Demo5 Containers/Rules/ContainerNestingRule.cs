using System;
using DragAndDropSystem.Core;
using DragAndDropSystem.Rules;

namespace DragAndDropSystem.Examples.Containers
{
    /// <summary>
    /// Правило запрета рекурсивного вложения контейнеров.
    /// 1. Нельзя положить контейнер внутрь себя
    /// 2. Нельзя положить контейнер внутрь своего потомка (транзитивно)
    /// 3. Ограничение глубины вложенности
    /// </summary>
    [Serializable]
    public class ContainerNestingRule : DragRuleBase, IInventoryRule
    {
        public override int Priority => 5;

        public override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            if (entry.Stack?.Item is not ContainerItemAdapter draggedAdapter)
                return RuleResult.Success();

            var draggedInstance = draggedAdapter.Instance;

            // Найти ContainerInventoryDataBinding целевого инвентаря
            var targetBinding = FindContainerBinding(context);
            if (targetBinding == null)
                return RuleResult.Success(); // Дроп в обычный инвентарь — ОК

            var targetContainer = targetBinding.BoundContainer;
            if (targetContainer == null)
                return RuleResult.Success();

            // 1. Прямой цикл
            if (ReferenceEquals(draggedInstance, targetContainer))
                return RuleResult.Failure("Cannot place a container inside itself");

            // 2. Транзитивный цикл — контейнер A содержит B, нельзя положить A в B
            if (draggedInstance.IsContainer && ContainsTransitive(draggedInstance, targetContainer))
                return RuleResult.Failure("Cannot place a container inside its own contents");

            // 3. Лимит глубины
            if (draggedInstance.IsContainer)
            {
                int maxAllowed = targetContainer.ContainerType?.MaxNestingDepth ?? 0;
                int depthAfterDrop = targetBinding.CurrentNestingDepth + 1 + GetMaxInternalDepth(draggedInstance);
                if (depthAfterDrop > maxAllowed)
                    return RuleResult.Failure("Container nesting depth limit exceeded");
            }

            return RuleResult.Success();
        }

        /// <summary> Содержит ли haystack (рекурсивно) needle? </summary>
        private static bool ContainsTransitive(ItemInstance haystack, ItemInstance needle)
        {
            foreach (var child in haystack.Contents)
            {
                if (ReferenceEquals(child, needle))
                    return true;
                if (child.IsContainer && ContainsTransitive(child, needle))
                    return true;
            }
            return false;
        }

        /// <summary> Максимальная внутренняя глубина контейнера. </summary>
        private static int GetMaxInternalDepth(ItemInstance container)
        {
            if (!container.IsContainer || container.Contents.Count == 0)
                return 0;

            int max = 0;
            foreach (var child in container.Contents)
            {
                if (child.IsContainer)
                    max = Math.Max(max, 1 + GetMaxInternalDepth(child));
            }
            return max;
        }

        private static ContainerInventoryDataBinding FindContainerBinding(DragContext context)
        {
            if (context.TargetInventory == null)
                return null;

            return context.TargetInventory.DataBinding as ContainerInventoryDataBinding;
        }
    }
}
