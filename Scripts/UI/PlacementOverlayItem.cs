using UnityEngine;
using UnityEngine.UI;
using UDND.Core;

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
        [SerializeField] private GameObject _countContainer;
        [SerializeField] private Text _countText;

        public RectTransform RectTransform => transform as RectTransform;
        public Placement CurrentPlacement { get; private set; }
        public PlacementOverlayRenderState CurrentState { get; private set; }

        /// <summary>
        /// Whether this item displays the placement stack count. The overlay sets it so the count is
        /// shown once per placement (on the anchor / spanning item), not on every covered cell.
        /// See ShapedStacking-Plan.md (C6).
        /// </summary>
        public bool ShowStackCount { get; set; } = true;

        public void Render(Placement placement, PlacementOverlayRenderState state, Color fallbackColor)
        {
            CurrentPlacement = placement;
            CurrentState = state;
            _image.sprite = placement?.Stack?.Icon;

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

        protected virtual void RenderFilled(Placement placement, Color fallbackColor)
        {
            RenderCount(placement);

            if (_image == null)
                return;

            _image.raycastTarget = false;
            _image.color = fallbackColor;
            gameObject.SetActive(true);
        }

        protected virtual void RenderFilledAndDraggedFrom(Placement placement, Color fallbackColor)
        {
            bool shouldShow = placement?.Stack.Count > 1;
            
            gameObject.SetActive(shouldShow);
            _countContainer.SetActive(ShowStackCount && shouldShow);
            
            
            if (shouldShow && _countText != null)
                _countText.text = (placement?.Stack.Count-1).ToString();
        }

        /// <summary>Shows the placement stack count (when &gt; 1) on the count-bearing item only.</summary>
        protected void RenderCount(Placement placement)
        {
            if (_countContainer == null)
                return;

            int count = placement?.Stack?.Count ?? 0;
            bool shouldShow = ShowStackCount && count > 1;
            _countContainer.SetActive(shouldShow);

            if (shouldShow && _countText != null)
                _countText.text = count.ToString();
        }

        protected virtual void RenderFilledAndDraggedTo(Placement placement, Color fallbackColor)
        {
            RenderFilled(placement, fallbackColor);
        }
    }
}
