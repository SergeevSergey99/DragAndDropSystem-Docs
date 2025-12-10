using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Inventories;
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
    /// UI область для выбрасывания предметов в 3D мир
    /// Не привязана к инвентарю - просто спавнит префаб в указанной точке
    /// и удаляет предмет из исходного инвентаря
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class WorldDropZone : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IDropTarget
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
            _canAcceptCurrentItem = draggedStack.Item is IWorld3DAdapter adapter && adapter.HasWorldRepresentation;

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
            // У WorldDropZone нет целевого слота - это специальная зона для выбрасывания
            return null;
        }

        public IInventory GetTargetInventory()
        {
            // У WorldDropZone нет инвентаря - это специальный виртуальный "инвентарь"
            // Возвращаем специальный маркер (можно создать DummyInventory если нужно)
            return new WorldDropInventory(this);
        }

        public void OnBecomeActiveTarget()
        {
            // Подсвечиваем область когда становимся активной целью
            HighlightArea(true, _canAcceptCurrentItem);
        }

        public void OnBecomeInactiveTarget()
        {
            // Снимаем подсветку когда перестаём быть активной целью
            HighlightArea(false, true);
        }

        /// <summary>
        /// Выбросить предмет в мир
        /// Вызывается из WorldDropInventory при TryAddToSlot
        /// </summary>
        public bool SpawnItemInWorld(ItemStack stack, IInventory sourceInventory, int sourceSlotIndex)
        {
            if (stack == null || stack.IsEmpty || stack.Item == null)
            {
                Extentions.DragAndDropLog("<color=red>[WorldDropZone] Cannot spawn: invalid stack</color>");
                return false;
            }

            // Проверяем, есть ли у предмета 3D префаб
            if (!(stack.Item is IWorld3DAdapter adapter) || !adapter.HasWorldRepresentation)
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

            // Удаляем весь стак (он теперь в мире)
            stack.RemoveFromStack(stack.Count);

            return true;
        }

        /// <summary>
        /// Виртуальный инвентарь для обработки drop операций в WorldDropZone
        /// Делегирует спавн обратно в WorldDropZone
        /// </summary>
        private class WorldDropInventory : IInventory
        {
            private readonly WorldDropZone _owner;

            public WorldDropInventory(WorldDropZone owner)
            {
                _owner = owner;
            }

            // Минимальная реализация IInventory для совместимости с DragAndDropManager
            public IReadOnlyList<ISlot> Slots => new List<ISlot>(); // Пустой список
            public int SlotCount => 0;
            public InventoryDataBindingBase DataBinding { get; }

            public ISlot GetSlot(int index) => null;
            public bool Contains(IInventoryItem item) => false;
            public int GetItemCount(IInventoryItem item) => 0;
            public void UpdateAllVisuals() { }
            public int GetDragAmount(ISlot slot) => 0;

            public bool TryAddItem(IInventoryItem item, int count = 1, int targetSlotIndex = -1)
            {
                var stack = new ItemStack(item, count);
                return TryAddStack(stack, targetSlotIndex);
            }

            public bool TryAddStack(ItemStack stack, int targetSlotIndex = -1)
            {
                // Делегируем спавн обратно в WorldDropZone
                return _owner.SpawnItemInWorld(stack, null, -1);
            }

            public bool TryAddToSlot(
                ItemStack stack,
                ISlot targetSlot,
                IInventory sourceInventory = null,
                int sourceSlotIndex = -1,
                SlotOperationContext operationContext = null)
            {
                // Делегируем спавн обратно в WorldDropZone
                return _owner.SpawnItemInWorld(stack, sourceInventory, sourceSlotIndex);
            }

            public int GetAcceptableCount(IInventoryItem item, int desiredCount) => int.MaxValue;

            public bool TryRemoveItem(IInventoryItem item, int count = 1, int sourceSlotIndex = -1)
            {
                return false; // Нельзя удалить из мира через эту зону
            }
        }
    }
}
