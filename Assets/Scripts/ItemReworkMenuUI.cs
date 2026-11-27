using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemReworkMenuUI : MonoBehaviour
{
    MainMenuUI _menu;
    ShopManager _shop;
    RectTransform _daily, _box, _lid;
    Button _open, _strike;
    readonly Button[] _quests = new Button[3];
    readonly Image[] _progress = new Image[3];
    TMP_Text _timer, _boxResult;
    int _hits, _reward;
    bool _duplicate, _opening;
    float _ready, _refresh;
    int _questNavigationState = -1;
    public bool BoxOpen => _box != null && _box.gameObject.activeInHierarchy;

    public void Initialize(MainMenuUI menu, Transform items)
    {
        _menu = menu; _shop = ShopManager.Instance;
        var chest = CoopUIElements.Panel(items, "Item box", new Vector2(.32f, .32f), new Vector2(.68f, .75f), new Color(.45f, .25f, .10f));
        var lid = CoopUIElements.Panel(chest, "Lid", new Vector2(-.03f, .75f), new Vector2(1.03f, 1.03f), new Color(.65f, .4f, .16f));
        var latch = CoopUIElements.Panel(chest, "Lock", new Vector2(.43f, .4f), new Vector2(.57f, .65f), new Color(.95f, .72f, .2f));
        ApplyBoxArtwork(chest.GetComponent<Image>(), lid.GetComponent<Image>(), latch.gameObject);
        _open = CoopUIElements.Button(items, "", new Vector2(.25f, .08f), new Vector2(.75f, .26f), OpenBox);
        _daily = CoopUIElements.Panel(menu.MenuPanel.transform, "Daily quests", new Vector2(.325f, .20f), new Vector2(.625f, .63f), new Color(.035f, .055f, .09f, .92f));
        CoopUIElements.Text(_daily, "Daily quests", new Vector2(.03f, .87f), new Vector2(.97f, .98f), 29);
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            float top = .84f - i * .235f;
            _quests[i] = CoopUIElements.Button(_daily, "", new Vector2(.035f, top - .21f), new Vector2(.965f, top), () => Claim(index));
            _progress[i] = CoopUIElements.Panel(_quests[i].transform, "Progress", Vector2.zero, Vector2.one, new Color(.08f, .32f, .64f)).GetComponent<Image>();
            _progress[i].raycastTarget = false;
            _progress[i].transform.SetAsFirstSibling();
            _quests[i].GetComponentInChildren<TMP_Text>().fontSizeMax = 23;
        }
        _timer = CoopUIElements.Text(_daily, "", new Vector2(.03f, .015f), new Vector2(.97f, .11f), 21);
        _shop.RefreshDaily();
        _shop.OnCollectionChanged += Refresh;
        Refresh();
    }
    void Update()
    {
        if (_shop == null || Time.unscaledTime < _refresh) return;
        _refresh = Time.unscaledTime + .25f;
        if (_menu.MenuPanel.activeInHierarchy) _shop.RefreshDaily();
        Refresh();
    }
    void Refresh()
    {
        if (_shop == null) return;
        _open.GetComponentInChildren<TMP_Text>().text = "Open box\n" + _shop.Progress.Keys + "/1 Keys";
        _open.interactable = !_shop.TutorialBlocksShopping && _shop.Progress.Keys > 0;
        _daily.gameObject.SetActive(!_shop.Progress.Tutorial.CoreActive);
        if (!_daily.gameObject.activeSelf)
        {
            RefreshQuestNavigation(0);
            return;
        }
        var data = _shop.Progress.Daily;
        int navigationState = 8;
        for (int i = 0; i < 3; i++)
        {
            int kind = data.Kinds[i];
            float progress = Mathf.Clamp01(data.Progress[i] / DailyQuests.Targets[kind]);
            bool ready = progress >= 1 && !data.Claimed[i];
            if (ready) navigationState |= 1 << i;
            _quests[i].interactable = ready;
            string state = data.Claimed[i] ? "Claimed" : ready ? "(Interact) to claim" : Mathf.FloorToInt(data.Progress[i]) + "/" + DailyQuests.Targets[kind];
            _quests[i].GetComponentInChildren<TMP_Text>().text = DailyQuests.Titles[kind] + "\n" + DailyQuests.Rewards[data.Rewards[i]] + (data.Bonus == i ? " + 1 key" : "") + "\n" + state;
            _progress[i].rectTransform.anchorMax = new Vector2(progress, 1);
            _progress[i].color = data.Claimed[i] ? new Color(.23f, .25f, .28f) : ready ? new Color(.08f, .48f + Mathf.Sin(Time.unscaledTime * 3) * .06f, .20f) : new Color(.08f, .32f, .64f);
        }
        long seconds = System.Math.Max(0, DailyQuests.ResetAt - DailyQuests.Now);
        _timer.text = "Refresh in " + System.TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss");
        RefreshQuestNavigation(navigationState);
    }
    void RefreshQuestNavigation(int state)
    {
        if (_questNavigationState == state || !_menu.MenuPanel.activeInHierarchy) return;
        _questNavigationState = state;
        ControllerMenuNavigation.ConfigureNavigation(_menu.MenuPanel);
    }
    void Claim(int index)
    {
        if (!_shop.ClaimQuest(index, out var cards, out string maxed)) return;
        if (cards.Count > 0) _menu.RevealQuestCards(cards);
        if (maxed != null) StartCoroutine(MaxedPopup(maxed));
        Refresh();
        if (cards.Count == 0) MainMenuUI.FocusFirstControl(_menu.MenuPanel);
    }
    IEnumerator MaxedPopup(string message)
    {
        var text = CoopUIElements.Text(_menu.OpeningOverlay.transform, message, new Vector2(.15f, .83f), new Vector2(.85f, .97f), 34);
        for (float elapsed = 0; elapsed < 3; elapsed += Time.unscaledDeltaTime)
        {
            text.rectTransform.anchoredPosition += Vector2.up * Time.unscaledDeltaTime * 15;
            text.alpha = Mathf.Min(1, 3 - elapsed);
            yield return null;
        }
        Destroy(text.gameObject);
    }
    void OpenBox()
    {
        if (_opening || BoxOpen) return;
        var levels = (int[])_shop.Progress.ItemLevels.Clone();
        _reward = _shop.OpenItemBox();
        if (_reward == 0) return;
        _duplicate = levels[_reward] > 0;
        _hits = Random.Range(3, 6); _ready = Time.unscaledTime + .3f;
        _menu.ShopPanel.SetActive(false);
        _menu.OpeningOverlay.SetActive(true);
        _menu.PackImage.gameObject.SetActive(false);
        foreach (var pack in _menu.AdditionalPackImages) if (pack != null) pack.gameObject.SetActive(false);
        _menu.SliceZone.SetActive(false); _menu.SliceHelpText.SetActive(false);
        _menu.CardRevealCenter.gameObject.SetActive(false);
        _menu.ContinueButton.gameObject.SetActive(false);
        if (_box != null) Destroy(_box.gameObject);
        _box = CoopUIElements.Panel(_menu.OpeningOverlay.transform, "Box opening", Vector2.zero, Vector2.one, new Color(.025f, .035f, .06f));
        _strike = CoopUIElements.Button(_box, "", new Vector2(.36f, .28f), new Vector2(.64f, .68f), Strike);
        _strike.image.color = new Color(.45f, .25f, .1f);
        _lid = CoopUIElements.Panel(_strike.transform, "Lid", new Vector2(-.03f, .75f), new Vector2(1.03f, 1.03f), new Color(.65f, .4f, .16f));
        _lid.GetComponent<Image>().raycastTarget = false;
        var lockImage = CoopUIElements.Panel(_strike.transform, "Lock", new Vector2(.43f, .4f), new Vector2(.57f, .65f), new Color(.95f, .72f, .2f)).GetComponent<Image>();
        lockImage.raycastTarget = false;
        ApplyBoxArtwork(_strike.image, _lid.GetComponent<Image>(), lockImage.gameObject);
        _boxResult = CoopUIElements.Text(_box, "", new Vector2(.15f, .07f), new Vector2(.85f, .25f), 36);
        StartCoroutine(Zoom());
        MainMenuUI.FocusFirstControl(_box.gameObject);
    }
    void ApplyBoxArtwork(Image body, Image lid, GameObject latch)
    {
        if (_menu.ItemBoxBody != null)
        {
            body.sprite = _menu.ItemBoxBody; body.color = Color.white; body.preserveAspect = true;
            latch.SetActive(false);
        }
        if (_menu.ItemBoxLid != null)
        { lid.sprite = _menu.ItemBoxLid; lid.color = Color.white; lid.preserveAspect = true; }
    }
    IEnumerator Zoom()
    {
        for (float elapsed = 0; elapsed < .3f; elapsed += Time.unscaledDeltaTime)
        {
            _strike.transform.localScale = Vector3.one * Mathf.Lerp(.6f, 1, elapsed / .3f);
            yield return null;
        }
        _strike.transform.localScale = Vector3.one;
    }
    void Strike()
    {
        if (_opening || Time.unscaledTime < _ready || _hits <= 0) return;
        _ready = Time.unscaledTime + .18f;
        _hits--;
        AudioManager.Instance?.PlaySFX("UI_button_Click");
        StartCoroutine(Shake());
        if (_hits == 0) StartCoroutine(Reveal());
    }
    IEnumerator Shake()
    {
        var rect = _box;
        for (float elapsed = 0; elapsed < .15f; elapsed += Time.unscaledDeltaTime)
        {
            rect.anchoredPosition = Random.insideUnitCircle * 9;
            yield return null;
        }
        rect.anchoredPosition = Vector2.zero;
    }
    IEnumerator Reveal()
    {
        _opening = true; _strike.interactable = false;
        for (float elapsed = 0; elapsed < .6f; elapsed += Time.unscaledDeltaTime)
        {
            _lid.anchoredPosition = new Vector2(elapsed * 180, elapsed * 500);
            _lid.localEulerAngles = new Vector3(0, 0, -elapsed * 100);
            yield return null;
        }
        _lid.gameObject.SetActive(false);
        var icon = CoopUIElements.Panel(_box, "Revealed item", new Vector2(.43f, .43f), new Vector2(.57f, .65f), Color.white).GetComponent<Image>();
        icon.sprite = ItemCatalog.Icon(_reward); icon.preserveAspect = true;
        _boxResult.text = ItemCatalog.Names[_reward] + (_duplicate ? "\n+10 shards" : "\nUnlocked");
        var close = CoopUIElements.Button(_box, "Continue", new Vector2(.72f, .05f), new Vector2(.95f, .16f), () => { HideBox(); _menu.ShowPanel(_menu.ShopPanel); });
        _opening = false;
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(close.gameObject);
    }
    public void HideBox()
    {
        if (_box != null) _box.gameObject.SetActive(false);
        _opening = false;
    }
    void OnDestroy() { if (_shop != null) _shop.OnCollectionChanged -= Refresh; }
}
