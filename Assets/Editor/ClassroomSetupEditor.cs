using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ClassroomSetupEditor
{
    // ── Room ────────────────────────────────────────────────────────────────
    const float FRONT_Z  = -7f;    // muro nord (lavagna)
    const float BACK_Z   =  7f;    // muro sud (fondo)
    const float FRONT_HW =  6f;    // semi-larghezza lato prof
    const float BACK_HW  = 10f;    // semi-larghezza lato fondo
    const float WALL_H   =  3.5f;
    const float WALL_Y   =  WALL_H / 2f;   // 1.75

    // ── Student grid: 5 righe × 4 colonne = 20 ──────────────────────────────
    static readonly float[] ROW_Z   = { -3f, -1f, 1f, 3.5f, 5.5f };
    const int               COLS    = 4;
    const float             X_MARGIN = 0.82f;   // margine dai muri

    static readonly (string name, Color color)[] STUDENT_DATA =
    {
        ("Elena",     new Color(0.196f, 0.667f, 0.333f)),
        ("Sara",      new Color(0.392f, 0.196f, 0.667f)),
        ("Paolo",     new Color(0.667f, 0.533f, 0.196f)),
        ("Giulia",    new Color(0.800f, 0.392f, 0.588f)),
        ("Lucia",     new Color(0.667f, 0.196f, 0.196f)),
        ("Tommaso",   new Color(0.196f, 0.667f, 0.667f)),
        ("Marco",     new Color(0.196f, 0.333f, 0.667f)),
        ("Chiara",    new Color(0.900f, 0.800f, 0.200f)),
        ("Luca",      new Color(0.200f, 0.500f, 0.900f)),
        ("Matteo",    new Color(0.500f, 0.900f, 0.200f)),
        ("Valentina", new Color(0.900f, 0.400f, 0.100f)),
        ("Riccardo",  new Color(0.300f, 0.800f, 0.800f)),
        ("Federica",  new Color(0.800f, 0.200f, 0.800f)),
        ("Andrea",    new Color(0.200f, 0.800f, 0.400f)),
        ("Lorenzo",   new Color(0.900f, 0.600f, 0.300f)),
        ("Francesca", new Color(0.600f, 0.300f, 0.900f)),
        ("Alice",     new Color(0.300f, 0.900f, 0.600f)),
        ("Davide",    new Color(0.900f, 0.900f, 0.300f)),
        ("Sofia",     new Color(0.900f, 0.300f, 0.500f)),
        ("Emilia",    new Color(0.400f, 0.600f, 0.900f)),
    };

    [MenuItem("Tools/Classroom/Setup Aula 20 Studenti + Prospettiva Forzata")]
    static void Execute()
    {
        Undo.SetCurrentGroupName("Classroom Setup");
        int group = Undo.GetCurrentGroup();

        SetupWalls();
        SetupFloorCeiling();
        List<StudentNPC> npcs = SetupStudentsAndDesks();
        LinkStudentManager(npcs);

        Undo.CollapseUndoOperations(group);
        Debug.Log($"[ClassroomSetup] Completato: {npcs.Count} studenti, prospettiva forzata applicata.\n" +
                  "IMPORTANTE: salva la scena (Ctrl+S) per rendere permanenti le modifiche.");
    }

    // ───────────────────────────── Walls ──────────────────────────────────────

    static void SetupWalls()
    {
        // Muro nord (lavagna): più stretto
        ApplyWall("Wall_North",
            pos:    new Vector3(0f, WALL_Y, FRONT_Z),
            scale:  new Vector3(FRONT_HW * 2f, WALL_H, 0.2f),
            eulerY: 0f);

        // Muro sud (fondo): più largo → crea la prospettiva forzata
        ApplyWall("Wall_South",
            pos:    new Vector3(0f, WALL_Y, BACK_Z),
            scale:  new Vector3(BACK_HW * 2f, WALL_H, 0.2f),
            eulerY: 0f);

        // Pareti laterali inclinate
        // West: da (-FRONT_HW, y, FRONT_Z) a (-BACK_HW, y, BACK_Z)
        // East: simmetrico
        float dx      = BACK_HW - FRONT_HW;             // 4  (espansione per lato)
        float dz      = BACK_Z  - FRONT_Z;              // 14 (profondità)
        float sideLen = Mathf.Sqrt(dx * dx + dz * dz);  // ≈ 14.56
        float angle   = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg; // ≈ 15.95°

        float midZ = (FRONT_Z + BACK_Z)   * 0.5f;  // 0
        float midX = (FRONT_HW + BACK_HW) * 0.5f;  // 8

        ApplyWall("Wall_West",
            pos:    new Vector3(-midX, WALL_Y, midZ),
            scale:  new Vector3(0.2f, WALL_H, sideLen),
            eulerY: -angle);

        ApplyWall("Wall_East",
            pos:    new Vector3( midX, WALL_Y, midZ),
            scale:  new Vector3(0.2f, WALL_H, sideLen),
            eulerY:  angle);
    }

    static void ApplyWall(string name, Vector3 pos, Vector3 scale, float eulerY)
    {
        var go = GameObject.Find(name);
        if (go == null) { Debug.LogWarning($"[ClassroomSetup] Muro '{name}' non trovato nella scena."); return; }
        Undo.RecordObject(go.transform, "Setup Wall");
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, eulerY, 0f));
        go.transform.localScale = scale;
    }

    // ───────────────────────────── Floor / Ceiling ────────────────────────────

    static void SetupFloorCeiling()
    {
        // Scala il pavimento per coprire la stanza trapezoidale più ampia.
        // Se i GameObject non si chiamano "Floor"/"Ceiling" questa funzione non fa nulla.
        ScalePlane("Floor",   new Vector3(0f, 0f,     0.5f));
        ScalePlane("Ceiling", new Vector3(0f, WALL_H, 0.5f));
    }

    static void ScalePlane(string name, Vector3 newPos)
    {
        var go = GameObject.Find(name);
        if (go == null) return;
        Undo.RecordObject(go.transform, "Scale Plane");
        go.transform.position = newPos;
        // Scala proporzionalmente per coprire BACK_HW * 2 in X e la profondità totale in Z.
        // Assume il piano sia un Unity Plane (10×10 di default con scale 1).
        go.transform.localScale = new Vector3(BACK_HW * 2f / 10f, 1f, (BACK_Z - FRONT_Z) / 10f);
    }

    // ───────────────────────────── Students & Desks ───────────────────────────

    static List<StudentNPC> SetupStudentsAndDesks()
    {
        var studentsParent = FindOrCreate("Students");
        var desksParent    = FindOrCreate("Desks");

        ClearChildren(studentsParent);
        ClearStudentDesks(desksParent);

        Material deskMat = GetFirstDescendantMaterial(desksParent);

        var npcs = new List<StudentNPC>();
        int idx  = 0;

        for (int row = 0; row < ROW_Z.Length && idx < STUDENT_DATA.Length; row++)
        {
            float z  = ROW_Z[row];
            float t  = Mathf.InverseLerp(FRONT_Z, BACK_Z, z);
            float hw = Mathf.Lerp(FRONT_HW, BACK_HW, t) * X_MARGIN;

            for (int col = 0; col < COLS && idx < STUDENT_DATA.Length; col++)
            {
                float x = Mathf.Lerp(-hw, hw, (float)col / (COLS - 1));
                var (sName, sColor) = STUDENT_DATA[idx];

                CreateDesk(desksParent, new Vector3(x, 0f, z), idx, deskMat);
                npcs.Add(CreateStudentNPC(studentsParent, new Vector3(x, 0f, z), sName, sColor));
                idx++;
            }
        }

        // Banco del giocatore: centro davanti alla prima fila
        CreatePlayerDesk(desksParent, new Vector3(0f, 0f, -0.5f), deskMat);

        return npcs;
    }

    static void CreateDesk(GameObject parent, Vector3 pos, int idx, Material mat)
    {
        var root = new GameObject($"StudentDesk_{idx:D2}");
        Undo.RegisterCreatedObjectUndo(root, "Create Desk");
        root.transform.SetParent(parent.transform, false);
        root.transform.position = pos;

        var geo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(geo, "Create Desk Geo");
        geo.name = "Desk_Placeholder";
        geo.transform.SetParent(root.transform, false);
        geo.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        geo.transform.localScale    = new Vector3(1.2f, 0.05f, 0.8f);
        if (mat != null && geo.TryGetComponent<MeshRenderer>(out var mr))
            mr.sharedMaterial = mat;
    }

    static void CreatePlayerDesk(GameObject parent, Vector3 pos, Material mat)
    {
        var root = new GameObject("PlayerDesk");
        Undo.RegisterCreatedObjectUndo(root, "Create PlayerDesk");
        root.transform.SetParent(parent.transform, false);
        root.transform.position = pos;

        var geo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(geo, "Create PlayerDesk Geo");
        geo.name = "Desk_Placeholder";
        geo.transform.SetParent(root.transform, false);
        geo.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        geo.transform.localScale    = new Vector3(1.2f, 0.05f, 0.8f);
        if (mat != null && geo.TryGetComponent<MeshRenderer>(out var mr))
            mr.sharedMaterial = mat;
    }

    static StudentNPC CreateStudentNPC(GameObject parent, Vector3 pos, string sName, Color sColor)
    {
        var go = new GameObject(sName);
        Undo.RegisterCreatedObjectUndo(go, "Create StudentNPC");
        go.transform.SetParent(parent.transform, false);
        go.transform.position = pos;

        var npc = go.AddComponent<StudentNPC>();
        npc.studentName  = sName;
        npc.studentColor = sColor;
        EditorUtility.SetDirty(npc);

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Undo.RegisterCreatedObjectUndo(body, "Create StudentBody");
        body.name = "Capsule";
        body.transform.SetParent(go.transform, false);
        body.transform.localPosition = new Vector3(0f, 1f, 0f);
        body.transform.localScale    = new Vector3(0.5f, 0.5f, 0.5f);

        if (body.TryGetComponent<MeshRenderer>(out var mr))
        {
            mr.sharedMaterial = new Material(Shader.Find("Standard")) { color = sColor };
        }

        return npc;
    }

    // ───────────────────────────── Student Manager ────────────────────────────

    static void LinkStudentManager(List<StudentNPC> npcs)
    {
#pragma warning disable CS0618
        var sm = Object.FindObjectOfType<StudentManager>();
#pragma warning restore CS0618
        if (sm == null) { Debug.LogWarning("[ClassroomSetup] StudentManager non trovato in scena."); return; }

        var so   = new SerializedObject(sm);
        var list = so.FindProperty("allStudents");
        list.ClearArray();
        for (int i = 0; i < npcs.Count; i++)
        {
            list.InsertArrayElementAtIndex(i);
            list.GetArrayElementAtIndex(i).objectReferenceValue = npcs[i];
        }
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(sm);
        Debug.Log($"[ClassroomSetup] StudentManager aggiornato con {npcs.Count} studenti.");
    }

    // ───────────────────────────── Helpers ────────────────────────────────────

    static GameObject FindOrCreate(string name)
    {
        var go = GameObject.Find(name);
        if (go != null) return go;
        go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        return go;
    }

    static void ClearChildren(GameObject parent)
    {
        for (int i = parent.transform.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(parent.transform.GetChild(i).gameObject);
    }

    static void ClearStudentDesks(GameObject parent)
    {
        for (int i = parent.transform.childCount - 1; i >= 0; i--)
        {
            var child = parent.transform.GetChild(i).gameObject;
            if (child.name.StartsWith("StudentDesk_") || child.name == "PlayerDesk")
                Undo.DestroyObjectImmediate(child);
        }
    }

    static Material GetFirstDescendantMaterial(GameObject root)
    {
        foreach (Transform child in root.transform)
        {
            var mr = child.GetComponentInChildren<MeshRenderer>();
            if (mr != null) return mr.sharedMaterial;
        }
        return null;
    }
}
