using System.Collections.Generic;
using CodeUtils;
using UDND.Core;
using UDND.Inventories;
using UDND.Rules;

namespace UDND.Examples.Containers
{
    /// <summary>
    /// Tracks the container views currently on screen, so an occupied-slot insert can be routed into
    /// the live inventory that displays a given container (incremental update instead of a full
    /// reload). The list lives on a MonoSingleton tied to scene lifetime, so there is no static
    /// state to clear between Play sessions.
    /// </summary>
    public class ContainerViewRegistry : MonoSingleton<ContainerViewRegistry>
    {
        private readonly List<ContainerInventoryDataBinding> _views = new();

        public void Register(ContainerInventoryDataBinding view)
        {
            if (view != null && !_views.Contains(view))
                _views.Add(view);
        }

        public void Unregister(ContainerInventoryDataBinding view)
        {
            _views.Remove(view);
        }

        /// <summary>
        /// Returns the live inventory currently displaying <paramref name="container"/>, if any.
        /// </summary>
        public bool TryGetLiveInventory(ContainerItemInstance container, out IInventory inventory)
        {
            inventory = null;
            if (container == null)
                return false;

            for (int i = 0; i < _views.Count; i++)
            {
                var view = _views[i];
                if (view != null && ReferenceEquals(view.currentContainer, container) && view.Inventory != null)
                {
                    inventory = view.Inventory;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Inserts the dragged item into <paramref name="container"/>.
        ///
        /// When the container is currently displayed by a live inventory, the item is moved into
        /// that inventory through the normal drop pipeline (like <c>InventoryDropArea</c>): the open
        /// view updates incrementally (one slot filled, not a full ReloadUI) and both bindings write
        /// their own data through the usual add/remove events. When the container is not displayed,
        /// the item is removed from its real source inventory and written into the data directly.
        /// </summary>
        public bool InsertIntoContainer(DragEntry entry, ContainerItemInstance container)
        {
            if (container == null)
                return false;

            if (TryGetLiveInventory(container, out var liveInventory))
            {
                var dropContext = new DragContext(entry.Stack, entry.SourceBaseSlot, entry.SourceInventory);
                return new InventoryDropProcessor(liveInventory, new GlobalRuleValidator())
                    .ProcessDropWithReport(dropContext)
                    .Success;
            }

            var sourceInventory = entry.SourceInventory ?? entry.SourceBaseSlot?.Inventory;
            sourceInventory?.RemoveItemsFromSlot(entry.SourceBaseSlot, entry.Stack);

            if (entry.Stack?.Adapters != null)
            {
                foreach (var adapter in entry.Stack.Adapters)
                    if (adapter is ContainerItemAdapterAdapter containerAdapter)
                        container.AddItem(containerAdapter.Instance);
            }

            Events.InvokeContainerContentChanged(container);
            return true;
        }
    }
}
