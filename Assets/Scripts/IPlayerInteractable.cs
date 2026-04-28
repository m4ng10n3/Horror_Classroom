public interface IPlayerInteractable
{
    bool CanInteract(EscapeInventory inventory);
    string GetInteractionPrompt(EscapeInventory inventory);
    string Interact(EscapeInventory inventory, GameManager gameManager);
}
