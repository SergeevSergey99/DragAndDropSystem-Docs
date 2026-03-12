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
                label.text = ctx.Slot.Stack.Item.DisplayName;
                label.gameObject.SetActive(true);
            }
            catch (Exception e)
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
                
                newNavigation.selectOnUp = entryViews[0].Selectable;
                newNavigation.selectOnDown = entryViews[entries.Count - 1].Selectable;
                
                slotInputAdapter.navigation = newNavigation;
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