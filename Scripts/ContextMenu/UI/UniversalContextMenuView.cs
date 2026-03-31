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
            var slotRect = ctx.Slot.transform as RectTransform;
            if (slotRect != null)
            {
                var rt = transform as RectTransform;
                var canvas = rt.GetComponentInParent<Canvas>().rootCanvas;
                Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

                // Позиция центра слота в экранных пикселях
                Vector2 slotScreenPos = RectTransformUtility.WorldToScreenPoint(uiCamera, slotRect.position);

                // Конвертируем в локальные координаты родителя меню
                var parentRT = rt.parent as RectTransform;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRT, slotScreenPos, uiCamera, out Vector2 localPos);

                float halfSlotW = slotRect.rect.width * 0.5f;
                float halfMenuW = rt.rect.width * 0.5f;

                if (slotScreenPos.x < Screen.width * 0.5f)
                {
                    // Слот слева — показываем меню справа от него
                    rt.anchoredPosition = new Vector2(localPos.x + halfSlotW + halfMenuW, localPos.y);
                }
                else
                {
                    // Слот справа — показываем меню слева от него
                    rt.anchoredPosition = new Vector2(localPos.x - halfSlotW - halfMenuW, localPos.y);
                }
            }
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
        }

        public override void Hide()
        {
            gameObject.SetActive(false);
            
            if (slotInputAdapter != null)
            {
                slotInputAdapter.navigation = lastSlotNavigation;
            }
        }
    }
}