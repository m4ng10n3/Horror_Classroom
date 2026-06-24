using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DialogueSequence
{
    public DialogueLine[] lines;
}

[Serializable]
public class TradeStep
{
    [Tooltip("Oggetto richiesto in questo passo della catena.")]
    public string requiredItem;

    [Tooltip("Se true, l'oggetto di questo passo viene consumato alla consegna.")]
    public bool consumeItem = true;

    [Tooltip("Dialogo mostrato finché manca l'oggetto: è la RICHIESTA di questo passo.")]
    public DialogueSequence requestDialogue;

    [Tooltip("Dialogo mostrato quando consegni l'oggetto. Di solito introduce la richiesta successiva.")]
    public DialogueSequence receivedDialogue;
}

[Serializable]
public class ItemDialogueTrigger
{
    [Tooltip("Nome esatto dell'oggetto nell'inventario che attiva questo dialogo.")]
    public string requiredItemName;

    [Tooltip("Sequenza di dialogo speciale mostrata quando l'oggetto è presente.")]
    public DialogueSequence dialogue;

    [Tooltip("Se true, questo dialogo si mostra solo la prima volta che l'oggetto è rilevato.")]
    public bool oneShot = true;

    [HideInInspector]
    public bool triggered = false;
}

public class StudentNPC : MonoBehaviour, IPlayerInteractable, IDialogueSequenceInteractable
{
    [Header("Identity")]
    public string studentName = "Studente";
    public Color studentColor = Color.white;

    [Header("Dialogo — Oggetto Mancante")]
    public DialogueLine[] missingItemSequence = new DialogueLine[]
    {
        new DialogueLine { speaker = DialogueLine.Speaker.NPC,    text = "Psst... ho bisogno di qualcosa." },
        new DialogueLine { speaker = DialogueLine.Speaker.Player, text = "Di cosa hai bisogno?" },
        new DialogueLine { speaker = DialogueLine.Speaker.NPC,    text = "Se mi porti l'oggetto giusto, ti do una mano." },
        new DialogueLine { speaker = DialogueLine.Speaker.Player, text = "Ok, ci provo." }
    };

    [Header("Dialogo — Baratto Completato")]
    public DialogueLine[] completedSequence = new DialogueLine[]
    {
        new DialogueLine { speaker = DialogueLine.Speaker.Player, text = "Ho quello che cercavi." },
        new DialogueLine { speaker = DialogueLine.Speaker.NPC,    text = "Perfetto! Tieni, potrebbe servirti." },
        new DialogueLine { speaker = DialogueLine.Speaker.Player, text = "Grazie, ne avevo bisogno." }
    };

    [Header("Dialogo — Ripetizioni Dialoghi")]
    [Tooltip("Sequenze mostrate dopo il baratto, in ordine. L'ultima viene ripetuta una volta esaurite le altre.")]
    public DialogueSequence[] repeatSequences = new DialogueSequence[]
    {
        new DialogueSequence { lines = new[] {
            new DialogueLine { speaker = DialogueLine.Speaker.NPC,    text = "Non ho altro da darti." },
            new DialogueLine { speaker = DialogueLine.Speaker.Player, text = "Capisco, grazie comunque." }
        }},
    };

    [Header("Dialoghi Speciali — Oggetti in Inventario")]
    [Tooltip("Dialoghi che si attivano se il player ha un certo oggetto nell'inventario. Controllati per primi, prima del baratto.")]
    public ItemDialogueTrigger[] itemDialogueTriggers = Array.Empty<ItemDialogueTrigger>();

    [Header("Trade System")]
    [Tooltip("Se true, questo studente non può sparire finché non ha completato il baratto.")]
    public bool essential = false;

    [Tooltip("Diventa true quando lo scambio è stato completato.")]
    public bool tradeDone = false;

    [Tooltip("Lascia vuoto se lo studente regala subito l'oggetto senza richiederne uno.")]
    public string requiredItem = "";

    [Tooltip("Oggetti richiesti aggiuntivi. Lo studente completa il baratto solo se il player ha TUTTI gli oggetti (questi + 'Required Item'). Lascia vuoto per richiederne uno solo.")]
    public string[] additionalRequiredItems = Array.Empty<string>();

    [Header("Trade Chain — Richieste in Sequenza")]
    [Tooltip("Se popolato, lo studente chiede questi oggetti UNO ALLA VOLTA, in ordine. Ogni consegna sblocca la richiesta successiva. Il reward viene dato solo dopo l'ultimo passo. Sostituisce 'Required Item' singolo.")]
    public TradeStep[] tradeSteps = Array.Empty<TradeStep>();

    [Tooltip("Oggetto ricevuto dal player al termine del baratto.")]
    public string rewardItem = "";

