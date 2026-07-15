using HarmonyLib;
using NoREroMod.Systems.CombatAi.Factions;
using Spine;
using System;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy.Patches;

/// <summary>
/// Birth spawn coordinator for the extended pregnancy system.
///
/// On the CREATE event we record the child data, replace the vanilla birth prefab with the
/// small slime prefab so the baby appears immediately, and set a marker for the capture patch.
/// A separate patch on <c>suraimu.Start</c> catches the actual slime instance and attaches a
/// transformer that replaces it with a small MafiaMuscle offspring after a short delay.
/// </summary>
[HarmonyPatch(typeof(BadstatusBirthMonster), "OnEvent")]
[HarmonyPriority(Priority.Last)]
internal static class BirthSpawnOverridePatch
{
    internal static int _conceptionFaction = FactionIds.Neutral;
    internal static bool _hasConceptionData = false;

    private static PendingBirth _pending;

    internal static void SetConceptionFaction(int factionSource)
    {
        _conceptionFaction = factionSource;
        _hasConceptionData = true;

        if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
            Plugin.Log?.LogInfo($"[Pregnancy.Birth] Conception recorded: faction={factionSource}");
    }

    internal static int GetConceptionFactionOrDefault()
    {
        if (_hasConceptionData)
        {
            _hasConceptionData = false;
            return _conceptionFaction;
        }

        if (WitchPregnancyState.SourceFaction != FactionIds.Neutral)
            return WitchPregnancyState.SourceFaction;

        return FactionIds.Monsters;
    }

    [HarmonyPrefix]
    private static void Prefix(BadstatusBirthMonster __instance, Spine.Event e, GameObject[] ___monster)
    {
        string eventName = e?.Data?.Name ?? "";

        if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
            Plugin.Log?.LogInfo($"[Pregnancy.Birth] OnEvent fired: eventName={eventName}");

        if (!PregnancyConfig.IsEnabled)
            return;

        if (eventName != "CREATE")
            return;

        try
        {
            int factionSource = GetConceptionFactionOrDefault();
            if (factionSource == FactionIds.Neutral)
                factionSource = FactionIds.Monsters;

            float scale = ChildData.InfantBirthScale;
            ChildData child = PregnancySlotStore.AddChild(factionSource);

            _pending = new PendingBirth
            {
                Child = child,
                FactionSource = factionSource,
                Scale = scale,
                BirthPosition = __instance.transform.position,
                Timestamp = Time.unscaledTime
            };

            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                Plugin.Log?.LogInfo($"[Pregnancy.Birth] CREATE event: pending slime capture for child {child.Guid} (faction={factionSource}, scale={scale:F2})");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[Pregnancy.Birth] Error in CREATE prefix: {ex.Message}");
            _pending = null;
            return;
        }

        try
        {
            if (___monster != null && ___monster.Length > 0)
            {
                if (NoREroMod.Systems.Spawn.EnemyPrefabRegistry.TryGetPrefab("Slaimu", out GameObject prefab) && prefab != null)
                {
                    ___monster[0] = prefab;
                    if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                        Plugin.Log?.LogInfo("[Pregnancy.Birth] Replaced birth monster[0] with sraimu prefab");
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[Pregnancy.Birth] Failed to replace birth monster[0]: {ex.Message}");
        }
    }

    [HarmonyFinalizer]
    private static void Finalizer(Spine.Event e)
    {
        string eventName = e?.Data?.Name ?? "";
        if (eventName == "JIGO" && _pending != null)
        {
            if (Time.unscaledTime - _pending.Timestamp > 10f)
            {
                if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                    Plugin.Log?.LogWarning($"[Pregnancy.Birth] Birth animation ended without capturing a slime; clearing stale pending birth {_pending.Child.Guid}");
                _pending = null;
            }
        }
    }

    internal static bool TryClaimPendingBirth(GameObject slime, out ChildData child, out int factionSource, out float scale)
    {
        child = null;
        factionSource = FactionIds.Neutral;
        scale = 0.15f;

        if (_pending == null || slime == null)
            return false;

        float elapsed = Time.unscaledTime - _pending.Timestamp;
        if (elapsed > 10f)
            return false;

        float dist = Vector3.Distance(slime.transform.position, _pending.BirthPosition);
        if (dist > 5f)
            return false;

        child = _pending.Child;
        factionSource = _pending.FactionSource;
        scale = _pending.Scale;
        _pending = null;

        if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
            Plugin.Log?.LogInfo($"[Pregnancy.Birth] Slime captured for birth: child={child.Guid}, dist={dist:F2}, elapsed={elapsed:F2}s");

        return true;
    }

    private class PendingBirth
    {
        public ChildData Child;
        public int FactionSource;
        public float Scale;
        public Vector3 BirthPosition;
        public float Timestamp;
    }
}
