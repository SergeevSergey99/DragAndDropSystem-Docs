namespace DragAndDropSystem.Examples.Demo2Loot
{
    /// <summary>
    /// Интерфейс для всех объектов с которыми можно взаимодействовать
    /// </summary>
    public interface IInteractable
    {
        bool CanInteract(PlayerInteraction player);
        /// <summary>
        /// Выполнить взаимодействие
        /// </summary>
        /// <param name="player">Игрок, который взаимодействует</param>
        void Interact(PlayerInteraction player);
    }
}
