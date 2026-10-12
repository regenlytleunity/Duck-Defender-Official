using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Pointer behavior shared by collection cards, inventory entries and equipped slots.
public class LoadoutInteraction : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public object Context;
    public bool IsCard;
    public int Slot = -1;
    public Sprite Icon;
    public Action ContextMenu, Remove;
    public Func<int, bool> Equip;
    static LoadoutInteraction _dragging;
    RectTransform _ghost;
    bool _dropped, _dragged;
    public void OnPointerClick(PointerEventData e)
    { if (e.button == PointerEventData.InputButton.Right && !_dragged) ContextMenu?.Invoke(); }
    public void OnBeginDrag(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || Equip == null) return;
        _dragging = this; _dragged = true; _dropped = false;
        var canvas = GetComponentInParent<Canvas>().rootCanvas;
        _ghost = CoopUIElements.Panel(canvas.transform, "Dragged equipment", Vector2.zero, Vector2.zero, Color.white);
        _ghost.sizeDelta = new Vector2(80, 100); _ghost.GetComponent<UnityEngine.UI.Image>().sprite = Icon;
        _ghost.GetComponent<UnityEngine.UI.Image>().preserveAspect = true; _ghost.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        OnDrag(e);
    }
    public void OnDrag(PointerEventData e)
    {
        if (_ghost == null) return;
        var canvas = _ghost.GetComponentInParent<Canvas>();
        RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)canvas.transform, e.position, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var world);
        _ghost.position = world;
    }
    public void OnDrop(PointerEventData e)
    {
        if (_dragging == null || Slot < 0 || IsCard != _dragging.IsCard || !ReferenceEquals(Context, _dragging.Context)) return;
        _dragging._dropped = true; _dragging.Equip?.Invoke(Slot);
    }
    public void OnEndDrag(PointerEventData e)
    {
        if (_dragging != this) return;
        if (!_dropped && Slot >= 0) Remove?.Invoke();
        _dragging = null; if (_ghost != null) Destroy(_ghost.gameObject);
    }
    void LateUpdate() { if (_dragging != this) _dragged = false; }
    void OnDisable() { if (_dragging == this) _dragging = null; if (_ghost != null) Destroy(_ghost.gameObject); }
}
