using UnityEngine;

public static class InputHelper
{
    private static bool IsMobile
    {
        get
        {
            return MobileInputController.Instance != null 
                && MobileInputController.Instance.IsMobileEnabled;
        }
    }

    public static float GetHorizontal()
    {
        if (IsMobile)
            return MobileInputController.Instance.GetHorizontal();
        return Input.GetAxisRaw("Horizontal");
    }

    public static float GetVertical()
    {
        if (IsMobile)
            return MobileInputController.Instance.GetVertical();
        return Input.GetAxisRaw("Vertical");
    }

    public static bool GetJumpDown()
    {
        if (IsMobile)
            return MobileInputController.Instance.GetJumpDown();
        return Input.GetButtonDown("Jump");
    }

    public static bool GetJumpHeld()
    {
        if (IsMobile)
            return MobileInputController.Instance.GetJumpHeld();
        return Input.GetButton("Jump");
    }

    public static bool GetDashDown()
    {
        if (IsMobile)
            return MobileInputController.Instance.GetDashDown();
        if (InputManager.Instance != null)
            return InputManager.Instance.IsDashPressed();
        return false;
    }

    public static bool GetShootHeld()
    {
        if (IsMobile)
            return MobileInputController.Instance.GetShootHeld();
        return Input.GetButton("Fire1");
    }

    public static Vector3 GetMousePosition()
    {
        if (IsMobile)
            return MobileInputController.Instance.GetMousePosition();
        return Input.mousePosition;
    }
}