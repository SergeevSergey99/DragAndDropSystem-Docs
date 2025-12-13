using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;
using DragAndDropSystem.Tools;
#if ENABLE_REFLEX_DI
using Reflex.Attributes;
#endif
using UnityEngine;
using UnityEngine.EventSystems;

namespace DragAndDropSystem.World3D
{
    /// <summary>
    /// UI area for dropping items into the 3D world.
    /// Not bound to an inventory - simply spawns a prefab at the specified point
    /// and removes the item from the source inventory.
    /// Implements IItemDropHandler directly (no fake inventory wrapper needed).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class WorldDropZone : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IDropTarget, IItemDropHandler
    {
#if ENABLE_REFLEX_DI
        [Inject] private DragAndDropManager _dragManager;
#else
        private DragAndDropManager _dragManager => DragAndDropManager.Instance;
#endif

        [Header("Spawn Settings")]
        [SerializeField, Tooltip("Точка спавна предметов в мире")]
        private Transform _spawnPoint;

        [SerializeField, Tooltip("Добавить случайное смещение при спавне")]
        private bool _randomizePosition = true;

        [SerializeField, Tooltip("Радиус случайного смещения")]
        private float _randomRadius = 0.5f;

        [SerializeField, Tooltip("Добавить начальную силу (Rigidbody)")]
        private bool _applyForce = true;

        [SerializeField, Tooltip("Сила выбрасывания")]
        private float _throwForce = 5f;

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

            var draggedStack = _dragManager.CurrentContext?.DraggedStack;
            if (draggedStack == null || draggedStack.Item == null)
                return;

            // Проверяем, есть ли у предмета 3D представление
            _canAcceptCurrentItem = draggedStack.Item is IWorld3DAdapter adapter && adapter.WorldPrefab != null;

            // Добавляем себя в стек целей
            _dragManager.PushDropTarget(this);

            Extentions.DragAndDropLog($"<color=cyan>[WorldDropZone] Entered, canAccept={_canAcceptCurrentItem}</color>");
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_dragManager == null || !_dragManager.IsDragging)
                return;

            // Удаляем себя из стека целей
            _dragManager.PopDropTarget(this);

            _canAcceptCurrentItem = false;

            Extentions.DragAndDropLog("<color=cyan>[WorldDropZone] Exited</color>");
        }

        private void OnDisable()
        {
#if !ENABLE_REFLEX_DI
            if (!DragAndDropManager.IsInstanceExist) return;
#endif
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

        public IItemDropHandler GetDropHandler()
        {
            // WorldDropZone IS the handler - return this
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

        // ===== IItemDropHandler Implementation =====

        public bool CanAcceptDrop(DragContext context)
        {
            if (context?.DraggedStack?.Item == null)
                return false;

            // Check if item has 3D world representation
            bool canAccept = context.DraggedStack.Item is IWorld3DAdapter adapter && adapter.WorldPrefab != null;

            Extentions.DragAndDropLog($"<color=cyan>[WorldDropZone] CanAcceptDrop: {canAccept}</color>");
            return canAccept;
        }

        public DropResult HandleDrop(DragContext context)
        {
            if (context?.DraggedStack == null)
            {
                return DropResult.Failed("Invalid drag context");
            }

            var stack = context.DraggedStack;
            var sourceSlot = context.SourceSlot;
            int amountToSpawn = stack.Count;

            if (!SpawnItemInWorld(stack))
            {
                return DropResult.Failed("Failed to spawn item in world");
            }

            // Remove items from source slot (the dragged stack is a copy, source slot still has items)
            if (sourceSlot?.Stack != null && !sourceSlot.Stack.IsEmpty)
            {
                sourceSlot.Stack.RemoveFromStack(amountToSpawn);
                sourceSlot.UpdateVisuals();
                Extentions.DragAndDropLog($"<color=green>[WorldDropZone] Removed {amountToSpawn} items from source slot {sourceSlot.Index}</color>");
            }

            return DropResult.Succeeded(
                item: stack.Item,
                amount: amountToSpawn,
                targetSlot: null,
                targetInventory: null);
        }

        /// <summary>
        /// Spawn item in world.
        /// Called from HandleDrop.
        /// </summary>
        private bool SpawnItemInWorld(ItemStack stack)
        {
            if (stack == null || stack.IsEmpty || stack.Item == null)
            {
                Extentions.DragAndDropLog("<color=red>[WorldDropZone] Cannot spawn: invalid stack</color>");
                return false;
            }

            // Проверяем, есть ли у предмета 3D префаб
            if (stack.Item is not IWorld3DAdapter adapter)
            {
                Extentions.DragAndDropLog($"<color=red>[WorldDropZone] Item {stack.Item.DisplayName} has no world prefab</color>");
                return false;
            }

            GameObject prefab = adapter.WorldPrefab;
            if (prefab == null)
            {
                Extentions.DragAndDropLog($"<color=red>[WorldDropZone] World prefab is null for {stack.Item.DisplayName}</color>");
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
                worldItem.Initialize(stack.Item, 1);

                // Применяем силу если есть Rigidbody
                if (_applyForce)
                {
                    var rb = spawnedObject.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        Vector3 forceDirection = (_spawnPoint != null ? _spawnPoint.forward : Vector3.forward) + Vector3.up * 0.5f;
                        rb.AddForce(forceDirection.normalized * _throwForce, ForceMode.Impulse);
                    }
                }

                // Небольшое смещение для следующего предмета
                if (_randomizePosition)
                {
                    Vector2 offsetPerItem = Random.insideUnitCircle * 0.1f;
                    spawnPosition += new Vector3(offsetPerItem.x, 0f, offsetPerItem.y);
                }
            }

            Extentions.DragAndDropLog($"<color=green>[WorldDropZone] Spawned {stack.Count}x {stack.Item.DisplayName} in world</color>");

            // Remove items from stack (they're now in the world)
            // Note: The source slot will be updated by DragAndDropManager after HandleDrop
            stack.RemoveFromStack(stack.Count);

            return true;
        }
    }
}
