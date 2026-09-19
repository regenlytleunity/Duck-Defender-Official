using UnityEngine;
using System.IO;
using System.Runtime.InteropServices;

/// <summary>
/// Save system that works in the Unity Editor, standalone PC builds, and WebGL 
/// (including GitHub Pages deployments).
/// 
/// WebGL specifically requires calling SyncFiles() after writes to actually 
/// persist data to the browser's IndexedDB. Without this, the data lives in 
/// memory only and is lost when the page closes.
/// 
/// USAGE (unchanged from before):
///   SaveSystem.SaveData(playerData);
///   PlayerData data = SaveSystem.LoadData();
///   SaveSystem.DeleteSaveData();
/// </summary>
public static class SaveSystem
{
    private const string SaveFileName = "duck_save.json";
    
    // WebGL-specific JavaScript function for flushing writes to IndexedDB.
    // Defined in Assets/Plugins/WebGL/SaveSystemBridge.jslib
    #if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SyncFiles();
    #endif
    
    private static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);
    
    /// <summary>
    /// Saves player data to disk. On WebGL, additionally syncs to IndexedDB 
    /// so the data survives page closure.
    /// </summary>
    public static void SaveData(PlayerData data)
    {
        if (data == null)
        {
            Debug.LogError("[SaveSystem] Cannot save null PlayerData.");
            return;
        }
        
        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
            
            // CRITICAL FOR WEBGL: without this, writes are lost when the tab closes.
            // No-op on other platforms.
            #if UNITY_WEBGL && !UNITY_EDITOR
            SyncFiles();
            #endif
            
            Debug.Log("[SaveSystem] Save successful.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveSystem] Failed to save: {e.Message}");
        }
    }
    
    /// <summary>
    /// Loads player data from disk. If no save exists or the file is corrupted, 
    /// returns a fresh new PlayerData instance.
    /// </summary>
    public static PlayerData LoadData()
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log("[SaveSystem] No save file found. Starting fresh.");
            return new PlayerData();
        }
        
        try
        {
            string json = File.ReadAllText(SavePath);
            
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("[SaveSystem] Save file is empty. Starting fresh.");
                return new PlayerData();
            }
            
            PlayerData data = JsonUtility.FromJson<PlayerData>(json);
            
            if (data == null)
            {
                Debug.LogWarning("[SaveSystem] Save file parsed to null. Starting fresh.");
                return new PlayerData();
            }
            
            // Defensive: ensure card list isn't null if save was made by an older version
            if (data.CardCollection == null)
                data.CardCollection = new System.Collections.Generic.List<CardSaveData>();
            
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveSystem] Failed to load: {e.Message}. Starting fresh.");
            return new PlayerData();
        }
    }
    
    /// <summary>
    /// Deletes the save file. Used by the Reset Game Data feature.
    /// </summary>
    public static void DeleteSaveData()
    {
        try
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
            
            #if UNITY_WEBGL && !UNITY_EDITOR
            SyncFiles();
            #endif
            
            Debug.Log("[SaveSystem] Save data deleted.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveSystem] Failed to delete save: {e.Message}");
        }
    }
}