using UnityEngine;
using UnityEngine.SceneManagement;

public static class CreaturePartySidebarBootstrap
{
    private static bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeAfterSceneLoad()
    {
        SubscribeSceneEvents();
        EnsurePartySidebarWired();
    }

    private static void SubscribeSceneEvents()
    {
        if (subscribed) return;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        subscribed = true;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsurePartySidebarWired();
    }

    private static void EnsurePartySidebarWired()
    {
        PlayerCreatureParty party = EnsurePartySource();
        InventoryUI inventoryUI = Object.FindFirstObjectByType<InventoryUI>();
        if (inventoryUI == null) return;

        CreaturePartySidebarUI sidebar = inventoryUI.GetComponent<CreaturePartySidebarUI>();
        if (sidebar == null)
        {
            sidebar = inventoryUI.gameObject.AddComponent<CreaturePartySidebarUI>();
        }

        if (sidebar.partySource == null)
        {
            sidebar.partySource = party;
        }
    }

    private static PlayerCreatureParty EnsurePartySource()
    {
        PlayerMover mover = Object.FindFirstObjectByType<PlayerMover>();
        if (mover == null) return null;

        PlayerCreatureParty party = mover.GetComponent<PlayerCreatureParty>();
        if (party == null)
        {
            party = mover.gameObject.AddComponent<PlayerCreatureParty>();
        }

        if (party.ActiveCreatures == null || party.ActiveCreatures.Count == 0)
        {
            party.InitializeParty();
        }

        return party;
    }
}

