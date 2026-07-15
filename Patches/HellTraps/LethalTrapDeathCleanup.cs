using HarmonyLib;
using NoREroMod;
using Spine.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Patches.HellTraps;

/// <summary>
/// Unified respawn cleanup for all HellGate lethal trap death clips (magic + cocoon).
/// Take Vengeance and Death_flag must call this — not a single trap display in isolation.
/// </summary>
internal static class LethalTrapDeathCleanup
{
    private static int _lastCleanupFrame = -1;
    private const string DeathClipRootName = "HellGateLethalMagicTrapDeathClip";

    internal static bool ShouldCleanupOnRespawn()
    {
        return LethalMagicTrapDeathDisplay.HasActiveClip ||
               LethalCocoonTrapDeathDisplay.HasActiveClip ||
               LethalMagicTrapDeathContext.IsCustomDeathActive ||
               LethalCocoonTrapDeathContext.IsCustomDeathActive ||
               LethalMagicTrapDeathContext.IsEroSuppressionActive ||
               LethalCocoonTrapDeathContext.IsEroSuppressionActive;
    }

    /// <summary>Restore player visuals, stop clips, clear trap death session flags.</summary>
    internal static void ForceCleanupForRespawn(playercon player = null)
    {
        int frame = Time.frameCount;
        if (frame == _lastCleanupFrame)
            return;

        _lastCleanupFrame = frame;

        if (player == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
                player = playerObj.GetComponent<playercon>();
        }

        LethalCocoonTrapDeathDisplay.ForceCleanupForRespawn(player);
        LethalMagicTrapDeathDisplay.ForceCleanupForRespawn(player);

        DestroyOrphanClipRoots();
        EnsurePlayerVisuallyRestored(player);

        if (player != null)
            LethalTrapDeathCommon.ClearDeathSlowMo(player);

        LethalMagicTrapDeathContext.ClearMagicHitState();
        LethalMagicTrapEroSuppression.ResetRuntimeState();
        LethalMagicTrapDeathAudio.OnCustomDeathEnded();
        LethalTrapDeathBlackScreen.Hide();

        Plugin.Log?.LogInfo("[LethalTrapDeathCleanup] Respawn cleanup complete (magic + cocoon).");
    }

    private static void DestroyOrphanClipRoots()
    {
        DestroyClipRootIfPresent(DeathClipRootName);
    }

    private static void DestroyClipRootIfPresent(string rootName)
    {
        GameObject clipRoot = GameObject.Find(rootName);
        if (clipRoot == null)
            return;

        LethalMagicTrapDeathClipRunner runner =
            clipRoot.GetComponent<LethalMagicTrapDeathClipRunner>();
        if (runner != null)
        {
            runner.Restore();
            return;
        }

        Object.Destroy(clipRoot);
    }

    /// <summary>
    /// Safety net when clip runners were destroyed without Restore (player stays invisible).
    /// Mirrors EmergencyRestorePlayer in both death display classes.
    /// </summary>
    internal static void EnsurePlayerVisuallyRestored(playercon player)
    {
        if (player == null)
            return;

        if (PlayerEroContextUtility.ShouldPreserveBadstatusBirthVisuals(player))
            return;

        SkeletonAnimation spine = player.GetComponent<SkeletonAnimation>();
        if (spine == null)
            spine = player.GetComponentInChildren<SkeletonAnimation>(true);

        if (spine != null)
        {
            spine.enabled = true;
            spine.timeScale = 1f;
            if (spine.skeleton != null)
                spine.skeleton.SetColor(Color.white);

            MeshRenderer meshRenderer = spine.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                meshRenderer.enabled = true;
        }

        Renderer[] renderers = player.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            string objName = renderer.gameObject.name;
            if (objName.IndexOf("HellGateLethalMagicTrap") >= 0 ||
                objName.IndexOf("HellGateLethalCocoonTrap") >= 0)
            {
                continue;
            }

            renderer.enabled = true;
        }

        SkeletonAnimation[] skeletonAnimations =
            player.GetComponentsInChildren<SkeletonAnimation>(true);
        for (int i = 0; i < skeletonAnimations.Length; i++)
        {
            if (skeletonAnimations[i] != null)
                skeletonAnimations[i].enabled = true;
        }

        RestoreNamedChild(player.transform, "UIface");
        RestoreNamedChild(player.transform, "damageUI");
        RestoreNamedChild(player.transform, "blood");
    }

    private static void RestoreNamedChild(Transform playerRoot, string childName)
    {
        Transform child = playerRoot.Find(childName);
        if (child != null && !child.gameObject.activeSelf)
            child.gameObject.SetActive(true);
    }
}

[HarmonyPatch(typeof(playercon), nameof(playercon.Death_flag))]
internal static class LethalTrapDeathFlagCleanupPatch
{
    [HarmonyPostfix]
    private static void Postfix(playercon __instance)
    {
        if (__instance == null)
            return;

        if (!Plugin.enableLethalMagicTrap.Value && !Plugin.enableLethalCocoonTrap.Value)
            return;

        if (!LethalTrapDeathCleanup.ShouldCleanupOnRespawn())
            return;

        LethalTrapDeathCleanup.ForceCleanupForRespawn(__instance);
    }
}
