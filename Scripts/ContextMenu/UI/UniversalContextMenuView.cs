using System;
using System.Collections.Generic;
using DragAndDropSystem.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace DragAndDropSystem.ContextMenu.UI
{
    public class UniversalContextMenuView : ContextMenuViewBase
    {
        [SerializeField] private TMPro.TextMeshProUGUI label;
        [SerializeField] private Transform entriesContainer;
        [SerializeField] private ContextMenuEntryView entryViewPrefab;
        
        private List<ContextMenuEntryView> entryViews = new();
        
        SlotInputAdapter slotInputAdapter;
        private Navigation lastSlotNavigation;
        public override void Show(IReadOnlyList<IContextMenuEntry> entries, ContextMenuContext ctx)
        {
            gameObject.SetActive(true);
            try
            {
                label.text = ctx.Slot.Stack.DisplayName;
                label.gameObject.SetActive(true);
            }
            catch (Exception)
            {
                label.gameObject.SetActive(false);
            }

            for (int i = entryViews.Count; i < entries.Count; i++)
            {
                var entryView = Instantiate(entryViewPrefab, entriesContainer);
                entryViews.Add(entryView);
            }

            for (int i = 0; i < entryViews.Count; i++)
            {
                if (i < entries.Count)
                {
                    entryViews[i].gameObject.SetActive(true);
                    entryViews[i].Setup(entries[i], ctx);
                }
                else
                {
                    entryViews[i].gameObject.SetActive(false);
                }
            }
            
            slotInputAdapter = ctx.Slot.GetComponent<SlotInputAdapter>();
            if (slotInputAdapter != null)
            {
                lastSlotNavigation = slotInputAdapter.navigation;
                var newNavigation = slotInputAdapter.navigation;
                newNavigation.mode = Navigation.Mode.Explicit;
                
                newNavigation.selectOnLeft = entryViews[0].Selectable;
                newNavigation.selectOnDown = entryViews[0].Selectable;
                newNavigation.selectOnUp = entryViews[entries.Count - 1].Selectable;
                newNavigation.selectOnRight = entryViews[entries.Count - 1].Selectable;
                
                slotInputAdapter.navigation = newNavigation;
            }

            // Навигация между entryViews: вверх/вниз — соседние записи (с wrap-around),
            // влево/вправо — исходный слот
            for (int i = 0; i < entries.Count; i++)
            {
                var selectable = entryViews[i].Selectable;
                var nav = selectable.navigation;
                nav.mode = Navigation.Mode.Explicit;

                nav.selectOnUp    = entryViews[(i - 1 + entries.Count) % entries.Count].Selectable;
                nav.selectOnDown  = entryViews[(i + 1) % entries.Count].Selectable;
                nav.selectOnLeft  = slotInputAdapter;
                nav.selectOnRight = slotInputAdapter;

                selectable.navigation = nav;
            }

            PositionMenu(ctx);
        }

        public override void Hide()
        {
            gameObject.SetActive(false);
            
            if (slotInputAdapter != null)
            {
                slotInputAdapter.navigation = lastSlotNavigation;
            }
        }

        private void PositionMenu(ContextMenuContext ctx)
        {
            var rt = transform as RectTransform;
            var parentRT = rt != null ? rt.parent as RectTransform : null;
            if (rt == null || parentRT == null)
                return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            Camera targetCamera = GetCanvasCamera(rt);
            Vector2 fallbackScreenPos = ctx.ScreenPosition;

            if (!TryGetSlotBoundsInParent(ctx.Slot?.transform as RectTransform, parentRT, targetCamera, out var slotCenterLocal, out float halfSlotWidthLocal, out var slotCenterScreenPos))
            {
                slotCenterScreenPos = fallbackScreenPos;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRT, fallbackScreenPos, targetCamera, out slotCenterLocal))
                    return;

                halfSlotWidthLocal = 0f;
            }

            GetRectBoundsInParent(rt, parentRT, targetCamera, targetCamera, out var menuMinLocal, out var menuMaxLocal);
            float menuWidthLocal = menuMaxLocal.x - menuMinLocal.x;
            float menuHeightLocal = menuMaxLocal.y - menuMinLocal.y;
            float halfMenuWidthLocal = menuWidthLocal * 0.5f;

            float centerX = slotCenterScreenPos.x < Screen.width * 0.5f
                ? slotCenterLocal.x + halfSlotWidthLocal + halfMenuWidthLocal
                : slotCenterLocal.x - halfSlotWidthLocal - halfMenuWidthLocal;

            Vector2 menuCenterLocal = new Vector2(centerX, slotCenterLocal.y);
            rt.anchoredPosition = new Vector2(
                menuCenterLocal.x + (rt.pivot.x - 0.5f) * menuWidthLocal,
                menuCenterLocal.y + (rt.pivot.y - 0.5f) * menuHeightLocal);
        }

        private static bool TryGetSlotBoundsInParent(RectTransform slotRect, RectTransform parentRT, Camera targetCamera, out Vector2 centerLocal, out float halfWidthLocal, out Vector2 centerScreenPos)
        {
            centerLocal = default;
            halfWidthLocal = 0f;
            centerScreenPos = default;

            if (slotRect == null)
                return false;

            Camera sourceCamera = GetCanvasCamera(slotRect);
            GetRectBoundsInParent(slotRect, parentRT, sourceCamera, targetCamera, out var minLocal, out var maxLocal);

            centerLocal = (minLocal + maxLocal) * 0.5f;
            halfWidthLocal = (maxLocal.x - minLocal.x) * 0.5f;

            var worldCenter = slotRect.TransformPoint(slotRect.rect.center);
            centerScreenPos = RectTransformUtility.WorldToScreenPoint(sourceCamera, worldCenter);
            return true;
        }

        private static void GetRectBoundsInParent(RectTransform rect, RectTransform parentRT, Camera sourceCamera, Camera targetCamera, out Vector2 minLocal, out Vector2 maxLocal)
        {
            var worldCorners = new Vector3[4];
            rect.GetWorldCorners(worldCorners);

            minLocal = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            maxLocal = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            for (int i = 0; i < worldCorners.Length; i++)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(sourceCamera, worldCorners[i]);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRT, screenPoint, targetCamera, out var localPoint))
                    continue;

                minLocal = Vector2.Min(minLocal, localPoint);
                maxLocal = Vector2.Max(maxLocal, localPoint);
            }

            if (float.IsInfinity(minLocal.x) || float.IsInfinity(maxLocal.x))
            {
                minLocal = Vector2.zero;
                maxLocal = new Vector2(rect.rect.width, rect.rect.height);
            }
        }

        private static Camera GetCanvasCamera(Component component)
        {
            var canvas = component != null ? component.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
                return null;

            var rootCanvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
            return rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        }
    }
}
