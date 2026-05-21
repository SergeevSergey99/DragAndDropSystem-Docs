using System;
using UnityEngine;
using UDND.Tools.Inspector;

namespace UDND.Core
{
    [Serializable]
    public sealed class DragRequestPolicySettings
    {
        [SerializeField, LabelText("Override Drag Amount"), Tooltip("Temporarily overrides the item amount only for the current StartDrag.")]
        private bool _overrideAmount;
        [SerializeField, ShowIf(nameof(_overrideAmount)), LabelText("Amount"), Tooltip("How many items to take from the source stack when starting a drag.")]
        private DragAmount _amount = DragAmount.All;
        [SerializeField, ShowIf(nameof(ShowCustomAmount)), LabelText("Custom Amount"), Tooltip("Used only when Amount = Custom.")]
        private int _customAmount = 1;

        private bool ShowCustomAmount => _overrideAmount && _amount == DragAmount.Custom;

        public DragRequestPolicy? TryBuild()
        {
            if (!_overrideAmount)
                return null;

            return new DragRequestPolicy(_amount, _customAmount);
        }
    }
}
