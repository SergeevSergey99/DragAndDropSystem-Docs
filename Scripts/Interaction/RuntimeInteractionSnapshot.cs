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

        public InteractionInputKind InputKind { get; }
        public UniversalInventory Inventory { get; }
        public BaseSlot ResolvedBaseSlot { get; }
        public BaseSlot FocusedBaseSlot { get; }
        public BaseSlot HoveredBaseSlot { get; }
        public BaseSlot PressedBaseSlot { get; }
        public InventoryDropArea DropArea { get; }
        public FocusSource ActiveFocusSource { get; }
        public PointerEventData PointerEventData { get; }
        public PointerTriggerPhase? PointerPhase { get; }
        public KeyTriggerPhase? KeyPhase { get; }
        public TriggerPhaseEnum? InputActionPhase { get; }
        public object NativeInputContext { get; }
        public bool IsDragging { get; }
        public DragContext CurrentDragContext { get; }
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