    [Tooltip("Se true, gli oggetti richiesti vengono consumati nel baratto.")]
    public bool consumeRequiredItem = true;

    [Header("Reward Item — Modello 3D")]
    [Tooltip("Prefab o oggetto in scena da mostrare nell'inventario fisico dopo il baratto.")]
    public GameObject rewardItemPrefab;

    [TextArea(2, 3)]
    [Tooltip("Descrizione mostrata nella scheda inventario.")]
    public string rewardItemDescription = "";

    [Tooltip("Se true il player può ispezionare l'oggetto in 3D dal Tab.")]
    public bool rewardItemCanInspect = true;

    [Min(0.05f)]
    public float rewardItemInspectionScaleMultiplier = 1f;

    [Header("Interaction Collider")]
    public bool addFallbackInteractionCollider = true;
    public float fallbackInteractionRadius = 0.45f;
    public float fallbackInteractionHeight = 1.6f;

    [Header("Steal System")]
    [Tooltip("Oggetti rubabili appartenenti a questo studente. Quando sparisce diventano raccoglibili gratis. Si popola anche da solo se imposti 'owner' sullo StealableItem.")]
    [SerializeField] private List<StealableItem> deskItems = new List<StealableItem>();

    [Header("State")]
    [SerializeField] private bool isVisible = true;

    // Quante volte questo studente ha visto il player rubare qualcosa. Aggancio per un
    // futuro sistema di reazione del testimone (Calmo / Insospettito / Agitato / Sta-per-parlare).
    private int timesWitnessed = 0;
    public int TimesWitnessed => timesWitnessed;

    private int repeatIndex = 0;
    private int tradeStepIndex = 0;
    private MeshRenderer bodyRenderer;
    private Material bodyMaterialInstance;

    // Effetti collaterali calcolati da GetDialogueSequence e applicati solo a
    // dialogo concluso (OnDialogueCompleted). Se il dialogo viene interrotto
    // restano in sospeso e vengono scartati alla successiva chiamata.
    private System.Action pendingCommit;

    public string SpeakerName => string.IsNullOrWhiteSpace(studentName) ? gameObject.name : studentName;

    void Awake()
    {
        bodyRenderer = GetComponentInChildren<MeshRenderer>();
        if (bodyRenderer != null)
        {
            bodyMaterialInstance = bodyRenderer.material;
            bodyMaterialInstance.color = studentColor;
        }

        EnsureInteractionCollider();
    }

#if UNITY_EDITOR
    // Bake da editor: scrive nei campi dell'inspector i valori della catena di
    // scambi in base al nome dello studente. Da eseguire UNA volta; dopodiché i
    // dati vivono nella scena e si modificano liberamente dall'inspector.
    [ContextMenu("Bake Trade Chain → Inspector")]
    public void BakeChainPresetFromName()
    {
        ApplySceneChainPreset();
        UnityEditor.EditorUtility.SetDirty(this);
        if (!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"[StudentNPC] Catena di scambi applicata a {SpeakerName}. Salva la scena (Ctrl+S).");
    }

