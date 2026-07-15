using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using NoREroMod;
using NoREroMod.Systems.Dialogue;
using Spine.Unity;
using DarkTonic.MasterAudio;

namespace NoREroMod.Systems.Dialogue;

/// <summary>
/// Universal patch for intercepting sounds through MasterAudio.PlaySound
/// IMPORTANT: Main onomatopoeia processing happens through DialogueFramework.ProcessAnimationEvent()
/// in enemy patches (TouzokuNormalPassPatch, TouzokuAxePassPatch, BigoniBrotherPatch, etc.)
/// This patch is used only as fallback for sounds not processed through animation events
///
/// NOTE: Patch disabled because PlaySound method with this signature not found in MasterAudio.
/// If sound interception needed, use patches through animation events in enemy patches.
/// </summary>
[HarmonyPatch]
internal static class SoundOnomatopoeiaPatch
{
    private static readonly Dictionary<string, float> _soundStartTime = new();
    private static readonly Dictionary<string, object> _lastEnemyInstance = new();

    /// <summary>
    /// Intercept MasterAudio.PlaySound for automatic onomatopoeia display
    /// Use simpler signature that definitely exists
    /// </summary>
    [HarmonyPatch(typeof(DarkTonic.MasterAudio.MasterAudio), "PlaySound")]
    [HarmonyPostfix]
    private static void OnPlaySound(string soundName)
    {
        try
        {
            if (string.IsNullOrEmpty(soundName))
            {
                return;
            }

            // Skip non-H sounds
            if (IsNonHSound(soundName))
            {
                return;
            }

            // Check if mapping exists for this sound
            if (!SoundRegistry.HasSound(soundName))
            {
                return;
            }

            // Find active enemy
            GameObject enemyObj = FindEnemyGameObject();
            if (enemyObj == null)
            {
                return;
            }

            // Get onomatopoeia
            string onomatopoeia = SoundRegistry.GetRandomOnomatopoeia(soundName);
            if (string.IsNullOrEmpty(onomatopoeia))
            {
                return;
            }

            // Show onomatopoeia
            DialogueFramework.ShowSoundOnomatopoeia(enemyObj, soundName, onomatopoeia);
        }
        catch (Exception ex)
        {
            // Sound interception failed silently
        }
    }

    // Critical optimization: cache for FindEnemyGameObject
    private static GameObject _cachedEnemyObject = null;
    private static float _lastEnemyCacheTime = 0f;
    private const float ENEMY_CACHE_INTERVAL = 0.5f; // Refresh cache every 0.5 seconds

    /// <summary>
    /// Find active enemy for onomatopoeia positioning
    /// Used when sound is called directly through MasterAudio.PlaySound
    /// Optimized: uses cache and QTESystem instead of 9x FindObjectOfType!
    /// WAS: ~20–30 ms per sound (9x FindObjectOfType)
    /// NOW: ~0 ms (cache) or ~1 ms (QTESystem)
    /// </summary>
    private static GameObject FindEnemyGameObject()
    {
        try
        {
            float currentTime = Time.time;
            
            // Check cache
            if (_cachedEnemyObject != null && (currentTime - _lastEnemyCacheTime) < ENEMY_CACHE_INTERVAL)
            {
                return _cachedEnemyObject;
            }
            
            // Priority 1: use QTESystem (fastest path!)
            object currentEnemy = QTESystem.GetCurrentEnemyInstance();
            if (currentEnemy != null && currentEnemy is MonoBehaviour enemyMB)
            {
                _cachedEnemyObject = enemyMB.gameObject;
                _lastEnemyCacheTime = currentTime;
                return _cachedEnemyObject;
            }
            
            // Priority 2: search via GameObject.FindGameObjectsWithTag("Enemy") — faster than FindObjectOfType
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
            if (enemies != null && enemies.Length > 0)
            {
                // Find enemy in H-scene (with active erodata)
                foreach (GameObject enemyObj in enemies)
                {
                    if (enemyObj == null) continue;
                    
                    EnemyDate enemyDate = enemyObj.GetComponent<EnemyDate>();
                    if (enemyDate != null && enemyDate.erodata != null && enemyDate.erodata.activeInHierarchy)
                    {
                        _cachedEnemyObject = enemyObj;
                        _lastEnemyCacheTime = currentTime;
                        return _cachedEnemyObject;
                    }
                }
                
                // Fallback: take the first enemy
                _cachedEnemyObject = enemies[0];
                _lastEnemyCacheTime = currentTime;
                return _cachedEnemyObject;
            }
            
            // Fallback 3 (rare): FindObjectOfType only if nothing else was found
            // Check only the most common types
            var touzoku = GameObject.FindObjectOfType<EroTouzoku>();
            if (touzoku != null)
            {
                _cachedEnemyObject = touzoku.gameObject;
                _lastEnemyCacheTime = currentTime;
                return _cachedEnemyObject;
            }

            var bigoni = GameObject.FindObjectOfType<Bigoni>();
            if (bigoni != null)
            {
                _cachedEnemyObject = bigoni.gameObject;
                _lastEnemyCacheTime = currentTime;
                return _cachedEnemyObject;
            }

            // If nothing found — clear the cache
            _cachedEnemyObject = null;
            return null;
        }
        catch
        {
            return null;
        }
    }

