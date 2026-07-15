using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Patches.Enemy.Kakash;

/// <summary>
/// After gangbang handoff the owning <see cref="Kakash"/> stays hidden (satisfied) until the player stands.
/// </summary>
internal static class KakasiHandoffHide
{
    private static readonly HashSet<global::Kakash> HiddenOwners = new();

    internal static bool IsHidden(global::Kakash kakash) =>
        kakash != null && HiddenOwners.Contains(kakash);

    internal static void HideAfterHandoff(global::Kakash kakash, GameObject erodataCross, GameObject erodataGround)
    {
        if (kakash == null)
            return;

        HiddenOwners.Add(kakash);
        kakash.eroflag = false;

        MeshRenderer mesh = kakash.GetComponent<MeshRenderer>();
        if (mesh != null)
            mesh.enabled = false;

        Transform ui = kakash.transform.Find("Canvas");
        if (ui != null)
            ui.gameObject.SetActive(false);

        if (erodataCross != null)
            erodataCross.SetActive(false);
        if (erodataGround != null)
            erodataGround.SetActive(false);

        if (kakash.state == global::Kakash.enemystate.EROWALK)
            kakash.state = global::Kakash.enemystate.IDLE;
    }

    internal static void ProcessPlayerStandCheck(playercon player)
    {
        if (player == null || player.erodown != 0 || HiddenOwners.Count == 0)
            return;
        RestoreAll();
    }

    internal static void RestoreAll()
    {
        foreach (global::Kakash kakash in HiddenOwners)
        {
            if (kakash == null)
                continue;

            MeshRenderer mesh = kakash.GetComponent<MeshRenderer>();
            if (mesh != null)
                mesh.enabled = true;

            Transform ui = kakash.transform.Find("Canvas");
            if (ui != null)
                ui.gameObject.SetActive(true);
        }

        HiddenOwners.Clear();
    }
}

[HarmonyPatch(typeof(global::Kakash), "OnTriggerStay2D")]
internal static class KakasiHandoffGrabBlockPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool BlockGrabWhenHandoffHidden(global::Kakash __instance)
    {
        return !KakasiHandoffHide.IsHidden(__instance);
    }
}

[HarmonyPatch(typeof(global::Kakash), "Update")]
internal static class KakasiHandoffStatePatch
{
    [HarmonyPostfix]
    private static void KeepHandoffHidden(global::Kakash __instance)
    {
        if (!KakasiHandoffHide.IsHidden(__instance))
            return;

        __instance.eroflag = false;

        MeshRenderer mesh = __instance.GetComponent<MeshRenderer>();
        if (mesh != null && mesh.enabled)
            mesh.enabled = false;

        Transform ui = __instance.transform.Find("Canvas");
        if (ui != null && ui.gameObject.activeSelf)
            ui.gameObject.SetActive(false);

        if (__instance.state == global::Kakash.enemystate.EROWALK)
            __instance.state = global::Kakash.enemystate.IDLE;
    }
}
