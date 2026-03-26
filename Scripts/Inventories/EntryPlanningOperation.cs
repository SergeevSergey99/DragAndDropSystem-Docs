using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Rules;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    internal readonly struct EntryPlanningOperation
    {
        public EntryPlanningOperation(
            DragContext context,
            DragEntry entry,
            ResolvedDropPolicy policy,
            IInventory targetInventory,
            ISlot targetSlotHint,
            bool preferHint,
            IReadOnlyList<VirtualSlotState> virtualSlots,
            GlobalRuleValidator globalRules,
            IInventoryItem targetItem,
            int requestedAmount,
            int acceptableByInventory)
        {
            Context = context;
            Entry = entry;
            Policy = policy;
            TargetInventory = targetInventory;
            TargetSlotHint = targetSlotHint;
            PreferHint = preferHint;
            VirtualSlots = virtualSlots;
            GlobalRules = globalRules;
            TargetItem = targetItem;
            RequestedAmount = requestedAmount;
            AcceptableByInventory = acceptableByInventory;
        }

        public DragContext Context { get; }
        public DragEntry Entry { get; }
        public ResolvedDropPolicy Policy { get; }
        public IInventory TargetInventory { get; }
        public ISlot TargetSlotHint { get; }
        public bool PreferHint { get; }
        public IReadOnlyList<VirtualSlotState> VirtualSlots { get; }
        public GlobalRuleValidator GlobalRules { get; }
        public IInventoryItem TargetItem { get; }
        public int RequestedAmount { get; }
        public int AcceptableByInventory { get; }
    }
}
