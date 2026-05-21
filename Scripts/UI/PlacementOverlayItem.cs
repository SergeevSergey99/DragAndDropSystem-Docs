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

        public RectTransform RectTransform => transform as RectTransform;
        public Placement CurrentPlacement { get; private set; }
        public PlacementOverlayRenderState CurrentState { get; private set; }

        public void Render(Placement placement, PlacementOverlayRenderState state, Color fallbackColor)
        {
            CurrentPlacement = placement;
            CurrentState = state;

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
            if (_image == null)
                return;

            _image.raycastTarget = false;
            _image.sprite = placement?.Stack?.Icon;
            _image.color = fallbackColor;
            gameObject.SetActive(true);
        }

        protected virtual void RenderFilledAndDraggedFrom(Placement placement, Color fallbackColor)
        {
            gameObject.SetActive(false);
        }

        protected virtual void RenderFilledAndDraggedTo(Placement placement, Color fallbackColor)
        {
            RenderFilled(placement, fallbackColor);
        }
    }
}
