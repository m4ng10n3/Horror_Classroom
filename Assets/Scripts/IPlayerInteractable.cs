public interface IPlayerInteractable
{
    bool CanInteract();
    string GetInteractionPrompt();
    string Interact(GameManager gameManager);
}
