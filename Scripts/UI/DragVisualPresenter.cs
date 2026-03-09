using System.Collections.Generic;
using CodeUtils;
using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using UnityEngine;
using Extentions = DragAndDropSystem.Tools.Extentions;

namespace DragAndDropSystem.UI
{
    [DisallowMultipleComponent]
    public class DragVisualPresenter : MonoSingleton<DragVisualPresenter>
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private DefaultDragVisual _defaultDragVisualPrefab;
        [SerializeField] private Transform _visualContainer;

        private readonly Dictionary<MonoBehaviour, IDragVisual> _visualCache = new Dictionary<MonoBehaviour, IDragVisual>();
        private readonly Dictionary<UniversalInventory, InventoryDragVisualBinder> _bindersByInventory = new Dictionary<UniversalInventory, InventoryDragVisualBinder>();

        private IDragVisual _defaultVisualInstance;
        private IDragVisual _activeVisual;
        private bool _subscribed;

        public Canvas PresentationCanvas => _canvas;
        public Transform VisualContainer => _visualContainer != null ? _visualContainer : _canvas != null ? _canvas.transform : transform;

        protected override void Init()
        {
            base.Init();

            if (_canvas != null)
                _canvas.worldCamera = Camera.main;

            SubscribeToManager();
        }

        protected override void DeInit()
        {
            UnsubscribeFromManager();
            base.DeInit();
        }

        private void Update()
        {
            if (_activeVisual != null && DragAndDropManager.IsInstanceExist && DragAndDropManager.Instance.IsDragging)
                _activeVisual.UpdatePosition(GetMousePosition());
        }

        public void RegisterBinder(InventoryDragVisualBinder binder)
        {
            if (binder == null || binder.Inventory == null)
                return;

            _bindersByInventory[binder.Inventory] = binder;
        }

        public void UnregisterBinder(InventoryDragVisualBinder binder)
        {
            if (binder == null || binder.Inventory == null)
                return;

            if (_bindersByInventory.TryGetValue(binder.Inventory, out var existing) && existing == binder)
                _bindersByInventory.Remove(binder.Inventory);
        }

        public MonoBehaviour ResolveVisualPrefab(IInventory inventory)
        {
            if (inventory is UniversalInventory universalInventory &&
                _bindersByInventory.TryGetValue(universalInventory, out var binder) &&
                binder != null &&
                binder.DragVisualPrefab != null)
            {
                return binder.DragVisualPrefab;
            }

            return _defaultDragVisualPrefab;
        }

        private void SubscribeToManager()
        {
            if (_subscribed)
                return;

            var manager = DragAndDropManager.Instance;
            manager.OnDragStarted += HandleDragStarted;
            manager.OnDragCancelled += HandleDragFinished;
            manager.OnDropCompleted += HandleDragFinished;
            _subscribed = true;
        }

        private void UnsubscribeFromManager()
        {
            if (!_subscribed || !DragAndDropManager.IsInstanceExist)
                return;

            var manager = DragAndDropManager.Instance;
            manager.OnDragStarted -= HandleDragStarted;
            manager.OnDragCancelled -= HandleDragFinished;
            manager.OnDropCompleted -= HandleDragFinished;
            _subscribed = false;
        }

        private void HandleDragStarted(DragContext context)
        {
            HideActiveVisual();

            if (context?.Entries == null || context.Entries.Count == 0)
                return;

            var visual = GetDragVisual(context.Entries[0].SourceInventory);
            if (visual == null)
                return;

            _activeVisual = visual;
            _activeVisual.UpdatePosition(GetMousePosition());
            _activeVisual.Show(context.Entries);
        }

        private void HandleDragFinished(DragContext _)
        {
            HideActiveVisual();
        }

        private void HideActiveVisual()
        {
            if (_activeVisual == null)
                return;

            _activeVisual.Hide();
            _activeVisual = null;
        }

        private IDragVisual GetDragVisual(IInventory inventory)
        {
            var visualPrefab = ResolveVisualPrefab(inventory);
            if (visualPrefab == null)
                return null;

            if (!ReferenceEquals(visualPrefab, _defaultDragVisualPrefab))
            {
                if (_visualCache.TryGetValue(visualPrefab, out var cachedVisual))
                {
                    Extentions.DragAndDropLog($"<color=cyan>Using cached custom visual from {inventory?.GetType().Name ?? "UnknownInventory"}</color>");
                    return cachedVisual;
                }

                var visualInstance = InstantiateVisual(visualPrefab);
                if (visualInstance != null)
                {
                    _visualCache[visualPrefab] = visualInstance;
                    Extentions.DragAndDropLog($"<color=cyan>Created new custom visual from {inventory?.GetType().Name ?? "UnknownInventory"}</color>");
                    return visualInstance;
                }
            }

            if (_defaultVisualInstance == null && _defaultDragVisualPrefab != null)
            {
                _defaultVisualInstance = InstantiateVisual(_defaultDragVisualPrefab);
                Extentions.DragAndDropLog("<color=cyan>Created default drag visual</color>");
            }

            Extentions.DragAndDropLog("<color=cyan>Using default drag visual</color>");
            return _defaultVisualInstance;
        }

        private IDragVisual InstantiateVisual(MonoBehaviour prefab)
        {
            if (prefab == null)
                return null;

            var instance = Instantiate(prefab, VisualContainer);
            if (instance is IDragVisual dragVisual)
                return dragVisual;

            Debug.LogError($"Prefab {prefab.name} does not implement IDragVisual!");
            Destroy(instance.gameObject);
            return null;
        }

        private Vector3 GetMousePosition()
        {
            if (_canvas == null)
                return Input.mousePosition;

            if (_canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return Input.mousePosition;

            if (_canvas.renderMode == RenderMode.ScreenSpaceCamera || _canvas.renderMode == RenderMode.WorldSpace)
            {
                RectTransform canvasRect = _canvas.GetComponent<RectTransform>();
                Vector2 localPoint;

                Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceCamera ? _canvas.worldCamera : Camera.main;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, cam, out localPoint))
                    return canvasRect.TransformPoint(localPoint);
            }

            return Input.mousePosition;
        }
    }
}
