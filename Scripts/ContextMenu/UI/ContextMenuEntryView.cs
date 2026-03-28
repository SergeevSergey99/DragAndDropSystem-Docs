using System;
using DragAndDropSystem.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DragAndDropSystem.ContextMenu.UI
{
    public class ContextMenuEntryView : MonoBehaviour
    {
        [SerializeField] private TMPro.TextMeshProUGUI label;
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

            if (InputModalityTracker.CurrentModality == InputModalityTracker.InputModality.Mouse)
            {
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(null);
            }
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