using System;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using Spine;
using Spine.Unity;
using NoREroMod;
using NoREroMod.Systems.Effects;
using NoREroMod.Systems.Cache;
using UnityEngine;

namespace NoREroMod.Systems.Effects;

/// <summary>
/// Universal trigger for black background activation and X-ray clip display.
/// Works on FIN/FIN1 animations and goblin 2ERO_iki.
/// </summary>
[HarmonyPatch]
internal static class HSceneBlackBackgroundTriggerPatch
{
        // Guard against multiple FIN triggers on same frame/animation
        private static readonly System.Collections.Generic.Dictionary<int, string> LastFinForEnemy = new System.Collections.Generic.Dictionary<int, string>();
        // Guard for all enemies: VAG clip shown only once per FIN animation
        private static readonly System.Collections.Generic.Dictionary<int, string> VAGClipShown = new System.Collections.Generic.Dictionary<int, string>();
        private const string KakasiName = "kakashi_ero2";
        private const string KakasiAlias = "kakasi";

        // Tracking cycles for white inquisitor
        private static readonly System.Collections.Generic.Dictionary<object, int> WhiteInqCycles = new System.Collections.Generic.Dictionary<object, int>();
        private static readonly System.Collections.Generic.Dictionary<object, bool> WhiteInqPassed = new System.Collections.Generic.Dictionary<object, bool>();

        /// <summary>
        /// Reset состояния white inquisitor
        /// </summary>
        internal static void ResetWhiteInqState()
        {
            WhiteInqCycles.Clear();
            WhiteInqPassed.Clear();
        }

    /// <summary>
    /// Universal method for checking FIN animations. Includes only FIN, FIN1.
    /// </summary>
    private static bool IsFinAnimation(string animName)
    {
        if (string.IsNullOrEmpty(animName)) return false;
        
        string animUpper = animName.ToUpperInvariant();
        return animUpper == "FIN"
            || animUpper == "FIN1";
    }
    
    /// <summary>
    /// Check FIN animation for goblins (2ERO_iki2).
    /// </summary>
    private static bool IsGoblinFinAnimation(string animName)
    {
        if (string.IsNullOrEmpty(animName)) return false;
        
        string animUpper = animName.ToUpperInvariant();
        return animUpper == "2ERO_IKI2";
    }
    
    /// <summary>
    /// Check FIN animations for SlaveBigAxe (FIN, JIGOFIN, JIGOPOSTFIN).
    /// </summary>
    private static bool IsSlaveBigAxeFinAnimation(string animName)
    {
        if (string.IsNullOrEmpty(animName)) return false;
        
        string animUpper = animName.ToUpperInvariant();
        return animUpper == "FIN"
            || animUpper == "FIN2"
            || animUpper == "JIGOFIN"
            || animUpper == "JIGOFIN2"
            || animUpper == "JIGOPOSTFIN"
            || animUpper == "JIGOPOSTFIN2";
    }

    /// <summary>
    /// Check FIN animations for BigoniBrother (FIN, FIN2)
    /// </summary>
    private static bool IsBigoniBrotherFinAnimation(string animName)
    {
        if (string.IsNullOrEmpty(animName)) return false;

        string animUpper = animName.ToUpperInvariant();
        return animUpper == "FIN"
            || animUpper == "FIN2";
    }

