namespace DragAndDropSystem
{
    public enum PointerTriggerPhase
    {
        Any = 0,
        Down = 1,
        Up = 2,
        Click = 3
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
        Alt = 3
    }

    public enum NavigationEventType
    {
        Submit = 0,
        Cancel = 1
    }
}
