using UnityEngine;
using UDND.Inventories;

namespace UDND.UI
{
    [DisallowMultipleComponent]
    public class InventoryDragVisualBinder : MonoBehaviour
    {
        [SerializeField] private UniversalInventory _inventory;
        [SerializeField] private MonoBehaviour _dragVisualPrefab;

        public UniversalInventory Inventory => _inventory;
        public MonoBehaviour DragVisualPrefab => _dragVisualPrefab;

        private void Awake()
        {
            if (_inventory == null)
                _inventory = GetComponent<UniversalInventory>();
        }

        private void OnEnable()
        {
            if (_inventory == null)
                _inventory = GetComponent<UniversalInventory>();

            DragVisualPresenter.AutoCreateInstance.RegisterBinder(this);
        }

        private void OnDisable()
        {
            if (DragVisualPresenter.IsInstanceExist)
                DragVisualPresenter.AutoCreateInstance.UnregisterBinder(this);
        }
    }
}
