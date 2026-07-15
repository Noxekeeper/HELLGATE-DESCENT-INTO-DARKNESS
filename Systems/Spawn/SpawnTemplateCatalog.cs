using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Preloads selected vanilla scenes and keeps reusable templates for coordinate-based spawns.
/// This is separate from the RPG drop system and is intended for trap placement.
/// </summary>
internal static class SpawnTemplateCatalog
{
    private const string CategoryTrap = "trap";

    private static readonly Dictionary<string, GameObject> trapTemplates = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
    private static readonly List<SpawnTemplateRequest> pendingRequests = new List<SpawnTemplateRequest>();
    private static readonly System.Reflection.FieldInfo woodWanaTrapField =
        typeof(WoodWana).GetField("trap", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
    private static readonly System.Reflection.FieldInfo trapButtonShotsField =
        typeof(Trap_button).GetField("trap", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
    private static readonly Type[] trapLikeComponentTypes =
    {
        typeof(WoodWana),
        typeof(TrapNormal),
        typeof(Ironmaiden_damage),
        typeof(BreakObjct),
        typeof(Inpactbreakkobj),
        typeof(Trap_button),
        typeof(Trap_button_ironmaiden),
        typeof(Trapshot),
        typeof(SpearThrowtrap),
        typeof(Magictrap),
        typeof(MagicTrapCreateObject),
        typeof(PictureTrap),
        typeof(IvyTrap),
        typeof(Cocoontrap),
        typeof(Cocooncreat),
        typeof(ImpactDamage),
        typeof(ImpactDamageBOX),
        typeof(WaveSpike),
        typeof(WaveSpikeGuard)
    };

    private static readonly Type[] hostageMobScriptTypes =
    {
        typeof(MobSlumSlave),
        typeof(MobCrawlingSlave),
        typeof(MobSpiderSlave),
        typeof(MobRosewarm),
        typeof(MobMutude),
        typeof(MobMachineSlave),
        typeof(witchslaveViolin),
        typeof(witchslaveslime),
        typeof(EvSlaveBigAxeAradia),
        typeof(EvSlaveBigAxeMOB),
        typeof(EvbunnyERO),
        typeof(EnemyMobCrowSlaveStandup),
        typeof(EnemyMobCrowSlaveBack)
    };

    private static readonly string[] hostageOrphanComponentNames =
    {
        "MeatShieldHelp",
        "ColCreateObj",
        "CowSlavespine",
        "ChainCowSlavespine",
        "NPCslaveEnable"
    };

    private static readonly Type[] trapAssemblyRootTypes =
    {
        typeof(WoodWana),
        typeof(Trap_button),
        typeof(Trap_button_ironmaiden),
        typeof(SpearThrowtrap),
        typeof(Magictrap),
        typeof(MagicTrapCreateObject),
        typeof(PictureTrap),
        typeof(IvyTrap),
        typeof(Cocoontrap),
        typeof(Trapshot)
    };

    private static readonly string[] trapKeyAliasNames =
    {
        "trapspider",
        "blackoozetypeb",
        "pictureero_non",
        "trap_rockinghorse",
        "trap_tentacleironmaiden",
        "trap_ironmaiden",
        "ironmaiden_damage",
        "trap_machine",
        "trap_hari",
        "trap",
        "ivytrap",
        "ivy_trap",
        "firecharge",
        "magic2",
        "impactdamagebox",
        "lightimpactnormal",
        "wana_start"
    };

    private static readonly string[] trapKeyAliasCanonical =
    {
        "trapthreadofspider",
        "blackoozetraptypeb",
        "pictureeronon",
        "trap_mokubaenemy",
        "tent_ironmaiden",
        "ironmaidendamage",
        "ironmaidendamage",
        "trapmachine",
        "trapnormal",
        "trapnormal",
        "ivy_trap",
        "ivy_trap",
        "magictrap",
        "magictrapcreateobject",
        "impactdamage",
        "impactdamage",
        "woodwana"
    };

    private static ConfigEntry<bool> dumpCatalogConfig;
    private static bool initialized;
    private static bool catalogDumpedOnce;
    private static string cachingSceneName = string.Empty;

    internal static readonly string[] HostageMobScriptTypeNames =
    {
        "MobSlumSlave",
        "MobCrawlingSlave",
        "MobSpiderSlave",
        "MobRosewarm",
        "MobMutude",
        "MobMachineSlave",
        "witchslaveViolin",
        "witchslaveslime",
        "EvSlaveBigAxeAradia",
        "EvSlaveBigAxeMOB",
        "EvbunnyERO",
        "EnemyMobCrowSlaveStandup",
        "EnemyMobCrowSlaveBack"
    };

    internal static readonly string[] TrapLikeComponentTypeNames =
    {
        "WoodWana",
        "TrapNormal",
        "Ironmaiden_damage",
        "BreakObjct",
        "Inpactbreakkobj",
        "Trap_button",
        "Trap_button_ironmaiden",
        "Trapshot",
        "SpearThrowtrap",
        "Magictrap",
        "MagicTrapCreateObject",
        "PictureTrap",
        "IvyTrap",
        "Cocoontrap",
        "Cocooncreat",
        "ImpactDamage",
        "ImpactDamageBOX",
        "WaveSpike",
        "WaveSpikeGuard"
    };

    public static bool IsReady => initialized;

    internal static int TemplateCount => trapTemplates.Count;

    internal static string NormalizeTemplateKey(string key) => NormalizeKey(key);

    internal static bool HasTemplate(string key)
    {
        return !string.IsNullOrEmpty(key) && trapTemplates.ContainsKey(NormalizeKey(key));
    }

    internal static bool TryGetTrapTemplate(string key, out GameObject template)
    {
        template = null;
        if (string.IsNullOrEmpty(key))
            return false;

        return trapTemplates.TryGetValue(NormalizeKey(key), out template) && template != null;
    }

    internal static bool TryRegisterCustomTrapTemplate(string key, GameObject template)
    {
        if (template == null || string.IsNullOrEmpty(key))
            return false;

        ForceCachePrefab(trapTemplates, key, template);
        MaybeDumpCatalog();
        return HasTemplate(key);
    }

    internal static Type ResolveMobScriptType(string typeName) => ResolveGameComponentType(typeName);

    internal static void CacheFromSceneForDiskRestore(Scene scene) => CacheFromScene(scene);

    internal static bool TryCacheFromResources(string key)
    {
        if (string.IsNullOrEmpty(key) || HasTemplate(key))
            return HasTemplate(key);

        TryCacheWhitelistedKeyFromResources(key);
        return HasTemplate(key);
    }

    public static void Initialize(Plugin plugin)
    {
        if (initialized)
            return;

        initialized = true;

        ConfigEntry<bool> legacyPreload = plugin.Config.Bind(
            "SpawnTemplates",
            "EnablePreloadScenes",
            false,
            "Deprecated. Additive scene preload is disabled — it caused rain/VFX leaks and hitches. Traps cache from visited scenes only.");

        plugin.Config.Bind(
            "SpawnTemplates",
            "PreloadScenes",
            string.Empty,
            "Deprecated — ignored. Leave empty.");

        dumpCatalogConfig = plugin.Config.Bind(
            "SpawnTemplates",
            "DumpAvailableCatalog",
            true,
            "Write cached trap template keys to HellGateSpawnPoint/AVAILABLE_SPAWN_TEMPLATES_RUNTIME.txt when the catalog grows.");

        SpawnTemplateDiskCache.BindConfig(plugin);
        EnemyPrefabDiskCache.BindConfig(plugin);
        SpawnTemplateWhitelist.BindConfig(plugin);

        if (legacyPreload.Value)
            Plugin.Log?.LogWarning("[SPAWN CATALOG] EnablePreloadScenes is ignored — additive preload removed. Traps cache on scene visit only.");

        SpawnTemplateDiskCache.LoadFromDisk();
        EnemyPrefabDiskCache.LoadFromDisk();
        SceneManager.sceneLoaded += OnSceneLoaded;
        CacheFromLoadedScenes();
        SpawnTemplateWhitelist.ReloadAndCache(plugin);
        RegisterTrapKeyAliases();
        TryRegisterLethalMagicTrapTemplate();
        TryRegisterLethalCocoonTrapTemplate();
        SpawnDecorCatalog.EnsureLoaded();
        MaybeDumpCatalog();
        SpawnTemplateDiskCache.ScheduleRestore(plugin);
        EnemyPrefabDiskCache.SchedulePreload(plugin);
    }

    public static bool TrySpawn(
        string category,
        string key,
        Vector2 position,
        int count,
        string logPrefix,
        string sceneName,
        bool flipX = false,
        float rotationZ = 0f,
        SpawnDepthSettings depth = default)
    {
        if (!initialized && Plugin.Instance != null)
            Initialize(Plugin.Instance);

        if (count <= 0)
            count = 1;

        if (!IsReady)
        {
            pendingRequests.Add(new SpawnTemplateRequest(
                category, key, position, count, logPrefix, sceneName,
                flipX, rotationZ, depth));
            Plugin.Log?.LogInfo($"{logPrefix} Queued template spawn (catalog not ready): {category}:{key}");
            return false;
        }

        return SpawnNow(category, key, position, count, logPrefix, flipX, rotationZ, depth);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsTitleScene(scene.name) || IsBootstrapScene(scene.name))
            return;

        int before = trapTemplates.Count;
        CacheFromScene(scene);
        SpawnTemplateWhitelist.ReloadAndCache(Plugin.Instance);
        RegisterTrapKeyAliases();
        if (trapTemplates.Count > before)
        {
            MaybeDumpCatalog(force: true);
            FlushPendingRequests();
        }
    }

    private static bool IsBootstrapScene(string sceneName)
    {
        return string.Equals(sceneName, "Common", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Do not additive-load level scenes while the title menu is active — it breaks Gametitle visuals.</summary>
    private static bool IsTitleScene(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) &&
               string.Equals(sceneName, "Gametitle", StringComparison.OrdinalIgnoreCase);
    }

    private static void CacheFromLoadedScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.IsValid() && scene.isLoaded && !IsTitleScene(scene.name) && !IsBootstrapScene(scene.name))
                CacheFromScene(scene);
        }
    }

    internal static void CacheSceneIfLoaded(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;

        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (scene.IsValid() && scene.isLoaded)
            CacheFromScene(scene);
    }

    private static void CacheFromScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || IsTitleScene(scene.name) || IsBootstrapScene(scene.name))
            return;

        try
        {
            cachingSceneName = scene.name;
            int before = trapTemplates.Count;
            CacheTrapTemplates(scene);
            CacheTrapLikeTemplates(scene);
            CacheGunTrapAssemblies(scene);
            CacheHostageTemplates(scene);
            CacheDecorTemplates(scene);
            EnemyPrefabRegistry.CacheBossesFromScene(scene);
            RegisterTrapKeyAliases();
            TryRegisterLethalMagicTrapTemplate();
            TryRegisterLethalCocoonTrapTemplate();
            if (trapTemplates.Count > before)
                MaybeDumpCatalog(force: true);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SPAWN CATALOG] Failed to cache scene {scene.name}: {ex.Message}");
        }
        finally
        {
            cachingSceneName = string.Empty;
        }
    }

    private static void MaybeDumpCatalog(bool force = false)
    {
        if (!force && catalogDumpedOnce)
            return;

        DumpAvailableCatalogIfEnabled();
        if (trapTemplates.Count > 0)
            catalogDumpedOnce = true;
    }

    private static bool IsExcludedTrapdataType(Type trapType)
    {
        if (trapType == null)
            return true;

        string name = trapType.Name;
        // Skip only types that broke additive preload (removed). Not a map restriction — any visited scene can cache traps.
        return string.Equals(name, "AngelStatue_Trap", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "GobTrap", StringComparison.OrdinalIgnoreCase);
    }

    private static void CacheTrapTemplates(Scene scene)
    {
        Trapdata[] traps = GetSceneComponents<Trapdata>(scene);
        for (int i = 0; i < traps.Length; i++)
        {
            Trapdata trap = traps[i];
            if (trap == null || trap.gameObject == null || IsCatalogTemplate(trap.gameObject))
                continue;
            if (IsExcludedTrapdataType(trap.GetType()))
                continue;

            CacheSceneTemplate(trapTemplates, trap.gameObject.name, trap.gameObject, "Trap");
            CacheSceneTemplate(trapTemplates, trap.GetType().Name, trap.gameObject, "Trap");

            string trapTypeAlias = GetTrapLikeAlias(trap.GetType());
            if (!string.IsNullOrEmpty(trapTypeAlias))
                CacheSceneTemplate(trapTemplates, trapTypeAlias, trap.gameObject, "Trap");
        }
    }

    /// <summary>Caches whole <see cref="SpawnSlave"/> roots (and orphan cow / meat-shield props) for HOSTAGE,Key,X,Y spawns.</summary>
    private static void CacheHostageTemplates(Scene scene)
    {
        try
        {
            SpawnSlave[] anchors = GetSceneComponents<SpawnSlave>(scene);
            for (int i = 0; i < anchors.Length; i++)
            {
                SpawnSlave anchor = anchors[i];
                if (anchor == null || anchor.gameObject == null || IsCatalogTemplate(anchor.gameObject))
                    continue;

                GameObject root = anchor.gameObject;
                CacheSceneTemplate(trapTemplates, root.name, root, "Hostage");

                for (int t = 0; t < hostageMobScriptTypes.Length; t++)
                {
                    Type mobType = hostageMobScriptTypes[t];
                    Component mob = root.GetComponentInChildren(mobType, true);
                    if (mob != null)
                        CacheSceneTemplate(trapTemplates, mobType.Name, root, "Hostage");
                }
            }

            for (int t = 0; t < hostageOrphanComponentNames.Length; t++)
            {
                Type componentType = ResolveGameComponentType(hostageOrphanComponentNames[t]);
                if (componentType == null)
                    continue;

                Component[] components = GetSceneComponents(scene, componentType);
                for (int j = 0; j < components.Length; j++)
                {
                    Component component = components[j];
                    if (component == null || component.gameObject == null || IsCatalogTemplate(component.gameObject))
                        continue;
                    if (component.GetComponentInParent<SpawnSlave>() != null)
                        continue;

                    CacheSceneTemplate(trapTemplates, componentType.Name, component.gameObject, "Hostage");
                    CacheSceneTemplate(trapTemplates, component.gameObject.name, component.gameObject, "Hostage");
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SPAWN CATALOG] Hostage cache failed for {scene.name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Scene decorations (corpses, static props) from SpawnDecorCatalog — plain GameObject roots without Trapdata/SpawnSlave.
    /// </summary>
    private static void CacheDecorTemplates(Scene scene)
    {
        try
        {
            SpawnDecorCatalog.EnsureLoaded();
            if (!SpawnDecorCatalog.HasEntries)
                return;

            HashSet<string> sceneKeys = SpawnDecorCatalog.GetNormalizedKeysForScene(scene.name);
            if (sceneKeys == null || sceneKeys.Count == 0)
                return;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null)
                    CacheDecorInHierarchy(roots[i].transform, sceneKeys);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SPAWN CATALOG] Decor cache failed for {scene.name}: {ex.Message}");
        }
    }

    private static void CacheDecorInHierarchy(Transform node, HashSet<string> sceneKeys)
    {
        if (node == null || sceneKeys == null || sceneKeys.Count == 0)
            return;

        GameObject go = node.gameObject;
        if (go != null && !IsCatalogTemplate(go))
        {
            string normalized = NormalizeKey(go.name);
            if (!string.IsNullOrEmpty(normalized) &&
                !SpawnDecorCatalog.IsBlocked(normalized) &&
                sceneKeys.Contains(normalized))
            {
                // Decor keys like InchurchSlave / scapegoat_Slave / VillageSlave often name a
                // parent GO while SpawnSlave lives on a child — cache the anchor, not the shell.
                SpawnSlave anchor = go.GetComponent<SpawnSlave>()
                    ?? go.GetComponentInChildren<SpawnSlave>(true);
                GameObject source = anchor != null ? anchor.gameObject : go;
                string prefix = anchor != null ? "Hostage" : "Object";
                CacheSceneTemplate(trapTemplates, go.name, source, prefix);
            }
        }

        for (int i = 0; i < node.childCount; i++)
            CacheDecorInHierarchy(node.GetChild(i), sceneKeys);
    }

    private static Type ResolveGameComponentType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return null;

        Type direct = Type.GetType(typeName);
        if (direct != null)
            return direct;

        Type mobSlum = typeof(MobSlumSlave);
        Assembly assembly = mobSlum.Assembly;
        return assembly.GetType(typeName, false, true);
    }

    private static void CacheTrapLikeTemplates(Scene scene)
    {
        for (int i = 0; i < trapLikeComponentTypes.Length; i++)
        {
            Type componentType = trapLikeComponentTypes[i];
            Component[] components = GetSceneComponents(scene, componentType);
            for (int j = 0; j < components.Length; j++)
            {
                Component component = components[j];
                if (component == null || component.gameObject == null || IsCatalogTemplate(component.gameObject))
                    continue;

                GameObject templateSource = GetTrapLikeTemplateSource(component, componentType);
                CacheSceneTemplate(trapTemplates, component.gameObject.name, templateSource, "Trap");
                CacheSceneTemplate(trapTemplates, componentType.Name, templateSource, "Trap");

                string alias = GetTrapLikeAlias(componentType);
                if (!string.IsNullOrEmpty(alias))
                    CacheSceneTemplate(trapTemplates, alias, templateSource, "Trap");
            }
        }
    }

    private static bool IsHostageCategory(string category)
    {
        if (string.IsNullOrEmpty(category))
            return false;

        string normalized = NormalizeKey(category);
        return normalized == "hostage" || normalized == "hostages" || normalized == "slave" ||
               normalized == "slaves" || normalized == "rescue";
    }

    private static bool SpawnNow(
        string category,
        string key,
        Vector2 position,
        int count,
        string logPrefix,
        bool flipX = false,
        float rotationZ = 0f,
        SpawnDepthSettings depth = default)
    {
        Dictionary<string, GameObject> templates = GetTemplates(category);
        if (templates == null)
        {
            Plugin.Log?.LogWarning($"{logPrefix} Unknown template category: {category}");
            return false;
        }

        if (!templates.TryGetValue(NormalizeKey(key), out GameObject template) || template == null)
        {
            Plugin.Log?.LogWarning($"{logPrefix} Template not found: {category}:{key}");
            return false;
        }

        for (int i = 0; i < count; i++)
        {
            Vector2 offset = CalculateOffset(i, count);
            Vector2 spawnPos = position + offset;
            float spawnZ = ResolveTemplateSpawnZ(template);
            SpawnDepthSettings spawnDepth = depth;
            SpawnDepthUtility.ApplyTrapHariFloorDefaults(key, rotationZ, ref spawnZ, ref spawnDepth);
            Vector3 worldPos = new Vector3(spawnPos.x, spawnPos.y, spawnZ);
            GameObject spawned = Object.Instantiate(template, worldPos, Quaternion.identity);
            if (spawned != null)
            {
                spawned.name = GetSpawnName(template.name);
                if (spawned.GetComponent<SpawnManagedInstance>() == null)
                    spawned.AddComponent<SpawnManagedInstance>();
                SpawnConfigExecutor.MoveSpawnedToGameplayScene(spawned);
                spawned.SetActive(true);
                RewireTrapButtons(spawned);

                if (IsHostageCategory(category) || spawned.GetComponentInChildren<SpawnSlave>(true) != null)
                    HellGateHostageRuntime.ConfigureSpawnedHostage(spawned, spawnPos);

                if (NoREroMod.Patches.HellTraps.LethalMagicTrapPaths.IsLethalMagicTrapKey(key))
                    NoREroMod.Patches.HellTraps.LethalMagicTrapRuntime.ConfigureSpawnedTrap(spawned);

                if (NoREroMod.Patches.HellTraps.LethalCocoonTrapPaths.IsLethalCocoonTrapKey(key))
                    NoREroMod.Patches.HellTraps.LethalCocoonTrapRuntime.ConfigureSpawnedTrap(spawned);

                if (Mathf.Abs(rotationZ) > 0.001f)
                {
                    if (NoREroMod.Patches.HellTraps.LethalMagicTrapPaths.IsLethalMagicTrapKey(key))
                    {
                        Plugin.Log?.LogWarning(
                            logPrefix
                            + " Rotation ignored for lethal_magictrap (use cfg scale/speed instead): "
                            + key);
                    }
                    else if (NoREroMod.Patches.HellTraps.LethalCocoonTrapPaths.IsLethalCocoonTrapKey(key))
                    {
                        Plugin.Log?.LogWarning(
                            logPrefix
                            + " Rotation ignored for lethal_cocoontrap (use cfg scale/acttime instead): "
                            + key);
                    }
                    else
                    {
                        SpawnRotationUtility.ApplyRotation(spawned, rotationZ);
                        if (SpawnSpikeKeys.IsSpikeLikeKey(key))
                        {
                            // Per-spawn verbose (disabled): rotation note on every spike trap spawn.
                            // Plugin.Log?.LogInfo(
                            //     logPrefix
                            //     + " Spike trap rotated "
                            //     + rotationZ.ToString("0.##")
                            //     + "°: "
                            //     + key);
                        }
                    }
                }

                if (flipX)
                    SpawnFlipUtility.LockHorizontalFlipLeft(spawned);

                SpawnDepthUtility.ApplyDepth(spawned, in spawnDepth);

                if (SpawnSpikeKeys.IsTrapHariKey(key) || spawnDepth.HasPositionOffset || spawnDepth.HasSortingOverride)
                {
                    // Per-spawn verbose (disabled): position/depth dump on every trap spawn & zone refresh.
                    // Vector3 finalPos = spawned.transform.position;
                    // Plugin.Log?.LogInfo(
                    //     logPrefix
                    //     + " Spawned "
                    //     + key
                    //     + " @ ("
                    //     + finalPos.x.ToString("F2")
                    //     + ","
                    //     + finalPos.y.ToString("F2")
                    //     + ","
                    //     + finalPos.z.ToString("F2")
                    //     + ") depth(y="
                    //     + spawnDepth.WorldYOffset.ToString("0.###")
                    //     + ", z="
                    //     + spawnDepth.WorldZOffset.ToString("0.###")
                    //     + ", sort="
                    //     + spawnDepth.SortingOrderOffset
                    //     + (spawnDepth.MatchPlayerSortingLayer ? ", layer=player" : string.Empty)
                    //     + (!string.IsNullOrEmpty(spawnDepth.SortingLayerName)
                    //         ? ", layer=" + spawnDepth.SortingLayerName
                    //         : string.Empty)
                    //     + ").");
                }
            }
        }

        return true;
    }

    internal static void FlushPendingRequestsAfterRestore() => FlushPendingRequests();

    private static void FlushPendingRequests()
    {
        if (pendingRequests.Count == 0)
            return;

        string currentScene = GetCurrentSceneName();
        List<SpawnTemplateRequest> requests = new List<SpawnTemplateRequest>(pendingRequests);
        pendingRequests.Clear();

        for (int i = 0; i < requests.Count; i++)
        {
            SpawnTemplateRequest request = requests[i];
            if (!string.IsNullOrEmpty(request.SceneName) &&
                !string.IsNullOrEmpty(currentScene) &&
                !string.Equals(request.SceneName, currentScene, StringComparison.OrdinalIgnoreCase))
            {
                Plugin.Log?.LogInfo($"{request.LogPrefix} Skipped queued template spawn after scene change: {request.Category}:{request.Key}");
                continue;
            }

            SpawnNow(
                request.Category,
                request.Key,
                request.Position,
                request.Count,
                request.LogPrefix,
                request.FlipX,
                request.RotationZ,
                request.Depth);
        }
    }

    private static void CacheDirectPrefab(Dictionary<string, GameObject> map, string key, GameObject prefab)
    {
        if (prefab == null)
            return;

        string normalized = NormalizeKey(key);
        if (string.IsNullOrEmpty(normalized) || map.ContainsKey(normalized))
            return;

        map[normalized] = prefab;
    }

    private static void CacheSceneTemplate(Dictionary<string, GameObject> map, string key, GameObject source, string prefix)
    {
        string normalized = NormalizeKey(key);
        if (string.IsNullOrEmpty(normalized) || source == null)
            return;

        if (map.TryGetValue(normalized, out GameObject existing) && existing != null)
        {
            bool existingHasSpawnSlave = existing.GetComponentInChildren<SpawnSlave>(true) != null;
            bool newHasSpawnSlave = source.GetComponent<SpawnSlave>() != null
                || source.GetComponentInChildren<SpawnSlave>(true) != null;
            if (existingHasSpawnSlave && !newHasSpawnSlave && !ShouldReplaceTrapTemplate(existing, source))
            {
                EnsureTemplateSpawnSettings(existing, source);
                return;
            }

            if (!ShouldReplaceTrapTemplate(existing, source))
            {
                EnsureTemplateSpawnSettings(existing, source);
                return;
            }

            Object.Destroy(existing);
            map.Remove(normalized);
        }

        GameObject template = Object.Instantiate(source);
        if (template == null)
            return;

        template.name = $"HellGate{prefix}Template_{CleanObjectName(source.name)}";
        template.SetActive(false);
        SpawnTemplateSpawnSettings spawnSettings = template.GetComponent<SpawnTemplateSpawnSettings>();
        if (spawnSettings == null)
            spawnSettings = template.AddComponent<SpawnTemplateSpawnSettings>();
        spawnSettings.SpawnWorldZ = source.transform.position.z;
        Object.DontDestroyOnLoad(template);
        map[normalized] = template;
        SpawnTemplateDiskCache.RecordTemplate(normalized, source, prefix, cachingSceneName, spawnSettings.SpawnWorldZ);
    }

    private static void EnsureTemplateSpawnSettings(GameObject template, GameObject source)
    {
        if (template == null || source == null)
            return;

        SpawnTemplateSpawnSettings settings = template.GetComponent<SpawnTemplateSpawnSettings>();
        if (settings == null)
            settings = template.AddComponent<SpawnTemplateSpawnSettings>();
        settings.SpawnWorldZ = source.transform.position.z;
    }

    private static float ResolveTemplateSpawnZ(GameObject template)
    {
        if (template == null)
            return 0f;

        SpawnTemplateSpawnSettings settings = template.GetComponent<SpawnTemplateSpawnSettings>();
        if (settings != null)
            return settings.SpawnWorldZ;

        return template.transform.position.z;
    }

    private static bool ShouldReplaceTrapTemplate(GameObject existingTemplate, GameObject newSource)
    {
        if (existingTemplate == null || newSource == null)
            return false;

        bool existingHasButton = existingTemplate.GetComponentInChildren<Trap_button>(true) != null;
        bool newHasButton = newSource.GetComponentInChildren<Trap_button>(true) != null;
        return !existingHasButton && newHasButton;
    }

    private static T[] GetSceneComponents<T>(Scene scene)
    {
        List<T> result = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == null || IsCatalogTemplate(roots[i]))
                continue;

            T[] components = roots[i].GetComponentsInChildren<T>(true);
            for (int j = 0; j < components.Length; j++)
                result.Add(components[j]);
        }
        return result.ToArray();
    }

    private static Component[] GetSceneComponents(Scene scene, Type componentType)
    {
        List<Component> result = new List<Component>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == null || IsCatalogTemplate(roots[i]))
                continue;

            Component[] components = roots[i].GetComponentsInChildren(componentType, true);
            for (int j = 0; j < components.Length; j++)
                result.Add(components[j]);
        }
        return result.ToArray();
    }

    private static Dictionary<string, GameObject> GetTemplates(string category)
    {
        string normalized = NormalizeKey(category);
        switch (normalized)
        {
            case CategoryTrap:
            case "traps":
            case "object":
            case "objects":
            case "sceneobject":
            case "prop":
            case "props":
            case "decor":
            case "decoration":
            case "decorations":
            case "sceneprop":
            case "sceneprops":
            case "hostage":
            case "hostages":
            case "slave":
            case "slaves":
            case "rescue":
                return trapTemplates;
            default:
                return null;
        }
    }

    private static void DumpAvailableCatalogIfEnabled()
    {
        if (dumpCatalogConfig != null && !dumpCatalogConfig.Value)
            return;

        try
        {
            string spawnDir = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "HellGateSpawnPoint");
            Directory.CreateDirectory(spawnDir);
            string path = Path.Combine(spawnDir, "AVAILABLE_SPAWN_TEMPLATES_RUNTIME.txt");

            using (StreamWriter writer = new StreamWriter(path, false))
            {
                writer.WriteLine("# HellGate cached spawn keys (auto-generated, overwritten on play)");
                writer.WriteLine("# Static full lists: SPAWN_TEMPLATE_KEYS.txt");
                writer.WriteLine("# Formats: TRAP,Key,... | OBJECT,Key,... | DECOR,Key,... | HOSTAGE,Key,...");
                writer.WriteLine("# Decor catalog: DECOR_CATALOG.txt");
                writer.WriteLine("# flip tokens: flip | mirror | -1 | left  (do not use hostage keys stand/back as flip tokens)");
                writer.WriteLine("# rotation: rot90 | rot180 | rot270 | 90 | 180 | 270 | upside (Z-axis, for spikes/traps)");
                writer.WriteLine("# Cached count: " + trapTemplates.Count);
                writer.WriteLine();
                WriteCatalogSection(writer, "CACHED_HOSTAGE", trapTemplates, IsHostageTemplateKey);
                WriteCatalogSection(
                    writer,
                    "CACHED_DECOR",
                    trapTemplates,
                    key => SpawnDecorCatalog.IsKnownDecorKey(key) && !IsHostageTemplateKey(key));
                WriteCatalogSection(
                    writer,
                    "CACHED_TRAP",
                    trapTemplates,
                    key => !IsHostageTemplateKey(key) && !SpawnDecorCatalog.IsKnownDecorKey(key));
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SPAWN CATALOG] Failed to write available catalog: {ex.Message}");
        }
    }

    private static readonly string[] KnownHostageScriptKeys =
    {
        "mobslumslave",
        "mobcrawlingslave",
        "mobspiderslave",
        "mobrosewarm",
        "mobmutude",
        "mobmachineslave",
        "witchslaveviolin",
        "witchslaveslime",
        "enemymobcrowslaveback",
        "enemymobcrowslavestandup",
        "slave",
        "stand"
    };

    internal static bool IsHostageTemplateKey(string normalizedKey)
    {
        if (string.IsNullOrEmpty(normalizedKey))
            return false;

        normalizedKey = NormalizeKey(normalizedKey);

        for (int i = 0; i < KnownHostageScriptKeys.Length; i++)
        {
            if (string.Equals(normalizedKey, KnownHostageScriptKeys[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (trapTemplates.TryGetValue(normalizedKey, out GameObject template) && template != null)
        {
            if (template.GetComponentInChildren<SpawnSlave>(true) != null)
                return true;
        }

        return false;
    }

    internal static string ResolveTemplateCategory(string key)
    {
        if (IsHostageTemplateKey(key))
            return "HOSTAGE";
        if (SpawnDecorCatalog.IsKnownDecorKey(key))
            return "Object";
        return "Trap";
    }

    private static void WriteCatalogSection(
        StreamWriter writer,
        string title,
        Dictionary<string, GameObject> map,
        Func<string, bool> includeKey)
    {
        writer.WriteLine("[" + title + "]");
        List<string> keys = new List<string>(map.Keys);
        keys.Sort(StringComparer.OrdinalIgnoreCase);
        int written = 0;
        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i];
            if (includeKey != null && !includeKey(key))
                continue;

            GameObject template = map[key];
            string sourceName = template != null ? GetSpawnName(template.name) : string.Empty;
            writer.WriteLine(key + (string.IsNullOrEmpty(sourceName) ? string.Empty : " -> " + sourceName));
            written++;
        }

        if (written == 0)
            writer.WriteLine("# (none yet — visit a map that has this content in vanilla)");
        writer.WriteLine();
    }

    private static Vector2 CalculateOffset(int index, int total)
    {
        if (total <= 1)
            return Vector2.zero;

        float angle = (360f / total) * index * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle) * 2f, Mathf.Sin(angle) * 1.4f);
    }

    private static string NormalizeKey(string key)
    {
        return CleanObjectName(key).Trim().ToLowerInvariant();
    }

    private static string CleanObjectName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        string cleaned = name.Replace("(Clone)", string.Empty);
        cleaned = Regex.Replace(cleaned, @"\(\d+\)", string.Empty);
        return cleaned.Trim();
    }

    private static string GetSpawnName(string templateName)
    {
        string cleaned = CleanObjectName(templateName);
        int marker = cleaned.IndexOf("Template_", StringComparison.Ordinal);
        if (cleaned.StartsWith("HellGate", StringComparison.Ordinal) && marker >= 0)
            return cleaned.Substring(marker + "Template_".Length).Trim();
        return cleaned;
    }

    private static string GetTrapLikeAlias(Type componentType)
    {
        if (componentType == typeof(Ironmaiden_damage))
            return "IronmaidenDamage";
        if (componentType == typeof(BlackOozeTrapTypeB))
            return "BlackOozeTrapTypeB";
        if (componentType == typeof(BlackOozetrap))
            return "BlackOozetrap";
        if (componentType == typeof(PictureEroNon))
            return "PictureEroNon";
        if (componentType == typeof(Trap_Rockinghorse))
            return "Trap_Rockinghorse";
        if (componentType == typeof(Trap_TentacleIronmaiden))
            return "Trap_TentacleIronmaiden";
        if (componentType == typeof(TrapSpider))
            return "TrapSpider";
        if (componentType == typeof(TrapMachine))
            return "TrapMachine";
        if (componentType == typeof(IvyTrap))
            return "ivy_trap";
        if (componentType == typeof(Magictrap))
            return "magictrap";
        if (componentType == typeof(MagicTrapCreateObject))
            return "magictrapcreateobject";
        if (componentType == typeof(ImpactDamage))
            return "ImpactDamage";
        if (componentType == typeof(ImpactDamageBOX))
            return "ImpactDamageBOX";
        return componentType.Name;
    }

    private static bool UsesTrapAssemblyRoot(Type componentType)
    {
        for (int i = 0; i < trapAssemblyRootTypes.Length; i++)
        {
            if (trapAssemblyRootTypes[i] == componentType)
                return true;
        }

        return false;
    }

    private static GameObject GetTrapLikeTemplateSource(Component component, Type componentType)
    {
        if (componentType == typeof(WoodWana))
        {
            GameObject linkedTrap = woodWanaTrapField != null ? woodWanaTrapField.GetValue(component) as GameObject : null;
            GameObject commonRoot = GetCommonAncestor(component.gameObject, linkedTrap);
            if (commonRoot != null)
                return commonRoot;
        }

        if (UsesTrapAssemblyRoot(componentType))
        {
            GameObject assemblyRoot = GetTrapAssemblyRoot(component);
            if (componentType == typeof(Trapshot))
                assemblyRoot = EnsureTrapAssemblyIncludesButton(assemblyRoot);
            if (assemblyRoot != null)
                return assemblyRoot;
        }

        if (componentType == typeof(trapDamage) && component.transform != null && component.transform.parent != null)
            return component.transform.parent.gameObject;
        return component.gameObject;
    }

    private static GameObject GetTrapAssemblyRoot(Component component)
    {
        if (component == null || component.transform == null)
            return null;

        Transform best = component.transform;
        int bestScore = ScoreTrapAssembly(best);
        Transform current = component.transform.parent;
        for (int depth = 0; depth < 8 && current != null; depth++)
        {
            int score = ScoreTrapAssembly(current);
            if (score > bestScore)
            {
                bestScore = score;
                best = current;
            }

            current = current.parent;
        }

        return best != null ? best.gameObject : null;
    }

    /// <summary>
    /// Gun traps on vanilla maps often split Trap_button and Trapshot under a shared parent.
    /// Include the button sibling so spawned trapgun* keys carry a working push plate.
    /// </summary>
    private static GameObject EnsureTrapAssemblyIncludesButton(GameObject assemblyRoot)
    {
        if (assemblyRoot == null)
            return null;

        if (assemblyRoot.GetComponentInChildren<Trap_button>(true) != null)
            return assemblyRoot;

        Transform parent = assemblyRoot.transform.parent;
        for (int depth = 0; depth < 3 && parent != null; depth++)
        {
            GameObject candidate = parent.gameObject;
            if (candidate.GetComponentInChildren<Trap_button>(true) != null &&
                candidate.GetComponentInChildren<Trapshot>(true) != null &&
                IsReasonableTrapAssembly(candidate))
            {
                return candidate;
            }

            parent = parent.parent;
        }

        return assemblyRoot;
    }

    private static bool IsReasonableTrapAssembly(GameObject root)
    {
        if (root == null)
            return false;

        return root.GetComponentsInChildren<Transform>(true).Length <= 32;
    }

    private static void RewireTrapButtons(GameObject root)
    {
        if (root == null || trapButtonShotsField == null)
            return;

        Trapshot[] shots = root.GetComponentsInChildren<Trapshot>(true);
        if (shots.Length == 0)
            return;

        Trap_button[] buttons = root.GetComponentsInChildren<Trap_button>(true);
        for (int i = 0; i < buttons.Length; i++)
            trapButtonShotsField.SetValue(buttons[i], shots);
    }

    private static int ScoreTrapAssembly(Transform node)
    {
        if (node == null)
            return 0;

        int score = 0;
        if (node.GetComponent<Trapdata>() != null)
            score += 12;
        if (node.GetComponent<Trapshot>() != null)
            score += 8;
        if (node.GetComponent<Trap_button>() != null ||
            node.GetComponent<Trap_button_ironmaiden>() != null ||
            node.GetComponent<Trap_button_versatility>() != null)
            score += 6;
        if (node.GetComponent<TrapNormal>() != null)
            score += 4;
        if (node.GetComponent<WaveSpike>() != null ||
            node.GetComponent<WaveSpikeGuard>() != null)
            score += 4;
        if (node.GetComponent<SpearThrowtrap>() != null)
            score += 4;
        if (node.GetComponent<WoodWana>() != null)
            score += 4;
        if (node.GetComponentInChildren<Trapshot>(true) != null)
            score += 3;
        if (node.GetComponentInChildren<Trapdata>(true) != null)
            score += 2;
        return score;
    }

    private static void RegisterTrapKeyAliases()
    {
        int count = trapKeyAliasNames.Length;
        if (trapKeyAliasCanonical.Length != count)
            return;

        for (int i = 0; i < count; i++)
        {
            string alias = trapKeyAliasNames[i];
            string canonical = trapKeyAliasCanonical[i];
            if (!trapTemplates.TryGetValue(NormalizeKey(canonical), out GameObject template) || template == null)
                continue;

            CacheDirectPrefab(trapTemplates, alias, template);
        }

        RegisterGunTrapAliases();
    }

    /// <summary>trapshot works because it caches the Trap_button assembly; trapgun2 often cached as gun-only — unify.</summary>
    private static void RegisterGunTrapAliases()
    {
        if (!TryGetGunTrapTemplate(out GameObject template))
            return;

        string[] gunKeys =
        {
            "trapgun2",
            "trapgun3",
            "trapgun4",
            "trapgun5",
            "trapgun6",
            "trap_gun"
        };

        for (int i = 0; i < gunKeys.Length; i++)
            ForceCachePrefab(trapTemplates, gunKeys[i], template);
    }

    private static bool TryGetGunTrapTemplate(out GameObject template)
    {
        template = null;
        if (trapTemplates.TryGetValue(NormalizeKey("trapshot"), out template) && template != null)
            return true;
        if (trapTemplates.TryGetValue(NormalizeKey("trap_button"), out template) && template != null)
            return true;
        return false;
    }

    private static void CacheGunTrapAssemblies(Scene scene)
    {
        Trap_button[] buttons = GetSceneComponents<Trap_button>(scene);
        for (int i = 0; i < buttons.Length; i++)
        {
            Trap_button button = buttons[i];
            if (button == null || button.gameObject == null || IsCatalogTemplate(button.gameObject))
                continue;

            GameObject assembly = EnsureTrapAssemblyIncludesButton(button.gameObject);
            if (!IsReasonableTrapAssembly(assembly))
                continue;

            CacheSceneTemplate(trapTemplates, "trapshot", assembly, "Trap");
            CacheSceneTemplate(trapTemplates, "trap_button", assembly, "Trap");
            CacheSceneTemplate(trapTemplates, button.gameObject.name, assembly, "Trap");

            Trapshot[] shots = assembly.GetComponentsInChildren<Trapshot>(true);
            for (int s = 0; s < shots.Length; s++)
            {
                if (shots[s] == null || shots[s].gameObject == null)
                    continue;
                CacheSceneTemplate(trapTemplates, shots[s].gameObject.name, assembly, "Trap");
            }
        }
    }

    internal static void RefreshAliasesAndDump()
    {
        RegisterTrapKeyAliases();
        TryRegisterLethalMagicTrapTemplate();
        TryRegisterLethalCocoonTrapTemplate();
        MaybeDumpCatalog(force: true);
    }

    private static void TryRegisterLethalMagicTrapTemplate()
    {
        try
        {
            NoREroMod.Patches.HellTraps.LethalMagicTrapRuntime.TryEnsureTemplateRegistered();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[SPAWN CATALOG] Lethal magic trap template registration failed: " + ex.Message);
        }
    }

    private static void TryRegisterLethalCocoonTrapTemplate()
    {
        try
        {
            NoREroMod.Patches.HellTraps.LethalCocoonTrapRuntime.TryEnsureTemplateRegistered();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[SPAWN CATALOG] Lethal cocoon trap template registration failed: " + ex.Message);
        }
    }

    internal static void CacheWhitelistedKeysFromResources(IList<SpawnTemplateWhitelist.Entry> whitelist)
    {
        if (whitelist == null || whitelist.Count == 0)
            return;

        int before = trapTemplates.Count;
        for (int i = 0; i < whitelist.Count; i++)
        {
            string key = whitelist[i].Key;
            if (string.IsNullOrEmpty(key))
                continue;

            if (trapTemplates.ContainsKey(NormalizeKey(key)))
                continue;

            TryCacheWhitelistedKeyFromResources(key);
        }

        if (trapTemplates.Count > before)
            RegisterTrapKeyAliases();
    }

    private static void TryCacheWhitelistedKeyFromResources(string key)
    {
        string normalized = NormalizeKey(key);
        if (string.IsNullOrEmpty(normalized))
            return;

        for (int i = 0; i < hostageMobScriptTypes.Length; i++)
        {
            Type mobType = hostageMobScriptTypes[i];
            if (!KeyMatchesType(normalized, mobType, null))
                continue;

            if (TryCacheFirstComponent(mobType, mobType.Name, key, hostage: true))
                return;
        }

        for (int i = 0; i < trapLikeComponentTypes.Length; i++)
        {
            Type componentType = trapLikeComponentTypes[i];
            if (!KeyMatchesType(normalized, componentType, GetTrapLikeAlias(componentType)))
                continue;

            if (TryCacheFirstComponent(componentType, componentType.Name, key, hostage: false))
                return;
        }

        Trapdata[] trapDataSamples = Resources.FindObjectsOfTypeAll<Trapdata>();
        for (int i = 0; i < trapDataSamples.Length; i++)
        {
            Trapdata trap = trapDataSamples[i];
            if (trap == null || trap.gameObject == null || IsCatalogTemplate(trap.gameObject))
                continue;
            if (IsExcludedTrapdataType(trap.GetType()))
                continue;

            if (!KeyMatchesType(normalized, trap.GetType(), GetTrapLikeAlias(trap.GetType())) &&
                !string.Equals(NormalizeKey(trap.gameObject.name), normalized, StringComparison.Ordinal))
                continue;

            CacheSceneTemplate(trapTemplates, key, trap.gameObject, "Trap");
            return;
        }

        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject obj = objects[i];
            if (obj == null || IsCatalogTemplate(obj))
                continue;
            if (!string.Equals(NormalizeKey(obj.name), normalized, StringComparison.Ordinal))
                continue;

            string prefix = SpawnDecorCatalog.IsKnownDecorKey(key) ? "Object" : "Trap";
            CacheSceneTemplate(trapTemplates, key, obj, prefix);
            return;
        }
    }

    private static bool KeyMatchesType(string normalizedKey, Type type, string alias)
    {
        if (type == null)
            return false;

        if (string.Equals(NormalizeKey(type.Name), normalizedKey, StringComparison.Ordinal))
            return true;

        return !string.IsNullOrEmpty(alias) &&
               string.Equals(NormalizeKey(alias), normalizedKey, StringComparison.Ordinal);
    }

    private static bool TryCacheFirstComponent(Type componentType, string typeName, string requestedKey, bool hostage)
    {
        cachingSceneName = string.Empty;
        try
        {
            UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(componentType);
            for (int i = 0; i < objects.Length; i++)
            {
                Component component = objects[i] as Component;
                if (component == null || component.gameObject == null || IsCatalogTemplate(component.gameObject))
                    continue;

                if (hostage)
                {
                    GameObject root = component.GetComponentInParent<SpawnSlave>()?.gameObject ?? component.gameObject;
                    CacheSceneTemplate(trapTemplates, requestedKey, root, "Hostage");
                    CacheSceneTemplate(trapTemplates, typeName, root, "Hostage");
                    return true;
                }

                GameObject source = GetTrapLikeTemplateSource(component, componentType);
                if (componentType == typeof(Trapshot))
                    source = EnsureTrapAssemblyIncludesButton(source);

                CacheSceneTemplate(trapTemplates, requestedKey, source, "Trap");
                CacheSceneTemplate(trapTemplates, typeName, source, "Trap");
                string alias = GetTrapLikeAlias(componentType);
                if (!string.IsNullOrEmpty(alias))
                    CacheSceneTemplate(trapTemplates, alias, source, "Trap");
                return true;
            }

            return false;
        }
        finally
        {
            cachingSceneName = string.Empty;
        }
    }

    private static void ForceCachePrefab(Dictionary<string, GameObject> map, string key, GameObject prefab)
    {
        if (prefab == null)
            return;

        string normalized = NormalizeKey(key);
        if (string.IsNullOrEmpty(normalized))
            return;

        if (map.TryGetValue(normalized, out GameObject existing) && existing != null && existing != prefab)
            Object.Destroy(existing);

        map[normalized] = prefab;
    }

    private static GameObject GetCommonAncestor(GameObject first, GameObject second)
    {
        if (first == null || second == null)
            return null;

        Transform firstTransform = first.transform;
        while (firstTransform != null)
        {
            Transform secondTransform = second.transform;
            while (secondTransform != null)
            {
                if (firstTransform == secondTransform)
                    return firstTransform.gameObject;
                secondTransform = secondTransform.parent;
            }

            firstTransform = firstTransform.parent;
        }

        return null;
    }

    private static bool IsCatalogTemplate(GameObject obj)
    {
        return obj != null && obj.name.StartsWith("HellGate", StringComparison.Ordinal) && obj.name.Contains("Template_");
    }

    private static string GetCurrentSceneName()
    {
        try
        {
            var fragMng = NoREroMod.Systems.Cache.UnifiedGameControllerCacheManager.GetGameFragMng();
            if (fragMng != null && !string.IsNullOrEmpty(fragMng._re_Scenename))
                return fragMng._re_Scenename;
        }
        catch
        {
        }

        try
        {
            return SceneManager.GetActiveScene().name;
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed class SpawnTemplateRequest
    {
        public readonly string Category;
        public readonly string Key;
        public readonly Vector2 Position;
        public readonly int Count;
        public readonly string LogPrefix;
        public readonly string SceneName;

        public readonly bool FlipX;
        public readonly float RotationZ;
        public readonly SpawnDepthSettings Depth;

        public SpawnTemplateRequest(
            string category,
            string key,
            Vector2 position,
            int count,
            string logPrefix,
            string sceneName,
            bool flipX,
            float rotationZ,
            SpawnDepthSettings depth)
        {
            Category = category;
            Key = key;
            Position = position;
            Count = count;
            LogPrefix = logPrefix;
            SceneName = sceneName;
            FlipX = flipX;
            RotationZ = rotationZ;
            Depth = depth;
        }
    }
}
