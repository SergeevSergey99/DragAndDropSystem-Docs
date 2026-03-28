using UnityEngine;

namespace DragAndDropSystem.Interaction
{
    [CreateAssetMenu(fileName = "HoldDragSettings", menuName = "DragAndDrop/Interaction/Hold Drag Settings")]
    public class HoldDragSettings : ScriptableObject
    {
        [SerializeField, Tooltip("Начальное количество предметов при мгновенном начале драга")]
        private int _startAmount = 1;

        [SerializeField, Min(0.01f), Tooltip("Интервал (секунды) между инкрементами количества")]
        private float _intervalSeconds = 0.3f;

        [SerializeField, Min(0), Tooltip("Максимальное количество (0 = без ограничения, берётся весь стак)")]
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
