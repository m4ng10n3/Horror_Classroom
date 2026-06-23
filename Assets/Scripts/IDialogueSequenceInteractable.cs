public interface IDialogueSequenceInteractable
{
    string SpeakerName { get; }

    // Calcola le battute da mostrare SENZA applicare effetti collaterali (baratto,
    // consegna oggetti, avanzamento stato). Gli effetti vanno applicati solo quando
    // il dialogo viene portato a termine, tramite OnDialogueCompleted.
    DialogueLine[] GetDialogueSequence(GameManager gameManager);

    // Chiamato quando il player legge fino all'ultima battuta e chiude il dialogo.
    // Qui si applicano gli effetti collaterali calcolati da GetDialogueSequence.
    void OnDialogueCompleted(GameManager gameManager);
}
