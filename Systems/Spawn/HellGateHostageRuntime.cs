using System;
using System.Reflection;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// HellGate-placed hostages: unique save slot + force not-rescued so they respawn every zone visit.
/// </summary>
internal static class HellGateHostageRuntime
{
    private const int ReservedStageMin = 10;
    private const int ReservedStageMax = 19;
    private const int SlavesPerStage = 5;

    public static void ConfigureSpawnedHostage(GameObject root, Vector2 spawnPosition)
    {
        if (root == null)
            return;

        try
        {
            // Decor-cached templates (InchurchSlave, etc.) often have SpawnSlave on a child,
            // not on the instantiated root — without this, rescued vanilla save slots stick after hot reload.
            SpawnSlave anchor = root.GetComponent<SpawnSlave>()
                ?? root.GetComponentInChildren<SpawnSlave>(true);
            if (anchor != null)
            {
                int stage;
                int slaveNumber;
                AllocateSaveSlot(spawnPosition, out stage, out slaveNumber);
                SetSpawnSlaveSlot(anchor, stage, slaveNumber);
                ClearRescuedFlag(stage, slaveNumber);
                EnsureSlaveChildActive(anchor);
            }
            else
            {
                Plugin.Log?.LogWarning($"[HOSTAGE] No SpawnSlave on {root.name} — cannot reset rescue slot.");
            }

            ResetMobSlaveState(root);
            EnsureHellGateMarker(root);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[HOSTAGE] Configure failed on {root.name}: {ex.Message}");
        }
    }

    /// <summary>
    /// HellGate-owned hostage slots (stage 10–19). Cleared before spawn hot-reload so rescued
    /// hostages can reappear even if a template failed to rebind SpawnSlave.
    /// </summary>
    internal static void ClearReservedSaveSlots()
    {
        try
        {
            var fragMng = NoREroMod.Systems.Cache.UnifiedGameControllerCacheManager.GetGameFragMng();
            if (fragMng?._helpslaveStage == null)
                return;

            int maxStage = fragMng._helpslaveStage.GetLength(0);
            int maxSlave = fragMng._helpslaveStage.GetLength(1);
            for (int stage = ReservedStageMin; stage <= ReservedStageMax && stage < maxStage; stage++)
            {
                for (int slave = 0; slave < SlavesPerStage && slave < maxSlave; slave++)
                    fragMng._helpslaveStage[stage, slave] = false;
            }
        }
        catch
        {
            // ignore
        }
    }

    private static void AllocateSaveSlot(Vector2 position, out int stage, out int slaveNumber)
    {
        string scene = HellGateLocationSpawnRefresh.GetReSceneName();
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + scene.GetHashCode();
            hash = hash * 31 + Mathf.RoundToInt(position.x * 10f);
            hash = hash * 31 + Mathf.RoundToInt(position.y * 10f);
            int idx = Math.Abs(hash) % ((ReservedStageMax - ReservedStageMin + 1) * SlavesPerStage);
            stage = ReservedStageMin + (idx / SlavesPerStage);
            slaveNumber = idx % SlavesPerStage;
        }
    }

    private static void SetSpawnSlaveSlot(SpawnSlave anchor, int stage, int slaveNumber)
    {
        FieldInfo stageField = typeof(SpawnSlave).GetField("stage", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo numberField = typeof(SpawnSlave).GetField("SlaveNumber", BindingFlags.Instance | BindingFlags.NonPublic);
        if (stageField != null)
            stageField.SetValue(anchor, stage);
        if (numberField != null)
            numberField.SetValue(anchor, slaveNumber);
    }

    private static void ClearRescuedFlag(int stage, int slaveNumber)
    {
        try
        {
            var fragMng = NoREroMod.Systems.Cache.UnifiedGameControllerCacheManager.GetGameFragMng();
            if (fragMng == null || fragMng._helpslaveStage == null)
                return;

            if (stage >= 0 && stage < fragMng._helpslaveStage.GetLength(0) &&
                slaveNumber >= 0 && slaveNumber < fragMng._helpslaveStage.GetLength(1))
            {
                fragMng._helpslaveStage[stage, slaveNumber] = false;
            }
        }
        catch
        {
            // ignore
        }
    }

    private static void EnsureSlaveChildActive(SpawnSlave anchor)
    {
        FieldInfo slaveField = typeof(SpawnSlave).GetField("slave", BindingFlags.Instance | BindingFlags.NonPublic);
        GameObject slaveChild = slaveField?.GetValue(anchor) as GameObject;
        if (slaveChild == null)
            return;

        if (!slaveChild.activeSelf)
            slaveChild.SetActive(true);

        SkeletonAnimation spine = slaveChild.GetComponent<SkeletonAnimation>();
        if (spine != null && spine.state != null)
            spine.timeScale = 1f;
    }

    private static void EnsureHellGateMarker(GameObject root)
    {
        if (root == null)
            return;

        if (root.GetComponent<HellGateSpawnedHostageMarker>() == null)
            root.AddComponent<HellGateSpawnedHostageMarker>();
    }

    private static void ResetMobSlaveState(GameObject root)
    {
        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;

            Type type = behaviour.GetType();
            string name = type.Name;
            if (!name.StartsWith("Mob", StringComparison.Ordinal) &&
                !name.StartsWith("witchslave", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ResetBoolField(type, behaviour, "helpflag", false);
            ResetBoolField(type, behaviour, "Stayflag", false);
            ResetBoolField(type, behaviour, "flag", false);
        }

        Slavehelp[] helpers = root.GetComponentsInChildren<Slavehelp>(true);
        for (int i = 0; i < helpers.Length; i++)
        {
            if (helpers[i] == null)
                continue;
            ResetBoolField(typeof(Slavehelp), helpers[i], "flag", false);
        }
    }

    private static void ResetBoolField(Type type, object target, string fieldName, bool value)
    {
        FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field != null && field.FieldType == typeof(bool))
            field.SetValue(target, value);
    }
}
