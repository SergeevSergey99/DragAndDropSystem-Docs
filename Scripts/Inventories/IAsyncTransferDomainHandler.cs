using System.Threading;
using System.Threading.Tasks;
using UDND.Core;
using UDND.Rules;

namespace UDND.Inventories
{
    /// <summary>
    /// Optional asynchronous transfer-wide veto.
    /// Called once before any entry is mutated by the asynchronous execution path.
    /// </summary>
    public interface IAsyncTransferDomainHandler
    {
        Task<RuleResult> CanStartTransferAsync(
            DragContext context,
            IInventory targetInventory,
            CancellationToken cancellationToken);
    }
}