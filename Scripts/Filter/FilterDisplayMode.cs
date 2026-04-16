namespace UniversalDragAndDrop.Filter
{
    public enum FilterDisplayMode
    {
        /// <summary>
        /// Hide filtered slots (SetActive(false)).
        /// </summary>
        Hide,

        /// <summary>
        /// Dim filtered slots but keep them visible (SetInteractable(false)).
        /// </summary>
        Dim,

        /// <summary>
        /// Move filtered slots to the end, keep visible but non-interactable.
        /// </summary>
        MoveToEnd
    }
}
