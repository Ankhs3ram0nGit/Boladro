using UnityEngine;
using UnityEngine.UI;

public class GameOverUI : MonoBehaviour
{
    public PlayerHealth player;
    public GameObject panel;
    public bool hideOnStart = true;

    void OnEnable()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerHealth>();
        }
        if (panel == null) panel = gameObject;

        Button button = null;
        Button[] buttons = GetComponentsInChildren<Button>(true);
        if (buttons != null && buttons.Length > 0)
        {
            button = buttons[0];
        }
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(Respawn);
        }

        if (player != null)
        {
            player.OnDied += Show;
        }
    }

    void Start()
    {
        if (panel != null && hideOnStart)
        {
            panel.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (player != null)
        {
            player.OnDied -= Show;
        }
    }

    void Show()
    {
        panel.SetActive(true);
    }

    public void Respawn()
    {
        if (player != null)
        {
            player.Respawn();
        }
        panel.SetActive(false);
    }
}
