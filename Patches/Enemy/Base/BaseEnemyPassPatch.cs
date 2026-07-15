using HarmonyLib;
using UnityEngine;
using Spine.Unity;
using System.Collections.Generic;
using System.Reflection;
using NoREroMod;
using NoREroMod.Systems.Cache;

namespace NoREroMod.Patches.Enemy.Base;

/// <summary>
/// Base class for enemy handoff patches
/// Contains all shared logic for cycle tracking and handoff
/// Optimized: uses UnifiedPlayerCacheManager instead of FindGameObjectWithTag
/// </summary>
public abstract class BaseEnemyPassPatch<TEnemyType> where TEnemyType : MonoBehaviour
{
    // Animation cycle tracking per enemy instance
    protected static Dictionary<object, int> enemyAnimationCycles = new Dictionary<object, int>();
    
    // H-session start time per enemy
    protected static Dictionary<object, float> enemySessionStartTime = new Dictionary<object, float>();
    
    // Last cycle time (prevents duplicates)
    protected static Dictionary<object, float> lastCycleTime = new Dictionary<object, float>();
    
    // Flag that enemy already passed GG (avoid repeat)
    protected static Dictionary<object, bool> enemyHasPassed = new Dictionary<object, bool>();
    
    // Flag that enemy should be disabled from patch
    protected static Dictionary<object, bool> enemyDisabled = new Dictionary<object, bool>();
    
    // Global handoff session tracking
    protected static int globalHandoffCount = 0;
    protected static float globalSessionStartTime = 0f;
    
    /// <summary>
    /// Enemy name for logging (e.g. "TouzokuNormal", "SixHandDemon")
    /// </summary>
    protected abstract string EnemyName { get; }
    
    /// <summary>
    /// Cycles before GG handoff (default 2)
    /// </summary>
    protected virtual int CyclesBeforePass => 2;
    
    /// <summary>
    /// Get cycles count for specific enemy (may be overridden for dynamic logic)
    /// Default returns CyclesBeforePass
    /// </summary>
    protected virtual int GetCyclesBeforePassForEnemy(object enemyInstance)
    {
        return CyclesBeforePass;
    }
    
    /// <summary>
    /// List of H-animations for this enemy type
    /// </summary>
    protected abstract string[] GetHAnimations();
    
    /// <summary>
    /// Determines completion of full animation cycle
    /// Must be overridden in each concrete class
    /// </summary>
    protected abstract bool IsCycleComplete(string animationName, string eventName, int seCount);
    
    /// <summary>
    /// Get enemy name for phrase system (e.g. "touzoku", "goblin")
    /// </summary>
    protected abstract string GetEnemyTypeName();
    
    /// <summary>
    /// Force animation to middle (for subsequent enemies in gangbang)
    /// </summary>
    protected virtual void ForceAnimationToMiddle(SkeletonAnimation spine)
    {
        try
        {
            if (spine == null) return;
            
            // Default: move to ERO2 (middle of cycle)
            const string middleAnim = "ERO2";
            const bool isLoop = true;
            
            // Plugin.Log.LogInfo($"[ANIMATION] Force {EnemyName} to middle: {middleAnim} (loop={isLoop})");
            
            if (spine.AnimationState != null)
            {
                var track = spine.AnimationState.SetAnimation(0, middleAnim, isLoop);
                if (track != null && track.Animation != null)
                {
                    // Use Duration instead of AnimationEnd
                    track.Time = track.Animation.Duration * 0.5f; // Start from middle
                }
            }
        }
        catch (System.Exception ex)
        {
        }
    }
    
