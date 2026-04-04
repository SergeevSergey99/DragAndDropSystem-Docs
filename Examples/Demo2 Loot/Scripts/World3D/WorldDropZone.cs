using DragAndDropSystem.Core;
using DragAndDropSystem.Tools;
using UnityEngine;

namespace DragAndDropSystem.Examples.Loot
{
    /// <summary>
    /// UI-зона для дропа предметов в 3D мир.
    /// Спавнит префаб в указанной точке, удаление из источника — автоматически (DropAreaBase).
    /// </summary>
    public class WorldDropZone : DropAreaBase
    {
        [Header("Spawn Settings")]
        [SerializeField, Tooltip("Точка спавна предметов в мире")]
        private Transform _spawnPoint;

        [SerializeField, Tooltip("Добавить случайное смещение при спавне")]
        private bool _randomizePosition = true;

        [SerializeField, Tooltip("Радиус случайного смещения")]
        private float _randomRadius = 1.5f;

        [Header("Visual Feedback")]
        [SerializeField, Tooltip("Подсветка зоны при наведении")]
        private UnityEngine.UI.Image _areaHighlight;

        [SerializeField] private Color _highlightColorValid = new Color(0f, 1f, 0f, 0.3f);
        [SerializeField] private Color _highlightColorInvalid = new Color(1f, 0f, 0f, 0.3f);
        [SerializeField] private Color _normalColor = new Color(1f, 1f, 1f, 0f);

        protected override bool CanAcceptEntry(DragEntry entry)
        {
            if (entry.Stack == null || entry.Stack.IsEmpty || entry.Stack.Adapters == null)
                return false;

            for (int i = 0; i < entry.Stack.Adapters.Count; i++)
            {
                if (entry.Stack.Adapters[i] is not ItemAdapterSoWith3DAdapter adapter || adapter.WorldPrefab == null)
                    return false;
            }
            return true;
        }

        protected override bool ProcessEntry(ItemStack stack, DragEntry entry)
        {
            if (stack == null || stack.IsEmpty || stack.Adapters == null || stack.Adapters.Count == 0)
                return false;

            Vector3 spawnPos = _spawnPoint != null ? _spawnPoint.position : transform.position;

            for (int i = 0; i < stack.Adapters.Count; i++)
            {
                var itemAdapter = stack.Adapters[i];
                if (itemAdapter is not ItemAdapterSoWith3DAdapter adapter || adapter.WorldPrefab == null)
                {
                    Extensions.DragAndDropLog($"<color=red>[WorldDropZone] Adapter at index {i} has no world prefab</color>");
                    return false;
                }

                var offset = Vector3.zero;
                if (_randomizePosition)
                {
                    Vector2 rnd = Random.insideUnitCircle * _randomRadius;
                    offset = new Vector3(rnd.x, 0f, rnd.y);
                }

                GameObject spawned = Instantiate(adapter.WorldPrefab, spawnPos + offset, Quaternion.identity);
                var worldItem = spawned.GetComponent<WorldItem>();
                if (worldItem == null)
                    worldItem = spawned.AddComponent<WorldItem>();
                
                worldItem.Initialize(adapter.item);
            }

            Extensions.DragAndDropLog($"<color=green>[WorldDropZone] Spawned {stack.Count}x {stack.DisplayName} in world</color>");
            return true;
        }

        protected override void OnHighlightChanged(bool highlighted, bool canAccept)
        {
            if (_areaHighlight == null)
                return;

            _areaHighlight.color = highlighted
                ? (canAccept ? _highlightColorValid : _highlightColorInvalid)
                : _normalColor;
        }
    }
}
