using UnityEngine;
using UDND.Tools.Inspector;

namespace UDND.Filter
{
    /// <summary>
    /// ScriptableObject wrapper for any <see cref="ISlotSorter"/>.
    /// Use to create shareable sorter assets configured via Inspector.
    /// The actual sort logic lives in the <see cref="ISlotSorter"/> implementation chosen via [SerializeReference].
    /// </summary>
    [CreateAssetMenu(fileName = "SlotSorter", menuName = "DragAndDrop/Filter/Slot Sorter", order = 101)]
    public class SlotSorterSO : ScriptableObject, ISlotSorter
    {
        [SerializeReference, ManagedReferencePicker]
        private ISlotSorter _sorter;

        public ISlotSorter Sorter
        {
            get => _sorter;
            set => _sorter = value;
        }

        public int Compare(in FilterContext a, in FilterContext b) => _sorter?.Compare(in a, in b) ?? 0;
    }
}
