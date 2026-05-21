using UnityEngine;

namespace UDND.Interaction
{
    [CreateAssetMenu(fileName = "HoldDragSettings", menuName = "DragAndDrop/Interaction/Hold Drag Settings")]
    public class HoldDragSettings : ScriptableObject
    {
        [SerializeField, Tooltip("Initial item amount when starting a drag immediately")]
        private int _startAmount = 1;

        [SerializeField, Min(0.01f), Tooltip("Interval (seconds) between amount increments")]
        private float _intervalSeconds = 0.3f;

        [SerializeField, Min(0), Tooltip("Maximum amount (0 = unlimited, the whole stack is taken)")]
        private int _maxAmount;

        public int StartAmount => _startAmount;
        public float IntervalSeconds => _intervalSeconds;
        public int MaxAmount => _maxAmount;

        public int ComputeAmount(float holdDuration, int stackCount)
        {
            int amount = _startAmount + Mathf.FloorToInt(holdDuration / _intervalSeconds);
            int cap = _maxAmount > 0 ? Mathf.Min(_maxAmount, stackCount) : stackCount;
            return Mathf.Clamp(amount, 1, cap);
        }
    }
}
