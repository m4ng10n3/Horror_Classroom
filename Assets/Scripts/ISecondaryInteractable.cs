// Azione secondaria opzionale su un interagibile (es. chiudere un cassetto senza
// raccogliere l'oggetto). Il PlayerInteractionController la attiva con un tasto
// dedicato solo se l'oggetto puntato implementa questa interfaccia.
public interface ISecondaryInteractable
{
    bool CanInteractSecondary();
    string GetSecondaryPrompt();
    string InteractSecondary(GameManager gameManager);
}