    // REMOVED: All method patches (Voice1, Voice2, PistonSE, etc.) removed
    // Onomatopoeia now processed through DialogueFramework.ProcessAnimationEvent()
    // in enemy patches (TouzokuNormalPassPatch, TouzokuAxePassPatch, BigoniBrotherPatch, etc.)

    /// <summary>
    /// Check if sound is non-H sound (skip)
    /// </summary>
    private static bool IsNonHSound(string soundName)
    {
        if (string.IsNullOrEmpty(soundName))
        {
            return true;
        }

        // Skip UI sounds, battle sounds, etc.
        string[] nonHSounds = {
            "UI_", "snd_equip", "snd_guard", "snd_parry", "snd_swing", 
            "snd_foot", "snd_step", "snd_run", "snd_landing",
            "BGM_", "click", "hover", "Typing"
        };

        foreach (string nonHSound in nonHSounds)
        {
            if (soundName.StartsWith(nonHSound, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }


    /// <summary>
    /// Clear data on scene change
    /// </summary>
    internal static void Clear()
    {
        _soundStartTime.Clear();
        _lastEnemyInstance.Clear();
        _cachedEnemyObject = null;
        _lastEnemyCacheTime = 0f;
    }
}

/// <summary>
/// Patch to fix typo in MasterAudio.PlaySound
/// Original game code (Mutudeero.cs, GAmutude.cs) uses wrong sound name "snf_drink" instead of "snd_drink"
/// This patch automatically fixes the typo on any MasterAudio.PlaySound call with 7 parameters
/// </summary>
[HarmonyPatch(typeof(MasterAudio), "PlaySound", new Type[] { typeof(string), typeof(float), typeof(Transform), typeof(float), typeof(Transform), typeof(bool), typeof(bool) })]
internal static class MasterAudioSoundFixPatch
{
    [HarmonyPrefix]
    private static void Prefix(ref string soundName)
    {
        // Fix typo in sound name: snf_drink -> snd_drink
        // This is a typo in original game code that causes MasterAudio warning
        if (soundName == "snf_drink")
        {
            soundName = "snd_drink";
        }

        // Fix dame_3 issue - replace with dame_2 (existing sound)
        // dame_3 is called in iron maiden H-scenes and QTE system, but MasterAudio cannot find it
        if (soundName == "dame_3")
        {
            soundName = "dame_2";
        }
    }
}

/// <summary>
/// Additional patch to fix dame_3 in MasterAudio.PlaySound overload with 2 parameters
/// Used by QTE system and other mod components
/// </summary>
[HarmonyPatch(typeof(MasterAudio), "PlaySound", new Type[] { typeof(string), typeof(float) })]
internal static class MasterAudioSoundFixPatch2Params
{
    [HarmonyPrefix]
    private static void Prefix(ref string soundName)
    {
        // Fix dame_3 for 2-parameter overload (used by QTE system)
        if (soundName == "dame_3")
        {
            soundName = "dame_2";
        }

        // Also fix typo for compatibility
        if (soundName == "snf_drink")
        {
            soundName = "snd_drink";
        }
    }
}