    private void ApplySceneChainPreset()
    {
        switch (SpeakerName.Trim().ToLowerInvariant())
        {
            case "nicole":
                ConfigureChainStep(
                    "",
                    "Tessuto",
                    "Ritagli di stoffa presi dal banco di Nicole.",
                    Array.Empty<DialogueLine>(),
                    Lines(
                        Player("Hey Nicole, hai ancora quei ritagli di tessuto?"),
                        Player("Me ne servono un paio. La classe sembra piu' fredda del solito."),
                        Npc("Certo. Prendi quelli viola, pero' non guardarli troppo."),
                        Npc("Da quando la prof ha chiuso la porta, sembrano respirare."),
                        Player("Sei la goat. Non lo diro' a nessuno.")));
                break;

            case "frank":
                ConfigureChainStep(
                    "Tessuto",
                    "Palla Da Calcio",
                    "La palla di Frank. Batte piano, anche quando resta ferma.",
                    Lines(
                        Npc("We, non sei venuto a fare il tifo al mio torneo."),
                        Player("Scusa, stavo cercando un modo per uscire vivo."),
                        Npc("Damn. Allora vuoi la palla? Portami il tessuto di Nicole."),
                        Npc("Mi serve per coprirla. Da sola rotola sempre verso la porta."),
                        Player("Non mi piace questa cosa, ma torno.")),
                    Lines(
                        Player("Tieni, ho preso il tessuto."),
                        Npc("Crazy. Ora la palla smette di fissare il corridoio."),
                        Player("Dammi il pallone, Frank."),
                        Npc("Tieni. Se rimbalza da sola, fai finta di niente.")));
                break;

            case "michel":
                ConfigureChainStep(
                    "Palla Da Calcio",
                    "Scotch",
                    "Scotch appiccicoso. Tiene unite anche cose che dovrebbero restare separate.",
                    Lines(
                        Player("Michel, mi serve lo scotch."),
                        Npc("Certo che ce l'ho, che domande. E chiamami Jackson."),
                        Npc("Pero' non te lo do senza il pallone di Frank."),
                        Npc("Devo sentire un ultimo rimbalzo prima che la campanella suoni da sola."),
                        Player("Ok. Niente passi di danza nel frattempo.")),
                    Lines(
                        Npc("Hee hee. Vedo la palla."),
                        Player("E io vedo lo scotch."),
                        Npc("AU. Scambio pulito."),
                        Npc("Se lo strappi e senti qualcuno sussurrare, non sono io.")));
                break;

            case "sam":
                ConfigureChainStep(
                    "Scotch",
                    "Forbice",
                    "Forbici di Sam. Le lame sembrano gia' sapere cosa tagliare.",
                    Lines(
                        Player("Sam, puoi prestarmi le forbici?"),
                        Npc("Posso, ma mi serve lo scotch."),
                        Npc("Il disegno del mio cane continua a staccarsi dal banco."),
                        Npc("Ogni volta che cade, sento graffiare sotto la sedia."),
                        Player("Damn. Ti porto lo scotch.")),
                    Lines(
                        Player("Ecco lo scotch."),
                        Npc("Grazie. Ora il cane resta fermo."),
                        Npc("Prendi le forbici, ma non tagliare il buio tra i banchi."),
                        Player("Non avevo intenzione di provarci.")));
                break;

            case "amanda":
                ConfigureChainStep(
                    "Forbice",
                    "Spilla Arrugginita",
                    "Una spilla fredda e arrugginita. Punge anche senza toccarla.",
                    Lines(
                        Npc("Crazy shit bro, ora hai le forbici."),
                        Player("Mi serve qualcosa di piccolo, metallico."),
                        Npc("Ho una spilla arrugginita nel diario."),
                        Npc("Tagliami via il filo rosso che lo tiene chiuso, e' troppo stretto."),
                        Player("Sembra una pessima idea. Quindi si'.")),
                    Lines(
                        Player("Ho tagliato il filo."),
                        Npc("Crazy. Il diario ha smesso di tremare."),
                        Npc("Prendi la spilla. Se punge, non succhiare il sangue."),
                        Player("Grazie per il consiglio normalissimo.")));
                break;

            case "jenny":
                ConfigureChainStep(
                    "Spilla Arrugginita",
                    "Appunto Macchiato",
                    "Un appunto di Jenny pieno di macchie scure e correzioni nervose.",
                    Lines(
                        Npc("Perche' non ascolti la lezione invece di girovagare?"),
                        Player("Perche' la porta e' chiusa e nessuno si muove."),
                        Npc("Allora dammi quella spilla. Mi serve per fissare gli appunti."),
                        Npc("Le pagine cercano di girarsi da sole."),
                        Player("Prendi. Poi mi dai un foglio.")),
                    Lines(
                        Npc("Finalmente stanno ferme."),
                        Player("Mi serve un appunto."),
                        Npc("Prendi questo. Non leggere l'ultima riga ad alta voce."),
                        Player("Non prometto niente.")));
                break;

            case "george":
                ConfigureChainStep(
                    "Appunto Macchiato",
                    "Gessetto Nero",
                    "Un gessetto nero. Lascia segni anche sull'aria.",
                    Lines(
                        Npc("Non provare nemmeno ad avvicinarti."),
                        Player("Mi serve il gessetto nero."),
                        Npc("Allora fammi vedere l'appunto di Jenny."),
                        Npc("Voglio sapere se anche lei ha visto la frase sulla lavagna."),
                        Player("E poi me lo dai.")),
                    Lines(
                        Player("Ecco l'appunto."),
                        Npc("...quindi non l'ho immaginata."),
                        Npc("Prendi il gessetto. Io non lo tocco piu'."),
                        Player("Perfetto. Molto rassicurante.")));
                break;

            case "juliet":
                ConfigureChainStep(
                    "Gessetto Nero",
                    "Nastro Viola",
                    "Un nastro viola da capelli. Si stringe da solo quando l'aula tace.",
                    Lines(
                        Npc("Ciao sono Juliet, mi piace usare il Gabinet."),
                        Npc("A volte esce Violet, e balla dentro la vetrina."),
                        Player("Juliet, mi serve il tuo nastro."),
                        Npc("Portami il gessetto nero, cosi' segno il ritmo alla regina."),
                        Player("Non ho capito, ma torno.")),
                    Lines(
                        Player("Ho il gessetto."),
                        Npc("La rima e' scritta, la porta e' ferita."),
                        Npc("Prendi il nastro viola, prima che torni Violet."),
                        Player("Va bene, va bene.")));
                break;

            case "daniel":
                ConfigureChainStep(
                    "Nastro Viola",
                    "Guanto Da Box",
                    "Un guanto da box consumato. Dentro e' tiepido.",
                    Lines(
                        Player("Daniel, mi serve il tuo guanto da box."),
                        Npc("Damn, hai visto le matite di Leo? Sembrano denti."),
                        Npc("Ti do il guanto se mi porti il nastro viola."),
                        Npc("Lo lego alle nocche. Se qualcosa esce dal banco, colpisco prima io."),
                        Player("Affare fatto.")),
                    Lines(
                        Player("Ecco il nastro."),
                        Npc("Damn. Ora posso fasciarmi la mano buona."),
                        Npc("Prendi il guanto. Non metterlo all'orecchio: dentro sussurra."),
                        Player("Non volevo farlo.")));
                break;

            case "leo":
                ConfigureChainStep(
                    "Guanto Da Box",
                    "Temperino",
                    "Un temperino super affilato. Guardah, da paura.",
                    Lines(
                        Player("Leo, puoi prestarmi il temperino?"),
                        Npc("Guardah. Sto temperino e' demoniaco."),
                        Npc("Fa delle punte che fanno PAURA."),
                        Npc("Dammi il guanto di Daniel, cosi' lo tengo fermo senza perdere dita."),
                        Player("Ok, faccio quello che posso.")),
                    Lines(
                        Player("Ecco il guanto."),
                        Npc("Guardah, sei appena in tempo."),
                        Npc("Le matite stavano puntando tutti. Anche il cane di Sam."),
                        Npc("Prendi il temperino, ma non temperare dopo il buio."),
                        Player("Dammi solo l'oggetto, Leo.")));
                break;

            case "eva":
                ConfigureChainStep(
                    "Temperino",
                    "Taglierino",
                    "Uno dei taglierini di Eva. La lama riflette una classe vuota.",
                    Lines(
                        Player("Eva, quanti taglierini hai sul banco?"),
                        Npc("Uno per ogni cosa che va aperta."),
                        Npc("Ma questo si e' spuntato. Voglio il temperino di Leo."),
                        Npc("Se torna affilato, ti do quello che tengo in tasca."),
                        Player("Damn.")),
                    Lines(
                        Player("Ho il temperino."),
                        Npc("Perfetto. Le lame non devono annoiarsi."),
                        Npc("Prendi il taglierino. Dopo raccontami cosa hai aperto."),
                        Player("La porta, spero.")));
                break;

            case "carlos":
                ConfigureChainStep(
                    "Taglierino",
                    "Accendino",
                    "L'accendino di Carlos. Scatta anche quando non lo tocchi.",
                    Lines(
                        Player("Carlos, smettila di usare quell'accendino su tutto."),
                        Npc("Perche'? A te non rimangono cose da incendiare?"),
                        Player("Mi serve, ma prima vuoi qualcosa, vero?"),
                        Npc("Il taglierino di Eva. Devo incidere il banco prima che lo faccia lui."),
                        Player("Questa classe peggiora ogni minuto.")),
                    Lines(
                        Player("Ecco il taglierino."),
                        Npc("Bene. Ora posso scrivere il mio nome dove la prof non guarda."),
                        Npc("Prendi l'accendino. Se la fiamma diventa blu, corri."),
                        Player("Ricevuto.")));
                break;

            case "will":
                ConfigureChainStep(
                    "Accendino",
                    "Ritratto Bruciato",
                    "Un ritratto di Eddy con gli occhi anneriti dal fumo.",
                    Lines(
                        Npc("Posso farti un ritratto?"),
                        Player("No."),
                        Npc("Allora portami una fiamma. Disegno meglio quando la carta ha paura."),
                        Player("Ho bisogno di qualcosa in cambio."),
                        Npc("Avrai il ritratto. Anche se non ti somigliera' del tutto.")),
                    Lines(
                        Player("Ho l'accendino."),
                        Npc("Stai fermo. Il fumo traccia gia' il tuo contorno."),
                        Npc("Ecco il ritratto. Non piegarlo: si lamenta."),
                        Player("Lo tengo lontano dalla faccia.")));
                break;

            case "mark":
                ConfigureChainStep(
                    "Ritratto Bruciato",
                    "Amo Piegato",
                    "Un amo da pesca piegato. Ha graffiato qualcosa di duro.",
                    Lines(
                        Npc("Dovresti venire a pesca con mio padre domenica."),
                        Player("Mark, mi serve qualcosa di tuo."),
                        Npc("Portami quel ritratto bruciato. Voglio usarlo come esca."),
                        Player("Per cosa?"),
                        Npc("Per quello che nuota nel secchio sotto la cattedra.")),
                    Lines(
                        Player("Ecco il ritratto."),
                        Npc("Figo. L'esca ha gia' gli occhi giusti."),
                        Npc("Prendi questo amo piegato. Non e' stato un pesce a piegarlo."),
                        Player("Non chiedo altro.")));
                break;

            case "owen":
                ConfigureChainStep(
                    "Amo Piegato",
                    "Cubetto Di Ghiaccio",
                    "Un cubetto di ghiaccio che non si scioglie.",
                    Lines(
                        Npc("Ti rendi conto di quanto sia pericoloso lo scioglimento dei ghiacciai?"),
                        Player("Damn, dimmi di piu'."),
                        Npc("No. Portami l'amo di Mark."),
                        Npc("C'e' qualcosa nel barattolo del laboratorio, e mi serve tirarlo fuori."),
                        Player("Poi mi dai quel ghiaccio che non si scioglie.")),
                    Lines(
                        Player("Ho l'amo."),
                        Npc("Perfetto. Il pianeta sta morendo, ma questo cubetto no."),
                        Npc("Prendilo. Se senti acqua sotto i piedi, non guardare giu'."),
                        Player("Damn.")));
                break;

            case "matilda":
                ConfigureChainStep(
                    "Cubetto Di Ghiaccio",
                    "Gomma Da Masticare",
                    "Una gomma masticata da Matilda. Si muove piano nella carta.",
                    Lines(
                        Npc("Ho attaccato tutte le mie gomme sotto al banco, vuoi vedere?"),
                        Player("No. Mi serve una gomma."),
                        Npc("Allora dammi il cubetto di Owen."),
                        Npc("Una gomma sta mordendo il legno. Col freddo si stacca meglio."),
                        Player("Crazy.")),
                    Lines(
                        Player("Ecco il cubetto."),
                        Npc("Senti? Ha smesso di masticare il banco."),
                        Npc("Prendi questa. Non metterla in bocca."),
                        Player("Mai avuto intenzione.")));
                break;

            case "rachel":
                ConfigureChainStep(
                    "Gomma Da Masticare",
                    "Bottone",
                    "Un bottone di Rachel. Il filo sembra appena strappato.",
                    Lines(
                        Player("Rachel, mi serve un bottone della tua collezione."),
                        Npc("Damn, della mia collezione? Sei impazzito?"),
                        Npc("Pero' quella gomma di Matilda mi serve per chiudere il sacchetto."),
                        Npc("I bottoni dentro continuano a sbattere come denti."),
                        Player("Scambio?")),
                    Lines(
                        Player("Ho la gomma."),
                        Npc("Tieni, allora. Prendi un bottone."),
                        Npc("Se ne trovi uno gia' cucito alla porta, non tirarlo."),
                        Player("Grazie... credo.")));
                break;

            case "maria":
                ConfigureChainStep(
                    "Bottone",
                    "Figurina Gatto",
                    "Figurina bellissima di un gattino troppo immobile.",
                    Lines(
                        Npc("Ti piace la mia collezione di figurine carine carine?"),
                        Player("Si', sono carine carine. Ne voglio una."),
                        Npc("Se mi porti un bottone di Rachel, la figurina sara' tua."),
                        Npc("Il gatto su questa miagola solo quando la prof si gira."),
                        Player("Perfetto. Normale.")),
                    Lines(
                        Player("Ti ho portato un bottone."),
                        Npc("Evviva! E' proprio quello di Rachel?"),
                        Player("Si', ora dammi la figurina."),
                        Npc("Tieni, ma porta un po' di rispetto. Il gatto ascolta.")));
                break;

            case "erika":
                ConfigureChainStep(
                    "Figurina Gatto",
                    "Graffetta",
                    "Una graffetta fredda, piegata come un piccolo uncino.",
                    Lines(
                        Player("Erika, puoi prestarmi una graffetta?"),
                        Npc("Cosa mi dai in cambio?"),
                        Npc("Anzi, sai cosa vorrei? La figurina col gattino di Maria."),
                        Npc("Mi guarda da tutta la mattina."),
                        Player("Ok.")),
                    Lines(
                        Player("Ho la figurina del gatto."),
                        Npc("Finalmente. Ora guardera' me, non te."),
                        Npc("Prendi la graffetta. Si apre le porte piccole, non quelle grandi."),
                        Player("Mi basta che serva a qualcosa.")));
                break;

            case "brad":
                ConfigureChainStep(
                    "Graffetta",
                    "Chiave",
                    "La chiave trovata da Brad. E' pesante come se ricordasse la serratura.",
                    Lines(
                        Npc("Ho trovato delle chiavi. A che servono?"),
                        Player("Forse alla porta. Mi servono."),
                        Npc("Questa e' incastrata nel portachiavi. Dammi la graffetta di Erika."),
                        Npc("Non voglio piu' sentire il metallo ridere nella tasca."),
                        Player("Te la porto.")),
                    Lines(
                        Player("Ecco la graffetta."),
                        Npc("Ok. La chiave si e' staccata da sola."),
                        Npc("Prendila e vai alla porta prima che cambi idea."),
                        Player("Finalmente.")));
                break;
        }
    }

