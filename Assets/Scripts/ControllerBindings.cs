using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// Saved controller mappings used by the existing input and menu owners.
public static class ControllerBindings
{
    public static readonly string[] Actions = { "Jump", "Dash", "Shoot", "Confirm", "Back", "UseItem", "Item1", "Item2", "Item3", "Item4", "Tutorial", "SkipTutorial" };
    public static readonly string[] ActionNames = { "Jump / fly up", "Dash / blink", "Shoot", "Interact", "Back", "Use item", "Item slot 1", "Item slot 2", "Item slot 3", "Item slot 4", "Show tutorial", "Skip tutorial" };
    public static readonly string[] Defaults = { "MoveUp", "leftShoulder", "rightTrigger", "buttonSouth", "buttonEast", "rightShoulder", "dpad/up", "dpad/right", "dpad/down", "dpad/left", "buttonNorth", "buttonWest" };
    public static readonly string[] Controls = { "buttonSouth", "buttonEast", "buttonWest", "buttonNorth", "leftShoulder", "rightShoulder", "leftTrigger", "rightTrigger", "leftStickPress", "rightStickPress", "start", "select", "dpad/up", "dpad/down", "dpad/left", "dpad/right" };
    public const string Prefix = "DuckDefender_Controller_";
    public static Gamepad MenuPad
    {
        get { foreach (var pad in Gamepad.all) if (pad.enabled) return pad; return null; }
    }
    public static string Get(string action)
    {
        MigrateMenuDefaults();
        if (!PlayerPrefs.HasKey(Prefix + "ItemBindingsVersion"))
        {
            // Migrate the former reserved controls once; subsequent choices are freely remappable.
            for (int i = 0; i < 3; i++)
                if (ReservedForItems(PlayerPrefs.GetString(Prefix + Actions[i], Defaults[i]))) PlayerPrefs.DeleteKey(Prefix + Actions[i]);
            PlayerPrefs.SetInt(Prefix + "ItemBindingsVersion", 1); PlayerPrefs.Save();
        }
        int index = System.Array.IndexOf(Actions, action);
        string control = PlayerPrefs.GetString(Prefix + action, index >= 0 ? Defaults[index] : "");
        return index >= 0 && System.Array.IndexOf(Controls, control) < 0 && !(action == "Jump" && control == "MoveUp") ? Defaults[index] : control;
    }
    static void MigrateMenuDefaults()
    {
        if (PlayerPrefs.HasKey(Prefix + "MenuBindingsVersion")) return;
        // Upgrade the old default pair together; keep deliberately customized pairs.
        if (PlayerPrefs.GetString(Prefix + "Confirm", "buttonEast") == "buttonEast" &&
            PlayerPrefs.GetString(Prefix + "Back", "buttonSouth") == "buttonSouth")
        {
            PlayerPrefs.DeleteKey(Prefix + "Confirm");
            PlayerPrefs.DeleteKey(Prefix + "Back");
        }
        else
        {
            if (!PlayerPrefs.HasKey(Prefix + "Confirm")) PlayerPrefs.SetString(Prefix + "Confirm", "buttonEast");
            if (!PlayerPrefs.HasKey(Prefix + "Back")) PlayerPrefs.SetString(Prefix + "Back", "buttonSouth");
        }
        if (PlayerPrefs.GetString(Prefix + "Tutorial", "select") == "select")
            PlayerPrefs.DeleteKey(Prefix + "Tutorial");
        // New tutorial shortcuts must not steal a saved gameplay/menu binding.
        foreach (string shortcut in new[] { "Tutorial", "SkipTutorial" })
        {
            int index = System.Array.IndexOf(Actions, shortcut);
            if (PlayerPrefs.HasKey(Prefix + shortcut)) continue;
            string control = Defaults[index];
            if (!UsedByOtherAction(shortcut, control)) continue;
            foreach (string candidate in Controls)
                if (!UsedByOtherAction(shortcut, candidate)) { PlayerPrefs.SetString(Prefix + shortcut, candidate); break; }
        }
        PlayerPrefs.SetInt(Prefix + "MenuBindingsVersion", 1); PlayerPrefs.Save();
    }
    static bool UsedByOtherAction(string action, string control)
    {
        for (int i = 0; i < Actions.Length; i++)
            if (Actions[i] != action && PlayerPrefs.GetString(Prefix + Actions[i], Defaults[i]) == control) return true;
        return false;
    }
    static bool ReservedForItems(string control) => control == "rightShoulder" || control.StartsWith("dpad/");
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
        (RightMoveStick ? pad.rightStick : pad.leftStick).ReadValue();
    public static Vector2 Aim(Gamepad pad) => pad == null || !pad.added ? Vector2.zero : (RightMoveStick ? pad.leftStick : pad.rightStick).ReadValue();
    public static Vector2 MenuMove(Gamepad pad) => pad == null || !pad.added ? Vector2.zero : pad.dpad.ReadValue().sqrMagnitude > .1f ? pad.dpad.ReadValue() : pad.leftStick.ReadValue();
    public static void SwapSticks() { PlayerPrefs.SetInt(Prefix + "RightMoveStick", RightMoveStick ? 0 : 1); PlayerPrefs.Save(); }
    public static bool TrySet(string action, string control, out string error)
    {
        int index = System.Array.IndexOf(Actions, action);
        if (index < 0 || System.Array.IndexOf(Controls, control) < 0 && !(action == "Jump" && control == "MoveUp"))
        { error = "Unsupported controller binding."; return false; }
        for (int i = 0; i < Actions.Length; i++)
            if (i != index && (IsTutorialAction(action) || IsTutorialAction(Actions[i]) || IsMenuAction(Actions[i]) == IsMenuAction(action)) && Get(Actions[i]) == control)
            { error = ControlLabel(control) + " is already used by " + Actions[i] + "."; return false; }
        PlayerPrefs.SetString(Prefix + action, control); PlayerPrefs.Save(); error = ""; return true;
    }
    static bool IsTutorialAction(string action) => action == "Tutorial" || action == "SkipTutorial";
    static bool IsMenuAction(string action) => action == "Confirm" || action == "Back" || IsTutorialAction(action);
    public static void Reset()
    {
        foreach (var action in Actions) PlayerPrefs.DeleteKey(Prefix + action);
        PlayerPrefs.DeleteKey(Prefix + "RightMoveStick"); PlayerPrefs.Save();
    }
}
