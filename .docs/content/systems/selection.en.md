# Selection

The selection system lets the player select several items and perform one action with them: drag a group, clear selection, select a range, or select every item in an inventory.

## Selection Operations

| Action | What happens |
|---|---|
| Click | Clears the previous selection and selects one item |
| Ctrl + click | Adds the item to the selection or removes it |
| Shift + click | Selects the range from the last selected slot to the current slot |
| Ctrl + A | Selects all non-empty slots in the current inventory |
| Escape | Clears selection |

Empty slots are usually ignored by selection operations. This prevents accidental selection of empty cells instead of items.

## Group Drag

When the player drags a selected item, the system transfers the selected group.

Important behavior:

- each selected slot is transferred separately
- the target slot is used as the starting point for placement search
- items that find a valid place are transferred
- items that do not fit or fail validation stay where they are
- already transferred items are not rolled back because a later item failed

If partial transfer is enabled, only the amount that fits can move from a stack.

Swap is not used for group transfer. The group is placed into free or compatible slots.

After a successful group transfer, selection is cleared automatically.

## Multiple Inventories

By default, items can be selected from multiple inventories if your selection operations allow it.

If you do not need this scenario, enable `_restrictToSameInventory`. Then a group is built from one inventory only.

## Setup

1. Add `SelectionManager` to the scene. Usually one instance per scene is enough.
2. Add `SlotSelectionView` to the slot prefab. It displays the selected-state highlight.
3. Configure selection operations through input:
   - `SelectionSlotAction` in `InteractionBindingsProfile` for Ctrl + click and Shift + click.
   - `InputActionSelectionTrigger` for hotkeys such as Ctrl + A and Escape.
   - `ButtonSelectionTrigger` for UI buttons like "Select All" and "Clear Selection".

## What To Check

| Symptom | Check |
|---|---|
| Item is not added to selection | Is there a `SelectionManager` in the scene |
| No highlight is visible | Is `SlotSelectionView` on the slot prefab |
| Ctrl/Shift do not work | Are the required `SelectionSlotAction` entries configured in the input profile |
| Ctrl + A or Escape do not work | Is `InputActionSelectionTrigger` or another trigger connected |
| The whole group does not transfer | Is there enough space in the target inventory and do rules pass for each item |

## Class Reference

| Class | Role |
|---|---|
| `SelectionManager` | Stores current selection |
| `SelectionContext` | Selection state at the moment it is read |
| `SlotSelectionView` | Highlights a slot when it is selected |
| `SelectionOperationBase` | Base class for selection operations |
| `ClearAndSelectOperation` | Normal click: clear all and select one slot |
| `ToggleFilledSlotOperation` | Ctrl + click: toggle a non-empty slot |
| `RangeSelectOperation` | Shift + click: select a range |
| `SelectAllOperation` | Ctrl + A: select all non-empty slots |
| `ClearSelectionOperation` | Escape: clear selection |
| `SelectByConditionOperation` | Select by predicate |
| `SelectionSlotAction` | Selection action for pointer binding |
| `StartMultiDragAction` | Starts dragging the selected group |
| `ButtonSelectionTrigger` | Runs a selection operation from a UI Button |
| `InputActionSelectionTrigger` | Runs a selection operation from an Input System Action |
