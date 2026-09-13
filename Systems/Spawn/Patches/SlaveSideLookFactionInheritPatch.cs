using HarmonyLib;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.CombatAi.Factions.Patches;
using UnityEngine;

namespace NoREroMod.Systems.Spawn.Patches;

/// <summary>
/// Attack/rescue Instantiates <c>CreateMonster</c> as a new root — vanilla never copies
/// HellGate <see cref="SpawnFactionOverride"/>. Inherit from the hostage / look prop hierarchy.
/// Covers: MobSlumSlave, MobCrawlingSlave, MobMutude, witchslaveViolin (via SlaveSideLook),
/// gob_look / Look_Dorei (ColCreateObj → SlaveSideLook),
/// EnemyMobCrowSlaveBack / EnemyMobCrowSlaveStandup (own createinvoke).
/// </summary>
internal static class HostageCreateMonsterFactionInherit
{
    internal static string FindFactionOverrideRaw(Transform start)
    {
        Transform current = start;
        while (current != null)
        {
            SpawnFactionOverride ov = current.GetComponent<SpawnFactionOverride>();
            if (ov != null && !string.IsNullOrEmpty(ov.FactionIdRaw))
                return ov.FactionIdRaw;
            current = current.parent;
        }

        return null;
    }

    internal static void ApplyInheritedFaction(GameObject spawned, string factionIdRaw)
    {
        if (spawned == null || string.IsNullOrEmpty(factionIdRaw))
            return;

        SpawnFactionOverride ov = spawned.GetComponent<SpawnFactionOverride>();
        if (ov == null)
            ov = spawned.AddComponent<SpawnFactionOverride>();
        ov.FactionIdRaw = factionIdRaw;

        if (!EnemyFactionsConfig.Enable)
            return;

        EnemyDate[] enemies = spawned.GetComponentsInChildren<EnemyDate>(true);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyDate enemy = enemies[i];
            if (enemy == null)
                continue;
            EnemyFactionRuntime.RegisterEnemy(enemy);
            EnemyDateFactionColorBootstrapPatch.ApplyFactionMarker(enemy);
        }
    }
}

[HarmonyPatch(typeof(SlaveSideLook), "createinvoke")]
internal static class SlaveSideLookFactionInheritPatch
{
    [HarmonyPrefix]
    private static bool Prefix(SlaveSideLook __instance)
    {
        if (__instance == null || __instance.gameObject == null)
            return true;

        GameObject prefab = Traverse.Create(__instance).Field("CreateMonster").GetValue<GameObject>();
        if (prefab == null)
            return true;

        string factionIdRaw = HostageCreateMonsterFactionInherit.FindFactionOverrideRaw(__instance.transform);
        GameObject spawned = Object.Instantiate(prefab, __instance.transform.position, __instance.transform.rotation);
        HostageCreateMonsterFactionInherit.ApplyInheritedFaction(spawned, factionIdRaw);

        Object.Destroy(__instance.gameObject);
        return false;
    }
}

[HarmonyPatch(typeof(EnemyMobCrowSlaveBack), "createinvoke")]
internal static class EnemyMobCrowSlaveBackFactionInheritPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyMobCrowSlaveBack __instance)
    {
        if (__instance == null || __instance.gameObject == null)
            return true;

        GameObject prefab = Traverse.Create(__instance).Field("CreateMonster").GetValue<GameObject>();
        if (prefab == null)
            return true;

        Vector2 pos = new Vector2(__instance.transform.position.x, __instance.transform.position.y - 0.1f);
        string factionIdRaw = HostageCreateMonsterFactionInherit.FindFactionOverrideRaw(__instance.transform);
        GameObject spawned = Object.Instantiate(prefab, pos, __instance.transform.rotation);
        if (spawned != null)
        {
            EnemydeathCheck deathCheck = spawned.GetComponent<EnemydeathCheck>();
            if (deathCheck != null)
                deathCheck._flag = true;
            HostageCreateMonsterFactionInherit.ApplyInheritedFaction(spawned, factionIdRaw);
        }

        return false;
    }
}

[HarmonyPatch(typeof(EnemyMobCrowSlaveStandup), "createinvoke")]
internal static class EnemyMobCrowSlaveStandupFactionInheritPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyMobCrowSlaveStandup __instance)
    {
        if (__instance == null || __instance.gameObject == null)
            return true;

        GameObject prefab = Traverse.Create(__instance).Field("CreateMonster").GetValue<GameObject>();
        if (prefab == null)
            return true;

        Vector2 pos = new Vector2(__instance.transform.position.x - 0.5f, __instance.transform.position.y + 1f);
        string factionIdRaw = HostageCreateMonsterFactionInherit.FindFactionOverrideRaw(__instance.transform);
        GameObject spawned = Object.Instantiate(prefab, pos, __instance.transform.rotation);
        if (spawned != null)
        {
            EnemydeathCheck deathCheck = spawned.GetComponent<EnemydeathCheck>();
            if (deathCheck != null)
                deathCheck._flag = true;
            HostageCreateMonsterFactionInherit.ApplyInheritedFaction(spawned, factionIdRaw);
        }

        return false;
    }
}
