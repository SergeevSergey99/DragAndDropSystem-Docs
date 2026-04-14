namespace DragAndDropSystem
{
    public enum PointerTriggerPhase
    {
        Any = 0,
        Down = 1,
        Up = 2,
        Click = 3,
        ClickShort = 4,
        ClickLong = 5,
        BeginDrag = 6
    }

    public enum TriggerPhaseEnum
    {
        Started,
        Performed,
        Canceled
    }
        
    public enum ModifierKey
    {
        None = 0,
        Ctrl = 1,
        Shift = 2,
        Alt = 3,
        Any = 4
    }

    public enum KeyTriggerPhase
    {
        Down = 0,
        Up = 1,
        Hold = 2
    }

}
