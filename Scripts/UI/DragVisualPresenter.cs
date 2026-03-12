using System.Collections.Generic;
using CodeUtils;
using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DragAndDropSystem.UI
{
    [DisallowMultipleComponent]
    public class DragVisualPresenter : MonoSingleton<DragVisualPresenter>
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private DefaultDragVisual _defaultDragVisualPrefab;
        [SerializeField] private Transform _visualContainer;
        [Header("Batch Layout")]
        [SerializeField, Min(0f)] private float _batchVisualRadius = 36f;
        [SerializeField, Min(0.1f)] private float _batchVisualMinScale = 0.65f;
        [SerializeField, Min(0f)] private float _batchVisualScaleStep = 0.08f;

        private readonly Dictionary<MonoBehaviour, List<VisualInstance>> _visualPool = new Dictionary<MonoBehaviour, List<VisualInstance>>();
        private readonly Dictionary<UniversalInventory, InventoryDragVisualBinder> _bindersByInventory = new Dictionary<UniversalInventory, InventoryDragVisualBinder>();

        private readonly List<ActiveVisual> _activeVisuals = new List<ActiveVisual>();
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
            if (_activeVisuals.Count > 0 && DragAndDropManager.IsInstanceExist && DragAndDropManager.Instance.IsDragging)
                UpdateActiveVisualPositions(GetDragAnchorScreenPosition());
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

            DragAndDropManager.OnDragStarted += HandleDragStarted;
            DragAndDropManager.OnDragCancelled += HandleDragFinished;
            DragAndDropManager.OnDropCompleted += HandleDragFinished;
            _subscribed = true;
        }

        private void UnsubscribeFromManager()
        {
            if (!_subscribed || !DragAndDropManager.IsInstanceExist)
                return;

            DragAndDropManager.OnDragStarted -= HandleDragStarted;
            DragAndDropManager.OnDragCancelled -= HandleDragFinished;
            DragAndDropManager.OnDropCompleted -= HandleDragFinished;
            _subscribed = false;
        }

        private void HandleDragStarted(DragContext context)
        {
            HideActiveVisuals();

            if (context?.Entries == null || context.Entries.Count == 0)
                return;

            var visualPrefab = ResolveVisualPrefab(context.Entries[0].SourceInventory);
            if (visualPrefab == null)
                return;

            for (int i = 0; i < context.Entries.Count; i++)
            {
                var visual = GetDragVisualInstance(visualPrefab, i);
                if (visual == null)
                    continue;

                var entryPayload = new List<DragEntry>(1) { context.Entries[i] };
                visual.View.Show(entryPayload);
                _activeVisuals.Add(new ActiveVisual(visual, i));
            }

            UpdateActiveVisualPositions(GetDragAnchorScreenPosition());
        }

        private void HandleDragFinished(DragContext _)
        {
            HideActiveVisuals();
        }

        private void HideActiveVisuals()
        {
            for (int i = 0; i < _activeVisuals.Count; i++)
            {
                var visual = _activeVisuals[i];
                if (visual.Instance == null)
                    continue;

                visual.Instance.View.Hide();
                visual.Instance.Transform.localScale = visual.Instance.BaseScale;
            }

            _activeVisuals.Clear();
        }

        private VisualInstance GetDragVisualInstance(MonoBehaviour visualPrefab, int index)
        {
            if (visualPrefab == null)
                return null;

            if (!_visualPool.TryGetValue(visualPrefab, out var pool))
            {
                pool = new List<VisualInstance>();
                _visualPool[visualPrefab] = pool;
            }

            while (pool.Count <= index)
            {
                var instance = InstantiateVisual(visualPrefab);
                if (instance == null)
                    return null;

                pool.Add(instance);
            }

            return pool[index];
        }

        private VisualInstance InstantiateVisual(MonoBehaviour prefab)
        {
            if (prefab == null)
                return null;

            var instance = Instantiate(prefab, VisualContainer);
            if (instance is IDragVisual dragVisual)
                return new VisualInstance(instance, dragVisual);

            Debug.LogError($"Prefab {prefab.name} does not implement IDragVisual!");
            Destroy(instance.gameObject);
            return null;
        }

        private void UpdateActiveVisualPositions(Vector2 anchorScreenPosition)
        {
            int total = _activeVisuals.Count;
            if (total == 0)
                return;

            float visualScale = Mathf.Max(_batchVisualMinScale, 1f - ((total - 1) * _batchVisualScaleStep));

            for (int i = 0; i < total; i++)
            {
                var activeVisual = _activeVisuals[i];
                if (activeVisual.Instance == null)
                    continue;

                Vector2 screenPosition = anchorScreenPosition + ResolveBatchOffset(activeVisual.Index, total);
                Vector3 position = ConvertScreenPointToPresentationPosition(screenPosition);
                activeVisual.Instance.Transform.localScale = new Vector3(
                    activeVisual.Instance.BaseScale.x * visualScale,
                    activeVisual.Instance.BaseScale.y * visualScale,
                    activeVisual.Instance.BaseScale.z);
                activeVisual.Instance.View.UpdatePosition(position);
            }
        }

        private Vector2 ResolveBatchOffset(int index, int total)
        {
            if (total <= 1 || _batchVisualRadius <= 0f)
                return Vector2.zero;

            float angleStep = 360f / total;
            float angleRadians = ((angleStep * index) - 90f) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians)) * _batchVisualRadius;
        }

        private Vector2 GetDragAnchorScreenPosition()
        {
            if (InputEventRouter.IsInstanceExist &&
                InputEventRouter.Instance.TryGetCurrentNavigationAnchor(out var selectedObject))
            {
                if (TryGetSelectableCenterScreenPoint(selectedObject, out var selectedPosition))
                    return selectedPosition;
            }

            return GetMouseScreenPosition();
        }

        private bool TryGetSelectableCenterScreenPoint(GameObject selectedObject, out Vector2 position)
        {
            position = default;
            if (selectedObject == null)
                return false;

            var selectedTransform = selectedObject.transform as RectTransform;
            if (selectedTransform == null)
                return false;

            var worldCenter = selectedTransform.TransformPoint(selectedTransform.rect.center);
            var camera = _canvas.worldCamera != null ? _canvas.worldCamera : Camera.main;
            position = RectTransformUtility.WorldToScreenPoint(
                _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? camera : null,
                worldCenter);
            return true;
        }

        private Vector3 ConvertScreenPointToPresentationPosition(Vector2 screenPoint)
        {
            if (_canvas == null)
                return screenPoint;

            if (_canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return screenPoint;

            if (_canvas.renderMode == RenderMode.ScreenSpaceCamera || _canvas.renderMode == RenderMode.WorldSpace)
            {
                RectTransform canvasRect = _canvas.GetComponent<RectTransform>();
                if (canvasRect == null)
                    return screenPoint;

                Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceCamera ? _canvas.worldCamera : Camera.main;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, cam, out var localPoint))
                    return canvasRect.TransformPoint(localPoint);
            }

            return screenPoint;
        }

        private Vector2 GetMouseScreenPosition()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            return mouse != null
                ? mouse.position.ReadValue()
                : Vector2.zero;
        }

        private sealed class VisualInstance
        {
            public VisualInstance(MonoBehaviour behaviour, IDragVisual view)
            {
                Behaviour = behaviour;
                View = view;
                BaseScale = behaviour != null ? behaviour.transform.localScale : Vector3.one;
            }

            public MonoBehaviour Behaviour { get; }
            public IDragVisual View { get; }
            public Vector3 BaseScale { get; }
            public Transform Transform => Behaviour != null ? Behaviour.transform : null;
        }

        private readonly struct ActiveVisual
        {
            public ActiveVisual(VisualInstance instance, int index)
            {
                Instance = instance;
                Index = index;
            }

            public VisualInstance Instance { get; }
            public int Index { get; }
        }
    }
}
