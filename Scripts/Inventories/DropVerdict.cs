namespace UDND.Inventories
{
    /// <summary>
    /// Whether the active drop preview would land on a given slot, and why not when it would not.
    /// <para>
    /// Resolved once per hover by <see cref="DropPreviewController"/> from the same probe the drop
    /// itself runs, and kept for as long as that preview is on screen. Feedback visuals read it;
    /// nothing re-derives it, so what the player sees cannot disagree with what release will do.
    /// </para>
    /// </summary>
    public readonly struct DropVerdict
    {
        private DropVerdict(bool canPlace, string failureReason)
        {
            CanPlace = canPlace;
            FailureReason = failureReason;
        }

        /// <summary>True when the drop would land on this exact slot.</summary>
        public bool CanPlace { get; }

        /// <summary>Why the drop was refused, when it was. Suitable for a tooltip.</summary>
        public string FailureReason { get; }

        public bool IsRejected => !CanPlace;

        public static DropVerdict Accepted() => new DropVerdict(true, null);

        public static DropVerdict Rejected(string failureReason) => new DropVerdict(false, failureReason);
    }
}
