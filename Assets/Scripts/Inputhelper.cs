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
        var player = Player(owner);
        if (player != null && player.UsesGamepad) return player.Move.x;
        if (IsMobile)
            return MobileInputController.Instance.GetHorizontal();
        return InputManager.Instance != null ? InputManager.Instance.GetHorizontalInput() : Input.GetAxisRaw("Horizontal");
    }

    public static float GetVertical(Component owner = null)
    {
        var player = Player(owner);
        if (player != null && player.UsesGamepad) return player.Move.y;
        if (IsMobile)
            return MobileInputController.Instance.GetVertical();
        return InputManager.Instance != null ? InputManager.Instance.GetVerticalInput() : Input.GetAxisRaw("Vertical");
    }

    public static bool GetJumpDown(Component owner = null)
    {
        var player = Player(owner);
        if (player != null && player.UsesGamepad) return player.JumpDown;
        if (IsMobile)
            return MobileInputController.Instance.GetJumpDown();
        return InputManager.Instance != null ? InputManager.Instance.IsJumpPressed() : Input.GetButtonDown("Jump");
    }

    public static bool GetJumpHeld(Component owner = null)
    {
        var player = Player(owner);
        if (player != null && player.UsesGamepad) return player.JumpHeld;
        if (IsMobile)
            return MobileInputController.Instance.GetJumpHeld();
        return InputManager.Instance != null ? InputManager.Instance.IsJumpHeld() : Input.GetButton("Jump");
    }

    public static bool GetDashDown(Component owner = null)
    {
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
        var player = Player(owner);
        if (player != null && player.UsesGamepad) return ControllerBindings.Held(player.Controller, "Shoot");
        if (IsMobile)
            return MobileInputController.Instance.GetShootHeld();
        return InputManager.Instance != null ? InputManager.Instance.IsShootHeld() : Input.GetButton("Fire1");
    }

    public static Vector3 GetMousePosition(Component owner = null)
    {
        var player = Player(owner);
        if (player != null && player.UsesGamepad) return player.AimScreenPosition();
        if (IsMobile)
            return MobileInputController.Instance.GetMousePosition();
        return Input.mousePosition;
    }
}
