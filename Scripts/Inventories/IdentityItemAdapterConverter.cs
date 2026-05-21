using UDND.Core;

namespace UDND.Inventories
{
    /// <summary>
    /// Default converter: passes items through unchanged.
    /// </summary>
    public sealed class IdentityItemAdapterConverter : IItemAdapterConverter
    {
        public static readonly IdentityItemAdapterConverter Instance = new();

        public IItemAdapter TryConvertIncoming(IItemAdapter itemAdapter) => itemAdapter;
        public IItemAdapter TryConvertOutgoing(IItemAdapter itemAdapter) => itemAdapter;
    }
}
