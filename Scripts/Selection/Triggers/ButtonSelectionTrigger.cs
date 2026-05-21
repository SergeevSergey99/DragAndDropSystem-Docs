using UnityEngine;
using UnityEngine.UI;

namespace UDND.Selection
{
    /// <summary>
    /// Selection trigger driven by a UI Button.
    /// Example: buttons like "Select all weapons", "Clear selection", or "Select rare items".
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ButtonSelectionTrigger : SelectionTriggerBase
    {
        [SerializeField] private Button _button;

        private void Awake()
        {
            if (_button == null)
                _button = GetComponent<Button>();
        }

        private void OnEnable()  => _button.onClick.AddListener(OnClick);
        private void OnDisable() => _button.onClick.RemoveListener(OnClick);

        private void OnClick() => TryExecute();
    }
}