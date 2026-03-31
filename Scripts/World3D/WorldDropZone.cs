using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;
using DragAndDropSystem.Tools;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DragAndDropSystem.World3D
{
    /// <summary>
    /// UI area for dropping items into the 3D world.
    /// Not bound to an inventory - simply spawns a prefab at the specified point
    /// and removes the itemAdapter from the source inventory.
    /// Implements IDropProcessor directly (no fake inventory wrapper needed).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class WorldDropZone : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IDropTarget, IDropProcessor
    {
        private DragAndDropManager _dragManager => DragAndDropManager.AutoCreateInstance;

        [Header("Spawn Settings")]
        [SerializeField, Tooltip("Точка спавна предметов в мире")]
        private Transform _spawnPoint;

        [SerializeField, Tooltip("Добавить случайное смещение при спавне")]
        private bool _randomizePosition = true;

        [SerializeField, Tooltip("Радиус случайного смещения")]
        private float _randomRadius = 0.5f;

        [Header("Visual Feedback")]
        [SerializeField, Tooltip("Подсветка зоны при наведении")]
        private UnityEngine.UI.Image _areaHighlight;

        [SerializeField] private Color _highlightColorValid = new Color(0f, 1f, 0f, 0.3f);
        [SerializeField] private Color _highlightColorInvalid = new Color(1f, 0f, 0f, 0.3f);
        [SerializeField] private Color _normalColor = new Color(1f, 1f, 1f, 0f);

        private bool _canAcceptCurrentItem;
        private bool _isHighlighted;

        private void OnValidate()
        {
            // Автоматически находим точку спавна
            if (_spawnPoint == null)
            {
                _spawnPoint = transform;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_dragManager == null || !_dragManager.IsDragging)
                return;

            var context = _dragManager.CurrentContext;
            if (context == null || context.Entries.Count == 0)
                return;

            var stack = context.Entries[0].Stack;
            if (stack == null || stack.ItemAdapter == null)
                return;

            // Проверяем, есть ли у предмета 3D представление
            _canAcceptCurrentItem = stack.ItemAdapter is IWorld3DAdapter adapter && adapter.WorldPrefab != null;

            // Добавляем себя в стек целей
            _dragManager.PushDropTarget(this);

            Extensions.DragAndDropLog($"<color=cyan>[WorldDropZone] Entered, canAccept={_canAcceptCurrentItem}</color>");
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_dragManager == null || !_dragManager.IsDragging)
                return;

            // Удаляем себя из стека целей
            _dragManager.PopDropTarget(this);

            _canAcceptCurrentItem = false;

            Extensions.DragAndDropLog("<color=cyan>[WorldDropZone] Exited</color>");
        }

        private void OnDisable()
        {
            if (!DragAndDropManager.IsInstanceExist) return;
            // Удаляем себя из стека при отключении
            if (_dragManager != null && _dragManager.IsDragging)
            {
                _dragManager.PopDropTarget(this);
            }

            HighlightArea(false, true);
        }

        /// <summary>
        /// Подсветить/снять подсветку области
        /// </summary>
        private void HighlightArea(bool highlight, bool valid)
        {
            if (_areaHighlight == null)
                return;

            _isHighlighted = highlight;

            if (highlight)
            {
                _areaHighlight.color = valid ? _highlightColorValid : _highlightColorInvalid;
            }
            else
            {
                _areaHighlight.color = _normalColor;
            }
        }

        // ===== IDropTarget Implementation =====

        public ISlot GetTargetSlot()
        {
            // WorldDropZone has no target slot - it's a special drop zone
            return null;
        }

        public IDropProcessor GetDropProcessor()
        {
            // WorldDropZone IS the processor - return this
            return this;
        }

        public void OnBecomeActiveTarget()
        {
            // Highlight area when becoming active target
            HighlightArea(true, _canAcceptCurrentItem);
        }

        public void OnBecomeInactiveTarget()
        {
            // Remove highlight when no longer active target
            HighlightArea(false, true);
        }

        // ===== IDropProcessor Implementation =====

        public bool CanAcceptDrop(DragContext context)
        {
            if (context == null || context.Entries.Count == 0)
                return false;

            // Check all entries have 3D world representation
            foreach (var entry in context.Entries)
            {
                if (entry.Stack?.ItemAdapter == null)
                    return false;

                if (entry.Stack.ItemAdapter is not IWorld3DAdapter adapter || adapter.WorldPrefab == null)
                {
                    Extensions.DragAndDropLog($"<color=cyan>[WorldDropZone] CanAcceptDrop: false (entry missing 3D adapter)</color>");
                    return false;
                }
            }

            Extensions.DragAndDropLog($"<color=cyan>[WorldDropZone] CanAcceptDrop: true</color>");
            return true;
        }

        public DropResult ProcessDrop(DragContext context)
        {
            if (context == null || context.Entries.Count == 0)
            {
                return DropResult.Failed("Invalid drag context");
            }

            int totalSpawned = 0;
            IItemAdapter lastItemAdapter = null;

            // Handle each entry
            foreach (var entry in context.Entries)
            {
                var stack = entry.Stack;
                var sourceSlot = entry.SourceSlot;

                if (stack == null || stack.IsEmpty)
                    continue;

                int amountToSpawn = stack.Count;

                if (!SpawnItemInWorld(stack))
                    continue;

                // Remove items from source slot
                if (sourceSlot?.Stack != null && !sourceSlot.Stack.IsEmpty)
                {
                    sourceSlot.Stack.RemoveFromStack(amountToSpawn);
                    sourceSlot.UpdateVisuals();
                    Extensions.DragAndDropLog($"<color=green>[WorldDropZone] Removed {amountToSpawn} items from source slot {sourceSlot.Index}</color>");
                }

                totalSpawned += amountToSpawn;
                lastItemAdapter = stack.ItemAdapter;
            }

            if (totalSpawned > 0)
            {
                return DropResult.Succeeded(
                    itemAdapter: lastItemAdapter,
                    amount: totalSpawned,
                    targetSlot: null,
                    targetInventory: null);
            }

            return DropResult.Failed("Failed to spawn items in world");
        }

        /// <summary>
        /// Spawn itemAdapter in world.
        /// Called from ProcessDrop.
        /// </summary>
        private bool SpawnItemInWorld(ItemStack stack)
        {
            if (stack == null || stack.IsEmpty || stack.ItemAdapter == null)
            {
                Extensions.DragAndDropLog("<color=red>[WorldDropZone] Cannot spawn: invalid stack</color>");
                return false;
            }

            // Проверяем, есть ли у предмета 3D префаб
            if (stack.ItemAdapter is not IWorld3DAdapter adapter)
            {
                Extensions.DragAndDropLog($"<color=red>[WorldDropZone] ItemAdapter {stack.DisplayName} has no world prefab</color>");
                return false;
            }

            GameObject prefab = adapter.WorldPrefab;
            if (prefab == null)
            {
                Extensions.DragAndDropLog($"<color=red>[WorldDropZone] World prefab is null for {stack.DisplayName}</color>");
                return false;
            }

            // Определяем позицию спавна
            Vector3 spawnPosition = _spawnPoint != null ? _spawnPoint.position : transform.position;

            if (_randomizePosition)
            {
                Vector2 randomOffset = Random.insideUnitCircle * _randomRadius;
                spawnPosition += new Vector3(randomOffset.x, 0f, randomOffset.y);
            }

            // Спавним предмет для каждого элемента в стаке
            for (int i = 0; i < stack.Count; i++)
            {
                GameObject spawnedObject = Instantiate(prefab, spawnPosition, Quaternion.identity);

                // Добавляем компонент WorldItem если его нет
                var worldItem = spawnedObject.GetComponent<WorldItem>();
                if (worldItem == null)
                {
                    worldItem = spawnedObject.AddComponent<WorldItem>();
                }
                worldItem.Initialize(stack.ItemAdapter, 1);

                // Небольшое смещение для следующего предмета
                if (_randomizePosition)
                {
                    Vector2 offsetPerItem = Random.insideUnitCircle * 0.1f;
                    spawnPosition += new Vector3(offsetPerItem.x, 0f, offsetPerItem.y);
                }
            }

            Extensions.DragAndDropLog($"<color=green>[WorldDropZone] Spawned {stack.Count}x {stack.DisplayName} in world</color>");

            return true;
        }
    }
}
