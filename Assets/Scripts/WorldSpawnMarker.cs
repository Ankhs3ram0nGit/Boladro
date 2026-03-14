using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WorldSpawnMarker : MonoBehaviour
{
    public string creatureID;
    public string zoneID;
    public int level;

    private static readonly HashSet<WorldSpawnMarker> ActiveSet = new HashSet<WorldSpawnMarker>();
    public static IReadOnlyCollection<WorldSpawnMarker> ActiveMarkers => ActiveSet;

    void OnEnable()
    {
        ActiveSet.Add(this);
    }

    void OnDisable()
    {
        ActiveSet.Remove(this);
    }

    void OnDestroy()
    {
        ActiveSet.Remove(this);
    }
}
