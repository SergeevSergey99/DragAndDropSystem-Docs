using DragAndDropSystem.Rules;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Опциональный hook для domain-логики вокруг уже спланированного переноса.
    /// </summary>
    public interface ITransferDomainHandler
    {
        RuleResult Validate(TransferDomainContext context);
        void OnTransferSucceeded(TransferDomainContext context);
    }
}
