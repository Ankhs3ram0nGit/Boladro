using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AreaSpawnConfig))]
public class AreaSpawnConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        AreaSpawnConfig cfg = (AreaSpawnConfig)target;
        if (cfg == null) return;

        DrawPoolPercentages("Main Pool Percentages", cfg.mainPool);
        DrawPoolPercentages("Rare Pool Percentages", cfg.rarePool);
    }

    void DrawPoolPercentages(string title, System.Collections.Generic.List<CreatureSpawnEntry> pool)
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        if (pool == null || pool.Count == 0)
        {
            EditorGUILayout.HelpBox("No entries.", MessageType.Info);
            return;
        }

        float total = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            CreatureSpawnEntry e = pool[i];
            if (e == null || e.weight <= 0) continue;
            total += e.weight;
        }

        if (total <= 0f)
        {
            EditorGUILayout.HelpBox("Total weight is zero.", MessageType.Warning);
            return;
        }

        for (int i = 0; i < pool.Count; i++)
        {
            CreatureSpawnEntry e = pool[i];
            if (e == null) continue;
            float pct = Mathf.Max(0f, e.weight) / total * 100f;
            string id = string.IsNullOrWhiteSpace(e.creatureID) ? "(empty id)" : e.creatureID;
            EditorGUILayout.LabelField(id, pct.ToString("0.0") + "%");
        }
    }
}
