using System.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Patches.HellTraps;

/// <summary>
/// Lethal cocoon death clip: same playback as lethal magic trap (bone start, fall to trap floor).
/// Only the PNG folder differs (WebSpike_Death vs Exp_Death).
/// </summary>
internal static class LethalCocoonTrapDeathDisplay
{
    internal static bool HasActiveClip =>
        LethalCocoonTrapDeathContext.IsCustomDeathActive &&
        LethalMagicTrapDeathDisplay.HasActiveClip;

    internal static void Preload()
    {
        Sprite[] frames = LethalCocoonTrapAssetLoader.GetDeathFrames();
        if (frames == null || frames.Length == 0)
        {
            Plugin.Log?.LogWarning(
                "[LethalCocoonTrapDeathDisplay] No death PNG frames. Check "
                + LethalCocoonTrapPaths.DefaultDeathClipRelative);
            return;
        }

        Plugin.Log?.LogInfo(
            "[LethalCocoonTrapDeathDisplay] Loaded "
            + frames.Length
            + " frame(s) from "
            + LethalCocoonTrapAssetLoader.GetCachedDirectory());
    }

    internal static void TryApply(playercon player)
    {
        if (!Plugin.enableLethalCocoonTrap.Value || player == null)
            return;

        Sprite[] frames = LethalCocoonTrapAssetLoader.GetDeathFrames();
        if (frames == null || frames.Length == 0)
        {
            Plugin.Log?.LogWarning("[LethalCocoonTrapDeathDisplay] Death clip skipped — no PNG frames.");
            return;
        }

        Vector3? anchor = LethalCocoonTrapDeathContext.TrapAnchorWorld;
        if (anchor.HasValue)
            LethalMagicTrapDeathContext.SetTrapFloorWorld(anchor.Value);

        float scale = ResolveCocoonConfiguredScale();
        if (!LethalMagicTrapDeathDisplay.TryApplyWithFrames(
                player,
                frames,
                scale,
                LethalDeathClipPlaybackProfile.CocoonWebSpike))
        {
            Plugin.Log?.LogWarning("[LethalCocoonTrapDeathDisplay] Death clip failed to start.");
            return;
        }

        LethalCocoonTrapDeathContext.MarkCustomDeathActive();

        Plugin.Log?.LogInfo(
            "[LethalCocoonTrapDeathDisplay] Playing death clip (bone start -> trap floor), trap="
            + (anchor.HasValue ? anchor.Value.ToString() : "none"));
    }

    internal static void ScheduleDeferredApply(playercon player)
    {
        if (player == null || LethalCocoonTrapDeathContext.IsCustomDeathActive)
            return;

        LethalCocoonTrapDeathApplyHost host =
            player.GetComponent<LethalCocoonTrapDeathApplyHost>();
        if (host == null)
            host = player.gameObject.AddComponent<LethalCocoonTrapDeathApplyHost>();

        host.Schedule(player);
    }

    internal static void ForceCleanupForRespawn(playercon player = null)
    {
        if (player != null)
        {
            LethalCocoonTrapDeathApplyHost host =
                player.GetComponent<LethalCocoonTrapDeathApplyHost>();
            if (host != null)
                Object.Destroy(host);
        }

        LethalCocoonTrapDeathContext.ClearCustomDeathActive();
        LethalMagicTrapDeathDisplay.ForceCleanupForRespawn(player);
    }

    private static float ResolveCocoonConfiguredScale()
    {
        float scale = Plugin.lethalCocoonTrapDeathClipDisplayScale?.Value
            ?? Plugin.lethalMagicTrapDeathClipDisplayScale?.Value
            ?? LethalMagicTrapDeathTuning.DisplayScale;
        return Mathf.Max(0.01f, scale);
    }
}

internal sealed class LethalCocoonTrapDeathApplyHost : MonoBehaviour
{
    private playercon _player;

    internal void Schedule(playercon player)
    {
        _player = player;
        StopAllCoroutines();
        StartCoroutine(ApplyNextFrame());
    }

    private IEnumerator ApplyNextFrame()
    {
        yield return null;

        if (_player == null || !Plugin.enableLethalCocoonTrap.Value)
            yield break;

        if (LethalCocoonTrapDeathContext.IsCustomDeathActive)
            yield break;

        LethalCocoonTrapDeathDisplay.TryApply(_player);
    }
}
