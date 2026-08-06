using UDND.Inventories;
using UnityEngine;

namespace UDND.Slots
{
    /// <summary>
    /// Slot that marks a refused drop with a red cross while it is highlighted.
    /// <para>
    /// The verdict is read back from the drop preview that highlighted this slot, which derived it
    /// from the same probe the drop itself runs. The slot deliberately does not probe on its own:
    /// a second probe would resolve its own policy and could contradict what happens on release.
    /// </para>
    /// </summary>
    public class CrossFeedbackSlot : UniversalSlot
    {
        [SerializeField] private GameObject _redCross;

        public override void Highlight(bool highlight)
        {
            base.Highlight(highlight);

            if (_redCross != null)
                _redCross.SetActive(highlight && IsCurrentDropRefused());
        }

        private bool IsCurrentDropRefused()
        {
            return Inventory is IInventoryInteraction interaction &&
                   interaction.TryGetActiveDropVerdict(this, out var verdict) &&
                   verdict.IsRejected;
        }
    }
}
