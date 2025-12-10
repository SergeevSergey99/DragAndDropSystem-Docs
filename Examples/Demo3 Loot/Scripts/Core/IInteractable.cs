namespace DragAndDropSystem.Examples.Demo3Loot
{
    /// <summary>
    /// Интерфейс для всех объектов с которыми можно взаимодействовать
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Выполнить взаимодействие
        /// </summary>
        /// <param name="player">Игрок, который взаимодействует</param>
        void Interact(PlayerInteraction player);
    }
}
