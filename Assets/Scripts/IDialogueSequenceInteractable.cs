public interface IDialogueSequenceInteractable
{
    string SpeakerName { get; }
    DialogueLine[] GetDialogueSequence(EscapeInventory inventory, GameManager gameManager);
}
