using UnityEngine;
using UnityEngine.InputSystem;

public static class InputHelper
{
    static LocalPlayer Player(Component owner) => owner != null ? owner.GetComponentInParent<LocalPlayer>() : null;

    private static bool IsMobile
    {
        get
        {
            return MobileInputController.Instance != null 
                && MobileInputController.Instance.IsMobileEnabled;
        }
    }

    public static float GetHorizontal(Component owner = null)
    {
        if (!TutorialDirector.AllowMovement) return 0;
        var clone = owner != null ? owner.GetComponent<ItemClone>() : null; if (clone != null) return clone.Move.x;
        var player = Player(owner);
        if (player != null && player.UsesGamepad) return player.Move.x;
        if (IsMobile)
            return MobileInputController.Instance.GetHorizontal();
        return InputManager.Instance != null ? InputManager.Instance.GetHorizontalInput() : Input.GetAxisRaw("Horizontal");
    }

    public static float GetVertical(Component owner = null)
    {
        if (!TutorialDirector.AllowMovement) return 0;
        var clone = owner != null ? owner.GetComponent<ItemClone>() : null; if (clone != null) return clone.Move.y;
        var player = Player(owner);
        if (player != null && player.UsesGamepad) return player.Move.y;
        if (IsMobile)
            return MobileInputController.Instance.GetVertical();
        return InputManager.Instance != null ? InputManager.Instance.GetVerticalInput() : Input.GetAxisRaw("Vertical");
    }

    public static bool GetJumpDown(Component owner = null)
    {
        if (!TutorialDirector.AllowJump) return false;
        var clone = owner != null ? owner.GetComponent<ItemClone>() : null; if (clone != null) return clone.Jump;
        var player = Player(owner);
        if (player != null && player.UsesGamepad) return player.JumpDown;
        if (IsMobile)
            return MobileInputController.Instance.GetJumpDown();
        return InputManager.Instance != null ? InputManager.Instance.IsJumpPressed() : Input.GetButtonDown("Jump");
    }

    public static bool GetJumpHeld(Component owner = null)
    {
        if (!TutorialDirector.AllowJump) return false;
        var clone = owner != null ? owner.GetComponent<ItemClone>() : null; if (clone != null) return clone.JumpHeld;
        var player = Player(owner);
        if (player != null && player.UsesGamepad) return player.JumpHeld;
        if (IsMobile)
            return MobileInputController.Instance.GetJumpHeld();
        return InputManager.Instance != null ? InputManager.Instance.IsJumpHeld() : Input.GetButton("Jump");
    }

    public static bool GetDashDown(Component owner = null)
    {
        if (TutorialDirector.Practice) return false;
        if (owner != null && owner.GetComponent<ItemClone>() != null) return false;
        var player = Player(owner);
        if (player != null && player.UsesGamepad) return ControllerBindings.Pressed(player.Controller, "Dash");
        if (IsMobile)
            return MobileInputController.Instance.GetDashDown();
        if (InputManager.Instance != null)
            return InputManager.Instance.IsDashPressed();
        return false;
    }

    public static bool GetShootHeld(Component owner = null)
    {
        if (!TutorialDirector.AllowShooting) return false;
        var clone = owner != null ? owner.GetComponent<ItemClone>() : null; if (clone != null) return clone.Shooting;
        var player = Player(owner);
        if (player != null && player.UsesGamepad) return ControllerBindings.Held(player.Controller, "Shoot");
        if (IsMobile)
            return MobileInputController.Instance.GetShootHeld();
        return InputManager.Instance != null ? InputManager.Instance.IsShootHeld() : Input.GetButton("Fire1");
    }

    public static Vector3 GetMousePosition(Component owner = null, bool effectCursor = false)
    {
        var clone = owner != null ? owner.GetComponent<ItemClone>() : null; if (clone != null) return clone.AimScreenPosition();
        var player = Player(owner);
        if (player != null && player.UsesGamepad) return player.AimScreenPosition(effectCursor);
        if (IsMobile)
            return MobileInputController.Instance.GetMousePosition();
        return Input.mousePosition;
    }

    public static int GetItemSelection(Component owner)
    {
        if (!TutorialDirector.AllowItems) return -1;
        var player = Player(owner);
        if (player != null && player.UsesGamepad)
        {
            var pad = player.Controller; if (pad == null || !pad.added) return -1;
            for (int i = 0; i < 4; i++) if (ControllerBindings.Pressed(pad, ItemActions[i])) return i;
            return -1;
        }
        if (IsMobile) return -1;
        for (int i = 0; i < 4; i++) if (Input.GetKeyDown(InputManager.Instance != null ? InputManager.Instance.GetKeybind(ItemActions[i]) : (KeyCode)((int)KeyCode.Alpha1 + i))) return i;
        return -1;
    }
    public static bool GetUseItemDown(Component owner)
    {
        if (!TutorialDirector.AllowItems) return false;
        var player = Player(owner);
        if (player != null && player.UsesGamepad) return player.Connected && ControllerBindings.Pressed(player.Controller, "UseItem");
        return !IsMobile && Input.GetKeyDown(InputManager.Instance != null ? InputManager.Instance.UseItemKey : KeyCode.Mouse1);
    }
    public static bool GetUseItemHeld(Component owner)
    {
        if (!TutorialDirector.AllowItems) return false;
        var player = Player(owner);
        if (player != null && player.UsesGamepad) return player.Connected && ControllerBindings.Held(player.Controller, "UseItem");
        return !IsMobile && Input.GetKey(InputManager.Instance != null ? InputManager.Instance.UseItemKey : KeyCode.Mouse1);
    }
    public static readonly string[] ItemActions = { "Item1", "Item2", "Item3", "Item4" };
}
