namespace UniversalDragAndDrop.Core
{
    /// <summary>
    /// Generic execution result for input and inventory actions.
    /// </summary>
    public readonly struct ActionResult
    {
        public bool Success { get; }
        public string FailureReason { get; }

        private ActionResult(bool success, string failureReason)
        {
            Success = success;
            FailureReason = failureReason;
        }

        public static ActionResult Succeeded()
            => new ActionResult(true, null);

        public static ActionResult Failed(string reason = null)
            => new ActionResult(false, reason);
    }
}
