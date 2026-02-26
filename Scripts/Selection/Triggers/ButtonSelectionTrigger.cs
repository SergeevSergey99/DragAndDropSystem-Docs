using UnityEngine;
using UnityEngine.UI;

namespace DragAndDropSystem.Selection
{
    /// <summary>
    /// Триггер выделения через UI Button.
    /// Пример: кнопка "Выделить всё оружие", "Снять выделение", "Выбрать редкие предметы".
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
