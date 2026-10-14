using HarmonyLib;

namespace NoREroMod.Patches.Enemy.UndeadSkeletonsModCustom;

/// <summary>
/// Hooks on the four vanilla bases used by undead skeletons. Every hook is a no-op for non-skeleton instances.
/// </summary>
[HarmonyPatch]
internal static class UndeadSkeletonsEnemyPatches
{
    [HarmonyPatch(typeof(BigMerman), "Start")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.First)]
    private static void BigMerman_Start(BigMerman __instance) => UndeadSkeletonsRuntime.ApplyStats(__instance);

    [HarmonyPatch(typeof(Bigoni), "Start")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.First)]
    private static void Bigoni_Start(Bigoni __instance) => UndeadSkeletonsRuntime.ApplyStats(__instance);

    [HarmonyPatch(typeof(Pilgrim), "Start")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.First)]
    private static void Pilgrim_Start(Pilgrim __instance) => UndeadSkeletonsRuntime.ApplyStats(__instance);

    [HarmonyPatch(typeof(Vagrant), "Start")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.First)]
    private static void Vagrant_Start(Vagrant __instance) => UndeadSkeletonsRuntime.ApplyStats(__instance);

    [HarmonyPatch(typeof(BigMerman), "Update")]
    [HarmonyPostfix]
    private static void BigMerman_Update(BigMerman __instance) => UndeadSkeletonsRuntime.ApplyAnimSpeed(__instance);

    [HarmonyPatch(typeof(Bigoni), "Update")]
    [HarmonyPostfix]
    private static void Bigoni_Update(Bigoni __instance) => UndeadSkeletonsRuntime.ApplyAnimSpeed(__instance);

    [HarmonyPatch(typeof(Pilgrim), "Update")]
    [HarmonyPostfix]
    private static void Pilgrim_Update(Pilgrim __instance) => UndeadSkeletonsRuntime.ApplyAnimSpeed(__instance);

    [HarmonyPatch(typeof(Vagrant), "Update")]
    [HarmonyPostfix]
    private static void Vagrant_Update(Vagrant __instance) => UndeadSkeletonsRuntime.ApplyAnimSpeed(__instance);

    [HarmonyPatch(typeof(BigMerman), "OnTriggerStay2D")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void BigMerman_StayPrefix(BigMerman __instance, out object __state)
        => UndeadSkeletonsRuntime.SuppressEroEntry(__instance, out __state);

    [HarmonyPatch(typeof(BigMerman), "OnTriggerStay2D")]
    [HarmonyPostfix]
    private static void BigMerman_StayPostfix(BigMerman __instance, object __state)
        => UndeadSkeletonsRuntime.RestoreEroEntry(__instance, __state);

    [HarmonyPatch(typeof(Bigoni), "OnTriggerStay2D")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Bigoni_StayPrefix(Bigoni __instance, out object __state)
        => UndeadSkeletonsRuntime.SuppressEroEntry(__instance, out __state);

    [HarmonyPatch(typeof(Bigoni), "OnTriggerStay2D")]
    [HarmonyPostfix]
    private static void Bigoni_StayPostfix(Bigoni __instance, object __state)
        => UndeadSkeletonsRuntime.RestoreEroEntry(__instance, __state);

    [HarmonyPatch(typeof(Pilgrim), "OnTriggerStay2D")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Pilgrim_StayPrefix(Pilgrim __instance, out object __state)
        => UndeadSkeletonsRuntime.SuppressEroEntry(__instance, out __state);

    [HarmonyPatch(typeof(Pilgrim), "OnTriggerStay2D")]
    [HarmonyPostfix]
    private static void Pilgrim_StayPostfix(Pilgrim __instance, object __state)
        => UndeadSkeletonsRuntime.RestoreEroEntry(__instance, __state);

    [HarmonyPatch(typeof(Vagrant), "OnTriggerStay2D")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Vagrant_StayPrefix(Vagrant __instance, out object __state)
        => UndeadSkeletonsRuntime.SuppressEroEntry(__instance, out __state);

    [HarmonyPatch(typeof(Vagrant), "OnTriggerStay2D")]
    [HarmonyPostfix]
    private static void Vagrant_StayPostfix(Vagrant __instance, object __state)
        => UndeadSkeletonsRuntime.RestoreEroEntry(__instance, __state);

    // While the player is downed (EROWALK) skeletons stand in their IDLE pose instead of walking into her.
    // Update still sees EROWALK, so facing and the return to combat when she gets up stay vanilla.

    [HarmonyPatch(typeof(BigMerman), "FixedUpdate")]
    [HarmonyPrefix]
    private static void BigMerman_FixedPrefix(BigMerman __instance, out object __state)
        => UndeadSkeletonsRuntime.SuppressEroEntry(__instance, out __state);

    [HarmonyPatch(typeof(BigMerman), "FixedUpdate")]
    [HarmonyPostfix]
    private static void BigMerman_FixedPostfix(BigMerman __instance, object __state)
        => UndeadSkeletonsRuntime.RestoreEroEntryAndHold(__instance, __state);

    [HarmonyPatch(typeof(Bigoni), "FixedUpdate")]
    [HarmonyPrefix]
    private static void Bigoni_FixedPrefix(Bigoni __instance, out object __state)
        => UndeadSkeletonsRuntime.SuppressEroEntry(__instance, out __state);

    [HarmonyPatch(typeof(Bigoni), "FixedUpdate")]
    [HarmonyPostfix]
    private static void Bigoni_FixedPostfix(Bigoni __instance, object __state)
        => UndeadSkeletonsRuntime.RestoreEroEntryAndHold(__instance, __state);

    [HarmonyPatch(typeof(Pilgrim), "FixedUpdate")]
    [HarmonyPrefix]
    private static void Pilgrim_FixedPrefix(Pilgrim __instance, out object __state)
        => UndeadSkeletonsRuntime.SuppressEroEntry(__instance, out __state);

    [HarmonyPatch(typeof(Pilgrim), "FixedUpdate")]
    [HarmonyPostfix]
    private static void Pilgrim_FixedPostfix(Pilgrim __instance, object __state)
        => UndeadSkeletonsRuntime.RestoreEroEntryAndHold(__instance, __state);

    [HarmonyPatch(typeof(Vagrant), "FixedUpdate")]
    [HarmonyPrefix]
    private static void Vagrant_FixedPrefix(Vagrant __instance, out object __state)
        => UndeadSkeletonsRuntime.SuppressEroEntry(__instance, out __state);

    [HarmonyPatch(typeof(Vagrant), "FixedUpdate")]
    [HarmonyPostfix]
    private static void Vagrant_FixedPostfix(Vagrant __instance, object __state)
        => UndeadSkeletonsRuntime.RestoreEroEntryAndHold(__instance, __state);

    [HarmonyPatch(typeof(BigMerman), "fun_animekind")]
    [HarmonyPrefix]
    private static void BigMerman_AnimPrefix(BigMerman __instance, out object __state)
        => UndeadSkeletonsRuntime.SuppressEroEntry(__instance, out __state);

    [HarmonyPatch(typeof(BigMerman), "fun_animekind")]
    [HarmonyPostfix]
    private static void BigMerman_AnimPostfix(BigMerman __instance, object __state)
        => UndeadSkeletonsRuntime.RestoreEroEntry(__instance, __state);

    [HarmonyPatch(typeof(Bigoni), "fun_animekind")]
    [HarmonyPrefix]
    private static void Bigoni_AnimPrefix(Bigoni __instance, out object __state)
        => UndeadSkeletonsRuntime.SuppressEroEntry(__instance, out __state);

    [HarmonyPatch(typeof(Bigoni), "fun_animekind")]
    [HarmonyPostfix]
    private static void Bigoni_AnimPostfix(Bigoni __instance, object __state)
        => UndeadSkeletonsRuntime.RestoreEroEntry(__instance, __state);

    [HarmonyPatch(typeof(Pilgrim), "fun_animekind")]
    [HarmonyPrefix]
    private static void Pilgrim_AnimPrefix(Pilgrim __instance, out object __state)
        => UndeadSkeletonsRuntime.SuppressEroEntry(__instance, out __state);

    [HarmonyPatch(typeof(Pilgrim), "fun_animekind")]
    [HarmonyPostfix]
    private static void Pilgrim_AnimPostfix(Pilgrim __instance, object __state)
        => UndeadSkeletonsRuntime.RestoreEroEntry(__instance, __state);

    [HarmonyPatch(typeof(Vagrant), "fun_animekind")]
    [HarmonyPrefix]
    private static void Vagrant_AnimPrefix(Vagrant __instance, out object __state)
        => UndeadSkeletonsRuntime.SuppressEroEntry(__instance, out __state);

    [HarmonyPatch(typeof(Vagrant), "fun_animekind")]
    [HarmonyPostfix]
    private static void Vagrant_AnimPostfix(Vagrant __instance, object __state)
        => UndeadSkeletonsRuntime.RestoreEroEntry(__instance, __state);
}
