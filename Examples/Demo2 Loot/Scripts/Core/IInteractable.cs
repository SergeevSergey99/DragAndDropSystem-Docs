namespace DragAndDropSystem.Examples.Loot
{
    /// <summary>
    /// Interface for all objects that can be interacted with
    /// </summary>
    public interface IInteractable
    {
        bool CanInteract(PlayerInteraction player);
        /// <summary>
        /// Perform interaction
        /// </summary>
        /// <param name="player">Player performing the interaction</param>
        void Interact(PlayerInteraction player);
    }
}