    /// <summary>
    /// Get SkeletonAnimation via reflection
    /// </summary>
    protected static SkeletonAnimation GetSpineAnimation(object enemyInstance)
    {
        var spineField = enemyInstance.GetType().GetField("myspine", BindingFlags.NonPublic | BindingFlags.Instance)
                      ?? enemyInstance.GetType().GetField("mySpine", BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (spineField != null)
        {
            return spineField.GetValue(enemyInstance) as SkeletonAnimation;
        }
        
        return null;
    }
    
    /// <summary>
    /// Checks if animation is H-animation
    /// </summary>
    protected bool IsHAnimation(string animationName)
    {
        if (string.IsNullOrEmpty(animationName)) return false;
        
        string[] hAnimations = GetHAnimations();
        
        foreach (string hAnim in hAnimations)
        {
            if (animationName.StartsWith(hAnim))
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Tracks animation cycles and passes GG after CyclesBeforePass cycles
    /// </summary>
    protected void TrackCycles(object enemyInstance, SkeletonAnimation spine, Spine.Event e, int seCount)
    {
        // if (EnemyName == "Goblin")
        // {
        //     string entryMsg = $"[GOBLIN TRACK] ENTRY anim={spine?.AnimationName}, event={e?.ToString()}, se={seCount}";
        //     Plugin.Log.LogInfo(entryMsg);
        // }
        // else
        // {
        //     Plugin.Log.LogInfo($"[TRACK CYCLES] ENTRY: anim={spine?.AnimationName}, event={e?.ToString()}");
        // }
        
        try
        {
            if (Plugin.enableEnemyPass != null && !Plugin.enableEnemyPass.Value)
            {
                // Plugin.Log.LogInfo("[TRACK CYCLES] EXIT: enabled=false");
                return;
            }
        }
        catch
        {
            // If Plugin not initialized, continue
        }
        
        // Check enemy hasn't passed GG yet
        if (enemyHasPassed.ContainsKey(enemyInstance) && enemyHasPassed[enemyInstance])
        {
            // Logs disabled
            // if (EnemyName == "Goblin")
            // {
            //     Plugin.Log.LogInfo("[GOBLIN TRACK] EXIT: enemy already passed");
            // }
            // else
            // {
            //     Plugin.Log.LogInfo("[TRACK CYCLES] EXIT: enemy already passed");
            // }
            return;
        }
        
        string currentAnim = spine.AnimationName;
        string eventName = e.ToString(); // Use ToString() as with all working enemies
        
        // Logs disabled
        // if (EnemyName == "Goblin")
        // {
        //     string debugMsg = $"[GOBLIN DEBUG] anim={currentAnim}, event={eventName}, se={seCount}";
        //     Plugin.Log.LogInfo(debugMsg);
        // }
        
        // Check if this is H-animation
        if (!IsHAnimation(currentAnim))
        {
            return; // Ignore combat animations
        }
        
        // eventName is set in TrackCycles via e.ToString()
        // Plugin.Log.LogInfo($"[{EnemyName}] H-anim called! anim={currentAnim}, event={e?.ToString()}, se_count={seCount}");
        
        // Initialize session if first call
        if (!enemySessionStartTime.ContainsKey(enemyInstance))
        {
            enemySessionStartTime[enemyInstance] = Time.time;
            
            // Initialize global session if first enemy (no handoff yet)
            if (EnemyHandoffSystem.GlobalHandoffCount == 0)
            {
                globalSessionStartTime = Time.time;
                // Plugin.Log.LogInfo($"[{EnemyName}] Start global session at {Time.time:F3}s (globalHandoffCount={globalHandoffCount})");
            }
            
            // Check if first enemy or subsequent (shared count — works for any enemy type)
            if (EnemyHandoffSystem.GlobalHandoffCount > 0)
            {
                // Plugin.Log.LogInfo($"[{EnemyName}] Subsequent enemy #{globalHandoffCount + 1} at {Time.time:F3}s - force to middle");
                ForceAnimationToMiddle(spine);
            }
            else
            {
                // Plugin.Log.LogInfo($"[{EnemyName}] First enemy in this session at {Time.time:F3}s - full animation {currentAnim}");
                // NOTE: For goblins, forced START -> 2ERO_START change happens in GoblinPass
                // before TrackCycles, so nothing to do here
            }
        }
        
        // Calculate time since session start
        float enemyElapsedTime = Time.time - enemySessionStartTime[enemyInstance];
        float globalElapsedTime = globalSessionStartTime > 0 ? Time.time - globalSessionStartTime : 0f;
        
        // Log important events
        if ((currentAnim.Contains("START") || currentAnim.Contains("ERO") || currentAnim.Contains("FIN") || currentAnim.Contains("JIGO")) 
            && (enemyElapsedTime % 1.0f < 0.1f))
        {
            // Plugin.Log.LogInfo($"[TIME] {EnemyName} | Global={globalElapsedTime:F1}s | Local={enemyElapsedTime:F1}s | Anim={currentAnim} | Event={eventName} | SE={seCount}");
        }
        
        if (IsCycleComplete(currentAnim, eventName, seCount))
        {
            if (!enemyAnimationCycles.ContainsKey(enemyInstance))
            {
                enemyAnimationCycles[enemyInstance] = 0;
            }
            enemyAnimationCycles[enemyInstance]++;
            
            // Plugin.Log.LogInfo($"[CYCLE] {EnemyName}: Completed cycle #{enemyAnimationCycles[enemyInstance]} in {enemyElapsedTime:F2}s (anim={currentAnim}, event={eventName})");
            
            // Get dynamic cycle count for this specific enemy
            int cyclesNeeded = GetCyclesBeforePassForEnemy(enemyInstance);
            // Plugin.Log.LogInfo($"[CYCLE] {EnemyName}: CyclesNeeded={cyclesNeeded}, current={enemyAnimationCycles[enemyInstance]}");
            
            // Pass GG after cyclesNeeded full cycles
            if (enemyAnimationCycles[enemyInstance] >= cyclesNeeded)
            {
                // Plugin.Log.LogInfo($"[{EnemyName}] ===== HANDOFF TRIGGERED! =====");
                globalHandoffCount++;
                EnemyHandoffSystem.GlobalHandoffCount++;
                // Plugin.Log.LogInfo($"[{EnemyName}] Passing GG after {enemyAnimationCycles[enemyInstance]} cycles! (Global handoff #{globalHandoffCount})");
                
                enemyHasPassed[enemyInstance] = true;
                
                // REMOVED: Dialogue system call
                
                // Pass GG with delay from config
                StartDelayedHandoff(enemyInstance);
                
                return;
            }
        }
    }
    
    /// <summary>
    /// Display handoff message (above enemy head)
    /// For goblins uses special implementation
    /// </summary>
    protected static void DisplayHandoffMessage(string message, float displayTime, object enemyInstance)
    {
        // For goblins use special implementation from GoblinPassLogic
        var typeName = enemyInstance?.GetType()?.Name ?? "NULL";
        if (typeName.Contains("goblinero") || typeName.Contains("Goblin"))
        {
            // Call method from GoblinPassLogic via reflection (use Assembly for reliability)
            // REMOVED: Dialogue system call via reflection
        }
        
        // For other enemies - log
        // Plugin.Log.LogInfo($"[HANDOFF] {message} (enemyType={typeName})");

        var enemyMono = enemyInstance as MonoBehaviour;
        if (enemyMono != null)
        {
            // REMOVED: Dialogue system call
        }
    }
    
    /// <summary>
    /// Start delayed GG handoff
    /// </summary>
    protected static void StartDelayedHandoff(object enemyInstance)
    {
        try
        {
            string enemyTypeName = enemyInstance?.GetType()?.Name ?? "NULL";
            // Plugin.Log.LogInfo($"[HANDOFF] StartDelayedHandoff for {enemyTypeName}");
            // Optimization: use cached playercon
            var playerObj = UnifiedPlayerCacheManager.GetPlayerObject();
            if (playerObj != null)
            {
                DelayedHandoffScript script = playerObj.GetComponent<DelayedHandoffScript>();
                if (script == null)
                {
                    script = playerObj.AddComponent<DelayedHandoffScript>();
                }
                script.StartDelayedHandoff(enemyInstance);
                // Plugin.Log.LogInfo($"[HANDOFF] Using DelayedHandoffScript for delay: {Plugin.handoffDelay?.Value ?? 3.0f}s");
            }
            else
            {
                // Fallback: create temp GameObject
                GameObject temp = new GameObject("DelayedHandoffTemp");
                var script = temp.AddComponent<DelayedHandoffScript>();
                script.StartDelayedHandoff(enemyInstance);
                // Plugin.Log.LogInfo($"[HANDOFF] Using temp GameObject for DelayedHandoffScript");
            }
        }
        catch (System.Exception ex)
        {
        }
    }
    
    /// <summary>
    /// Reset all data for all enemies
    /// </summary>
    internal static void ResetAll()
    {
        enemyAnimationCycles.Clear();
        enemySessionStartTime.Clear();
        lastCycleTime.Clear();
        enemyHasPassed.Clear();
        enemyDisabled.Clear();
        globalHandoffCount = 0;
        globalSessionStartTime = 0f;
    }
    
    // Instance storage for Harmony static methods
    private static BaseEnemyPassPatch<TEnemyType> _instance;
    
    protected static BaseEnemyPassPatch<TEnemyType> GetInstance()
    {
        return _instance;
    }
    
    protected static void SetInstance(BaseEnemyPassPatch<TEnemyType> instance)
    {
        _instance = instance;
    }
}
