using DragAndDropSystem.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DragAndDropSystem.ContextMenu.UI
{
    public class ContextMenuEntryView : MonoBehaviour
    {
        // Replace to TMP Support
        // [SerializeField] private TMPro.TextMeshProUGUI label;
        [SerializeField] private Text label;
        [SerializeField] private Button _button;
        
        public Selectable Selectable => _button;
        
        public void Setup(IContextMenuEntry entry, ContextMenuContext ctx)
        {
            label.text = entry.GetLabel(ctx);
            _button.interactable = entry.IsEnabled(ctx);
            
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => Click(entry, ctx));
        }

        void Click(IContextMenuEntry entry, ContextMenuContext ctx)
        {
            entry.Execute(ctx);

            if (ContextMenuManager.IsInstanceExist)
                ContextMenuManager.AutoCreateInstance.Hide();

#if DNDS_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            if (InputModalityTracker.CurrentModality == InputModalityTracker.InputModality.Mouse)
            {
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(null);
            }
#endif
        }

        private void OnDisable()
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }
}