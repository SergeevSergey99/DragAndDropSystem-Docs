using System.Collections.Generic;
using UDND.Core;
using UDND.Slots;

namespace UDND.Tests
{
    /// <summary>
    /// Recording double for IDropTarget. The interface carries no Unity types, so the
    /// drop-target stack can be exercised in EditMode without GameObjects, Selectable
    /// or an EventSystem — the same seam a UI Toolkit adapter would push/pop through.
    ///
    /// Activation history is recorded per instance and, for ordering assertions across
    /// several targets, into a shared <see cref="ActivationLog"/>.
    /// </summary>
    public sealed class FakeDropTarget : IDropTarget
    {
        private readonly BaseSlot _targetSlot;
        private readonly IDropProcessor _processor;

        public FakeDropTarget(
            string name = "FakeTarget",
            BaseSlot targetSlot = null,
            IDropProcessor processor = null,
            List<string> activationLog = null)
        {
            Name = name;
            _targetSlot = targetSlot;
            _processor = processor;
            ActivationLog = activationLog;
        }

        public string Name { get; }

        /// <summary>Optional shared log, written as "{Name}:active" / "{Name}:inactive".</summary>
        public List<string> ActivationLog { get; }

        public int ActivateCount { get; private set; }
        public int DeactivateCount { get; private set; }

        /// <summary>True while this target is the activated top of the stack.</summary>
        public bool IsActive { get; private set; }

        public BaseSlot GetTargetSlot() => _targetSlot;

        public IDropProcessor GetDropProcessor() => _processor;

        public void OnBecomeActiveTarget()
        {
            ActivateCount++;
            IsActive = true;
            ActivationLog?.Add($"{Name}:active");
        }

        public void OnBecomeInactiveTarget()
        {
            DeactivateCount++;
            IsActive = false;
            ActivationLog?.Add($"{Name}:inactive");
        }

        public override string ToString() => $"FakeDropTarget({Name})";
    }

    /// <summary>
    /// Minimal IDropProcessor double. Records the contexts it was asked about so tests
    /// can assert that CompleteDrag routed through the active target's processor.
    /// </summary>
    public sealed class FakeDropProcessor : IDropProcessor
    {
        public bool AcceptDrop { get; set; } = true;
        public bool SucceedDrop { get; set; } = true;

        public int CanAcceptCalls { get; private set; }
        public int ProcessCalls { get; private set; }
        public DragContext LastProcessedContext { get; private set; }

        public bool CanAcceptDrop(DragContext context)
        {
            CanAcceptCalls++;
            return AcceptDrop;
        }

        public DropResult ProcessDrop(DragContext context)
        {
            ProcessCalls++;
            LastProcessedContext = context;

            if (!SucceedDrop)
                return DropResult.Failed("FakeDropProcessor configured to fail");

            var adapter = context?.Entries != null && context.Entries.Count > 0
                ? context.Entries[0].Stack.PrimaryAdapter
                : null;

            return DropResult.Succeeded(adapter, amount: 1);
        }
    }
}
