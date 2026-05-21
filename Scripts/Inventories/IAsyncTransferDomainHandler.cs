using System.Threading;
using System.Threading.Tasks;
using UDND.Rules;

namespace UDND.Inventories
{
    /// <summary>
    /// Optional async pre-commit validation for transfer-level domain logic.
    /// Use this for server-backed or other asynchronous veto checks that must run
    /// once before a local transfer is committed.
    /// </summary>
    public interface IAsyncTransferDomainHandler
    {
        Task<RuleResult> CanCommitTransferAsync(TransferDomainContext context, CancellationToken cancellationToken);
    }
}
