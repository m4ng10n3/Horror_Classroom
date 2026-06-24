using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BakeTradeChainEditor
{
    // Applica una volta sola la catena di scambi (oggetti + dialoghi) a tutti gli
    // StudentNPC in scena, scrivendo i valori nei campi dell'inspector. Dopo il
    // bake la logica non gira più a runtime: tutto si modifica dall'inspector.
    [MenuItem("Tools/Classroom/Bake Trade Chain Into Scene")]
    static void BakeAll()
    {
        var students = Object.FindObjectsByType<StudentNPC>(FindObjectsSortMode.None);
        if (students.Length == 0)
        {
            Debug.LogWarning("[BakeTradeChain] Nessun StudentNPC trovato in scena.");
            return;
        }

        Undo.RecordObjects(students, "Bake Trade Chain");
        foreach (var s in students)
            s.BakeChainPresetFromName();

        if (students.Length > 0)
            EditorSceneManager.MarkSceneDirty(students[0].gameObject.scene);

        Debug.Log($"[BakeTradeChain] Catena applicata a {students.Length} studenti. Salva la scena (Ctrl+S).");
    }
}