    private void ConfigureChainStep(
        string required,
        string reward,
        string description,
        DialogueLine[] missing,
        DialogueLine[] completed)
    {
        essential = true;
        consumeRequiredItem = true;
        requiredItem = required;
        additionalRequiredItems = Array.Empty<string>();
        tradeSteps = Array.Empty<TradeStep>();
        rewardItem = reward;
        rewardItemDescription = description;
        missingItemSequence = missing ?? Array.Empty<DialogueLine>();
        completedSequence = completed ?? Array.Empty<DialogueLine>();
        itemDialogueTriggers = Array.Empty<ItemDialogueTrigger>();
        repeatSequences = new[]
        {
            new DialogueSequence
            {
                lines = Lines(
                    Npc("Non ho altro da darti. La prossima cosa che sparisce potrei essere io."),
                    Player("Capisco. Resta dove posso vederti."))
            }
        };
    }

    private static DialogueLine[] Lines(params DialogueLine[] lines) => lines;

    private static DialogueLine Player(string text)
    {
        return new DialogueLine { speaker = DialogueLine.Speaker.Player, text = text };
    }

    private static DialogueLine Npc(string text)
    {
        return new DialogueLine { speaker = DialogueLine.Speaker.NPC, text = text };
    }
#endif

    public bool CanDisappear() => isVisible && (!essential || tradeDone);
    public bool IsVisible => isVisible;

