using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Inventories;
using UniversalDragAndDrop.Selection;
using UniversalDragAndDrop.Slots;
using UniversalDragAndDrop.UI;
using UnityEngine.EventSystems;

namespace UniversalDragAndDrop.Interaction
{
    public enum InteractionInputKind
    {
        Pointer = 0,
        Key = 1,
        InputAction = 2
    }

    public sealed class RuntimeInteractionSnapshot
    {
        // Empty snapshot used as a safe default when no interaction state is available.
        public static readonly RuntimeInteractionSnapshot Empty = new RuntimeInteractionSnapshot(
            inputKind: InteractionInputKind.Pointer,
            inventory: null,
            resolvedBaseSlot: null,
            focusedBaseSlot: null,
            hoveredBaseSlot: null,
            pressedBaseSlot: null,
            dropArea: null,
            activeFocusSource: FocusSource.None,
            pointerEventData: null,
            pointerPhase: null,
            keyPhase: null,
            inputActionPhase: null,
            nativeInputContext: null,
            isDragging: false,
            currentDragContext: null,
            selection: SelectionContext.Empty);

        public RuntimeInteractionSnapshot(
            InteractionInputKind inputKind,
            UniversalInventory inventory,
            BaseSlot resolvedBaseSlot,
            BaseSlot focusedBaseSlot,
            BaseSlot hoveredBaseSlot,
            BaseSlot pressedBaseSlot,
            InventoryDropArea dropArea,
            FocusSource activeFocusSource,
            PointerEventData pointerEventData,
            PointerTriggerPhase? pointerPhase,
            KeyTriggerPhase? keyPhase,
            TriggerPhaseEnum? inputActionPhase,
            object nativeInputContext,
            bool isDragging,
            DragContext currentDragContext,
            SelectionContext selection)
        {
            InputKind = inputKind;
            Inventory = inventory;
            ResolvedBaseSlot = resolvedBaseSlot;
            FocusedBaseSlot = focusedBaseSlot;
            HoveredBaseSlot = hoveredBaseSlot;
            PressedBaseSlot = pressedBaseSlot;
            DropArea = dropArea;
            ActiveFocusSource = activeFocusSource;
            PointerEventData = pointerEventData;
            PointerPhase = pointerPhase;
            KeyPhase = keyPhase;
            InputActionPhase = inputActionPhase;
            NativeInputContext = nativeInputContext;
            IsDragging = isDragging;
            CurrentDragContext = currentDragContext;
            Selection = selection ?? SelectionContext.Empty;
        }

        // Which input pipeline triggered this snapshot: pointer, legacy key binding, or InputAction.
        public InteractionInputKind InputKind { get; }

        // Inventory currently being routed. May be null for global/default-profile input.
        public UniversalInventory Inventory { get; }

        // Slot chosen as the concrete action target for this execution.
        public BaseSlot ResolvedBaseSlot { get; }

        // Slot currently focused by navigation.
        public BaseSlot FocusedBaseSlot { get; }

        // Slot currently hovered by the pointer.
        public BaseSlot HoveredBaseSlot { get; }

        // Slot that received the active press and is used for click/hold resolution.
        public BaseSlot PressedBaseSlot { get; }

        // Focused drop area target when execution is happening outside a slot.
        public InventoryDropArea DropArea { get; }

        // Last active focus source tracked by the router.
        public FocusSource ActiveFocusSource { get; }

        // Native pointer event payload for pointer-driven execution paths.
        public PointerEventData PointerEventData { get; }

        // Concrete pointer phase that triggered this snapshot.
        public PointerTriggerPhase? PointerPhase { get; }

        // Concrete key-binding phase that triggered this snapshot.
        public KeyTriggerPhase? KeyPhase { get; }

        // Concrete InputAction callback phase that triggered this snapshot.
        public TriggerPhaseEnum? InputActionPhase { get; }

        // Optional raw source payload from the originating input system callback.
        public object NativeInputContext { get; }

        // Whether a drag operation is active at the time of execution.
        public bool IsDragging { get; }

        // Current drag context owned by DragAndDropManager, if any.
        public DragContext CurrentDragContext { get; }

        // Current multi-selection snapshot from SelectionManager.
        public SelectionContext Selection { get; }

        public bool HasSlot => ResolvedBaseSlot != null;
        public bool HasDropArea => DropArea != null;
        public bool HasConcreteTarget => HasSlot || HasDropArea;

        public RuntimeInteractionSnapshot WithKeyPhase(KeyTriggerPhase? keyPhase)
        {
            return new RuntimeInteractionSnapshot(
                inputKind: InputKind,
                inventory: Inventory,
                resolvedBaseSlot: ResolvedBaseSlot,
                focusedBaseSlot: FocusedBaseSlot,
                hoveredBaseSlot: HoveredBaseSlot,
                pressedBaseSlot: PressedBaseSlot,
                dropArea: DropArea,
                activeFocusSource: ActiveFocusSource,
                pointerEventData: PointerEventData,
                pointerPhase: PointerPhase,
                keyPhase: keyPhase,
                inputActionPhase: InputActionPhase,
                nativeInputContext: NativeInputContext,
                isDragging: IsDragging,
                currentDragContext: CurrentDragContext,
                selection: Selection);
        }
    }
}
