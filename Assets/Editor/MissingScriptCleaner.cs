using UnityEditor;
using UnityEngine;

public static class MissingScriptCleaner
{
    const string PrefKey = "Boladro_MissingScriptsCleaned";

    [InitializeOnLoadMethod]
    static void AutoCleanOnLoad()
    {
        if (EditorPrefs.GetBool(PrefKey, false)) return;
        int removed = CleanMissingScriptsInternal();
        EditorPrefs.SetBool(PrefKey, true);
        Debug.Log("Auto-cleaned missing scripts: " + removed);
    }

    [MenuItem("Tools/Boladro/Clean Missing Scripts")]
    public static void CleanMissingScripts()
    {
        int removed = CleanMissingScriptsInternal();
        Debug.Log("Removed missing scripts: " + removed);
    }

    static int CleanMissingScriptsInternal()
    {
        int removed = 0;
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject go in objects)
        {
            if (go == null) continue;
            int before = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (before > 0)
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                removed += before;
            }
        }
        return removed;
    }
}