    public void Disappear()
    {
        isVisible = false;

        // Gli oggetti rubabili rimasti diventano raccoglibili gratis: niente hold,
        // niente esposizione, niente sospetto. Sono GameObject separati dall'NPC,
        // quindi restano attivi anche dopo che lo studente è stato disattivato.
        if (deskItems != null)
        {
            foreach (StealableItem item in deskItems)
            {
                if (item != null)
                    item.ConvertToFreePickup();
            }
        }

        gameObject.SetActive(false);
        Debug.Log($"[Student] {SpeakerName} è sparito...");
    }

    // Registra un oggetto rubabile come appartenente a questo studente. Chiamato
    // automaticamente da StealableItem.Awake quando 'owner' è impostato.
    public void RegisterStealable(StealableItem item)
    {
        if (item == null) return;
        if (deskItems == null) deskItems = new List<StealableItem>();
        if (!deskItems.Contains(item))
            deskItems.Add(item);
    }

    // Aggancio minimo per la reazione del testimone: per ora conta e logga soltanto.
    public void Witness()
    {
        timesWitnessed++;
        Debug.Log($"[Student] {SpeakerName} ha visto un furto! (timesWitnessed={timesWitnessed})");
    }

    public void Reappear()
    {
        isVisible = true;
        gameObject.SetActive(true);
    }

