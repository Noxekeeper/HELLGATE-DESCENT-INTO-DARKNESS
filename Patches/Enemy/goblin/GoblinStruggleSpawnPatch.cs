using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;
using System.Reflection;
using NoREroMod.Systems.Spawn;

namespace NoREroMod.Patches.Enemy;

/// <summary>
/// Spawns 2 additional goblins when player breaks free from goblin START or 2ERO_START animation
/// During START/2ERO_START animation, 3 goblins appear (1 real + 2 animation parts)
/// When player escapes, only 1 real goblin remains, so we spawn 2 more to maintain consistency
/// HARDMODE: This feature can be disabled in config if needed
/// </summary>
internal static class GoblinStruggleSpawnPatch
{
    private static bool wasGoblinStartAnimation = false;

    /// <summary>
    /// Track when goblin START animation occurs
    /// </summary>
    [HarmonyPatch(typeof(goblinero), "OnEvent")]
    [HarmonyPostfix]
    private static void TrackGoblinStartAnimation(goblinero __instance)
    {
        try
        {
            // Check if feature is enabled
            if (Plugin.enableGoblinStruggleSpawn?.Value != true)
            {
                return;
            }

            // Get spine animation to check current animation
            var spineField = typeof(goblinero).GetField("myspine", BindingFlags.NonPublic | BindingFlags.Instance);
            if (spineField == null) return;

            var spine = spineField.GetValue(__instance) as Spine.Unity.SkeletonAnimation;
            if (spine == null || spine.AnimationState == null) return;

            string currentAnim = spine.AnimationName;
            if (currentAnim == "START" || currentAnim == "2ERO_START")
            {
                wasGoblinStartAnimation = true;
                // Plugin.Log.LogInfo($"[GOBLIN STRUGGLE SPAWN] {currentAnim} animation detected - tracking for spawn on escape"); // Disabled by request
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"[GOBLIN STRUGGLE SPAWN] Error tracking START animation: {ex.Message}");
        }
    }

    /// <summary>
    /// Monitor player escape from H-scene via StruggleSystem.startGrabInvul()
    /// This method is called when player successfully escapes from grab
    /// </summary>
    [HarmonyPatch(typeof(StruggleSystem), "startGrabInvul")]
    [HarmonyPostfix]
    private static void CheckPlayerEscapeOnStruggleSuccess()
    {
        try
        {
            // Check if feature is enabled
            if (Plugin.enableGoblinStruggleSpawn?.Value != true)
            {
                return;
            }

            // Check if we tracked START animation
            if (wasGoblinStartAnimation)
            {
                // Find player
                GameObject playerObject = GameObject.FindWithTag("Player");
                if (playerObject != null)
                {
                    var player = playerObject.GetComponent<playercon>();
                    if (player != null)
                    {
                        // Plugin.Log.LogInfo("[GOBLIN STRUGGLE SPAWN] Player escaped from START animation via struggle!"); // Disabled by request
                        SpawnGoblinsOnEscape(player);
                    }
                }
                wasGoblinStartAnimation = false; // Reset flag
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"[GOBLIN STRUGGLE SPAWN] Error checking escape: {ex.Message}");
        }
    }

    /// <summary>
    /// Spawn 2 goblins to the left and right of player
    /// </summary>
    private static void SpawnGoblinsOnEscape(playercon player)
    {
        try
        {
            // Initialize registry if needed
            EnemyPrefabRegistry.Initialize();

            // Get goblin prefab
            GameObject goblinPrefab = EnemyPrefabRegistry.GetPrefab("Goblin");
            if (goblinPrefab == null)
            {
                Plugin.Log.LogWarning("[GOBLIN STRUGGLE SPAWN] Goblin prefab not found in registry!");
                return;
            }

            Vector2 playerPos = player.transform.position;
            float spawnOffset = 3f; // Distance from player

            // Spawn left goblin
            Vector2 leftPos = new Vector2(playerPos.x - spawnOffset, playerPos.y);
            GameObject leftGoblin = Object.Instantiate(goblinPrefab, leftPos, Quaternion.identity);
            // Plugin.Log.LogInfo($"[GOBLIN STRUGGLE SPAWN] Spawned left goblin at ({leftPos.x:F2}, {leftPos.y:F2})"); // Disabled by request

            // Spawn right goblin
            Vector2 rightPos = new Vector2(playerPos.x + spawnOffset, playerPos.y);
            GameObject rightGoblin = Object.Instantiate(goblinPrefab, rightPos, Quaternion.identity);
            // Plugin.Log.LogInfo($"[GOBLIN STRUGGLE SPAWN] Spawned right goblin at ({rightPos.x:F2}, {rightPos.y:F2})"); // Disabled by request

            // Plugin.Log.LogInfo("[GOBLIN STRUGGLE SPAWN] Successfully spawned 2 goblins after escape from START animation"); // Disabled by request
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"[GOBLIN STRUGGLE SPAWN] Error spawning goblins: {ex.Message}\n{ex.StackTrace}");
        }
    }
}