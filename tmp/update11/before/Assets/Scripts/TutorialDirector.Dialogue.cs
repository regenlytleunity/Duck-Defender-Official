using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public partial class TutorialDirector
{
    RectTransform _portrait, _tail;
    UnityEngine.UI.Button _continueButton;
    UnityEngine.UI.Image _continueGlow;
    CanvasGroup _dialogueFade, _portraitFade;
    float _revealedCharacters, _linger, _collapse;
    bool _collapsed, _wasReady;
    const float CharactersPerSecond = 42;
    float ReadingSeconds => Mathf.Clamp(2 + _message.textInfo.characterCount / 22f, 5, 11);
    bool DialogueRevealed => _message != null && _revealedCharacters >= _message.textInfo.characterCount;

    void BuildDialogue()
    {
        _panel = CoopUIElements.Panel(_canvas, "Tutorial prompt", new Vector2(.22f, .77f), new Vector2(.80f, .98f), PromptColor);
        _dialogueFade = _panel.gameObject.AddComponent<CanvasGroup>();
        _panel.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        var rim = CoopUIElements.Panel(_panel, "Speech box rim", Vector2.zero, Vector2.one, Color.clear);
        rim.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        var top = CoopUIElements.Panel(rim, "Top rim", new Vector2(0, 1), Vector2.one, new Color(.38f, .5f, .64f));
        top.pivot = new Vector2(.5f, 1); top.sizeDelta = new Vector2(-8, 3); top.anchoredPosition = new Vector2(0, -4);
        top.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        var tailObject = new GameObject("Speech tail", typeof(RectTransform), typeof(CanvasRenderer), typeof(TutorialSpeechTail));
        _tail = (RectTransform)tailObject.transform; _tail.SetParent(_panel, false);
        _tail.anchorMin = _tail.anchorMax = new Vector2(.88f, 0); _tail.pivot = new Vector2(.5f, 1);
        _tail.sizeDelta = new Vector2(80, 32);
        var tailGraphic = tailObject.GetComponent<TutorialSpeechTail>(); tailGraphic.color = PromptColor; tailGraphic.raycastTarget = false;

        _message = CoopUIElements.Text(_panel, "", new Vector2(.12f, .29f), new Vector2(.96f, .92f), 30);
        _message.useMaxVisibleDescender = false;
        _message.alignment = TextAlignmentOptions.MidlineLeft;
        var shadow = _message.gameObject.AddComponent<UnityEngine.UI.Shadow>();
        shadow.effectColor = new Color(0, 0, 0, .95f); shadow.effectDistance = new Vector2(2, -2);
        _hint = CoopUIElements.Text(_panel, "", new Vector2(.13f, .04f), new Vector2(.87f, .25f), 20);
        _continueButton = CoopUIElements.Button(_panel, "Continue", new Vector2(.22f, .035f), new Vector2(.78f, .25f), () =>
        {
            if (_collapsed && (_next == null || !_ready)) ExpandDialogue();
            else ContinueDialogue();
        });
        _continueButton.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        var continueLabel = _continueButton.GetComponentInChildren<TMP_Text>();
        continueLabel.fontSize = continueLabel.fontSizeMax = 22; continueLabel.fontSizeMin = 16;
        continueLabel.alignment = TextAlignmentOptions.Midline;
        CoopUIElements.Stretch(continueLabel.rectTransform, new Vector2(.035f, .15f), new Vector2(.965f, .85f));
        _continueGlow = TutorialGuidance.Attach((RectTransform)_continueButton.transform);

        var circle = CoopUIElements.Panel(_panel, "Stick illustration", new Vector2(.065f, .61f), new Vector2(.065f, .61f), new Color(.2f, .28f, .4f));
        circle.sizeDelta = new Vector2(64, 64);
        circle.GetComponent<UnityEngine.UI.Image>().sprite = ItemArea.Circle;
        circle.GetComponent<UnityEngine.UI.Image>().preserveAspect = true;
        circle.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        _stick = CoopUIElements.Panel(circle, "Stick", Vector2.one * .5f, Vector2.one * .5f, Color.white);
        _stick.sizeDelta = new Vector2(27, 27);
        _stick.GetComponent<UnityEngine.UI.Image>().sprite = ItemArea.Circle;
        _stick.GetComponent<UnityEngine.UI.Image>().preserveAspect = true;
        _stick.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;

        _portrait = CoopUIElements.Panel(_canvas, "Tutorial duck", new Vector2(.94f, .53f), new Vector2(.94f, .53f), Color.white);
        _portrait.sizeDelta = Vector2.one * (_canvas.rect.height * .44f);
        _portraitFade = _portrait.gameObject.AddComponent<CanvasGroup>();
        var portraitImage = _portrait.GetComponent<UnityEngine.UI.Image>();
        var host = _menu != null ? _menu.GetComponent<HostMenuUI>() : null;
        portraitImage.sprite = _player != null && _player.Sprite != null ? _player.Sprite.sprite : host != null ? host.DuckPreviewSprite : null;
        portraitImage.preserveAspect = true;
        portraitImage.raycastTarget = false;
        // The authored sprite faces left, inward toward the speech box.
        _portrait.localScale = Vector3.one;
        _portrait.gameObject.SetActive(false);
    }

    void SetPromptText(string text, bool restart = false)
    {
        if (!restart && _message.text == text) return;
        _message.text = text;
        // Layout every character first; visibility changes never add words or reflow lines.
        _message.maxVisibleCharacters = int.MaxValue;
        _message.ForceMeshUpdate(true);
        _revealedCharacters = 0; _message.maxVisibleCharacters = 0;
        _linger = 0; _collapse = 0; _collapsed = false; _wasReady = _ready;
        _panel.gameObject.SetActive(true); _dialogueFade.alpha = _portraitFade.alpha = 1;
        _message.gameObject.SetActive(true);
        if (_portrait != null) _portrait.gameObject.SetActive(true);
    }

    void RevealDialogue()
    {
        _revealedCharacters = _message.textInfo.characterCount;
        _message.maxVisibleCharacters = int.MaxValue; _linger = 0;
    }
    void ExpandDialogue()
    {
        if (_lastPrompt == null || _skipOpen) return;
        _collapsed = false; _linger = 0; _collapse = 0;
        _panel.gameObject.SetActive(true); _dialogueFade.alpha = _portraitFade.alpha = 1;
        RevealDialogue();
    }
    void UpdateDialogue(float elapsed)
    {
        if (_ready && !_wasReady) ExpandDialogue();
        _wasReady = _ready;
        if (!DialogueRevealed)
        {
            _revealedCharacters += elapsed * CharactersPerSecond;
            _message.maxVisibleCharacters = Mathf.FloorToInt(_revealedCharacters);
        }
        else
        {
            _linger += elapsed;
            if (_linger >= ReadingSeconds) _collapsed = true;
        }
        _collapse = Mathf.MoveTowards(_collapse, _collapsed ? 1 : 0, elapsed * 5);
    }
    void UpdateDialogueControls(bool blocked)
    {
        bool canContinue = _next != null && _ready;
        bool hidden = _collapse >= 1;
        _dialogueFade.alpha = _portraitFade.alpha = 1 - _collapse;
        _dialogueFade.blocksRaycasts = !hidden && !blocked;
        _panel.gameObject.SetActive(!hidden);
        _portrait.gameObject.SetActive(!hidden && _portrait.GetComponent<UnityEngine.UI.Image>().sprite != null);
        _message.gameObject.SetActive(true);
        _tail.gameObject.SetActive(true);
        _hint.gameObject.SetActive(!canContinue);
        _hint.text = "Show again: " + TutorialBindingLabel;
        _continueButton.gameObject.SetActive(canContinue);
        _continueButton.interactable = !blocked;
        var label = _continueButton.GetComponentInChildren<TMP_Text>();
        label.text = DialogueRevealed ? ContinueHint : "Reveal text";
        _continueGlow.gameObject.SetActive(canContinue && DialogueRevealed && !hidden);
        var rect = (RectTransform)_continueButton.transform;
        CoopUIElements.Stretch(rect, new Vector2(.22f, .035f), new Vector2(.78f, .25f));
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(42, rect.rect.height));
        bool showStick = _practice && _step >= PracticeStep.Move && _step <= PracticeStep.Shoot && (UsingPad || UsingTouch);
        _stick.parent.gameObject.SetActive(showStick);
        _stick.anchoredPosition = _step == PracticeStep.Jump ? new Vector2(0, Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3)) * 16) :
            _step >= PracticeStep.Aim ? new Vector2(Mathf.Cos(Time.unscaledTime * 2), Mathf.Sin(Time.unscaledTime * 2)) * 16 :
            new Vector2(Mathf.Sin(Time.unscaledTime * 3) * 16, 0);
        _portrait.anchoredPosition = !blocked && !DialogueRevealed ? new Vector2(0, Mathf.Sin(Time.unscaledTime * 5) * 3) : Vector2.zero;
        if (_lastPrompt != null && _lastPrompt.StartsWith("TUTORIAL COMPLETE"))
        {
            var color = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * .2f, 1), .6f, .3f);
            color.a = PromptColor.a; _panel.GetComponent<UnityEngine.UI.Image>().color = color;
        }
    }
    void LayoutDialogue()
    {
        if (_panel == null || !_panel.gameObject.activeSelf) return;
        Vector2 min = new Vector2(.22f, .77f), max = new Vector2(.80f, .98f);
        if (_panel.anchorMin != min || _panel.anchorMax != max) CoopUIElements.Stretch(_panel, min, max);
        // Leave the complete head, including its right-side eye, inside the screen.
        _portrait.anchorMin = _portrait.anchorMax = new Vector2(.94f, .53f);
        _portrait.sizeDelta = Vector2.one * (_canvas.rect.height * .44f);
        _tail.anchorMin = _tail.anchorMax = new Vector2(.88f, 0);
        _tail.localRotation = Quaternion.identity;
    }

    readonly List<RaycastResult> _pointerHits = new List<RaycastResult>();
    PointerEventData _pointer;
    readonly List<RectTransform> _shakeRoots = new List<RectTransform>();
    Vector2 _shakeOffset;
    Camera _shakeCamera;
    Vector3 _cameraOffset;
    float _rejectedAt = -1;
    public static void RejectInteraction()
    {
        if (Instance == null || Time.unscaledTime - Instance._rejectedAt < .3f) return;
        Instance.RestoreRejectedFeedback();
        Instance._rejectedAt = Time.unscaledTime;
        Instance._shakeRoots.Clear();
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                foreach (Transform child in canvas.transform)
                    if (child is RectTransform rect) Instance._shakeRoots.Add(rect);
        Instance._shakeCamera = Camera.main;
    }
    void CheckBlockedPointer()
    {
        RestoreRejectedFeedback();
        if (_menu == null || !MenuLocked || EventSystem.current == null || !Input.GetMouseButtonDown(0)) return;
        _pointer = new PointerEventData(EventSystem.current);
        _pointer.position = Input.mousePosition; _pointerHits.Clear();
        EventSystem.current.RaycastAll(_pointer, _pointerHits);
        if (_pointerHits.Count == 0) return;
        var button = _pointerHits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>();
        if (button != null && _gates.TryGetValue(button, out var gate) && gate != null && !gate.interactable) RejectInteraction();
    }
    void UpdateRejectedFeedback()
    {
        foreach (var root in _shakeRoots) if (root != null) root.anchoredPosition -= _shakeOffset;
        _shakeOffset = Vector2.zero;
        float elapsed = Time.unscaledTime - _rejectedAt;
        if (_rejectedAt < 0 || elapsed >= .18f) { _shakeRoots.Clear(); return; }
        _shakeOffset = new Vector2(Mathf.Sin(elapsed * 105), Mathf.Sin(elapsed * 83) * .5f) * (4 * (1 - elapsed / .18f));
        foreach (var root in _shakeRoots) if (root != null) root.anchoredPosition += _shakeOffset;
        if (_shakeCamera != null)
        {
            _cameraOffset = (Vector3)_shakeOffset * (2 * _shakeCamera.orthographicSize / Mathf.Max(1, Screen.height));
            _shakeCamera.transform.position += _cameraOffset;
        }
    }
    void RestoreCameraShake()
    {
        if (_shakeCamera != null) _shakeCamera.transform.position -= _cameraOffset;
        _cameraOffset = Vector3.zero;
    }
    void RestoreRejectedFeedback()
    {
        RestoreCameraShake();
        foreach (var root in _shakeRoots) if (root != null) root.anchoredPosition -= _shakeOffset;
        _shakeOffset = Vector2.zero;
    }
}

// Geometry stays in the existing runtime UI; no generated art asset or Inspector wiring.
public sealed class TutorialSpeechTail : UnityEngine.UI.MaskableGraphic
{
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
    {
        mesh.Clear(); var r = rectTransform.rect;
        Triangle(mesh, new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax * .7f, r.yMin), Color.black);
        Triangle(mesh, new Vector2(r.xMin + 7, r.yMax), new Vector2(r.xMax - 5, r.yMax), new Vector2(r.xMax * .7f - 1, r.yMin + 6), color);
    }
    static void Triangle(UnityEngine.UI.VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(a, color, Vector2.zero); mesh.AddVert(b, color, Vector2.zero); mesh.AddVert(c, color, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
    }
}
