using UDND.Core;
using UDND.Rules;

namespace UDND.Inventories
{
    /// <summary>
    /// Optional hooks for domain logic around a transfer.
    /// </summary>
    public interface ITransferDomainHandler
    {
        /// <summary>
        /// Called once before any entry is mutated. May reject the whole transfer.
        /// Implementations may run their own simulation here; simulation is not required by core.
        /// </summary>
        RuleResult CanStartTransfer(DragContext context, IInventory targetInventory);

        RuleResult CanCommitTransfer(TransferDomainContext context);
        void OnTransferSucceeded(TransferDomainContext context);
    }
}