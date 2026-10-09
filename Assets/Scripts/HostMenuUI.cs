using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class HostMenuUI : MonoBehaviour
{
    public UnityEngine.UI.Button HostButton;
    public GameObject Panel;
    public bool CardFilterOpen { get; private set; }
    MainMenuUI _menu;
    TMP_Text _players, _difficulty, _respawn, _colors, _status;
    UnityEngine.UI.Button _start;
    int _count = 2, _difficultyIndex;
    void Start()
    {
        _menu = GetComponent<MainMenuUI>();
        if (HostButton != null) HostButton.onClick.AddListener(Open);
        if (GetComponent<ControllerMenuNavigation>() == null) gameObject.AddComponent<ControllerMenuNavigation>();
    }
    public void Open()
    {
        if (_menu == null) _menu = GetComponent<MainMenuUI>();
        _menu.ShowPanel(_menu.MenuPanel); _menu.MenuPanel.SetActive(false);
        EnsurePanel(); Panel.SetActive(true); Refresh();
    }
    public void Hide() { if (Panel != null) Panel.SetActive(false); }
    public void OpenCards()
    {
        CardFilterOpen = true;
        var index = _menu.IndexPanel.GetComponent<CardIndexUI>() ?? _menu.IndexPanel.GetComponentInChildren<CardIndexUI>(true);
        index.HostFilterMode = true;
        _menu.ShowPanel(_menu.IndexPanel); index.GenerateIndex();
    }
    public bool ReturnFromCards()
    {
        if (!CardFilterOpen) return false;
        CardFilterOpen = false;
        var index = _menu.IndexPanel.GetComponentInChildren<CardIndexUI>(true);
        index.HostFilterMode = false; Open(); return true;
    }
    void EnsurePanel()
    {
        if (Panel != null) return;
        var panel = CoopUIElements.Panel(transform, "Host local game", Vector2.zero, Vector2.one, new Color(.04f, .065f, .1f)); Panel = panel.gameObject;
        CoopUIElements.Text(panel, "HOST LOCAL CO-OP", new Vector2(.12f, .87f), new Vector2(.88f, .98f), 48);
        CoopUIElements.Text(panel, "Controllers required for every player.\nLeft stick / D-pad: move and jump  |  Right stick: aim  |  RT: fire  |  LB: dash\nB (right face button): select  |  A (bottom face button): back", new Vector2(.1f, .71f), new Vector2(.9f, .87f), 26);
        _players = Row(panel, .61f, () => { _count = _count == 4 ? 2 : _count + 1; Refresh(); });
        _difficulty = Row(panel, .50f, () => { _difficultyIndex = (_difficultyIndex + 1) % 3; Refresh(); });
        _respawn = Row(panel, .39f, () => { LocalCoopSession.Respawning = !LocalCoopSession.Respawning; Refresh(); });
        _colors = Row(panel, .28f, () => { LocalCoopSession.ColoredHealthBars = !LocalCoopSession.ColoredHealthBars; Refresh(); });
        CoopUIElements.Button(panel, "Enabled cards", new Vector2(.35f, .17f), new Vector2(.65f, .25f), OpenCards);
        _status = CoopUIElements.Text(panel, "", new Vector2(.05f, .09f), new Vector2(.95f, .16f), 24);
        CoopUIElements.Button(panel, "Back", new Vector2(.05f, .015f), new Vector2(.25f, .085f), () => { Hide(); _menu.ShowPanel(_menu.MenuPanel); });
        _start = CoopUIElements.Button(panel, "Start co-op", new Vector2(.7f, .015f), new Vector2(.95f, .085f), StartGame);
    }
    static TMP_Text Row(Transform panel, float y, UnityEngine.Events.UnityAction action)
    {
        var button = CoopUIElements.Button(panel, "", new Vector2(.25f, y), new Vector2(.75f, y + .085f), action);
        return button.GetComponentInChildren<TMP_Text>();
    }
    void Update() { if (Panel != null && Panel.activeSelf) Refresh(); }
    void Refresh()
    {
        if (_players == null) return;
        bool test = LocalCoopSession.KeyboardTest && _count == 2;
        int required = test ? 1 : _count;
        _players.text = "Players: " + _count + "   >";
        _difficulty.text = "Difficulty: " + ((RunDifficulty)_difficultyIndex) + "   >";
        _respawn.text = "Respawn next wave: " + (LocalCoopSession.Respawning ? "ON" : "OFF");
        _colors.text = "Player-colored health bars: " + (LocalCoopSession.ColoredHealthBars ? "ON" : "OFF");
        _start.interactable = Gamepad.all.Count >= required;
        _status.text = (test ? "Dev mode: P1 keyboard/mouse + P2 controller. " : "") + Gamepad.all.Count + "/" + required + " controllers connected. Press a controller button if it is not detected.";
    }
    public void StartGame()
    {
        int required = LocalCoopSession.KeyboardTest && _count == 2 ? 1 : _count;
        if (Gamepad.all.Count < required) { Refresh(); return; }
        LocalCoopSession.RequestedPlayers = _count; GameDifficulty.Select(_difficultyIndex);
        var controller = FindFirstObjectByType<MainMenuController>();
        Time.timeScale = 1;
        UnityEngine.SceneManagement.SceneManager.LoadScene(controller.GameSceneName);
    }
}
