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
            BaseSlot targetBaseSlotHint,
            bool preferHint,
            IReadOnlyList<VirtualSlotState> virtualSlots,
            GlobalRuleValidator globalRules,
            IItemAdapter targetItemAdapter,
            int requestedAmount,
            int acceptableByInventory)
        {
            Context = context;
            Entry = entry;
            Policy = policy;
            TargetInventory = targetInventory;
            TargetBaseSlotHint = targetBaseSlotHint;
            PreferHint = preferHint;
            VirtualSlots = virtualSlots;
            GlobalRules = globalRules;
            TargetItemAdapter = targetItemAdapter;
            RequestedAmount = requestedAmount;
            AcceptableByInventory = acceptableByInventory;
        }

        public DragContext Context { get; }
        public DragEntry Entry { get; }
        public ResolvedDropPolicy Policy { get; }
        public IInventory TargetInventory { get; }
        public BaseSlot TargetBaseSlotHint { get; }
        public bool PreferHint { get; }
        public IReadOnlyList<VirtualSlotState> VirtualSlots { get; }
        public GlobalRuleValidator GlobalRules { get; }
        public IItemAdapter TargetItemAdapter { get; }
        public int RequestedAmount { get; }
        public int AcceptableByInventory { get; }
    }
}
