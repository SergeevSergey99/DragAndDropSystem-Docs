using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UDND.Interaction;
using UDND.Tools;

namespace UDND.ContextMenu.UI
{
    public class UniversalContextMenuView : ContextMenuViewBase
    {
        // Replace to TMP Support
        // [SerializeField] private TMPro.TextMeshProUGUI label;
        [SerializeField] private Text label;
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
                label.text = ctx.BaseSlot.Stack.DisplayName;
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
            
            slotInputAdapter = ctx.BaseSlot.GetComponent<SlotInputAdapter>();
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

            // Navigation between entryViews: up/down moves through neighboring entries (with wrap-around),
            // left/right returns to the source slot
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

            Camera targetCamera = Extensions.GetCanvasCamera(rt);
            Vector2 fallbackScreenPos = ctx.ScreenPosition;

            if (!Extensions.TryGetSlotBoundsInParent(ctx.BaseSlot?.transform as RectTransform, parentRT, targetCamera, out var slotCenterLocal, out float halfSlotWidthLocal, out var slotCenterScreenPos))
            {
                slotCenterScreenPos = fallbackScreenPos;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRT, fallbackScreenPos, targetCamera, out slotCenterLocal))
                    return;

                halfSlotWidthLocal = 0f;
            }

            Extensions.GetRectBoundsInParent(rt, parentRT, targetCamera, targetCamera, out var menuMinLocal, out var menuMaxLocal);
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
    }
}
