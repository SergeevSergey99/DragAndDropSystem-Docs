using UnityEngine;
using UnityEngine.UI;
using UniversalDragAndDrop.Core;

namespace UniversalDragAndDrop.UI
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
            var image = ResolveImage();
            if (image == null)
                return;

            image.raycastTarget = false;
            image.sprite = placement?.Stack?.Icon;
            image.color = fallbackColor;
            image.preserveAspect = image.sprite != null;
            image.enabled = true;
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

        protected Image ResolveImage()
        {
            if (_image == null)
                _image = GetComponent<Image>();

            return _image;
        }
    }
}