    public bool CanInteract() => isVisible;

    public string GetInteractionPrompt() => $"[F] Parla con {SpeakerName}";

    // Fallback usato solo se IDialogueSequenceInteractable non è supportato dal controller.
    public string Interact(GameManager gameManager)
    {
        DialogueLine[] seq = GetDialogueSequence(gameManager);
        if (seq == null || seq.Length == 0)
            return string.Empty;
        return $"{SpeakerName}: \"{seq[0].text}\"";
    }

    public DialogueLine[] GetDialogueSequence(GameManager gameManager)
    {
        // Ogni calcolo riparte da zero: nessun effetto collaterale ancora in sospeso.
        pendingCommit = null;

        PhysicalInventory inventory = ResolvePhysicalInventory();
        if (inventory == null)
        {
            return new[] { new DialogueLine { speaker = DialogueLine.Speaker.NPC, text = "Non so dove metterti gli oggetti." } };
        }

        // Priorità 1: dialoghi speciali legati a oggetti nell'inventario.
        DialogueLine[] specialDialogue = TryGetItemDialogue(inventory);
        if (specialDialogue != null)
            return specialDialogue;

        // Priorità 2: baratto già completato → ciclo ripetizioni Dark Souls.
        if (tradeDone)
            return GetNextRepeatSequence();

        // Priorità 2.5: catena di richieste in sequenza (se configurata).
        if (UsesTradeChain)
            return GetTradeChainSequence(inventory);

        // Priorità 3: oggetti richiesti ancora mancanti (servono TUTTI).
        if (NeedsRequiredItem() && !HasAllRequiredItems(inventory))
            return missingItemSequence;

        // Priorità 4: inventario pieno → il baratto resta in sospeso finché non si libera spazio.
        // Se arriviamo qui con NeedsRequiredItem, tutti gli oggetti richiesti sono in mano.
        bool willFreeSlotWithTrade = NeedsRequiredItem() && consumeRequiredItem;
        if (!string.IsNullOrWhiteSpace(rewardItem) && inventory.IsFull && !willFreeSlotWithTrade)
        {
            return new[] { new DialogueLine
            {
                speaker = DialogueLine.Speaker.NPC,
                text = $"Hai gia' due oggetti in mano. Lascia qualcosa dallo zaino e torna da me."
            } };
        }

        // Priorità 5: baratto. Calcola le battute ma applica gli effetti solo a fine dialogo.
        bool hasReward = !string.IsNullOrWhiteSpace(rewardItem);
        pendingCommit = () =>
        {
            if (NeedsRequiredItem() && consumeRequiredItem)
            {
                foreach (string item in GetRequiredItems())
                    inventory.RemoveItem(item);
            }

            tradeDone = true;

            if (hasReward)
                GivePhysicalItem(inventory);
        };

        if (hasReward)
        {
            var extended = new DialogueLine[completedSequence.Length + 1];
            System.Array.Copy(completedSequence, extended, completedSequence.Length);
            extended[completedSequence.Length] = new DialogueLine
            {
                speaker = DialogueLine.Speaker.NPC,
                text = $"[Ricevuto: {rewardItem}]"
            };
            return extended;
        }

        return completedSequence;
    }

