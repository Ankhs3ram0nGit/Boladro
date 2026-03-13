using UnityEngine;

[DisallowMultipleComponent]
public class SpawnToBattleBridge : MonoBehaviour
{
    public BattleSystem battleSystem;

    void Awake()
    {
        if (battleSystem == null) battleSystem = GetComponent<BattleSystem>();
        if (battleSystem == null)
        {
            GameObject player = GameObject.Find("Player");
            if (player != null) battleSystem = player.GetComponent<BattleSystem>();
        }
    }

    void OnEnable()
    {
        SpawnManager.Instance.OnEncounterResolved += HandleEncounterResolved;
    }

    void OnDisable()
    {
        if (SpawnManager.HasInstance)
        {
            SpawnManager.Instance.OnEncounterResolved -= HandleEncounterResolved;
        }
    }

    void HandleEncounterResolved(CreatureEncounterData data)
    {
        if (battleSystem == null) return;
        battleSystem.StartEncounterFromSpawn(data);
    }
}
