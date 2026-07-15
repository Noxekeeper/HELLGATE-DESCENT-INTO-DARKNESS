using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Patches.Player;

/// <summary>
/// Clears lingering vanilla hit blood (<see cref="playercon"/> blood / Blood7_* hierarchy) after damage.
/// With HellGate loaded, sub-emitters (Head/Right/Main/Left) can leave local-space dots that follow the player.
/// </summary>
internal static class PlayerHitBloodCleanupPatch
{
    private static Coroutine? _cleanupCoroutine;

    [HarmonyPatch(typeof(playercon), nameof(playercon.fun_damage))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void FunDamage_Postfix(playercon __instance)
    {
        Schedule(__instance);
    }

    [HarmonyPatch(typeof(playercon), nameof(playercon.fun_damage_Improvement))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void FunDamageImprovement_Postfix(playercon __instance)
    {
        Schedule(__instance);
    }

    private static void Schedule(playercon pc)
    {
        if (!(Plugin.enableHitBloodParticleCleanup?.Value ?? true))
            return;
        if (pc == null)
            return;

        GameObject? blood;
        try
        {
            blood = Traverse.Create(pc).Field("blood").GetValue<GameObject>();
        }
        catch
        {
            return;
        }

        if (blood == null)
            return;

        var host = Plugin.Instance;
        if (host == null)
            return;

        if (_cleanupCoroutine != null)
            host.StopCoroutine(_cleanupCoroutine);

        float delay = Mathf.Clamp(Plugin.hitBloodParticleCleanupDelaySeconds?.Value ?? 1.25f, 0.2f, 5f);
        _cleanupCoroutine = host.StartCoroutine(CleanupAfterDelay(blood, delay));
    }

    private static IEnumerator CleanupAfterDelay(GameObject bloodRoot, float delaySec)
    {
        yield return new WaitForSecondsRealtime(delaySec);
        _cleanupCoroutine = null;
        if (bloodRoot == null)
            yield break;

        foreach (ParticleSystem ps in bloodRoot.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps == null)
                continue;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
        }

        bloodRoot.SetActive(false);
    }

    internal static bool IsUnderPlayerBloodHierarchy(Transform t)
    {
        while (t != null)
        {
            string n = t.name;
            if (!string.IsNullOrEmpty(n) &&
                n.IndexOf("Blood", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            t = t.parent;
        }

        return false;
    }
}
