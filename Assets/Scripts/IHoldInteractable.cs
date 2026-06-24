// Interazione "tieni premuto" a tempo (es. furto), distinta dal tap istantaneo di
// IPlayerInteractable. Il PlayerInteractionController gestisce il timer, mentre
// l'oggetto decide cosa accade ad ogni frame, al completamento e all'annullamento.
public interface IHoldInteractable : IPlayerInteractable
{
    // Secondi di pressione continua necessari per completare l'azione.
    float HoldDuration { get; }

    // Controllo anticipato PRIMA di iniziare il hold (es. inventario pieno):
    // se ritorna false l'azione non parte e blockReason viene mostrato come feedback.
    bool CanBeginHold(out string blockReason);

    // Chiamato ogni frame mentre il tasto resta premuto e il bersaglio è puntato.
    // progress01 va da 0 a 1. Ritorna false per ANNULLARE il hold a metà
    // (es. esposizione arrivata al massimo): l'eventuale conseguenza la gestisce
    // chi ha causato l'annullamento, non il controller.
    bool OnHoldTick(float deltaTime, float progress01);

    // Hold portato a termine (timer >= HoldDuration).
    void OnHoldCompleted(GameManager gameManager);

    // Hold interrotto prima del completamento (tasto rilasciato, bersaglio perso,
    // oppure OnHoldTick ha ritornato false). Nessuna penalità per definizione.
    void OnHoldCancelled();
}
