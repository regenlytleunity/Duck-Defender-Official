#if UNITY_EDITOR
using UnityEngine;

// Opt-in Play Mode isolation, matching the project's existing verification pattern.
public static class UIUpdatePlayProbe
{
    public const string SessionKey = "DuckDefender.UIVerificationSave";
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void IsolateSave()
    {
        string path = UnityEditor.SessionState.GetString(SessionKey, "");
        if (!string.IsNullOrEmpty(path))
        {
            SaveSystem.VerificationSavePath = path;
            Application.runInBackground = true;
        }
    }
}
#endif
