using DragAndDropSystem.Core;

namespace DragAndDropSystem.Inventories
{
    public interface IDropPolicyProvider
    {
        ResolvedDropPolicy ResolveDropPolicy(DropRequestPolicy? requested, DragContext context);
    }
}