    /// <summary>
    /// Get current animation via reflection (universal method).
    /// </summary>
    private static string GetCurrentAnimation(object instance, Spine.AnimationState state)
    {
        if (instance == null) return string.Empty;
        
        try
        {
            var instanceType = instance.GetType();
            if (instanceType == null) return string.Empty;
            
            // Try different spine field variants
            var spineField = instanceType.GetField("myspine", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
                          ?? instanceType.GetField("mySpine", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
                          ?? instanceType.GetField("_myspine", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            
            if (spineField != null)
            {
                var spine = spineField.GetValue(instance) as SkeletonAnimation;
                if (spine != null && !string.IsNullOrEmpty(spine.AnimationName))
                {
                    return spine.AnimationName;
                }
            }
            
            // Fallback: via state
            if (state != null)
            {
                var track = state.GetCurrent(0);
                if (track?.Animation != null)
                {
                    return track.Animation.Name;
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[HScene Black Background Trigger] Error getting animation: {ex.Message}");
        }
        
        return string.Empty;
    }
    
    
    /// <summary>
    /// Universal patch for all enemy classes with OnEvent method.
    /// Tracks FIN, FIN1, FIN2, FIN3 events.
    /// IMPORTANT: Don't use return for goblins to avoid blocking other systems (handoff, dialogue, etc.).
    /// </summary>
    private static void ProcessOnEvent(object __instance, Spine.AnimationState state, Spine.Event e)
    {
        // Special handling for InquisitionWhiteERO - передача ГГ
        if (__instance is InquisitionWhiteERO whiteEro)
        {
            HandleInquisitionWhiteHandoff(whiteEro, state, e);
        }

        try
        {
            if (__instance == null || e == null) return;

            // DEBUG: Log ALL events to see if BigoniBrother is being processed
            // Plugin.Log.LogInfo($"[DEBUG] HSceneBlackBackgroundTriggerPatch.ProcessOnEvent called for {__instance.GetType().Name} with event {e.Data?.Name}");

            // Optimization: use cached playercon
            var player = UnifiedPlayerCacheManager.GetPlayer();
            if (player == null || !player.eroflag || player.erodown == 0)
            {
                return;
            }

            var instanceType = __instance.GetType();
            if (instanceType == null) return;

            string eventName = e.Data?.Name ?? string.Empty;
            string currentAnim = GetCurrentAnimation(__instance, state);
            string enemyType = instanceType.Name;

            // Plugin.Log.LogInfo($"[HSceneTrigger] ProcessOnEvent started - class: '{enemyType}', event: '{eventName}', anim: '{currentAnim}'");

            // Log game object hierarchy for debugging
            var comp = __instance as Component;
            if (comp != null && comp.gameObject != null)
            {
                string objName = comp.gameObject.name;
                string parentName = comp.transform.parent != null ? comp.transform.parent.name : "no parent";
                // Plugin.Log.LogInfo($"[HSceneTrigger] GameObject: '{objName}', Parent: '{parentName}'");
            }

            // Special handling for BigoniBrother: check for BigoniBrotherERO class
            if (__instance.GetType().Name == "BigoniBrotherERO")
            {
                // Plugin.Log.LogInfo($"[HSceneTrigger] BigoniBrotherERO detected, changing enemyType from '{enemyType}' to 'bigonibrother'");
                enemyType = "bigonibrother";
            }
            // Check for BigoniBrother regardless of class type (works for both StartBigoniERO and BigoniBrotherERO)
            else if (enemyType == "StartBigoniERO" || enemyType == "BigoniBrotherERO")
            {
                var component = __instance as Component;
                if (component != null && component.gameObject != null)
                {
                    string gameObjectName = component.gameObject.name;
                    // Plugin.Log.LogInfo($"[HSceneTrigger] Checking for BigoniBrother in {enemyType}, gameObject: '{gameObjectName}'");

                    // Check if the gameObject itself has BigoniBrother in name
                    if (gameObjectName.Contains("BigoniBrother"))
                    {
                        // Plugin.Log.LogInfo($"[HSceneTrigger] BigoniBrother detected by name, changing enemyType from '{enemyType}' to 'bigonibrother'");
                        enemyType = "bigonibrother";
                    }
                    // Check parent object name (ERO components are on child objects)
                    else if (component.transform.parent != null && component.transform.parent.name.Contains("BigoniBrother"))
                    {
                        // Plugin.Log.LogInfo($"[HSceneTrigger] BigoniBrother detected by parent name '{component.transform.parent.name}', changing enemyType from '{enemyType}' to 'bigonibrother'");
                        enemyType = "bigonibrother";
                    }
                    // Also try to find Bigoni component and check its name (for edge cases)
                    else
                    {
                        var bigoniComponent = component.GetComponent<Bigoni>() ?? component.GetComponentInChildren<Bigoni>() ??
                                             component.GetComponentInParent<Bigoni>();
                        if (bigoniComponent != null && bigoniComponent.gameObject.name.Contains("BigoniBrother"))
                        {
                            // Plugin.Log.LogInfo($"[HSceneTrigger] BigoniBrother detected by Bigoni component, changing enemyType from '{enemyType}' to 'bigonibrother'");
                            enemyType = "bigonibrother";
                        }
                        else
                        {
                            // Plugin.Log.LogInfo($"[HSceneTrigger] BigoniBrother NOT detected. enemyType remains '{enemyType}', gameObject: '{gameObjectName}'");
                            if (component.transform.parent != null)
                            {
                                // Plugin.Log.LogInfo($"[HSceneTrigger] Parent name: '{component.transform.parent.name}'");
                            }
                        }
                    }
                }
                else
                {
                    // Plugin.Log.LogInfo($"[HSceneTrigger] Component is null or gameObject is null for {enemyType}");
                }
            }

            // Special rule for Mutude: enable FIN, ignore FIN2 (by animation and eventName)
            bool isMutude = enemyType.Contains("Mutude") || enemyType == "Mutudeero" || enemyType == "Mutude";
            if (isMutude && !string.IsNullOrEmpty(currentAnim))
            {
                string mutudeAnimUpper = currentAnim.ToUpperInvariant();
                string mutudeEventUpper = eventName?.ToUpperInvariant() ?? string.Empty;
                if (mutudeAnimUpper == "FIN2" || mutudeEventUpper == "FIN2")
                {
                    return;
                }
                if (mutudeAnimUpper == "FIN")
                {
                    HSceneBlackBackgroundSystem.Activate(enemyType, currentAnim);
                    return;
                }
            }
            
            bool isKakasi = enemyType.Equals("kakashi_ero2", StringComparison.OrdinalIgnoreCase) ||
                            enemyType.IndexOf("kakasi", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isGoblin = enemyType.ToLowerInvariant().Contains("goblin");
            bool isSlaveBigAxe = enemyType.Equals("SlaveBigAxeEro", StringComparison.OrdinalIgnoreCase) ||
                                 enemyType.IndexOf("slavebigaxe", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isBigoniBrother = enemyType.Equals("bigonibrother", StringComparison.OrdinalIgnoreCase);

            // Check FIN animations: standard FIN/FIN1, goblin 2ERO_iki2, SlaveBigAxe FIN/JIGOFIN/JIGOPOSTFIN, or BigoniBrother FIN/FIN2
            bool isFinAnim = IsFinAnimation(currentAnim) ||
                            (isGoblin && IsGoblinFinAnimation(currentAnim)) ||
                            (isSlaveBigAxe && IsSlaveBigAxeFinAnimation(currentAnim)) ||
                            (isBigoniBrother && IsBigoniBrotherFinAnimation(currentAnim));
            
            // Check FIN/FIN1 and goblin special event (strictly 2ERO_iki)
            string evtUpper = eventName.ToUpperInvariant();
            string animUpper = currentAnim.ToUpperInvariant();
            
            // Clear VAG show flag for all enemies if animation changed (no longer FIN)
            // For goblins: clear flag when animation is NOT ERO_iki, ERO_iki2, 2ERO_iki, or 2ERO_iki2 (transition to other animation)
            if (!isFinAnim)
            {
                // For goblins: clear only if current animation is not ERO_iki, ERO_iki2, 2ERO_iki, or 2ERO_iki2
                if (isGoblin)
                {
                    if (animUpper != "ERO_IKI" && animUpper != "ERO_IKI2" && animUpper != "2ERO_IKI" && animUpper != "2ERO_IKI2")
                    {
                        int key = __instance != null ? __instance.GetHashCode() : 0;
                        if (key != 0 && VAGClipShown.ContainsKey(key))
                        {
                            VAGClipShown.Remove(key);
                        }
                    }
                }
                else
                {
                    // For other enemies: standard cleanup
                    int key = __instance != null ? __instance.GetHashCode() : 0;
                    if (key != 0 && VAGClipShown.ContainsKey(key))
                    {
                        VAGClipShown.Remove(key);
                    }
                }
            }
            
            // Goblins: activate black background and show clip on SE event during 2ERO_iki animation only
            // ERO_iki and ERO_iki2 should NOT trigger black background
            // Show clip on first SE event (se_count == 1) during 2ERO_iki animation
            bool is2EroIkiAnim = animUpper == "2ERO_IKI";
            bool isEroIkiAnim = animUpper == "ERO_IKI" || animUpper == "ERO_IKI2";
            
            // CRITICAL: Для ERO_iki и ERO_iki2 НЕ активируем черный экран (может block events)
            if (isGoblin && isEroIkiAnim)
            {
                // Skip обработку черного экраon for ERO_iki/ERO_iki2
                // Продолжаем обработку other событий
            }
            else if (isGoblin && evtUpper == "SE" && is2EroIkiAnim)
            {
                // Get se_count via reflection
                int seCount = 0;
                try
                {
                    var seCountField = __instance.GetType().GetField("se_count", 
                        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    if (seCountField != null)
                    {
                        var seCountValue = seCountField.GetValue(__instance);
                        if (seCountValue != null && int.TryParse(seCountValue.ToString(), out int parsedSeCount))
                        {
                            seCount = parsedSeCount;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning($"[HScene Black Background Trigger] Error getting se_count: {ex.Message}");
                }
                
                // Always show black screen for SE events during 2ERO_iki
                // But only if not already active to prevent multiple activations
                if (!HSceneBlackBackgroundSystem.IsActive)
                {
                    HSceneBlackBackgroundSystem.Activate(enemyType, currentAnim);
                }
                    
                // Show clip only when se_count == 1 (first SE event during animation)
                if (seCount == 1)
                {
                    int key = __instance != null ? __instance.GetHashCode() : 0;
                    string finAnimSignature = "2ERO_iki";
                    
                    // Universal guard: show VAG clip only once per animation
                    string prevAnim = null;
                    bool alreadyShown = key != 0 && VAGClipShown.TryGetValue(key, out prevAnim) && prevAnim == finAnimSignature;
                    
                    if (!alreadyShown)
                    {
                        // Mark that VAG already shown for this animation
                        if (key != 0)
                        {
                            VAGClipShown[key] = finAnimSignature;
                        }
                        
                        // Show special goblin clip
                        try
                        {
                            var cameraController = NoREroMod.Systems.Camera.HSceneCameraController.Instance;
                            if (cameraController != null)
                            {
                                var cumDisplay = cameraController.GetCumDisplay();
                                if (cumDisplay != null)
                                {
                                    cumDisplay.ShowClimax(enemyType);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Plugin.Log?.LogWarning($"[HScene Black Background Trigger] CumDisplay error (Goblin): {ex.Message}");
                        }
                    }
                }
                // Don't return - allow system to continue processing other events
            }
            
            // Goblins: continue black background on animation switch event 2ERO_iki only (without clip)
            // ERO_iki and ERO_iki2 should NOT trigger black background
            // CRITICAL: Проверяем, that this НЕ ERO_iki or ERO_iki2 анимация
            if (isGoblin && evtUpper == "2ERO_IKI" && !isEroIkiAnim)
            {
                // Only activate if not already active to prevent multiple activations
                if (!HSceneBlackBackgroundSystem.IsActive)
                {
                    HSceneBlackBackgroundSystem.Activate(enemyType, currentAnim);
                }
                // Don't return - allow system to continue processing
            }
            
            if (isFinAnim)
            {
                // Guard against infinite FIN triggering
                int key = __instance != null ? __instance.GetHashCode() : 0;
                string finSignature = $"{currentAnim}|{eventName}";
                if (key != 0 && LastFinForEnemy.TryGetValue(key, out var prev) && prev == finSignature)
                {
                    return;
                }
                if (key != 0)
                {
                    LastFinForEnemy[key] = finSignature;
                }

                // Universal guard: show VAG clip only once per FIN animation for all enemies
                string finAnimSignature = currentAnim ?? "FIN";
                
                if (key != 0 && VAGClipShown.TryGetValue(key, out var prevAnim) && prevAnim == finAnimSignature)
                {
                    // Already showed VAG for this FIN animation - skip
                    return;
                }
                
                // Mark that VAG already shown for this FIN animation
                if (key != 0)
                {
                    VAGClipShown[key] = finAnimSignature;
                }

                if (isKakasi)
                {
                    // For Kakasi: X-ray banner only with explicit vaginal clip selection, no black background
                    try
                    {
                        var cameraController = NoREroMod.Systems.Camera.HSceneCameraController.Instance;
                        if (cameraController != null)
                        {
                            var cumDisplay = cameraController.GetCumDisplay();
                            if (cumDisplay != null)
                    {
                        // Random selection from two VAG clips
                        string[] kakasiClips = { @"FIN\VAG\Cum_inside_Action1", @"FIN\VAG\Cum_inside_Action2" };
                        int idx = UnityEngine.Random.Range(0, kakasiClips.Length);
                        string clipFolder = kakasiClips[idx];
                                cumDisplay.ShowClimax("kakashi_ero2", clipFolder);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.LogWarning($"[HScene Black Background Trigger] CumDisplay error (Kakasi): {ex.Message}");
                    }
                    return;
                }

                // Goblins already processed on 2ERO_iki event - don't show ShowClimax again
                if (isGoblin)
                {
                    // Black background only, no VAG banner (already shown on 2ERO_iki)
                    HSceneBlackBackgroundSystem.Activate(enemyType, currentAnim);
                    return;
                }

                // SlaveBigAxe: show black background and X-ray banner on all 3 FIN phases
                if (isSlaveBigAxe)
                {
                    HSceneBlackBackgroundSystem.Activate(enemyType, currentAnim);
                    try
                    {
                        var cameraController = NoREroMod.Systems.Camera.HSceneCameraController.Instance;
                        if (cameraController != null)
                        {
                            var cumDisplay = cameraController.GetCumDisplay();
                            if (cumDisplay != null)
                            {
                                cumDisplay.ShowClimax(enemyType);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.LogWarning($"[HScene Black Background Trigger] CumDisplay error (SlaveBigAxe): {ex.Message}");
                    }
                    return;
                }

                // BigoniBrother: show black background and X-ray banner on FIN and FIN2
                if (isBigoniBrother && isFinAnim)
                {
                    HSceneBlackBackgroundSystem.Activate(enemyType, currentAnim);
                    try
                    {
                        var cameraController = NoREroMod.Systems.Camera.HSceneCameraController.Instance;
                        if (cameraController != null)
                        {
                            var cumDisplay = cameraController.GetCumDisplay();
                            if (cumDisplay != null)
                            {
                                cumDisplay.ShowClimax(enemyType);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.LogWarning($"[HScene Black Background Trigger] CumDisplay error (BigoniBrother): {ex.Message}");
                    }
                    return;
                }

                HSceneBlackBackgroundSystem.Activate(enemyType, currentAnim);
                try
                {
                    var cameraController = NoREroMod.Systems.Camera.HSceneCameraController.Instance;
                    if (cameraController != null)
                    {
                        var cumDisplay = cameraController.GetCumDisplay();
                        if (cumDisplay != null)
                        {
                            // Special handling for BigoniBrother in general case
                            if (enemyType == "StartBigoniERO")
                            {
                                var component = __instance as Component;
                                if (component != null && component.gameObject != null)
                                {
                                    string gameObjectName = component.gameObject.name;
                                    string parentName = component.transform.parent != null ? component.transform.parent.name : "";

                                    if (gameObjectName.Contains("BigoniBrother") || parentName.Contains("BigoniBrother"))
                                    {
                                        enemyType = "bigonibrother";
                                        // Plugin.Log.LogInfo($"[HSceneTrigger] BigoniBrother detected in general case, changed enemyType to 'bigonibrother'");
                                    }
                                }
                            }

                            // Plugin.Log.LogInfo($"[HSceneTrigger] Calling ShowClimax with enemyType: '{enemyType}'");
                            cumDisplay.ShowClimax(enemyType);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning($"[HScene Black Background Trigger] CumDisplay error: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[HScene Black Background Trigger] Error: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// Universal patch for all classes with OnEvent method.
    /// Uses HarmonyTargetMethod for dynamic class discovery.
    /// </summary>
    static System.Collections.Generic.IEnumerable<MethodBase> TargetMethods()
    {
        // List of class names for patching (use strings to avoid compilation errors)
        string[] typeNames = new string[]
        {
            "kakashi_ero2",
            "EroAnimation",
            "EroTouzoku",
            "EroTouzokuAXE",
            "goblinero",
            "SuccubusERO",
            "MimickERO",
            "MushroomERO",
            "MummyManERO",
            "MummyDogERO",
            "PilgrimERO",
            "LibrarianERO",
            "PraymaidenERO",
            "TyoukyoushiERO",
            "BigoniERO",
            "CoolmaidenERO",
            "MinotaurosuERO",
            "UndeadERO",
            "SnailshellERO",
            "SisiruiruiERO",
            "VagrantERO",
            "VagrantMainERO",
            "PrisonOfficerERO",
            "SinnerslaveCrossbowERO",
            "skeltonOozeERO",
            "SlaveBigAxeEro",
            "SlumToiletERO",
            "SlumToiletGoEro",
            "RosewarmEro",
            "Mutudeero",
            "MermanERO",
            "Ivy_ERO",
            "ArulauneERO",
            "BlackMafiaERO",
            "BlackOozetrapERO",
            "BlackOozeTrapTypeBERO",
            "BOSS_VillageERO",
            "CocoonmanERO",
            "CocconBOSS_ERO",
            "CrawlingDeadERO",
            "CrawlingCreatureERO",
            "CrowInquisitionERO",
            "EroBOSSTouzoku",
            "EroMafiamuscle",
            "InquisitionRedERO",
            "InquisitionWhiteERO",
            "InquiBlackEro",
            "KinokoERO",
            "LakeEroAradia",
            "LakeEroMng",
            "MesgakiEroSpine",
            "SisterKnightEro",
            "SisterMobero",
            "SisterMobEroNoDialog",
            "Tentacles_ero",
            "TrapMachineERO",
            "Trap_RockinghorseERO",
            "TrapSpiderERO",
            "Trap_TentacleIronmaidenERO",
            "WallHipERO",
            "StartTyoukyoushiERO",
            "StartPraymaidenERO",
            "StartBigoniERO",
            "EvbunnyERO",
            "EvbunnyEROSTART",
            "EvbunnyDownERO",
            "EvCandoreERO",
            "EvBarEROMng",
            "LastIbaranoMajyoERO",
            "ERODemonRequiemKnight",
            "CendEroAnimationSpine",
            "BendEroAnimationSpine",
            "EroAnimation_suraimu",
            "PictureEro",
            "BeastBellyERO",
            "GorotukiERO",
            "BadstatusEro",
            "CocconEROStart",
            "GoblineroStart",
            "GobRiderEROStart",
            "PunishmentEROTentacleIronmaiden",
            "PunishmentEROTentacleIronmaidenCandore",
            "Mob_rockinghorseERO",
            "GobTrapEROMng",
            "CrawlingSisterKnightERO",
            "AngelStatue_ERO",
        };
        
        // Also patch base classes directly (they definitely exist)
        var baseTypes = new Type[]
        {
            typeof(kakashi_ero2),
            typeof(EroAnimation),
            typeof(EroTouzoku),
            typeof(EroTouzokuAXE),
            typeof(goblinero),
        };
        
        // Patch base classes
        foreach (var type in baseTypes)
        {
            var method = AccessTools.Method(type, "OnEvent");
            if (method != null)
            {
                yield return method;
            }
        }
        
        // Patch other classes via name search (safely)
        foreach (string typeName in typeNames)
        {
            var type = AccessTools.TypeByName(typeName);
            if (type != null)
            {
                // Check method existence via reflection to avoid HarmonyX warnings
                var method = type.GetMethod("OnEvent", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                    null,
                    new Type[] { typeof(Spine.AnimationState), typeof(int), typeof(Spine.Event) },
                    null);
                
                if (method != null)
                {
                    yield return method;
                }
                // If method not found - just skip this class (not critical, some classes don't have OnEvent)
            }
        }
    }

    /// <summary>
    /// Processing handoff of GG for white inquisitor
    /// </summary>
    private static void HandleInquisitionWhiteHandoff(InquisitionWhiteERO __instance, Spine.AnimationState state, Spine.Event e)
    {
        try
        {
            string eventName = e?.Data?.Name ?? e?.ToString() ?? string.Empty;

            // Get spine и se_count via reflection
            var spineField = typeof(InquisitionWhiteERO).GetField("myspine", BindingFlags.NonPublic | BindingFlags.Instance);
            var spine = spineField?.GetValue(__instance) as SkeletonAnimation;
            string animName = spine?.AnimationName ?? "UNKNOWN";

            var seCountField = typeof(InquisitionWhiteERO).GetField("se_count", BindingFlags.Public | BindingFlags.Instance);
            int seCount = (int)(seCountField?.GetValue(__instance) ?? 0);

            // ЛОГИРОВАТЬ ВСЕ СОБЫТИЯ ДЛЯ ДИАГНОСТИКИ
            Plugin.Log.LogInfo($"[WHITE INQ] InquisitionWhiteERO.OnEvent: event={eventName}, anim={animName}, seCount={seCount}");

            // Optimization: use cached playercon
            var player = UnifiedPlayerCacheManager.GetPlayer();
            if (player == null || !player.eroflag || player.erodown == 0)
            {
                return; // H-сцеon not активна
            }

            // Проверяем условия передачи on JIGO (раньше чем JIGO2 for бесшовности)
            if (eventName == "JIGO")
            {
                Plugin.Log.LogInfo($"[WHITE INQ] HANDOFF CONDITION MET: JIGO event (earlier for seamless transition), seCount={seCount}");

                // Check if not передали ли already этого enemy
                if (WhiteInqPassed.ContainsKey(__instance) && WhiteInqPassed[__instance])
                {
                    Plugin.Log.LogInfo($"[WHITE INQ] Enemy already passed, skipping");
                    return;
                }

                // Increment counter циклов
                if (!WhiteInqCycles.ContainsKey(__instance))
                {
                    WhiteInqCycles[__instance] = 0;
                }
                WhiteInqCycles[__instance]++;

                // Pass after 1 цикла
                if (WhiteInqCycles[__instance] >= 1)
                {
                    Plugin.Log.LogInfo($"[WHITE INQ] HANDOFF TRIGGERED! Cycles: {WhiteInqCycles[__instance]}");
                    WhiteInqPassed[__instance] = true;
                    EnemyHandoffSystem.GlobalHandoffCount++;

                    // Optimization: use cached playercon
                    var playerObj = UnifiedPlayerCacheManager.GetPlayerObject();
                    var delayedScript = playerObj?.GetComponent<NoREroMod.DelayedHandoffScript>()
                                      ?? playerObj?.AddComponent<NoREroMod.DelayedHandoffScript>();
                    if (delayedScript != null)
                    {
                        delayedScript.StartDelayedHandoff(__instance);
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"[WHITE INQ] Error in HandleInquisitionWhiteHandoff: {ex.Message}");
        }
    }

    [HarmonyPrefix]
    private static void UniversalOnEvent_Prefix(object __instance, Spine.AnimationState state, int trackIndex, Spine.Event e)
    {
        ProcessOnEvent(__instance, state, e);
    }
    
}