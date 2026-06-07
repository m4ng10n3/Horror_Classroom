public interface IDialogueSequenceInteractable
{
    string SpeakerName { get; }
    DialogueLine[] GetDialogueSequence(GameManager gameManager);
}
