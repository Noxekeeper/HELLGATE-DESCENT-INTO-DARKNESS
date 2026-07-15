using System;
using System.Collections;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.Pregnancy.OffspringArchetype;
using NoREroMod.Systems.Spawn;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy.Patches;

/// <summary>
/// Respawns Aradia's children in the ParishChurch hideout whenever that scene is loaded.
/// Children are read from the per-slot JSON store and are instantiated manually (not via
/// SpawnManagedInstance, so they are not destroyed by zone refresh).
/// </summary>
internal static class OffspringHideoutSpawner
{
    public static void SpawnAllInHideout()
    {
        if (!HideoutSceneUtility.IsParishHideoutActive())
            return;

        if (NoREroMod.Systems.Pregnancy.ShelterAttack.ShelterAttackSceneGuard.IsCombatSpawnActive())
            return;

        SyncSpawnFlagsWithScene();

        var children = PregnancySlotStore.GetAliveChildrenInHideout();
        int spawned = 0;
        foreach (var child in children)
        {
            if (child.IsSpawned)
                continue;
            if (SpawnChild(child))
                spawned++;
        }

        if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
            Plugin.Log?.LogInfo($"[Pregnancy.Hideout] Spawned {spawned} children in ParishChurch (alive in hideout: {children.Count})");
    }

    internal static void RequestDeferredSpawn()
    {
        if (Plugin.Instance == null)
        {
            SpawnAllInHideout();
            return;
        }

        Plugin.Instance.StartCoroutine(SpawnAllInHideoutDeferred());
    }

    private static IEnumerator SpawnAllInHideoutDeferred()
    {
        float deadline = Time.unscaledTime + 15f;

        // OnSceneLoaded can run before zone refresh starts — wait for the refresh cycle to begin.
        for (int i = 0; i < 30 && !HellGateLocationSpawnRefresh.IsZoneRefreshInFlight && Time.unscaledTime < deadline; i++)
            yield return null;

        while (HellGateLocationSpawnRefresh.IsZoneRefreshInFlight && Time.unscaledTime < deadline)
            yield return null;

        // fun_ALLreset / RefreshAfterAltar can run on the same tail frames as zone refresh.
        yield return null;
        yield return null;

        if (!HideoutSceneUtility.IsParishHideoutActive())
            yield break;

        SpawnAllInHideout();
    }

    private static void SyncSpawnFlagsWithScene()
    {
        var children = PregnancySlotStore.GetAliveChildrenInHideout();
        for (int i = 0; i < children.Count; i++)
        {
            ChildData child = children[i];
            if (!child.IsSpawned)
                continue;

            if (!IsHideoutVisualAlive(child.Guid))
                child.IsSpawned = false;
        }
    }

    private static bool IsHideoutVisualAlive(string guid)
    {
        if (string.IsNullOrEmpty(guid))
            return false;

        WitchOffspringController[] controllers = UnityEngine.Object.FindObjectsOfType<WitchOffspringController>();
        for (int i = 0; i < controllers.Length; i++)
        {
            WitchOffspringController controller = controllers[i];
            if (controller == null || !controller.IsHideoutResident)
                continue;

            if (controller.ChildGuid == guid)
                return true;
        }

        return false;
    }

    private static bool SpawnChild(ChildData child)
    {
        try
        {
            if (!OffspringPrefabResolver.TryResolvePrefab(child, out GameObject prefab, out string archetypeKey) || prefab == null)
            {
                Plugin.Log?.LogError($"[Pregnancy.Hideout] Failed to resolve offspring prefab for child {child.Guid}");
                return false;
            }

            Vector2 nodePos = HideoutSceneUtility.GetNodePosition(child.HideoutNodeIndex);
            Vector3 pos = new Vector3(nodePos.x, nodePos.y, 0f);
            GameObject childObj = (GameObject)UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
            // Must not use the Enemy tag: vanilla altar (fun_ALLreset) and zone cleanup destroy every Enemy.
            childObj.tag = "Untagged";
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
                childObj.layer = enemyLayer;
            childObj.name = OffspringPrefabResolver.BuildObjectName(archetypeKey);

            float scale = child.GetScaleForGrowthStage();

            var controller = childObj.AddComponent<WitchOffspringController>();
            controller.Initialize(child.FactionSource, scale);
            controller.ChildGuid = child.Guid;
            controller.IsHideoutResident = true;

            WitchOffspringVisuals.ConfigureSpawnedOffspring(childObj, scale, hideoutCompanion: true);

            var enemyDate = childObj.GetComponent<EnemyDate>();
            if (enemyDate != null)
            {
                try { EnemyFactionRuntime.RegisterEnemy(enemyDate); } catch { }
                try { EnemyFactionRuntime.SetFaction(childObj, FactionIds.Witch); } catch { }
            }

            WitchOffspringVisuals.AddFactionEmblem(childObj, child.FactionSource);

            OffspringHideoutHealth.ApplyStoredHealth(child, enemyDate);

            child.IsSpawned = true;

            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                Plugin.Log?.LogInfo($"[Pregnancy.Hideout] Spawned child {child.Guid} archetype={archetypeKey} at node {child.HideoutNodeIndex} (scale={scale:F2})");

            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[Pregnancy.Hideout] Failed to spawn child {child.Guid}: {ex.Message}");
            return false;
        }
    }

