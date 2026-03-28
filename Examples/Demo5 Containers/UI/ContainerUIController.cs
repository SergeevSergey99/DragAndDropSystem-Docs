using UnityEngine;
using UnityEngine.UI;

namespace DragAndDropSystem.Examples.Containers
{
    /// <summary>
    /// Медиатор: открывает/закрывает панель контейнера.
    /// Один открытый контейнер за раз.
    /// </summary>
    public class ContainerUIController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject _containerPanel;
        [SerializeField] private Text _containerTitle;

        [Header("DataBinding")]
        [SerializeField] private ContainerInventoryDataBinding _containerBinding;

        [Header("Settings")]
        [SerializeField] private KeyCode _closeKey = KeyCode.Escape;

        private ItemInstance _currentContainer;
        private int _currentDepth;

        public ItemInstance CurrentContainer => _currentContainer;

        /// <summary>
        /// Открыть контейнер. Если уже открыт другой — закрывает предыдущий.
        /// </summary>
        public void OpenContainer(ItemInstance container, int depth = -1)
        {
            if (container == null || !container.IsContainer)
                return;

            // Вычислить глубину
            if (depth < 0)
            {
                // Если открываем из уже открытого контейнера — +1
                depth = _currentContainer != null ? _currentDepth + 1 : 0;
            }

            // Закрыть предыдущий
            if (_currentContainer != null)
                CloseContainerInternal();

            _currentContainer = container;
            _currentDepth = depth;

            // Обновить UI
            if (_containerTitle != null)
                _containerTitle.text = container.ItemSO.DisplayName;

            _containerBinding.BindToContainer(container, depth);
            _containerPanel.SetActive(true);
        }

        /// <summary>
        /// Закрыть текущий контейнер.
        /// </summary>
        public void CloseContainer()
        {
            if (_currentContainer == null)
                return;

            CloseContainerInternal();
        }

        private void CloseContainerInternal()
        {
            _containerBinding.BindToContainer(null);
            _containerPanel.SetActive(false);
            _currentContainer = null;
            _currentDepth = 0;
        }

        private void Update()
        {
            if (_currentContainer != null && Input.GetKeyDown(_closeKey))
                CloseContainer();
        }
    }
}
