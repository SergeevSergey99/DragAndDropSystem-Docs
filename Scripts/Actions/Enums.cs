namespace DragAndDropSystem
{
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