    internal static void ApplyGrowthStageToSpawnedChild(ChildData child)
    {
        if (child == null || string.IsNullOrEmpty(child.Guid))
            return;

        float scale = child.GetScaleForGrowthStage();
        WitchOffspringController[] controllers = UnityEngine.Object.FindObjectsOfType<WitchOffspringController>();
        for (int i = 0; i < controllers.Length; i++)
        {
            WitchOffspringController controller = controllers[i];
            if (controller == null || !controller.IsHideoutResident || controller.ChildGuid != child.Guid)
                continue;

            controller.SetBirthScale(scale);

            WitchOffspringSpawnSetup setup = controller.GetComponent<WitchOffspringSpawnSetup>();
            if (setup != null)
                setup.UniformScale = scale;

            EnemyDate enemyDate = controller.GetComponent<EnemyDate>();
            if (enemyDate != null)
            {
                WitchOffspringVisuals.ApplyUniformOffspringScale(enemyDate, scale);
                WitchOffspringVisuals.SnapFeetToGround(controller.gameObject);
            }
        }
    }
}

/// <summary>HP persistence and hideout healing (altar / hot reload).</summary>
internal static class OffspringHideoutHealth
{
    internal static void ApplyStoredHealth(ChildData child, EnemyDate enemyDate)
    {
        if (child == null || enemyDate == null)
            return;

        if (child.CurrentHp <= 0f || child.CurrentHp > enemyDate.MaxHp)
            enemyDate.Hp = enemyDate.MaxHp;
        else
            enemyDate.Hp = child.CurrentHp;

        RestoreToughness(enemyDate);
    }

    internal static void RestoreAllHideoutResidentsToFull()
    {
        if (!HideoutSceneUtility.IsParishHideoutActive())
            return;

        foreach (ChildData child in PregnancySlotStore.GetAliveChildrenInHideout())
            child.CurrentHp = -1f;

        WitchOffspringController[] controllers = UnityEngine.Object.FindObjectsOfType<WitchOffspringController>();
        for (int i = 0; i < controllers.Length; i++)
        {
            WitchOffspringController controller = controllers[i];
            if (controller == null || !controller.IsHideoutResident)
                continue;

            EnemyDate enemyDate = controller.GetComponent<EnemyDate>();
            if (enemyDate == null)
                continue;

            enemyDate.Hp = enemyDate.MaxHp;
            RestoreToughness(enemyDate);
        }

        if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
            Plugin.Log?.LogInfo("[Pregnancy.Hideout] Restored hideout offspring HP to full");
    }

    internal static void SyncHideoutHealthToStore()
    {
        WitchOffspringController[] controllers = UnityEngine.Object.FindObjectsOfType<WitchOffspringController>();
        for (int i = 0; i < controllers.Length; i++)
            SyncControllerToStore(controllers[i]);
    }

    internal static void SyncControllerToStore(WitchOffspringController controller)
    {
        if (controller == null || !controller.IsHideoutResident || string.IsNullOrEmpty(controller.ChildGuid))
            return;

        EnemyDate enemyDate = controller.GetComponent<EnemyDate>();
        if (enemyDate == null)
            return;

        foreach (ChildData child in PregnancySlotStore.GetAllChildren())
        {
            if (child.Guid != controller.ChildGuid)
                continue;

            child.CurrentHp = enemyDate.Hp <= 0f ? 0f : enemyDate.Hp;
            PregnancySlotStore.MarkDirty();
            return;
        }
    }

    private static void RestoreToughness(EnemyDate enemyDate)
    {
        if (enemyDate == null)
            return;

        try { enemyDate.enmTough = enemyDate.enmMAXtough; } catch { }
    }
}
