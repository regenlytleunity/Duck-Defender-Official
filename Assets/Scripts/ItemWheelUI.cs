using TMPro;
using UnityEngine;

public class ItemWheelUI : MonoBehaviour
{
    PlayerItems _items;
    LocalPlayer _player;
    RectTransform _canvas, _wheel, _spin;
    readonly UnityEngine.UI.Image[] _icons = new UnityEngine.UI.Image[4], _quarters = new UnityEngine.UI.Image[4];
    readonly TMP_Text[] _keys = new TMP_Text[4];
    TMP_Text _result;
    RectTransform _mobile;
    static readonly string[] DirectionLabels = { "Up", "Right", "Down", "Left" };
    public void Initialize(PlayerItems items, LocalPlayer player)
    {
        _items = items; _player = player;
        _canvas = CoopUIElements.Canvas("Item HUD " + (player.Index + 1), null, 15);
        _wheel = CoopUIElements.Panel(_canvas, "Item selection", Vector2.one * .5f, Vector2.one * .5f, Color.clear);
        _wheel.sizeDelta = new Vector2(210, 210); _wheel.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        Vector2[] positions = { new Vector2(.5f, .78f), new Vector2(.78f, .5f), new Vector2(.5f, .22f), new Vector2(.22f, .5f) };
        for (int i = 0; i < 4; i++)
        {
            var quarter = CoopUIElements.Panel(_wheel, "Quarter " + (i + 1), Vector2.zero, Vector2.one, Color.white).GetComponent<UnityEngine.UI.Image>();
            quarter.sprite = ItemArea.Circle; quarter.type = UnityEngine.UI.Image.Type.Filled; quarter.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360; quarter.fillOrigin = 2; quarter.fillAmount = .25f;
            quarter.rectTransform.localEulerAngles = new Vector3(0, 0, 45 - i * 90); quarter.raycastTarget = false; _quarters[i] = quarter;
            var icon = CoopUIElements.Panel(_wheel, "Item icon", positions[i] - Vector2.one * .11f, positions[i] + Vector2.one * .11f, Color.white).GetComponent<UnityEngine.UI.Image>();
            icon.preserveAspect = true; icon.raycastTarget = false; _icons[i] = icon;
            _keys[i] = CoopUIElements.Text(_wheel, "", positions[i] + new Vector2(-.14f, -.2f), positions[i] + new Vector2(.14f, -.1f), 16);
        }
        foreach (var quarter in _quarters) quarter.transform.SetAsFirstSibling();
        _spin = CoopUIElements.Panel(_canvas, "Casino wheel", Vector2.one * .5f, Vector2.one * .5f, new Color(.8f, .1f, .1f));
        _spin.sizeDelta = new Vector2(150, 150); _spin.GetComponent<UnityEngine.UI.Image>().sprite = ItemArea.Circle;
        var blue = CoopUIElements.Panel(_spin, "Positive 80 percent", Vector2.zero, Vector2.one, new Color(.08f, .4f, 1)).GetComponent<UnityEngine.UI.Image>();
        blue.sprite = ItemArea.Circle; blue.type = UnityEngine.UI.Image.Type.Filled; blue.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360; blue.fillAmount = .8f; blue.raycastTarget = false;
        _spin.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        _result = CoopUIElements.Text(_canvas, "", Vector2.one * .5f, Vector2.one * .5f, 27); _result.rectTransform.sizeDelta = new Vector2(430, 65);
        _mobile = CoopUIElements.Panel(_canvas, "Touch items", new Vector2(.32f, .01f), new Vector2(.68f, .15f), new Color(.04f, .06f, .12f, .8f));
        for (int i = 0; i < 4; i++)
        {
            int slot = i; var button = CoopUIElements.Button(_mobile, (i + 1).ToString(), new Vector2(i * .2f, 0), new Vector2(i * .2f + .19f, 1), () => _items.Select(slot));
            var icon = CoopUIElements.Panel(button.transform, "Icon", new Vector2(.2f, .35f), new Vector2(.8f, .9f), Color.white).GetComponent<UnityEngine.UI.Image>();
            icon.sprite = ItemCatalog.Icon(items.Slots[i]); icon.preserveAspect = true; icon.raycastTarget = false; icon.enabled = icon.sprite != null;
            CoopUIElements.Stretch(button.GetComponentInChildren<TMP_Text>().rectTransform, new Vector2(0, 0), new Vector2(1, .35f));
        }
        CoopUIElements.Button(_mobile, "Use", new Vector2(.8f, 0), Vector2.one, () => _items.UseSelected());
    }
    void LateUpdate()
    {
        if (_canvas == null || _items == null || _player == null) return;
        bool alive = _player.Alive;
        _wheel.gameObject.SetActive(alive && Time.time < _items.SelectionUntil);
        _spin.gameObject.SetActive(alive && Time.time < _items.SpinUntil);
        _result.gameObject.SetActive(alive && Time.time < _items.ResultUntil);
        _mobile.gameObject.SetActive(alive && MobileInputController.Instance != null && MobileInputController.Instance.IsMobileEnabled);
        if (Camera.main != null)
        {
            Vector2 screen = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 3);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out var position);
            position.x = Mathf.Clamp(position.x, _canvas.rect.xMin + 110, _canvas.rect.xMax - 110);
            position.y = Mathf.Clamp(position.y, _canvas.rect.yMin + 110, _canvas.rect.yMax - 110);
            _wheel.anchoredPosition = _spin.anchoredPosition = position; _result.rectTransform.anchoredPosition = position;
        }
        for (int i = 0; i < 4; i++)
        {
            _quarters[i].color = i == _items.SelectedSlot ? new Color(.35f, .5f, .65f, .94f) : new Color(.06f, .1f, .17f, .9f);
            _icons[i].sprite = ItemCatalog.Icon(_items.Slots[i]); _icons[i].enabled = _icons[i].sprite != null;
            _keys[i].text = _player.UsesGamepad ? DirectionLabels[i] : (i + 1).ToString();
            if (_mobile.gameObject.activeSelf)
            {
                var button = _mobile.GetChild(i).GetComponent<UnityEngine.UI.Button>();
                button.image.color = i == _items.SelectedSlot ? new Color(.4f, .5f, .65f) : new Color(.16f, .23f, .32f);
                var icon = button.transform.Find("Icon").GetComponent<UnityEngine.UI.Image>(); icon.sprite = _icons[i].sprite; icon.enabled = icon.sprite != null;
            }
        }
        _spin.localEulerAngles = new Vector3(0, 0, (_items.SpinUntil - Time.time) * 720);
        _result.text = _items.ResultText; _result.alpha = Mathf.Clamp01(_items.ResultUntil - Time.time);
    }
    void OnDestroy() { if (_canvas != null) Destroy(_canvas.gameObject); }
}