    // Applica gli effetti collaterali del dialogo appena concluso. Se il dialogo è
    // stato interrotto (player allontanato) questo non viene chiamato e lo stato
    // resta invariato, così alla prossima interazione si riparte dall'inizio.
    public void OnDialogueCompleted(GameManager gameManager)
    {
        pendingCommit?.Invoke();
        pendingCommit = null;
    }

    // Controlla se qualche trigger di oggetto deve scattare e restituisce il dialogo relativo.
    private DialogueLine[] TryGetItemDialogue(PhysicalInventory inventory)
    {
        if (itemDialogueTriggers == null) return null;

        foreach (var trigger in itemDialogueTriggers)
        {
            if (trigger == null) continue;
            if (string.IsNullOrWhiteSpace(trigger.requiredItemName)) continue;
            if (trigger.oneShot && trigger.triggered) continue;
            if (!inventory.HasItem(trigger.requiredItemName)) continue;
            if (trigger.dialogue == null || trigger.dialogue.lines == null || trigger.dialogue.lines.Length == 0) continue;

            // Il trigger oneShot si "consuma" solo a dialogo concluso.
            var capturedTrigger = trigger;
            pendingCommit = () => capturedTrigger.triggered = true;
            return trigger.dialogue.lines;
        }

        return null;
    }

    // Restituisce la prossima sequenza di ripetizione, bloccandosi sull'ultima.
    // L'avanzamento dell'indice avviene solo a dialogo concluso.
    private DialogueLine[] GetNextRepeatSequence()
    {
        if (repeatSequences == null || repeatSequences.Length == 0)
            return new[] { new DialogueLine { speaker = DialogueLine.Speaker.NPC, text = "..." } };

        int index = Mathf.Clamp(repeatIndex, 0, repeatSequences.Length - 1);
        DialogueLine[] lines = repeatSequences[index]?.lines;

        pendingCommit = () =>
        {
            if (repeatIndex < repeatSequences.Length - 1)
                repeatIndex++;
        };

        return lines ?? Array.Empty<DialogueLine>();
    }

    private bool UsesTradeChain => tradeSteps != null && tradeSteps.Length > 0;

