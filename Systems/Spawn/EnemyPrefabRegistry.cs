using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Universal enemy prefab registry
/// Automatically finds and caches enemy prefabs based on ALL_ENEMIES.txt
/// </summary>
internal static class EnemyPrefabRegistry
{
    private static readonly Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();
    private static readonly System.Reflection.FieldInfo enemyField = typeof(Spawnenemy).GetField("enemy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    private static bool isInitialized = false;

    /// <summary>
    /// Enemy name mappings: ConfigName -> PrefabName(s)
    /// Based on ALL_ENEMIES.txt and decompiled Assembly-CSharp
    /// </summary>
    private static readonly Dictionary<string, string[]> enemyMappings = new Dictionary<string, string[]>
    {
        // Basic enemies
        { "TouzokuNormal", new[] { "Touzoku" } },
        { "TouzokuAxe", new[] { "Touzoku_Axe" } },
        { "Inquisition", new[] { "InquisitionBlack", "Inquisition" } },
        { "InquisitionRED", new[] { "Inquisition_RED" } },
        { "InquisitionWhite", new[] { "Inquisition_white" } },
        { "CrowInquisition", new[] { "CrowInquisition" } },
        { "Goblin", new[] { "goblin" } },
        { "Kakasi", new[] { "kakasienemy" } },
        { "Pilgrim", new[] { "Pilgrim" } },
        { "PrisonOfficer", new[] { "PrisonofficerB", "Prisonofficer" } },
        { "RequiemKnight", new[] { "requiemKnight" } },
        { "Dorei", new[] { "SinnerslaveCrossbow", "Dorei" } },
        { "SlaveBigAxe", new[] { "SlaveBigAxe" } },
        { "OtherSlavebigAxe", new[] { "Axe" } },
        { "Mutude", new[] { "Mutude", "SixHand", "Six_Hand" } },
        { "Bigoni", new[] { "Bigoni_spine" } },
        { "BigoniBrother", new[] { "Bigoni_spine" } }, // Кастомный мини-boss (the same же префаб, but with переименованием)
        { "GobBigAlter", new[] { "GobBigAlter" } },
        { "GobRider", new[] { "GobRider" } },
        { "Slaughterer", new[] { "Slaughterer" } },
        { "Sisterknight", new[] { "Sisterknight" } },
        { "CrawlingCreatures", new[] { "CrawlingCreatures" } },
        { "CrawlingDead", new[] { "CrawlingDead_spine" } },
        { "CrawlingSisterKnight", new[] { "CrawlingSisterKnight" } },
        { "Undead", new[] { "Undead" } },
        { "MummyDog", new[] { "MummyDog_spine" } },
        { "Wolf", new[] { "MummyDog_spine" } }, // Wolf Mod: the same же префаб, скелет заменяется on WolfE/Wolf
        { "MummyMan", new[] { "Mummyman_spine" } },
        { "Vagrant", new[] { "Vagrant_spine" } },
        { "VagrantThrow", new[] { "Vagrant_Throw_spine" } },
        { "VagrantGuard", new[] { "Vagrant_Guard_spine" } },
        { "Mafia", new[] { "mafia_spine" } },
        { "Mafiamuscle", new[] { "mafia_muscle" } },
        { "MafiaBossCustom", new[] { "mafia_muscle" } }, // Кастомный мафия-boss (handoff, HP 600, the same же префаб)
        { "Kinoko", new[] { "kinoko_spine" } },
        { "Arulaune", new[] { "arulaune_spine" } },
        { "Coolmaiden", new[] { "coolmaiden_spine" } },
        { "Tyoukyousi", new[] { "Tyoukyousi_spine" } },
        { "TyoukyousiRed", new[] { "Tyoukyousi_Red" } },
        { "Gorotuki", new[] { "Gorotuki" } },
        { "Cocoonman", new[] { "CocoonmanStart" } },
        { "NormalSnailshell", new[] { "NormalSnailshell" } },
        { "Snailshell", new[] { "Snailshell" } },
        { "BigMerman", new[] { "BigMerman" } },
        { "DifferentBigMerman", new[] { "DifferentBigMerman" } },
        { "BlackOoze", new[] { "BlackOoze_Monster" } },
        { "SkeltonOoze", new[] { "skelton_ooze" } },
        { "Sisiruirui", new[] { "sisiruirui" } },
        { "Minotaurosu", new[] { "Minotaurosu" } },
        { "HighInquisitionFemale", new[] { "HighInquisition_famale" } },
        { "AngelStatue", new[] { "angel_Statue" } },
        { "Librarian", new[] { "Librarian" } },
        { "Sheepheaddemon", new[] { "Head" } },
        { "Slaimu", new[] { "sraimu" } }
    };

    /// <summary>
    /// Initialize and cache all enemy prefabs
    /// Call this once before first spawn
    /// </summary>
    public static void Initialize()
    {
        if (isInitialized)
        {
            return;
        }

        try
        {
            // Disabled: too many logs
            // Plugin.Log.LogInfo("[ENEMY REGISTRY] Initializing enemy prefab registry...");

            // Cache from existing spawn points
            Spawnenemy[] spawnPoints = Object.FindObjectsOfType<Spawnenemy>();
            // Plugin.Log.LogInfo($"[ENEMY REGISTRY] Found {spawnPoints.Length} spawn points");

            foreach (Spawnenemy spawnPoint in spawnPoints)
            {
                GameObject enemyPrefab = enemyField.GetValue(spawnPoint) as GameObject;
                if (enemyPrefab == null) continue;

                string prefabName = enemyPrefab.name;
                TryCacheByPrefabName(prefabName, enemyPrefab);
            }

            // Fallback: search through Resources
            // Plugin.Log.LogInfo("[ENEMY REGISTRY] Searching Resources for missing prefabs...");
            foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (obj != null && !string.IsNullOrEmpty(obj.name))
                {
                    TryCacheByPrefabName(obj.name, obj);
                }
            }

            isInitialized = true;
            // Plugin.Log.LogInfo($"[ENEMY REGISTRY] Initialization complete! Cached {prefabCache.Count} enemy types");
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"[ENEMY REGISTRY] Error initializing: {ex.Message}");
        }
    }

    /// <summary>
    /// Try to cache prefab if it matches any enemy mapping
    /// IMPORTANT: Uses exact match only to avoid conflicts (e.g., Touzoku_Axe should NOT match Touzoku)
    /// Some mappings have multiple names (e.g., Inquisition can be "InquisitionBlack" or "Inquisition")
    /// </summary>
    private static void TryCacheByPrefabName(string prefabName, GameObject prefab)
    {
        foreach (var mapping in enemyMappings)
        {
            // Check exact match only (no substring matching to avoid conflicts)
            foreach (string expectedName in mapping.Value)
            {
                if (string.Equals(prefabName, expectedName, System.StringComparison.OrdinalIgnoreCase))
                {
                    // Кешируем for всех маппингов, которые используют эthe same префаб (включая BigoniBrother)
                    if (!prefabCache.ContainsKey(mapping.Key))
                    {
                        prefabCache[mapping.Key] = prefab;
                        // Plugin.Log.LogInfo($"[ENEMY REGISTRY] Cached {mapping.Key} -> {prefabName}");
                    }
                    // Do not делаем return - продолжаем поиск for other маппингоin with тем же префабом
                }
            }
        }
    }

    /// <summary>
    /// Get enemy prefab by config name
    /// </summary>
    /// <param name="enemyType">Enemy type name from config (e.g., "TouzokuNormal")</param>
    /// <returns>GameObject prefab or null if not found</returns>
    public static GameObject GetPrefab(string enemyType)
    {
        if (!isInitialized)
        {
            Initialize();
        }

        if (prefabCache.TryGetValue(enemyType, out GameObject prefab))
        {
            return prefab;
        }

        Plugin.Log.LogWarning($"[ENEMY REGISTRY] Prefab not found for: {enemyType}");
        return null;
    }

    /// <summary>
    /// Reset cache (call on scene change if needed)
    /// </summary>
    public static void Reset()
    {
        prefabCache.Clear();
        isInitialized = false;
    }

    /// <summary>
    /// Get list of all available enemy types
    /// </summary>
    public static string[] GetAvailableEnemyTypes()
    {
        List<string> types = new List<string>();
        foreach (var key in enemyMappings.Keys)
        {
            types.Add(key);
        }
        return types.ToArray();
    }
}
