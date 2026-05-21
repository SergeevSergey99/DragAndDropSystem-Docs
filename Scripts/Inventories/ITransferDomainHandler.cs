using UDND.Rules;

namespace UDND.Inventories
{
    /// <summary>
    /// Optional hook for domain logic around an already planned transfer.
    /// </summary>
    public interface ITransferDomainHandler
    {
        RuleResult CanCommitTransfer(TransferDomainContext context);
        void OnTransferSucceeded(TransferDomainContext context);
    }
}
