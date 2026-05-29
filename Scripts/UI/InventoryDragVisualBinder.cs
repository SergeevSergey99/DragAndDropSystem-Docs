using UnityEngine;
using UDND.Inventories;

namespace UDND.UI
{
    [DisallowMultipleComponent]
    public class InventoryDragVisualBinder : MonoBehaviour
    {
        [SerializeField] private BaseInventory _inventory;
        [SerializeField] private MonoBehaviour _dragVisualPrefab;

        public IInventory Inventory => _inventory;
        public MonoBehaviour DragVisualPrefab => _dragVisualPrefab;

        private void Awake()
        {
            if (_inventory == null)
                _inventory = GetComponent<BaseInventory>();
        }

        private void OnEnable()
        {
            if (_inventory == null)
                _inventory = GetComponent<BaseInventory>();

            DragVisualPresenter.AutoCreateInstance.RegisterBinder(this);
        }

        private void OnDisable()
        {
            if (DragVisualPresenter.IsInstanceExist)
                DragVisualPresenter.AutoCreateInstance.UnregisterBinder(this);
        }
    }
}
