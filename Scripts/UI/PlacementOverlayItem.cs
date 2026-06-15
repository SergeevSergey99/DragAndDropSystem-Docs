using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UDND.Core;
using UDND.Slots;

namespace UDND.UI
{
    public enum PlacementOverlayRenderState
    {
        Filled,
        FilledAndDraggedFrom,
        FilledAndDraggedTo
    }

    /// <summary>
    /// Visual component used by PlacementOverlay for one shaped placement.
    /// Inherit from this class on overlay prefabs to customize placement rendering.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlacementOverlayItem : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [Header("Stack count (optional)")]
        [SerializeField] private RectTransform _countContainer;
        [SerializeField] private Text _countText;

        public RectTransform RectTransform => transform as RectTransform;
        public Placement CurrentPlacement { get; private set; }
        public PlacementOverlayRenderState CurrentState { get; private set; }
       
        private IReadOnlyList<BaseSlot> _coveredSlots => _parentOverlay.CollectCoveredSlots(CurrentPlacement);
        private float _rotation => _parentOverlay.GetRotation(CurrentPlacement);

        PlacementOverlay _parentOverlay;
        
        Vector2 startCounterAnchor;
        public void Init(PlacementOverlay overlay)
        {
            _parentOverlay = overlay;
            startCounterAnchor = _countContainer.anchoredPosition;
        }
        
        public void Render(
            Placement placement,
            PlacementOverlayRenderState state,
            Color fallbackColor)
        {
            CurrentPlacement = placement;
            CurrentState = state;
            _image.sprite = placement?.Stack?.Icon;
            _image.transform.localEulerAngles = new Vector3(0, 0, _rotation);

            switch (state)
            {
                case PlacementOverlayRenderState.FilledAndDraggedFrom:
                    RenderFilledAndDraggedFrom(placement, fallbackColor);
                    break;
                case PlacementOverlayRenderState.FilledAndDraggedTo:
                    RenderFilledAndDraggedTo(placement, fallbackColor);
                    break;
                default:
                    RenderFilled(placement, fallbackColor);
                    break;
            }
        }

        void RenderFilled(Placement placement, Color fallbackColor)
        {
            RenderCount(placement, placement?.Stack.Count ?? 0);

            if (_image == null)
                return;

            _image.raycastTarget = false;
            _image.color = fallbackColor;
            gameObject.SetActive(true);
        }

        void RenderFilledAndDraggedFrom(Placement placement, Color fallbackColor)
        {
            bool shouldShow = placement?.Stack.Count > 1;

            gameObject.SetActive(shouldShow);
            _countContainer.gameObject.SetActive(shouldShow);

            RenderCount(placement, placement?.Stack.Count - 1 ?? 0);
        }
        
        void RenderFilledAndDraggedTo(Placement placement, Color fallbackColor) => RenderFilled(placement, fallbackColor);

        /// <summary>Shows the placement stack count (when &gt; 1) on the count-bearing item only.</summary>
        void RenderCount(Placement placement, int count)
        {
            if (_countContainer == null)
                return;
            bool shouldShow = count > 1;
            
            
            _countContainer.gameObject.SetActive(shouldShow);

            if (!shouldShow)
                return;
            
            if (_countText != null)
                _countText.text = count.ToString();

            var lastSlot = _coveredSlots[^1];
            var contParent = _countContainer.parent;
            _countContainer.SetParent(lastSlot.transform, worldPositionStays: false);
            _countContainer.anchoredPosition = startCounterAnchor;
            _countContainer.SetParent(contParent, worldPositionStays: true);
        }
    }
}
