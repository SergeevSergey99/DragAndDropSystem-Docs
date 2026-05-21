using UDND.Core;

namespace UDND.Inventories
{
    public interface IDropPolicyProvider
    {
        ResolvedDropPolicy ResolveDropPolicy(DropRequestPolicy? requested, DragContext context);
    }
}
