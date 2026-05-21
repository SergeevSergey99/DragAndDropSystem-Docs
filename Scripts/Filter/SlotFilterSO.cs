using UnityEngine;
using UDND.Tools.Inspector;

namespace UDND.Filter
{
    /// <summary>
    /// ScriptableObject wrapper for any <see cref="ISlotFilter"/>.
    /// Use to create shareable filter assets configured via Inspector.
    /// The actual filter logic lives in the <see cref="ISlotFilter"/> implementation chosen via [SerializeReference].
    /// </summary>
    [CreateAssetMenu(fileName = "SlotFilter", menuName = "DragAndDrop/Filter/Slot Filter", order = 100)]
    public class SlotFilterSO : ScriptableObject, ISlotFilter
    {
        [SerializeReference, ManagedReferencePicker]
        private ISlotFilter _filter;

        public ISlotFilter Filter
        {
            get => _filter;
            set => _filter = value;
        }

        public bool Evaluate(in FilterContext ctx) => _filter?.Evaluate(in ctx) ?? true;
    }
}
