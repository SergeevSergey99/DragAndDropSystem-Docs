using DragAndDropSystem.Inventories;
using UnityEngine;

namespace DragAndDropSystem.ContextMenu
{
    [DisallowMultipleComponent]
    public class InventoryContextMenuViewBinder : MonoBehaviour
    {
        [SerializeField] private UniversalInventory _inventory;
        [SerializeField] private ContextMenuViewBase _viewPrefab;

        public UniversalInventory Inventory => _inventory;
        public ContextMenuViewBase ViewPrefab => _viewPrefab;

        private void Awake()
        {
            if (_inventory == null)
                _inventory = GetComponent<UniversalInventory>();
        }

        private void OnEnable()
        {
            if (_inventory == null)
                _inventory = GetComponent<UniversalInventory>();

            ContextMenuManager.AutoCreateInstance.RegisterViewBinder(this);
        }

        private void OnDisable()
        {
            if (ContextMenuManager.IsInstanceExist)
                ContextMenuManager.AutoCreateInstance.UnregisterViewBinder(this);
        }
    }
}
