using System;
using System.Collections.Generic;
using DragAndDropSystem.Examples.Containers;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace DragAndDropSystem.Examples.Containers.UI
{
    public class ContainerUIController : MonoBehaviour
    {
        [FormerlySerializedAs("_openedContainerBinding")] [SerializeField] private ContainerInventoryDataBinding containerBinding;
        [SerializeField] private GameObject _containerPanel;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private string _emptyTitle = "Open Container";

        private ContainerItemInstance _currentContainer;
        
        private void OnEnable()
        {
            Events.OnOpenClick += OpenContainer;
        }

        private void OnDisable()
        {
            Events.OnOpenClick -= OpenContainer;
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

        private void UpdateLabels()
        {
            if (_titleText != null && _currentContainer != null)
                _titleText.text = _currentContainer.GetItem().DisplayName;
            else 
                _titleText.text = _emptyTitle;
        }
    }
}
