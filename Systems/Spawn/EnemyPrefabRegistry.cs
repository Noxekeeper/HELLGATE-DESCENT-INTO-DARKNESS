using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        { "HellishTouzokuSword", new[] { "Touzoku" } },
        { "TouzokuAxe", new[] { "Touzoku_Axe" } },
        { "HellishTouzokuAxe", new[] { "Touzoku_Axe" } },
        { "BossTouzoku", new[] { "BossTouzoku" } },
        { "BossTouzokuCustom", new[] { "BossTouzoku" } },
        { "HellishTouzokuBoss", new[] { "BossTouzoku" } },
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
        // Vanilla prefab root is named "axe"; it is not preloaded by NoRSceneLoader (unlike SlaveBigAxe).
        { "OtherSlavebigAxe", new[] { "Axe", "axe" } },
        { "Mutude", new[] { "Mutude", "SixHand", "Six_Hand" } },
        { "Bigoni", new[] { "Bigoni_spine" } },
        { "BigoniBrother", new[] { "Bigoni_spine" } }, // Custom mini-boss (same prefab, but renamed)
        { "GobBigAlter", new[] { "GobBigAlter" } },
        { "GobRider", new[] { "GobRider" } },
        { "Slaughterer", new[] { "Slaughterer" } },
        { "Butcher", new[] { "Slaughterer" } }, // RickEnemyMod: vanilla Slaughterer combat, custom fatality spine on erodata
        { "Sisterknight", new[] { "Sisterknight" } },
        { "CrawlingCreatures", new[] { "CrawlingCreatures" } },
        { "CrawlingDead", new[] { "CrawlingDead_spine" } },
        { "CrawlingSisterKnight", new[] { "CrawlingSisterKnight" } },
        { "Undead", new[] { "Undead" } },
        { "MummyDog", new[] { "MummyDog_spine" } },
        { "Wolf", new[] { "MummyDog_spine" } }, // Wolf Mod: same prefab; skeleton is replaced with WolfE/Wolf
        { "MummyMan", new[] { "Mummyman_spine" } },
        { "Vagrant", new[] { "Vagrant_spine" } },
        { "VagrantThrow", new[] { "Vagrant_Throw_spine" } },
        { "VagrantGuard", new[] { "Vagrant_Guard_spine" } },
        { "Mafia", new[] { "mafia_spine" } },
        { "Mafiamuscle", new[] { "mafia_muscle" } },
        { "MafiaBossCustom", new[] { "mafia_muscle" } }, // Custom mafia boss (handoff, HP 600, same prefab)
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
        { "Tentacle", new[] { "Tentacles_trap", "Tentacle" } },
        { "WoodWana", new[] { "WoodWana" } },
        { "WaveSpike", new[] { "WaveSpike" } },
        { "WaveSpikeGuard", new[] { "WaveSpikeGuard" } },
        { "Cocoonspear", new[] { "Cocoonspear" } },
        { "TrapNormal", new[] { "TrapNormal" } },
        { "IronmaidenDamage", new[] { "Ironmaiden_damage" } },
        { "Slaimu", new[] { "sraimu" } },
        { "biscord", new[] { "sraimu", "suraimu" } }
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
                if (obj != null && !string.IsNullOrEmpty(obj.name) && IsRegistryCandidate(obj))
                {
                    TryCacheByPrefabName(obj.name, obj);
                }
            }

            EnsureOtherSlavebigAxePrefab();
            TryCacheFromNoRSceneLoader(null);
            EnsureSceneLockedBossPrefabs();
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
        if (prefab == null || string.IsNullOrEmpty(prefabName))
            return;

        string cleanName = CleanObjectName(prefabName);
        foreach (var mapping in enemyMappings)
        {
            // Check exact match only (no substring matching to avoid conflicts)
            foreach (string expectedName in mapping.Value)
            {
                if (!string.Equals(cleanName, expectedName, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(prefabName, expectedName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!PrefabMatchesConfigKey(mapping.Key, prefab))
                    continue;

                if (!prefabCache.TryGetValue(mapping.Key, out GameObject existing) || existing == null)
                {
                    prefabCache[mapping.Key] = prefab;
                    continue;
                }

                // Replace name-only junk / destroyed refs with a component-validated prefab.
                if (!PrefabMatchesConfigKey(mapping.Key, existing))
                    prefabCache[mapping.Key] = prefab;
            }
        }
    }

    /// <summary>
    /// True when <paramref name="prefab"/> has the combat component expected for a config key.
    /// Prevents caching random GameObjects that only share an enemy name.
    /// </summary>
    internal static bool PrefabMatchesConfigKey(string configKey, GameObject prefab)
    {
        if (prefab == null || string.IsNullOrEmpty(configKey))
            return false;

        if (string.Equals(configKey, "Butcher", StringComparison.OrdinalIgnoreCase)
            || string.Equals(configKey, "Slaughterer", StringComparison.OrdinalIgnoreCase))
            return prefab.GetComponent<Slaughterer>() != null;

        if (string.Equals(configKey, "Sisterknight", StringComparison.OrdinalIgnoreCase))
            return prefab.GetComponent<Sisterknight>() != null;

        if (string.Equals(configKey, "Wolf", StringComparison.OrdinalIgnoreCase))
            return prefab.GetComponent<EnemyDate>() != null;

        return prefab.GetComponent<EnemyDate>() != null
            || prefab.GetComponentInChildren<EnemyDate>(true) != null;
    }

    /// <summary>
    /// Get enemy prefab by config name
    /// </summary>
    /// <param name="enemyType">Enemy type name from config (e.g., "TouzokuNormal")</param>
    /// <returns>GameObject prefab or null if not found</returns>
    public static GameObject GetPrefab(string enemyType)
    {
        if (TryGetPrefab(enemyType, out GameObject prefab))
            return prefab;

        Plugin.Log.LogWarning($"[ENEMY REGISTRY] Prefab not found for: {enemyType}");
        return null;
    }

    /// <summary>
    /// Same as <see cref="GetPrefab"/> but does not log when the type is missing (for internal probing).
    /// </summary>
    public static bool TryGetPrefab(string enemyType, out GameObject prefab)
    {
        prefab = null;
        if (!isInitialized)
            Initialize();

        if (TryGetCachedValidPrefab(enemyType, out prefab))
            return true;

        // NoRSceneLoader keeps authoritative enemy prefab refs after splash scene crawl.
        if (TryCacheFromNoRSceneLoader(enemyType) && TryGetCachedValidPrefab(enemyType, out prefab))
            return true;

        // One-shot init often runs before additive scenes with Spawnenemy are loaded.
        // Re-scan loaded spawn points / Resources for this type (Butcher/Slaughterer, Sisterknight, …).
        if (TryDiscoverPrefab(enemyType, out prefab))
            return true;

        if (string.Equals(enemyType, "OtherSlavebigAxe", StringComparison.OrdinalIgnoreCase) &&
            EnsureOtherSlavebigAxePrefab() &&
            prefabCache.TryGetValue("OtherSlavebigAxe", out prefab))
        {
            return true;
        }

        if (EnsureSceneLockedBossPrefab(enemyType) &&
            prefabCache.TryGetValue(enemyType, out prefab))
        {
            return true;
        }

        if ((string.Equals(enemyType, "BossTouzokuCustom", StringComparison.OrdinalIgnoreCase)
             || string.Equals(enemyType, "HellishTouzokuBoss", StringComparison.OrdinalIgnoreCase))
            && prefabCache.TryGetValue("BossTouzoku", out prefab)
            && prefab != null)
        {
            return true;
        }

        return false;
    }

    private static bool TryGetCachedValidPrefab(string enemyType, out GameObject prefab)
    {
        prefab = null;
        if (string.IsNullOrEmpty(enemyType))
            return false;

        if (prefabCache.TryGetValue(enemyType, out prefab) && prefab != null
            && PrefabMatchesConfigKey(enemyType, prefab))
            return true;

        foreach (var kv in prefabCache)
        {
            if (!string.Equals(kv.Key, enemyType, StringComparison.OrdinalIgnoreCase))
                continue;
            if (kv.Value == null || !PrefabMatchesConfigKey(enemyType, kv.Value))
                continue;
            prefab = kv.Value;
            return true;
        }

        prefab = null;
        return false;
    }

    /// <summary>
    /// Merge Spawnenemy prefabs from currently loaded scenes into the cache.
    /// Call before HellGate zone Execute so walk transitions can resolve enemies
    /// that were not present during the first <see cref="Initialize"/>.
    /// </summary>
    public static void RefreshFromLoadedScenes()
    {
        if (!isInitialized)
        {
            Initialize();
            return;
        }

        try
        {
            TryCacheFromNoRSceneLoader(null);

            Spawnenemy[] spawnPoints = Object.FindObjectsOfType<Spawnenemy>();
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                Spawnenemy spawnPoint = spawnPoints[i];
                if (spawnPoint == null || enemyField == null)
                    continue;

                GameObject enemyPrefab = enemyField.GetValue(spawnPoint) as GameObject;
                if (enemyPrefab == null)
                    continue;

                TryCacheByPrefabName(enemyPrefab.name, enemyPrefab);
            }

            EnsureEliteTemplatesPersisted();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[ENEMY REGISTRY] RefreshFromLoadedScenes failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Pull prefabs from NoRSceneLoader's private enemyPrefabs map (loaded at splash).
    /// Pass null <paramref name="onlyKey"/> to import all known mappings present in that map.
    /// </summary>
    private static bool TryCacheFromNoRSceneLoader(string onlyKey)
    {
        try
        {
            Type loaderType = Type.GetType("NoRSceneLoader.NoRSceneLoaderPlugin, NoRSceneLoader");
            if (loaderType == null)
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    loaderType = assembly.GetType("NoRSceneLoader.NoRSceneLoaderPlugin");
                    if (loaderType != null)
                        break;
                }
            }

            if (loaderType == null)
                return false;

            UnityEngine.Object[] loaders = Object.FindObjectsOfType(loaderType);
            if (loaders == null || loaders.Length == 0)
            {
                // Plugin may live on a hidden DontDestroyOnLoad object; also try Resources.
                loaders = Resources.FindObjectsOfTypeAll(loaderType);
            }

            if (loaders == null || loaders.Length == 0)
                return false;

            FieldInfo field = loaderType.GetField(
                "enemyPrefabs",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                return false;

            bool any = false;
            for (int i = 0; i < loaders.Length; i++)
            {
                object loader = loaders[i];
                if (loader == null)
                    continue;

                if (!(field.GetValue(loader) is System.Collections.IDictionary map) || map.Count == 0)
                    continue;

                foreach (System.Collections.DictionaryEntry entry in map)
                {
                    string prefabName = entry.Key as string;
                    GameObject go = entry.Value as GameObject;
                    if (string.IsNullOrEmpty(prefabName) || go == null)
                        continue;

                    if (!string.IsNullOrEmpty(onlyKey))
                    {
                        if (!enemyMappings.TryGetValue(onlyKey, out string[] names))
                        {
                            bool keyMatch = false;
                            foreach (var mapping in enemyMappings)
                            {
                                if (!string.Equals(mapping.Key, onlyKey, StringComparison.OrdinalIgnoreCase))
                                    continue;
                                names = mapping.Value;
                                keyMatch = true;
                                break;
                            }
                            if (!keyMatch || names == null)
                                continue;
                        }

                        bool nameOk = false;
                        for (int n = 0; n < names.Length; n++)
                        {
                            if (!string.Equals(prefabName, names[n], StringComparison.OrdinalIgnoreCase))
                                continue;
                            nameOk = true;
                            break;
                        }
                        if (!nameOk)
                            continue;
                    }

                    TryCacheByPrefabName(prefabName, go);
                    any = true;
                }
            }

            return any;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[ENEMY REGISTRY] NoRSceneLoader bridge failed: {ex.Message}");
            return false;
        }
    }

    private static void EnsureEliteTemplatesPersisted()
    {
        PersistEliteIfPresent("Slaughterer");
        PersistEliteIfPresent("Sisterknight");
        if (prefabCache.TryGetValue("Slaughterer", out GameObject slaughterer) && slaughterer != null
            && PrefabMatchesConfigKey("Slaughterer", slaughterer))
        {
            if (!prefabCache.ContainsKey("Butcher") || !PrefabMatchesConfigKey("Butcher", prefabCache["Butcher"]))
                prefabCache["Butcher"] = slaughterer;
        }
    }

    private static void PersistEliteIfPresent(string configKey)
    {
        if (!TryGetCachedValidPrefab(configKey, out GameObject source) || source == null)
            return;

        // Already a HellGate inactive template.
        if (source.name != null
            && source.name.StartsWith("HellGate_" + configKey + "_Template", StringComparison.Ordinal))
            return;

        TryPersistBossTemplate(configKey, source, source.scene.IsValid() ? source.scene.name : "NoRSceneLoader");
    }

    private static bool TryDiscoverPrefab(string enemyType, out GameObject prefab)
    {
        prefab = null;
        if (string.IsNullOrEmpty(enemyType))
            return false;

        string resolvedKey = enemyType;
        string[] expectedNames = null;
        if (!enemyMappings.TryGetValue(enemyType, out expectedNames))
        {
            foreach (var mapping in enemyMappings)
            {
                if (!string.Equals(mapping.Key, enemyType, StringComparison.OrdinalIgnoreCase))
                    continue;
                resolvedKey = mapping.Key;
                expectedNames = mapping.Value;
                break;
            }
        }

        if (expectedNames == null || expectedNames.Length == 0)
            return false;

        RefreshFromLoadedScenes();
        if (prefabCache.TryGetValue(resolvedKey, out prefab) && prefab != null)
            return true;

        // Component-typed discovery for known scene-locked elites (Slaughterer → Butcher).
        if (string.Equals(resolvedKey, "Butcher", StringComparison.OrdinalIgnoreCase)
            || string.Equals(resolvedKey, "Slaughterer", StringComparison.OrdinalIgnoreCase))
        {
            if (TryPersistFromComponentType<Slaughterer>("Slaughterer"))
            {
                if (!prefabCache.ContainsKey("Butcher") && prefabCache.TryGetValue("Slaughterer", out GameObject slaughterer))
                    prefabCache["Butcher"] = slaughterer;

                if (prefabCache.TryGetValue(resolvedKey, out prefab) && prefab != null)
                    return true;
            }
        }

        foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (obj == null || string.IsNullOrEmpty(obj.name) || !IsRegistryCandidate(obj))
                continue;

            string clean = CleanObjectName(obj.name);
            for (int i = 0; i < expectedNames.Length; i++)
            {
                if (!string.Equals(clean, expectedNames[i], StringComparison.OrdinalIgnoreCase))
                    continue;

                TryCacheByPrefabName(clean, obj);
                if (prefabCache.TryGetValue(resolvedKey, out prefab) && prefab != null)
                {
                    Plugin.Log?.LogInfo($"[ENEMY REGISTRY] Late-discovered {resolvedKey} -> {clean}");
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryPersistFromComponentType<T>(string configKey) where T : EnemyDate
    {
        if (prefabCache.TryGetValue(configKey, out GameObject cached) && cached != null)
            return true;

        T[] components = Resources.FindObjectsOfTypeAll<T>();
        GameObject best = null;
        int bestScore = int.MinValue;
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null || component.gameObject == null || !IsRegistryCandidate(component.gameObject))
                continue;

            int score = ScoreBossCandidate(component.gameObject, configKey);
            if (score > bestScore)
            {
                bestScore = score;
                best = component.gameObject;
            }
        }

        if (best == null)
            return false;

        string sceneName = best.scene.IsValid() ? best.scene.name : string.Empty;
        return TryPersistBossTemplate(configKey, best, sceneName);
    }

    private static bool IsRegistryCandidate(GameObject obj)
    {
        if (obj.GetComponent<SpawnManagedInstance>() != null)
            return false;

        string name = obj.name;
        if (name.StartsWith("HellGate", StringComparison.Ordinal) && name.Contains("Template_"))
            return false;

        return true;
    }

    /// <summary>
    /// OtherSlavebigAxe is rarely present in NoRSceneLoader bootstrap scenes. Resolve by component scan,
    /// then synthesize a hidden template from SlaveBigAxe when the vanilla prefab was never loaded.
    /// </summary>
    private static bool EnsureOtherSlavebigAxePrefab()
    {
        if (prefabCache.TryGetValue("OtherSlavebigAxe", out GameObject cached) && cached != null)
            return true;

        OtherSlavebigAxe[] components = Resources.FindObjectsOfTypeAll<OtherSlavebigAxe>();
        GameObject best = null;
        int bestScore = int.MinValue;
        for (int i = 0; i < components.Length; i++)
        {
            OtherSlavebigAxe component = components[i];
            if (component == null || component.gameObject == null || !IsRegistryCandidate(component.gameObject))
                continue;

            int score = ScoreOtherSlavebigAxeCandidate(component.gameObject);
            if (score > bestScore)
            {
                bestScore = score;
                best = component.gameObject;
            }
        }

        if (best != null)
        {
            prefabCache["OtherSlavebigAxe"] = best;
            return true;
        }

        return TryBuildOtherSlavebigAxeFromSlaveBigAxe();
    }

    private static int ScoreOtherSlavebigAxeCandidate(GameObject obj)
    {
        int score = 0;
        if (string.Equals(obj.name, "axe", StringComparison.OrdinalIgnoreCase))
            score += 100;

        if (!obj.scene.IsValid() || string.IsNullOrEmpty(obj.scene.name))
            score += 20;

        if (obj.name.StartsWith("HellGate_OtherSlavebigAxe_Template", StringComparison.Ordinal))
            score += 50;

        if (obj.activeInHierarchy)
            score -= 5;

        return score;
    }

    private static bool TryBuildOtherSlavebigAxeFromSlaveBigAxe()
    {
        if (!prefabCache.TryGetValue("SlaveBigAxe", out GameObject slavePrefab) || slavePrefab == null)
            return false;

        try
        {
            GameObject template = Object.Instantiate(slavePrefab);
            template.name = "HellGate_OtherSlavebigAxe_Template";
            template.SetActive(false);

            SlaveBigAxe slaveComponent = template.GetComponent<SlaveBigAxe>();
            if (slaveComponent == null)
            {
                Object.Destroy(template);
                return false;
            }

            OtherSlavebigAxe otherComponent = template.AddComponent<OtherSlavebigAxe>();
            CopyComponentFields(slaveComponent, otherComponent);
            Object.DestroyImmediate(slaveComponent);

            Object.DontDestroyOnLoad(template);
            prefabCache["OtherSlavebigAxe"] = template;
            Plugin.Log?.LogInfo("[ENEMY REGISTRY] Built OtherSlavebigAxe template from SlaveBigAxe fallback.");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[ENEMY REGISTRY] OtherSlavebigAxe fallback failed: {ex.Message}");
            return false;
        }
    }

    private static void CopyComponentFields(Component source, Component destination)
    {
        if (source == null || destination == null)
            return;

        Type sourceType = source.GetType();
        Type destinationType = destination.GetType();
        while (sourceType != null && destinationType != null)
        {
            FieldInfo[] sourceFields = sourceType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < sourceFields.Length; i++)
            {
                FieldInfo sourceField = sourceFields[i];
                if (sourceField.IsStatic)
                    continue;

                FieldInfo destinationField = destinationType.GetField(
                    sourceField.Name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (destinationField == null || destinationField.FieldType != sourceField.FieldType)
                    continue;

                destinationField.SetValue(destination, sourceField.GetValue(source));
            }

            sourceType = sourceType.BaseType;
            destinationType = destinationType.BaseType;
        }
    }

    internal static void CacheBossesFromScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        try
        {
            CacheBossComponent<BossTouzoku>(scene, "BossTouzoku");
            CacheBossComponent<Slaughterer>(scene, "Slaughterer");
            CacheBossComponent<Sisterknight>(scene, "Sisterknight");
            CacheSpawnenemyPrefabsFromScene(scene);

            if (prefabCache.TryGetValue("Slaughterer", out GameObject slaughterer) && slaughterer != null
                && PrefabMatchesConfigKey("Slaughterer", slaughterer)
                && (!prefabCache.ContainsKey("Butcher") || !PrefabMatchesConfigKey("Butcher", prefabCache["Butcher"])))
            {
                prefabCache["Butcher"] = slaughterer;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[ENEMY REGISTRY] Boss cache failed in {scene.name}: {ex.Message}");
        }
    }

    private static void CacheSpawnenemyPrefabsFromScene(Scene scene)
    {
        if (enemyField == null)
            return;

        Spawnenemy[] spawnPoints = GetSceneComponents<Spawnenemy>(scene);
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            Spawnenemy spawnPoint = spawnPoints[i];
            if (spawnPoint == null)
                continue;

            GameObject enemyPrefab = enemyField.GetValue(spawnPoint) as GameObject;
            if (enemyPrefab == null)
                continue;

            TryCacheByPrefabName(enemyPrefab.name, enemyPrefab);

            string clean = CleanObjectName(enemyPrefab.name);
            if (string.Equals(clean, "Slaughterer", StringComparison.OrdinalIgnoreCase)
                && PrefabMatchesConfigKey("Slaughterer", enemyPrefab))
            {
                TryPersistBossTemplate("Slaughterer", enemyPrefab, scene.name);
                if (!prefabCache.ContainsKey("Butcher") || !PrefabMatchesConfigKey("Butcher", prefabCache["Butcher"]))
                    prefabCache["Butcher"] = prefabCache.ContainsKey("Slaughterer")
                        ? prefabCache["Slaughterer"]
                        : enemyPrefab;
            }
            else if (string.Equals(clean, "Sisterknight", StringComparison.OrdinalIgnoreCase)
                     && PrefabMatchesConfigKey("Sisterknight", enemyPrefab))
            {
                TryPersistBossTemplate("Sisterknight", enemyPrefab, scene.name);
            }
        }
    }

    private static void CacheBossComponent<T>(Scene scene, string configKey) where T : EnemyDate
    {
        T[] bosses = GetSceneComponents<T>(scene);
        for (int i = 0; i < bosses.Length; i++)
        {
            T boss = bosses[i];
            if (boss == null || boss.gameObject == null || !IsRegistryCandidate(boss.gameObject))
                continue;

            if (TryPersistBossTemplate(configKey, boss.gameObject, scene.name))
                return;
        }
    }

    private static T[] GetSceneComponents<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return new T[0];

        var list = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == null)
                continue;

            list.AddRange(roots[i].GetComponentsInChildren<T>(true));
        }

        return list.ToArray();
    }

    private static bool TryPersistBossTemplate(string configKey, GameObject source, string sceneName)
    {
        if (string.IsNullOrEmpty(configKey) || source == null)
            return false;

        if (prefabCache.TryGetValue(configKey, out GameObject existing) && existing != null
            && PrefabMatchesConfigKey(configKey, existing)
            && existing.name != null
            && existing.name.StartsWith("HellGate_" + configKey + "_Template", StringComparison.Ordinal))
            return true;

        try
        {
            GameObject template = Object.Instantiate(source);
            template.name = "HellGate_" + configKey + "_Template";
            // Force Awake/Spine init before parking inactive — otherwise clones spawn invisible.
            if (!template.activeSelf)
                template.SetActive(true);
            SpawnConfigExecutor.PrepareSpawnedEnemyPresentation(template);
            template.SetActive(false);
            Object.DontDestroyOnLoad(template);
            prefabCache[configKey] = template;
            if (string.Equals(configKey, "Slaughterer", StringComparison.OrdinalIgnoreCase))
                prefabCache["Butcher"] = template;
            EnemyPrefabDiskCache.RecordBoss(configKey, source, sceneName);
            Plugin.Log?.LogInfo($"[ENEMY REGISTRY] Cached boss template: {configKey} from scene \"{sceneName}\".");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[ENEMY REGISTRY] Failed to persist boss {configKey}: {ex.Message}");
            return false;
        }
    }

    private static void EnsureSceneLockedBossPrefabs()
    {
        EnsureBossTouzokuPrefab();
        TryCacheFromNoRSceneLoader(null);
        TryPersistFromComponentType<Slaughterer>("Slaughterer");
        TryPersistFromComponentType<Sisterknight>("Sisterknight");
        EnsureEliteTemplatesPersisted();
    }

    private static bool EnsureSceneLockedBossPrefab(string enemyType)
    {
        if (string.Equals(enemyType, "BossTouzoku", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(enemyType, "BossTouzokuCustom", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(enemyType, "HellishTouzokuBoss", StringComparison.OrdinalIgnoreCase))
        {
            return EnsureBossTouzokuPrefab();
        }

        if (string.Equals(enemyType, "Slaughterer", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(enemyType, "Butcher", StringComparison.OrdinalIgnoreCase))
        {
            TryCacheFromNoRSceneLoader("Slaughterer");
            TryCacheFromNoRSceneLoader("Butcher");
            if (!TryPersistFromComponentType<Slaughterer>("Slaughterer"))
                EnsureEliteTemplatesPersisted();
            if (prefabCache.TryGetValue("Slaughterer", out GameObject slaughterer) && slaughterer != null
                && PrefabMatchesConfigKey("Slaughterer", slaughterer))
                prefabCache["Butcher"] = slaughterer;
            return TryGetCachedValidPrefab(enemyType, out _);
        }

        if (string.Equals(enemyType, "Sisterknight", StringComparison.OrdinalIgnoreCase))
        {
            TryCacheFromNoRSceneLoader("Sisterknight");
            if (!TryPersistFromComponentType<Sisterknight>("Sisterknight"))
                EnsureEliteTemplatesPersisted();
            return TryGetCachedValidPrefab("Sisterknight", out _);
        }

        return false;
    }

    private static bool EnsureBossTouzokuPrefab()
    {
        if (prefabCache.TryGetValue("BossTouzoku", out GameObject cached) && cached != null)
            return true;

        BossTouzoku[] components = Resources.FindObjectsOfTypeAll<BossTouzoku>();
        GameObject best = null;
        int bestScore = int.MinValue;
        for (int i = 0; i < components.Length; i++)
        {
            BossTouzoku component = components[i];
            if (component == null || component.gameObject == null || !IsRegistryCandidate(component.gameObject))
                continue;

            int score = ScoreBossCandidate(component.gameObject, "BossTouzoku");
            if (score > bestScore)
            {
                bestScore = score;
                best = component.gameObject;
            }
        }

        if (best == null)
            return false;

        string sceneName = best.scene.IsValid() ? best.scene.name : string.Empty;
        return TryPersistBossTemplate("BossTouzoku", best, sceneName);
    }

    private static int ScoreBossCandidate(GameObject obj, string configKey)
    {
        int score = 0;
        if (string.Equals(CleanObjectName(obj.name), configKey, StringComparison.OrdinalIgnoreCase))
            score += 100;

        if (obj.name.StartsWith("HellGate_" + configKey + "_Template", StringComparison.Ordinal))
            score += 80;

        if (!obj.scene.IsValid() || string.IsNullOrEmpty(obj.scene.name))
            score += 20;

        if (obj.activeInHierarchy)
            score -= 5;

        return score;
    }

    private static string CleanObjectName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        int cloneIndex = name.IndexOf("(Clone)", StringComparison.Ordinal);
        if (cloneIndex >= 0)
            name = name.Substring(0, cloneIndex);

        return name.Trim();
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
