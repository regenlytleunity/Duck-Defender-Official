using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// Saved controller mappings used by the existing input and menu owners.
public static class ControllerBindings
{
    public static readonly string[] Actions = { "Jump", "Dash", "Shoot", "Confirm", "Back" };
    public static readonly string[] Defaults = { "MoveUp", "leftShoulder", "rightTrigger", "buttonEast", "buttonSouth" };
    public static readonly string[] Controls = { "buttonSouth", "buttonEast", "buttonWest", "buttonNorth", "leftShoulder", "rightShoulder", "leftTrigger", "rightTrigger", "leftStickPress", "rightStickPress", "start", "select", "dpad/up", "dpad/down", "dpad/left", "dpad/right" };
    public const string Prefix = "DuckDefender_Controller_";
    public static string Get(string action)
    {
        int index = System.Array.IndexOf(Actions, action);
        return PlayerPrefs.GetString(Prefix + action, index >= 0 ? Defaults[index] : "");
    }
    public static string Label(string action) => ControlLabel(Get(action));
    public static string ControlLabel(string control)
    {
        switch (control)
        {
            case "MoveUp": return "Move up";
            case "buttonSouth": return "A / Bottom";
            case "buttonEast": return "B / Right";
            case "buttonWest": return "X / Left";
            case "buttonNorth": return "Y / Top";
            case "leftShoulder": return "LB";
            case "rightShoulder": return "RB";
            case "leftTrigger": return "LT";
            case "rightTrigger": return "RT";
            case "leftStickPress": return "Left stick click";
            case "rightStickPress": return "Right stick click";
            case "start": return "Start";
            case "select": return "View / Select";
            default: return control.Replace("dpad/", "D-pad ");
        }
    }
    public static ButtonControl Button(Gamepad pad, string action) => pad != null && pad.added ? pad.TryGetChildControl<ButtonControl>(Get(action)) : null;
    public static bool Pressed(Gamepad pad, string action) => Button(pad, action)?.wasPressedThisFrame == true;
    public static bool Held(Gamepad pad, string action) => Button(pad, action)?.isPressed == true;
    public static bool RightMoveStick => PlayerPrefs.GetInt(Prefix + "RightMoveStick", 0) != 0;
    public static Vector2 Move(Gamepad pad) => pad == null || !pad.added ? Vector2.zero :
        pad.dpad.ReadValue().sqrMagnitude > .01f ? pad.dpad.ReadValue() : (RightMoveStick ? pad.rightStick : pad.leftStick).ReadValue();
    public static Vector2 Aim(Gamepad pad) => pad == null || !pad.added ? Vector2.zero : (RightMoveStick ? pad.leftStick : pad.rightStick).ReadValue();
    public static Vector2 MenuMove(Gamepad pad) => pad == null || !pad.added ? Vector2.zero : pad.dpad.ReadValue().sqrMagnitude > .1f ? pad.dpad.ReadValue() : pad.leftStick.ReadValue();
    public static void SwapSticks() { PlayerPrefs.SetInt(Prefix + "RightMoveStick", RightMoveStick ? 0 : 1); PlayerPrefs.Save(); }
    public static bool TrySet(string action, string control, out string error)
    {
        int index = System.Array.IndexOf(Actions, action);
        if (index < 0 || System.Array.IndexOf(Controls, control) < 0 && !(action == "Jump" && control == "MoveUp"))
        { error = "Unsupported controller binding."; return false; }
        for (int i = 0; i < Actions.Length; i++)
            if (i != index && (i < 3) == (index < 3) && Get(Actions[i]) == control)
            { error = ControlLabel(control) + " is already used by " + Actions[i] + "."; return false; }
        PlayerPrefs.SetString(Prefix + action, control); PlayerPrefs.Save(); error = ""; return true;
    }
    public static void Reset()
    {
        foreach (var action in Actions) PlayerPrefs.DeleteKey(Prefix + action);
        PlayerPrefs.DeleteKey(Prefix + "RightMoveStick"); PlayerPrefs.Save();
    }
}
