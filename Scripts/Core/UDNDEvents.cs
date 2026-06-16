using System;
using UnityEngine;
using UDND.Interaction;
using UDND.Selection;
using UDND.Slots;

namespace UDND.Core
{
    /// <summary>
    /// Central hub for the package's global (static) runtime events. External code subscribes here
    /// (<c>UDNDEvents.OnX += ...</c>); only UDND runtime code raises them (the <c>Raise*</c> methods
    /// are <c>internal</c>, preserving the "only the owner publishes" guarantee at the assembly level).
    /// <para>
    /// All subscribers are cleared once at the start of every Play session via
    /// <see cref="Reset"/>, so "Fast Enter Play Mode" (domain reload disabled) can't carry stale
    /// listeners between sessions. Adding a new event here means it is covered by that single reset.
    /// </para>
    /// </summary>
    public static class UDNDEvents
    {
        // ── Drag lifecycle ───────────────────────────────────────────────────────────────────
        public static event Action<DragContext> OnDragAttempting;
        public static event Action<DragContext> OnDragStarted;
        public static event Action<DragContext> OnDragEnterSlot;
        public static event Action<DragContext> OnDragExitSlot;
        public static event Action<DragContext> OnDropAttempting;
        public static event Action<DragContext> OnDropCompleted;
        public static event Action<DragContext> OnDragCancelled;
        public static event Action<DragContext> OnDragStackChanged;
        public static event Action<DragContext> OnDragOrientationChanged;
        public static event Action OnDragEnded;

        // ── Auto-transfer ────────────────────────────────────────────────────────────────────
        public static event Action<DragContext> OnAutoTransferAttempting;
        public static event Action<DragContext> OnAutoTransferCompleted;
        public static event Action<DragContext> OnAutoTransferFailed;

        // ── Swap ─────────────────────────────────────────────────────────────────────────────
        public static event Action<InventorySwapContext> OnSwapAttempting;
        public static event Action<InventorySwapContext> OnSwapCompleted;

        // ── Hold-to-count preview ────────────────────────────────────────────────────────────
        public static event Action<BaseSlot, int, int> OnHoldPreviewChanged;
        public static event Action OnHoldPreviewEnded;

        // ── Input modality ───────────────────────────────────────────────────────────────────
        public static event Action<InputModalityTracker.InputModality> OnModalityChanged;
        public static event Action<bool> OnNavigationModeChanged;

        // ── Slot hover ───────────────────────────────────────────────────────────────────────
        public static event Action<SlotHoverEventArgs> OnAnySlotHoverEnter;
        public static event Action<SlotHoverEventArgs> OnAnySlotHoverExit;

        // ── Selection ────────────────────────────────────────────────────────────────────────
        public static event Action<SelectionContext> OnSelectionChanged;

        internal static void RaiseDragAttempting(DragContext context) => OnDragAttempting?.Invoke(context);
        internal static void RaiseDragStarted(DragContext context) => OnDragStarted?.Invoke(context);
        internal static void RaiseDragEnterSlot(DragContext context) => OnDragEnterSlot?.Invoke(context);
        internal static void RaiseDragExitSlot(DragContext context) => OnDragExitSlot?.Invoke(context);
        internal static void RaiseDropAttempting(DragContext context) => OnDropAttempting?.Invoke(context);
        internal static void RaiseDropCompleted(DragContext context) => OnDropCompleted?.Invoke(context);
        internal static void RaiseDragCancelled(DragContext context) => OnDragCancelled?.Invoke(context);
        internal static void RaiseDragStackChanged(DragContext context) => OnDragStackChanged?.Invoke(context);
        internal static void RaiseDragOrientationChanged(DragContext context) => OnDragOrientationChanged?.Invoke(context);
        internal static void RaiseDragEnded() => OnDragEnded?.Invoke();

        internal static void RaiseAutoTransferAttempting(DragContext context) => OnAutoTransferAttempting?.Invoke(context);
        internal static void RaiseAutoTransferCompleted(DragContext context) => OnAutoTransferCompleted?.Invoke(context);
        internal static void RaiseAutoTransferFailed(DragContext context) => OnAutoTransferFailed?.Invoke(context);

        internal static void RaiseSwapAttempting(InventorySwapContext context) => OnSwapAttempting?.Invoke(context);
        internal static void RaiseSwapCompleted(InventorySwapContext context) => OnSwapCompleted?.Invoke(context);

        internal static void RaiseHoldPreviewChanged(BaseSlot slot, int amount, int maxAmount) =>
            OnHoldPreviewChanged?.Invoke(slot, amount, maxAmount);
        internal static void RaiseHoldPreviewEnded() => OnHoldPreviewEnded?.Invoke();

        internal static void RaiseModalityChanged(InputModalityTracker.InputModality modality) => OnModalityChanged?.Invoke(modality);
        internal static void RaiseNavigationModeChanged(bool active) => OnNavigationModeChanged?.Invoke(active);

        internal static void RaiseAnySlotHoverEnter(SlotHoverEventArgs args) => OnAnySlotHoverEnter?.Invoke(args);
        internal static void RaiseAnySlotHoverExit(SlotHoverEventArgs args) => OnAnySlotHoverExit?.Invoke(args);

        internal static void RaiseSelectionChanged(SelectionContext context) => OnSelectionChanged?.Invoke(context);

        // Cleared before any scene loads, on every Play session. Harmless no-op in builds.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            OnDragAttempting = null;
            OnDragStarted = null;
            OnDragEnterSlot = null;
            OnDragExitSlot = null;
            OnDropAttempting = null;
            OnDropCompleted = null;
            OnDragCancelled = null;
            OnDragStackChanged = null;
            OnDragOrientationChanged = null;
            OnDragEnded = null;

            OnAutoTransferAttempting = null;
            OnAutoTransferCompleted = null;
            OnAutoTransferFailed = null;

            OnSwapAttempting = null;
            OnSwapCompleted = null;

            OnHoldPreviewChanged = null;
            OnHoldPreviewEnded = null;

            OnModalityChanged = null;
            OnNavigationModeChanged = null;

            OnAnySlotHoverEnter = null;
            OnAnySlotHoverExit = null;

            OnSelectionChanged = null;
        }
    }
}
