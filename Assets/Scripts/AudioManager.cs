using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Central audio system for Duck Defender. Singleton that persists across scenes.
/// Handles all sound effect playback, music streaming, volume controls, and saves preferences.
/// 
/// USAGE:
///   AudioManager.Instance.PlaySFX("Player_Shoot");
///   AudioManager.Instance.PlayMusic("Main_Menu_Track_1");
///   AudioManager.Instance.PlayInGameMusicRandom();
///   AudioManager.Instance.StopMusic();
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    /// <summary>
    /// How to pick which portion of the clip to play.
    /// 
    /// None: plays the entire clip from start to end (default behavior).
    /// 
    /// EqualSlices: divides the clip into N equal-length chunks and plays a random one. 
    /// Use this when your audio file contains N variations recorded back-to-back with the 
    /// same length each (e.g. 4 coin-pickup sounds, each exactly 0.5 seconds, total 2.0s).
    /// 
    /// CustomSlices: lets you specify the start and end time of each slice individually. 
    /// Use this when slice lengths vary, or when there's silence between them you want to skip.
    /// </summary>
    public enum SliceMode
    {
        None,
        EqualSlices,
        CustomSlices
    }

    [System.Serializable]
    public struct AudioSlice
    {
        [Tooltip("Start time of this slice in seconds (from the beginning of the clip).")]
        public float StartTime;
        
        [Tooltip("End time of this slice in seconds. Must be greater than StartTime.")]
        public float EndTime;
    }

    [System.Serializable]
    public class Sound
    {
        [Tooltip("The name used to play this sound (e.g., 'Player_Shoot'). Must be unique.")]
        public string Name;
        
        [Tooltip("The audio file to play.")]
        public AudioClip Clip;
        
        [Tooltip("Per-sound volume multiplier (0-1). Final volume = this × category volume.")]
        [Range(0f, 1f)]
        public float Volume = 1f;
        
        [Tooltip("Base playback pitch. 1 = normal speed/pitch.")]
        [Range(0.1f, 3f)]
        public float Pitch = 1f;
        
        [Tooltip("If true, pitch is randomized slightly each play to prevent repetition fatigue. " +
                 "Recommended for frequently played sounds like shooting, hits, coin pickups.")]
        public bool RandomizePitch = false;
        
        [Tooltip("How much to vary pitch when RandomizePitch is on. " +
                 "0.1 = ±10% pitch variation. Subtle is best.")]
        [Range(0f, 0.5f)]
        public float PitchVariation = 0.1f;
        
        [Header("Variation Slicing")]
        [Tooltip("How to pick which portion of the clip to play. None plays the whole clip. " +
                 "EqualSlices and CustomSlices each pick one random slice per playback.")]
        public SliceMode SliceMode = SliceMode.None;
        
        [Tooltip("Used only when SliceMode = EqualSlices. The clip will be divided into this " +
                 "many equal-length chunks. e.g. set to 4 if your clip contains 4 variations of " +
                 "equal length recorded back-to-back.")]
        [Range(2, 32)]
        public int EqualSliceCount = 4;
        
        [Tooltip("Used only when SliceMode = CustomSlices. Each entry defines the start/end time " +
                 "of one variation. The clip's player picks one slice at random per play.")]
        public AudioSlice[] CustomSlices;
        
        [Tooltip("If true, the same slice index cannot be picked twice in a row. Useful for " +
                 "sounds that play in rapid succession (you don't want 5 identical pickups back " +
                 "to back). Only applies when there are 2+ slices available.")]
        public bool AvoidImmediateRepeat = true;
        
        // Internal: remember the last slice we played so AvoidImmediateRepeat works.
        // Marked [System.NonSerialized] so Unity doesn't try to preserve it across edits.
        [System.NonSerialized] public int LastSliceIndex = -1;
    }

    [Header("Sound Library")]
    [Tooltip("All SFX sounds available. Add entries here for each sound effect.")]
    public Sound[] SFXLibrary;
    
    [Tooltip("All music tracks available. Add entries here for menu and gameplay music.")]
    public Sound[] MusicLibrary;
    
    [Header("Audio Source Pool")]
    [Tooltip("How many SFX can play simultaneously. If many enemies die at once, " +
             "additional sounds will replace the oldest in the pool.")]
    public int SFXPoolSize = 15;
    
    [Header("Music Settings")]
    [Tooltip("How long the crossfade lasts when switching music tracks (seconds).")]
    public float MusicFadeDuration = 1.0f;
    
    [Header("In-Game Music Rotation")]
    [Tooltip("Names of music tracks to randomly rotate during gameplay. " +
             "When one ends, a different random track from this list plays next.")]
    public string[] InGameMusicTracks;
    
    [Header("Default Volumes")]
    [Range(0f, 1f)] public float DefaultSFXVolume = 0.8f;
    [Range(0f, 1f)] public float DefaultMusicVolume = 0.6f;

    // Runtime state
    private Dictionary<string, Sound> _sfxDictionary;
    private Dictionary<string, Sound> _musicDictionary;
    private List<AudioSource> _sfxPool;
    private int _nextPoolIndex = 0;
    
    private AudioSource _musicSource;
    private AudioSource _musicFadeSource;
    private Coroutine _musicCrossfadeRoutine;
    private Coroutine _inGameRotationRoutine;
    private string _currentMusicName = "";
    private bool _inGameRotationActive = false;
    
    // Volume state - saved/loaded via PlayerPrefs
    private float _sfxVolume;
    private float _musicVolume;
    
    private const string PREFS_SFX_VOLUME = "DuckDefender_SFXVolume";
    private const string PREFS_MUSIC_VOLUME = "DuckDefender_MusicVolume";

    void Awake()
    {
        // Singleton pattern with persistence across scenes
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioSystem();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    void InitializeAudioSystem()
    {
        // Build lookup dictionaries for O(1) sound retrieval by name
        _sfxDictionary = new Dictionary<string, Sound>();
        foreach (Sound s in SFXLibrary)
        {
            if (s.Clip == null)
            {
                Debug.LogWarning($"[AudioManager] SFX '{s.Name}' has no clip assigned.");
                continue;
            }
            if (_sfxDictionary.ContainsKey(s.Name))
            {
                Debug.LogWarning($"[AudioManager] Duplicate SFX name '{s.Name}'. Only first will be used.");
                continue;
            }
            _sfxDictionary[s.Name] = s;
        }
        
        _musicDictionary = new Dictionary<string, Sound>();
        foreach (Sound m in MusicLibrary)
        {
            if (m.Clip == null)
            {
                Debug.LogWarning($"[AudioManager] Music '{m.Name}' has no clip assigned.");
                continue;
            }
            if (_musicDictionary.ContainsKey(m.Name))
            {
                Debug.LogWarning($"[AudioManager] Duplicate music name '{m.Name}'. Only first will be used.");
                continue;
            }
            _musicDictionary[m.Name] = m;
        }
        
        // Create the SFX audio source pool
        _sfxPool = new List<AudioSource>();
        for (int i = 0; i < SFXPoolSize; i++)
        {
            GameObject sourceObj = new GameObject($"SFX_Source_{i}");
            sourceObj.transform.SetParent(transform);
            AudioSource source = sourceObj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            _sfxPool.Add(source);
        }
        
        // Create two music sources (for crossfading between tracks)
        GameObject musicObj = new GameObject("Music_Source");
        musicObj.transform.SetParent(transform);
        _musicSource = musicObj.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.loop = true;
        
        GameObject musicFadeObj = new GameObject("Music_Fade_Source");
        musicFadeObj.transform.SetParent(transform);
        _musicFadeSource = musicFadeObj.AddComponent<AudioSource>();
        _musicFadeSource.playOnAwake = false;
        _musicFadeSource.loop = true;
        
        // Load saved volume preferences
        _sfxVolume = PlayerPrefs.GetFloat(PREFS_SFX_VOLUME, DefaultSFXVolume);
        _musicVolume = PlayerPrefs.GetFloat(PREFS_MUSIC_VOLUME, DefaultMusicVolume);
    }

    // === SFX PLAYBACK ===

    /// <summary>
    /// Plays a sound effect by name. If the sound is not found, logs a warning and does nothing.
    /// If the sound has variation slicing configured, a random slice is picked each call.
    /// </summary>
    public void PlaySFX(string soundName)
    {
        PlaySFXInternal(soundName, 1f);
    }
    
    /// <summary>
    /// Plays a sound effect with a custom volume multiplier. Useful for sounds that 
    /// should be quieter/louder based on context (e.g., distant explosions).
    /// </summary>
    public void PlaySFXWithVolume(string soundName, float volumeMultiplier)
    {
        PlaySFXInternal(soundName, Mathf.Clamp01(volumeMultiplier));
    }
    
    /// <summary>
    /// Shared implementation for PlaySFX variants. Handles lookup, audio source 
    /// allocation, volume/pitch setup, and slice selection.
    /// </summary>
    void PlaySFXInternal(string soundName, float volumeMultiplier)
    {
        if (string.IsNullOrEmpty(soundName)) return;
        
        if (_sfxDictionary == null || !_sfxDictionary.TryGetValue(soundName, out Sound sound))
        {
            Debug.LogWarning($"[AudioManager] SFX '{soundName}' not found in library.");
            return;
        }
        
        AudioSource source = GetNextSFXSource();
        source.clip = sound.Clip;
        source.volume = sound.Volume * _sfxVolume * volumeMultiplier;
        
        float pitch = sound.Pitch;
        if (sound.RandomizePitch)
        {
            pitch += Random.Range(-sound.PitchVariation, sound.PitchVariation);
        }
        source.pitch = pitch;
        
        // Slice selection: if slicing is configured, pick a random slice and play just 
        // that portion of the clip. Otherwise play the whole clip from the start.
        if (sound.SliceMode == SliceMode.None)
        {
            // Default behavior: play the entire clip from t=0.
            source.time = 0f;
            source.Play();
        }
        else
        {
            PlaySlice(source, sound, pitch);
        }
    }
    
    /// <summary>
    /// Picks a random slice from the sound's slice config, seeks the audio source to 
    /// the slice's start time, plays it, and schedules it to stop at the slice's end time.
    /// 
    /// HOW THIS WORKS UNDER THE HOOD:
    /// 
    /// Unity's AudioSource can't natively "play a portion of a clip" — Play() always 
    /// plays to the end. We work around this with two pieces:
    /// 
    ///   1. source.time = sliceStart  → moves the playhead to where we want to start.
    ///   2. source.SetScheduledEndTime(dspEndTime)  → tells the audio engine the exact 
    ///      DSP timestamp at which to stop. This is sample-accurate and runs on the 
    ///      audio thread (no coroutine drift, no frame-rate dependency).
    /// 
    /// AudioSettings.dspTime is the audio engine's "current time" in seconds. Adding 
    /// the slice's length (adjusted for pitch, since faster pitch = shorter real-world 
    /// duration) gives us the precise DSP time at which to stop playback.
    /// </summary>
    void PlaySlice(AudioSource source, Sound sound, float effectivePitch)
    {
        int sliceIndex = PickSliceIndex(sound);
        if (sliceIndex < 0)
        {
            // Slice config is invalid; fall back to playing the whole clip so the 
            // player still hears SOMETHING.
            source.time = 0f;
            source.Play();
            return;
        }
        
        float sliceStart, sliceEnd;
        GetSliceBounds(sound, sliceIndex, out sliceStart, out sliceEnd);
        
        // Defensive clamp: a misconfigured slice that runs past clip end would throw.
        float clipLength = sound.Clip.length;
        sliceStart = Mathf.Clamp(sliceStart, 0f, Mathf.Max(0f, clipLength - 0.01f));
        sliceEnd = Mathf.Clamp(sliceEnd, sliceStart + 0.01f, clipLength);
        
        // Seek to the slice's start. Unity buffers this for the next Play() call.
        source.time = sliceStart;
        
        // Calculate the real-world duration of the slice, factoring in pitch.
        // If pitch is 2.0, a 1-second slice plays in 0.5 real seconds.
        float sliceLength = (sliceEnd - sliceStart) / Mathf.Max(0.01f, Mathf.Abs(effectivePitch));
        
        // Schedule the stop on the DSP (audio) clock for sample-accurate timing.
        double startDspTime = AudioSettings.dspTime;
        source.Play();
        source.SetScheduledEndTime(startDspTime + sliceLength);
    }
    
    /// <summary>
    /// Picks a random slice index for the sound, respecting AvoidImmediateRepeat.
    /// Returns -1 if the sound's slice config is invalid (no slices defined).
    /// </summary>
    int PickSliceIndex(Sound sound)
    {
        int sliceCount = GetSliceCount(sound);
        if (sliceCount <= 0) return -1;
        if (sliceCount == 1) return 0;
        
        int picked;
        if (sound.AvoidImmediateRepeat && sound.LastSliceIndex >= 0 && sliceCount > 1)
        {
            // Pick from [0, sliceCount - 1) and shift past the last one. This gives a 
            // uniform distribution over the non-repeat indices without a rejection loop.
            picked = Random.Range(0, sliceCount - 1);
            if (picked >= sound.LastSliceIndex) picked++;
        }
        else
        {
            picked = Random.Range(0, sliceCount);
        }
        
        sound.LastSliceIndex = picked;
        return picked;
    }
    
    /// <summary>
    /// Returns how many slices this sound has, regardless of which slice mode it uses.
    /// </summary>
    int GetSliceCount(Sound sound)
    {
        if (sound.SliceMode == SliceMode.EqualSlices)
        {
            return Mathf.Max(1, sound.EqualSliceCount);
        }
        if (sound.SliceMode == SliceMode.CustomSlices)
        {
            return sound.CustomSlices != null ? sound.CustomSlices.Length : 0;
        }
        return 0;
    }
    
    /// <summary>
    /// Resolves slice index to its (startTime, endTime) bounds in seconds.
    /// </summary>
    void GetSliceBounds(Sound sound, int sliceIndex, out float start, out float end)
    {
        if (sound.SliceMode == SliceMode.EqualSlices)
        {
            // Equal slices: divide the clip into N chunks of equal length.
            float clipLength = sound.Clip.length;
            float perSlice = clipLength / sound.EqualSliceCount;
            start = sliceIndex * perSlice;
            end = start + perSlice;
        }
        else if (sound.SliceMode == SliceMode.CustomSlices && 
                 sound.CustomSlices != null && 
                 sliceIndex < sound.CustomSlices.Length)
        {
            start = sound.CustomSlices[sliceIndex].StartTime;
            end = sound.CustomSlices[sliceIndex].EndTime;
        }
        else
        {
            // Fallback: play the whole clip.
            start = 0f;
            end = sound.Clip.length;
        }
    }
    
    /// <summary>
    /// Rotates through the audio source pool. When all sources are busy, 
    /// the oldest one gets cut off to make room. This is fine for short SFX 
    /// since by the time we cycle through 15 sources, the first ones are done.
    /// </summary>
    AudioSource GetNextSFXSource()
    {
        AudioSource source = _sfxPool[_nextPoolIndex];
        _nextPoolIndex = (_nextPoolIndex + 1) % _sfxPool.Count;
        return source;
    }

    // === MUSIC PLAYBACK ===
    
    /// <summary>
    /// Plays a music track by name with a crossfade from the current track.
    /// If the same track is already playing, does nothing.
    /// </summary>
    public void PlayMusic(string musicName)
    {
        if (string.IsNullOrEmpty(musicName)) return;
        if (_currentMusicName == musicName && _musicSource.isPlaying) return;
        
        if (_musicDictionary == null || !_musicDictionary.TryGetValue(musicName, out Sound music))
        {
            Debug.LogWarning($"[AudioManager] Music '{musicName}' not found in library.");
            return;
        }
        
        // Stop the in-game rotation if it was active
        StopInGameRotation();
        
        _currentMusicName = musicName;
        StartCrossfade(music, loop: true);
    }
    
    /// <summary>
    /// Stops the current music with a fade-out.
    /// </summary>
    public void StopMusic()
    {
        StopInGameRotation();
        
        if (_musicCrossfadeRoutine != null) StopCoroutine(_musicCrossfadeRoutine);
        _musicCrossfadeRoutine = StartCoroutine(FadeOutMusic());
        _currentMusicName = "";
    }
    
    /// <summary>
    /// Starts the in-game music rotation. Picks a random track from InGameMusicTracks 
    /// and plays it. When it ends, picks another random track (different from the last one 
    /// if possible). Loops indefinitely.
    /// </summary>
    public void PlayInGameMusicRandom()
    {
        if (InGameMusicTracks == null || InGameMusicTracks.Length == 0)
        {
            Debug.LogWarning("[AudioManager] No in-game music tracks configured.");
            return;
        }
        
        StopInGameRotation();
        _inGameRotationActive = true;
        _inGameRotationRoutine = StartCoroutine(InGameRotationLoop());
    }
    
    /// <summary>
    /// Stops the in-game rotation without affecting the currently playing track.
    /// </summary>
    public void StopInGameRotation()
    {
        _inGameRotationActive = false;
        if (_inGameRotationRoutine != null)
        {
            StopCoroutine(_inGameRotationRoutine);
            _inGameRotationRoutine = null;
        }
    }
    
    IEnumerator InGameRotationLoop()
    {
        string lastTrack = "";
        
        while (_inGameRotationActive)
        {
            // Pick a random track, avoiding the last one if we have more than 1 option
            string nextTrack = PickRandomTrack(lastTrack);
            
            if (_musicDictionary.TryGetValue(nextTrack, out Sound music))
            {
                _currentMusicName = nextTrack;
                // In-game tracks DON'T loop individually; they play once then we pick another
                StartCrossfade(music, loop: false);
                lastTrack = nextTrack;
                
                // Wait for the track to finish (length / pitch accounts for any speed changes)
                float duration = music.Clip.length / Mathf.Max(music.Pitch, 0.01f);
                yield return new WaitForSeconds(duration);
                
                // Small gap between tracks (optional, feels more natural than instant)
                yield return new WaitForSeconds(0.5f);
            }
            else
            {
                Debug.LogWarning($"[AudioManager] In-game track '{nextTrack}' not found in music library.");
                yield return new WaitForSeconds(1f); // Avoid tight loop on bad data
            }
        }
    }
    
    string PickRandomTrack(string excludeTrack)
    {
        if (InGameMusicTracks.Length == 1) return InGameMusicTracks[0];
        
        // Build a list of candidates excluding the last one
        List<string> candidates = new List<string>();
        foreach (string t in InGameMusicTracks)
        {
            if (t != excludeTrack) candidates.Add(t);
        }
        
        if (candidates.Count == 0) return InGameMusicTracks[0];
        
        return candidates[Random.Range(0, candidates.Count)];
    }
    
    void StartCrossfade(Sound newMusic, bool loop)
    {
        if (_musicCrossfadeRoutine != null) StopCoroutine(_musicCrossfadeRoutine);
        _musicCrossfadeRoutine = StartCoroutine(CrossfadeRoutine(newMusic, loop));
    }
    
    IEnumerator CrossfadeRoutine(Sound newMusic, bool loop)
    {
        // Move the currently-playing source to fadeOut, start new one on main source
        AudioSource oldSource = _musicSource;
        AudioSource newSource = _musicFadeSource;
        
        // Swap roles
        _musicSource = newSource;
        _musicFadeSource = oldSource;
        
        // Configure new source
        newSource.clip = newMusic.Clip;
        newSource.volume = 0f;
        newSource.pitch = newMusic.Pitch;
        newSource.loop = loop;
        newSource.Play();
        
        float targetNewVolume = newMusic.Volume * _musicVolume;
        float startOldVolume = oldSource.volume;
        float elapsed = 0f;
        
        while (elapsed < MusicFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime; // Use unscaled so music fades work during pause
            float t = elapsed / MusicFadeDuration;
            
            newSource.volume = Mathf.Lerp(0f, targetNewVolume, t);
            oldSource.volume = Mathf.Lerp(startOldVolume, 0f, t);
            
            yield return null;
        }
        
        newSource.volume = targetNewVolume;
        oldSource.Stop();
        oldSource.volume = 0f;
        
        _musicCrossfadeRoutine = null;
    }
    
    IEnumerator FadeOutMusic()
    {
        float startVolume = _musicSource.volume;
        float elapsed = 0f;
        
        while (elapsed < MusicFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / MusicFadeDuration;
            _musicSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }
        
        _musicSource.Stop();
        _musicSource.volume = 0f;
        _musicCrossfadeRoutine = null;
    }

    // === VOLUME CONTROLS ===
    
    /// <summary>
    /// Sets the SFX volume (0-1) and saves the preference.
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PREFS_SFX_VOLUME, _sfxVolume);
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// Sets the music volume (0-1) and saves the preference. 
    /// Updates the currently playing music immediately.
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        _musicVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PREFS_MUSIC_VOLUME, _musicVolume);
        PlayerPrefs.Save();
        
        // Update currently playing music to reflect new volume
        if (_musicSource != null && _musicSource.isPlaying && _musicCrossfadeRoutine == null)
        {
            if (!string.IsNullOrEmpty(_currentMusicName) && 
                _musicDictionary.TryGetValue(_currentMusicName, out Sound music))
            {
                _musicSource.volume = music.Volume * _musicVolume;
            }
        }
    }
    
    public float GetSFXVolume() => _sfxVolume;
    public float GetMusicVolume() => _musicVolume;
}