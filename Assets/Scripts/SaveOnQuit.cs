using UnityEngine;

/// <summary>
/// Forces a save when the application loses focus or is about to close. 
/// Critical for WebGL where players close tabs without using an in-game 
/// "Quit" button - we need to catch the close and flush IndexedDB.
/// </summary>
public class SaveOnQuit : MonoBehaviour
{
    void Awake()
    {
        // Persist across scene loads so we catch quits in any scene
        DontDestroyOnLoad(gameObject);
    }
    
    void OnApplicationQuit()
    {
        // Fires on standalone builds and editor stop. 
        // On WebGL, this is less reliable - OnApplicationPause is the catch-all.
        ForceFinalSave("OnApplicationQuit");
    }
    
    void OnApplicationPause(bool isPaused)
    {
        // WebGL fires this when the page is hidden or about to unload. 
        // Mobile fires this when the user backgrounds the app. 
        // Either way, it's our last chance to flush a save.
        if (isPaused)
        {
            ForceFinalSave("OnApplicationPause");
        }
    }
    
    /// <summary>
    /// Saves the current in-memory PlayerData. Reload-then-resave ensures 
    /// anything saved by the game logic this frame gets flushed to IndexedDB.
    /// </summary>
    private void ForceFinalSave(string reason)
    {
        try
        {
            if (LevelManager.Instance != null) LevelManager.Instance.FlushCoinSave();
            PlayerData currentData = SaveSystem.LoadData();
            SaveSystem.SaveData(currentData);
            Debug.Log($"[SaveOnQuit] Forced save triggered by {reason}.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveOnQuit] Failed during {reason}: {e.Message}");
        }
    }
}
