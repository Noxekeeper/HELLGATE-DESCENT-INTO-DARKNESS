using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.Gameplay;

/// <summary>Air block: postfix <see cref="playercon.guard_fun"/> + <see cref="playercon.anime_fun"/>.</summary>
internal static class AirGuardGuardFunPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(playercon), "guard_fun")]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(playercon __instance)
    {
        if (__instance == null) return;
        if (!(Plugin.enableAirGuard?.Value ?? true)) return;
        if (__instance.m_Grounded) return;
        if (!OuterGuardInputAllowed(__instance)) return;

        __instance.guard = true;

        float dt = Time.deltaTime;

        float guradCount = AirGuardFields.GetGuradCount(__instance);
        if (guradCount > 0f)
        {
            guradCount -= dt;
            if (guradCount < 0f) guradCount = 0f;
        }
        AirGuardFields.SetGuradCount(__instance, guradCount);

        var ps = AirGuardFields.GetPlayerStatus(__instance);
        if (ps == null) return;

        if (__instance.justguard < ps._GuardCutTime + 0.2f)
            __instance.justguard += dt;
    }

    private static bool OuterGuardInputAllowed(playercon pc)
    {
        if (!AirGuardFields.GetKeyGuard(pc)) return false;
        if (pc.Attacknow) return false;
        if (AirGuardFields.GetStepKind(pc) != 0) return false;
        if (pc.nowdamage) return false;
        if (pc.magicnow) return false;
        var ps = AirGuardFields.GetPlayerStatus(pc);
        if (ps == null || !ps._SOUSA) return false;
        if (AirGuardFields.GetItemUse(pc)) return false;
        if (AirGuardFields.GetDeath(pc)) return false;
        if (AirGuardFields.GetParry(pc)) return false;
        return true;
    }
}

[HarmonyPatch(typeof(playercon), "anime_fun")]
internal static class AirGuardAnimeFunPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(playercon __instance)
    {
        if (__instance == null) return;
        if (!(Plugin.enableAirGuard?.Value ?? true)) return;
        if (__instance.m_Grounded) return;
        if (!__instance.guard) return;
        if (__instance.nowdamage) return;
        if (__instance.magicnow || __instance.Attacknow || AirGuardFields.GetItemUse(__instance) || __instance._stabnow) return;
        if (AirGuardFields.GetParry(__instance)) return;
        if (__instance.grapfrag) return;

        float axis = AirGuardFields.GetAxis(__instance);
        __instance.state = axis == 0f ? "GUARD" : "GUARDMOVE";
        Traverse.Create(__instance).Field<bool>("loopanim").Value = true;
        Traverse.Create(__instance).Field<float>("imagetime").Value = 1f;
    }
}

internal static class AirGuardFields
{
    internal static bool GetKeyGuard(playercon pc) =>
        Traverse.Create(pc).Field<bool>("key_guard").Value;

    internal static float GetGuradCount(playercon pc) =>
        Traverse.Create(pc).Field<float>("guradcount").Value;

    internal static void SetGuradCount(playercon pc, float v) =>
        Traverse.Create(pc).Field<float>("guradcount").Value = v;

    internal static bool GetParry(playercon pc) =>
        Traverse.Create(pc).Field<bool>("Parry").Value;

    internal static int GetStepKind(playercon pc) =>
        Traverse.Create(pc).Field<int>("stepkind").Value;

    internal static bool GetItemUse(playercon pc) =>
        Traverse.Create(pc).Field<bool>("Itemuse").Value;

    internal static bool GetDeath(playercon pc) =>
        Traverse.Create(pc).Field<bool>("Death").Value;

    internal static float GetAxis(playercon pc) =>
        Traverse.Create(pc).Field<float>("axis").Value;

    internal static PlayerStatus GetPlayerStatus(playercon pc) =>
        Traverse.Create(pc).Field<PlayerStatus>("playerstatus").Value;
}