    // Gestisce la catena di richieste in sequenza: ogni passo chiede un oggetto, lo
    // consuma alla consegna e sblocca il passo successivo. Il reward (rewardItem)
    // viene dato solo a conclusione dell'ultimo passo. Come per gli altri dialoghi,
    // gli effetti collaterali sono applicati solo a dialogo concluso (pendingCommit).
    private DialogueLine[] GetTradeChainSequence(PhysicalInventory inventory)
    {
        int index = Mathf.Clamp(tradeStepIndex, 0, tradeSteps.Length - 1);
        TradeStep step = tradeSteps[index];
        if (step == null)
            return Array.Empty<DialogueLine>();

        bool needsItem = !string.IsNullOrWhiteSpace(step.requiredItem);

        // Oggetto di questo passo ancora mancante → mostra la richiesta.
        if (needsItem && !inventory.HasItem(step.requiredItem))
            return step.requestDialogue?.lines ?? Array.Empty<DialogueLine>();

        bool isLastStep = index >= tradeSteps.Length - 1;
        bool hasReward = isLastStep && !string.IsNullOrWhiteSpace(rewardItem);

        // Solo all'ultimo passo, con un reward da consegnare: se l'inventario è pieno
        // e questo passo non libera uno slot, il baratto resta in sospeso.
        bool willFreeSlot = needsItem && step.consumeItem;
        if (hasReward && inventory.IsFull && !willFreeSlot)
        {
            return new[] { new DialogueLine
            {
                speaker = DialogueLine.Speaker.NPC,
                text = "Hai gia' due oggetti in mano. Lascia qualcosa dallo zaino e torna da me."
            } };
        }

        TradeStep capturedStep = step;
        bool finalStep = isLastStep;
        pendingCommit = () =>
        {
            if (needsItem && capturedStep.consumeItem)
                inventory.RemoveItem(capturedStep.requiredItem);

            if (finalStep)
            {
                tradeDone = true;
                if (!string.IsNullOrWhiteSpace(rewardItem))
                    GivePhysicalItem(inventory);
            }
            else
            {
                tradeStepIndex++;
            }
        };

        DialogueLine[] received = capturedStep.receivedDialogue?.lines ?? Array.Empty<DialogueLine>();

        if (finalStep && !string.IsNullOrWhiteSpace(rewardItem))
        {
            var extended = new DialogueLine[received.Length + 1];
            System.Array.Copy(received, extended, received.Length);
            extended[received.Length] = new DialogueLine
            {
                speaker = DialogueLine.Speaker.NPC,
                text = $"[Ricevuto: {rewardItem}]"
            };
            return extended;
        }

        return received;
    }

    // Elenca tutti gli oggetti richiesti non vuoti: 'requiredItem' più gli additionalRequiredItems.
    private IEnumerable<string> GetRequiredItems()
    {
        if (!string.IsNullOrWhiteSpace(requiredItem))
            yield return requiredItem;

        if (additionalRequiredItems != null)
        {
            foreach (string item in additionalRequiredItems)
                if (!string.IsNullOrWhiteSpace(item))
                    yield return item;
        }
    }

    private bool NeedsRequiredItem()
    {
        foreach (string _ in GetRequiredItems())
            return true;
        return false;
    }

    private bool HasAllRequiredItems(PhysicalInventory inventory)
    {
        foreach (string item in GetRequiredItems())
            if (!inventory.HasItem(item))
                return false;
        return true;
    }

    private void EnsureInteractionCollider()
    {
        if (!addFallbackInteractionCollider || GetComponentInChildren<Collider>() != null)
        {
            return;
        }

        CapsuleCollider collider = gameObject.AddComponent<CapsuleCollider>();
        collider.radius = Mathf.Max(0.05f, fallbackInteractionRadius);
        collider.height = Mathf.Max(collider.radius * 2f, fallbackInteractionHeight);
        collider.center = new Vector3(0f, collider.height * 0.5f, 0f);
    }

    private void GivePhysicalItem(PhysicalInventory inventory)
    {
        if (inventory == null || string.IsNullOrWhiteSpace(rewardItem)) return;

        GameObject snapshot = null;
        if (rewardItemPrefab != null)
        {
            snapshot = Instantiate(rewardItemPrefab);
            snapshot.name = "__snapshot_" + rewardItem;
            snapshot.transform.position = new Vector3(0f, -5000f, 0f);
            snapshot.transform.rotation = Quaternion.identity;
            snapshot.SetActive(true);

            foreach (var col in snapshot.GetComponentsInChildren<Collider>())
                Destroy(col);
            foreach (var pu in snapshot.GetComponentsInChildren<PickupItem>())
                Destroy(pu);

            DontDestroyOnLoad(snapshot);
        }

        inventory.AddItem(new CollectedItem
        {
            name        = string.IsNullOrWhiteSpace(rewardItem) ? rewardItemPrefab.name : rewardItem,
            description = rewardItemDescription,
            canInspect  = rewardItemCanInspect && snapshot != null,
            inspectionScaleMultiplier = rewardItemInspectionScaleMultiplier,
            worldSource = snapshot
        });
    }

    private PhysicalInventory ResolvePhysicalInventory()
    {
        return PhysicalInventory.Instance != null
            ? PhysicalInventory.Instance
            : FindFirstObjectByType<PhysicalInventory>();
    }
}
