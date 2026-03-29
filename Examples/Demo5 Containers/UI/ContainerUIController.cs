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
        [SerializeField] private TMP_Text _pathText;
        [SerializeField] private string _emptyTitle = "Open Container";

        private readonly List<ContainerItemInstance> _navigationStack = new();

        public ContainerItemInstance CurrentContainer { get; private set; }
        public bool HasOpenContainer => CurrentContainer != null;
        public bool CanGoBack => _navigationStack.Count > 0;

        public event Action<ContainerItemInstance> CurrentContainerChanged;

        private void Awake()
        {
            ApplyCurrentContainer(refreshUI: false);
        }

        public void OpenContainer(IContainerizeItemInstance adapterInstance)
        {
            OpenContainer(adapterInstance as ContainerItemInstance);
        }

        public void OpenContainer(ContainerItemInstance container)
        {
            if (container == null)
                return;

            if (ReferenceEquals(CurrentContainer, container))
                return;

            if (CurrentContainer != null)
                _navigationStack.Add(CurrentContainer);

            CurrentContainer = container;
            ApplyCurrentContainer(refreshUI: true);
        }

        public void GoBack()
        {
            if (_navigationStack.Count == 0)
            {
                CloseCurrentContainer();
                return;
            }

            int lastIndex = _navigationStack.Count - 1;
            CurrentContainer = _navigationStack[lastIndex];
            _navigationStack.RemoveAt(lastIndex);
            ApplyCurrentContainer(refreshUI: true);
        }

        public void CloseCurrentContainer()
        {
            _navigationStack.Clear();
            CurrentContainer = null;
            ApplyCurrentContainer(refreshUI: true);
        }

        private void ApplyCurrentContainer(bool refreshUI)
        {
            if (_containerPanel != null)
                _containerPanel.SetActive(CurrentContainer != null);

            if (containerBinding != null)
                containerBinding.SetContainer(CurrentContainer, refreshUI);

            UpdateLabels();
            CurrentContainerChanged?.Invoke(CurrentContainer);
        }

        private void UpdateLabels()
        {
            if (_titleText != null)
                _titleText.text = CurrentContainer != null
                    ? CurrentContainer.GetItem()?.DisplayName ?? _emptyTitle
                    : _emptyTitle;

            if (_pathText == null)
                return;

            if (CurrentContainer == null)
            {
                _pathText.text = string.Empty;
                return;
            }

            var pathParts = new List<string>(_navigationStack.Count + 1);
            for (int i = 0; i < _navigationStack.Count; i++)
            {
                string part = _navigationStack[i]?.GetItem()?.DisplayName;
                if (!string.IsNullOrEmpty(part))
                    pathParts.Add(part);
            }

            string currentName = CurrentContainer.GetItem()?.DisplayName;
            if (!string.IsNullOrEmpty(currentName))
                pathParts.Add(currentName);

            _pathText.text = string.Join(" / ", pathParts);
        }
    }
}
