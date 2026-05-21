using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace UDND.Examples.Containers.UI
{
    public class ContainerUIController : MonoBehaviour
    {
        [FormerlySerializedAs("_openedContainerBinding")] [SerializeField] private ContainerInventoryDataBinding containerBinding;
        [SerializeField] private GameObject _containerPanel;
        
        // Replace to TMP Support
        // [SerializeField] private TMPro.TMP_Text _titleText;
        [SerializeField] private Text _titleText;
        
        [SerializeField] private string _emptyTitle = "Open Container";

        private ContainerItemInstance _currentContainer;
        
        private void OnEnable()
        {
            Events.OnOpenClick += OpenContainer;
            Events.OnContainerContentChanged += HandleContainerContentChanged;
        }

        private void OnDisable()
        {
            Events.OnOpenClick -= OpenContainer;
            Events.OnContainerContentChanged -= HandleContainerContentChanged;
        }

        void OpenContainer(ContainerItemInstance container)
        {
            if (container == null || ReferenceEquals(_currentContainer, container))
                return;

            _currentContainer = container;
            containerBinding.SetContainer(_currentContainer);
            _containerPanel.SetActive(true);

            UpdateLabels();
        }

        public void CloseCurrentContainer()
        {
            _currentContainer = null;
            _containerPanel.SetActive(false);
        }

        private void HandleContainerContentChanged(ContainerItemInstance container)
        {
            if (_currentContainer != null && ReferenceEquals(_currentContainer, container))
                containerBinding.SetContainer(_currentContainer);
        }

        private void UpdateLabels()
        {
            if (_titleText != null && _currentContainer != null)
                _titleText.text = _currentContainer.GetItem().DisplayName;
            else 
                _titleText.text = _emptyTitle;
        }
    }
}
