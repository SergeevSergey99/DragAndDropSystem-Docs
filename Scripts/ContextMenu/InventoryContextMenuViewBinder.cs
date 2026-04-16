using UnityEngine;
using UniversalDragAndDrop.Inventories;

namespace UniversalDragAndDrop.ContextMenu
{
    [DisallowMultipleComponent]
    public class InventoryContextMenuViewBinder : MonoBehaviour
    {
        [SerializeField] private UniversalInventory _inventory;
        [SerializeField] private ContextMenuViewBase _viewPrefab;

        public UniversalInventory Inventory => _inventory;
        public ContextMenuViewBase ViewPrefab => _viewPrefab;

        private void OnEnable()
        {
            if (_inventory == null)
                _inventory = GetComponent<UniversalInventory>();

            ContextMenuManager.AutoCreateInstance.RegisterViewBinder(this);
        }

        private void OnDisable()
        {
            if (ContextMenuManager.IsInstanceExist)
                ContextMenuManager.Instance.UnregisterViewBinder(this);
        }
    }
}
