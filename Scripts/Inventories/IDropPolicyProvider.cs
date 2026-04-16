using UniversalDragAndDrop.Core;

namespace UniversalDragAndDrop.Inventories
{
    public interface IDropPolicyProvider
    {
        ResolvedDropPolicy ResolveDropPolicy(DropRequestPolicy? requested, DragContext context);
    }
}
