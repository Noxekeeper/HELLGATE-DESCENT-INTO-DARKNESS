using System.Collections.Generic;
using HarmonyLib;
using NoREroMod.Systems.CombatAi.Factions;
using UnityEngine;

namespace NoREroMod.Patches.Enemy;

/// <summary>
/// After gangbang handoff the owning <see cref="MummyMan"/> is fully deactivated
/// (no AI, no faction icon, no attacks) until the player stands.
/// Same contract as Dorei / Bigoni / Goblin handoff (<c>SetActive(false)</c>),
/// not mesh-only hide (that left Update running).
/// </summary>
internal static class MummyManHandoffHide
{
    private static readonly HashSet<MummyMan> HiddenOwners = new();

    internal static bool IsHidden(MummyMan mummy) =>
        mummy != null && HiddenOwners.Contains(mummy);

    internal static void HideAfterHandoff(MummyMan mummy, GameObject erodata)
    {
        if (mummy == null)
            return;

        HiddenOwners.Add(mummy);
        mummy.eroflag = false;

        try
        {
            mummy.CancelInvoke("fun_DisableWhenOneTarget_reset");
            mummy.ero_camerareset();
            mummy.fun_DisableWhenOneTarget_reset();
        }
        catch
        {
        }

        if (erodata != null)
            erodata.SetActive(false);

        if (mummy.state == MummyMan.enemystate.EROWALK)
            mummy.state = MummyMan.enemystate.IDLE;

        // Full deactivate: stops Update AI, attack, and HellGate faction world icon.
        mummy.gameObject.SetActive(false);
    }

    /// <summary>
    /// Nudge other active MummyMen into EROWALK so they approach the prone player.
    /// </summary>
    internal static void WakeNearbyForGrab(playercon player)
    {
        if (player == null || player.erodown == 0)
            return;

        MummyMan[] all = Object.FindObjectsOfType<MummyMan>();
        for (int i = 0; i < all.Length; i++)
        {
            MummyMan mummy = all[i];
            if (mummy == null || IsHidden(mummy))
                continue;
            if (!mummy.gameObject.activeInHierarchy || mummy.eroflag || mummy.Hp <= 0f)
                continue;

            try
            {
                EnemyFactionRuntime.RestoreVanillaPlayerApproach(mummy);
            }
            catch
            {
            }

            Rigidbody2D body = mummy.GetComponent<Rigidbody2D>();
            if (body != null && !body.simulated)
                body.simulated = true;

            // Vanilla only auto-promotes IDLE/WALK/RUN → EROWALK; force out of combat blanks.
            if (mummy.state == MummyMan.enemystate.DEATH)
                continue;

            if (mummy.state != MummyMan.enemystate.IDLE
                && mummy.state != MummyMan.enemystate.WALK
                && mummy.state != MummyMan.enemystate.RUN
                && mummy.state != MummyMan.enemystate.EROWALK)
            {
                mummy.state = MummyMan.enemystate.IDLE;
            }

            mummy.state = MummyMan.enemystate.EROWALK;
        }
    }

    internal static void ProcessPlayerStandCheck(playercon player)
    {
        if (player == null || player.erodown != 0 || HiddenOwners.Count == 0)
            return;
        RestoreAll();
    }

    internal static void RestoreAll()
    {
        foreach (MummyMan mummy in HiddenOwners)
        {
            if (mummy == null)
                continue;

            if (!mummy.gameObject.activeSelf)
                mummy.gameObject.SetActive(true);

            mummy.eroflag = false;

            MeshRenderer mesh = AccessTools.Field(typeof(MummyMan), "myspinerennder")?.GetValue(mummy) as MeshRenderer;
            if (mesh != null)
                mesh.enabled = true;

            GameObject ui = AccessTools.Field(typeof(MummyMan), "UI")?.GetValue(mummy) as GameObject;
            if (ui != null)
                ui.SetActive(true);

            if (mummy.erodata != null && mummy.erodata.activeSelf)
                mummy.erodata.SetActive(false);
        }

        HiddenOwners.Clear();
    }
}

[HarmonyPatch(typeof(MummyMan), "OnTriggerStay2D")]
internal static class MummyManHandoffGrabBlockPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool BlockGrabWhenHandoffHidden(MummyMan __instance)
    {
        return !MummyManHandoffHide.IsHidden(__instance);
    }
}

// Keep patch registered for Harmony consistency with Kakasi; with SetActive(false)
// Update does not run while hidden. If something re-activates the GO early, freeze him.
[HarmonyPatch(typeof(MummyMan), "Update")]
internal static class MummyManHandoffStatePatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool SkipUpdateWhenHandoffHidden(MummyMan __instance)
    {
        if (!MummyManHandoffHide.IsHidden(__instance))
            return true;

        // Should not normally run (GO inactive); if it does, kill AI for this frame.
        __instance.eroflag = false;
        if (__instance.state == MummyMan.enemystate.EROWALK
            || __instance.state == MummyMan.enemystate.ATK
            || __instance.state == MummyMan.enemystate.ATK1
            || __instance.state == MummyMan.enemystate.ATK2
            || __instance.state == MummyMan.enemystate.ATK5
            || __instance.state == MummyMan.enemystate.ATK6
            || __instance.state == MummyMan.enemystate.ATK7
            || __instance.state == MummyMan.enemystate.RUN
            || __instance.state == MummyMan.enemystate.WALK)
        {
            __instance.state = MummyMan.enemystate.IDLE;
        }

        if (__instance.gameObject.activeSelf)
            __instance.gameObject.SetActive(false);

        return false;
    }
}
