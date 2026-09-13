using System.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Patches.HellTraps;

/// <summary>
/// Lightning fatal death clip: PNG fixed at trap world coords + black backdrop (no player bone).
/// </summary>
internal static class LethalLightningTrapDeathDisplay
{
    internal static bool HasActiveClip =>
        LethalLightningTrapDeathContext.IsCustomDeathActive &&
        LethalMagicTrapDeathDisplay.HasActiveClip;

    internal static void Preload()
    {
        Sprite[] frames = LethalLightningTrapAssetLoader.GetDeathFrames();
        if (frames == null || frames.Length == 0)
        {
            Plugin.Log?.LogWarning(
                "[LethalLightningTrapDeathDisplay] No death PNG frames. Check "
                + LethalLightningTrapPaths.DefaultDeathClipRelative);
            return;
        }

        Plugin.Log?.LogInfo(
            "[LethalLightningTrapDeathDisplay] Loaded "
            + frames.Length
            + " frame(s) from "
            + LethalLightningTrapAssetLoader.GetCachedDirectory());
    }

    internal static void TryApply(playercon player)
    {
        if (!Plugin.IsLethalLightningTrapActive || player == null)
            return;

        Sprite[] frames = LethalLightningTrapAssetLoader.GetDeathFrames();
        if (frames == null || frames.Length == 0)
        {
            Plugin.Log?.LogWarning("[LethalLightningTrapDeathDisplay] Death clip skipped — no PNG frames.");
            return;
        }

        Vector3 anchor = LethalLightningTrapDeathContext.TrapAnchorWorld.HasValue
            ? LethalLightningTrapDeathContext.TrapAnchorWorld.Value
            : player.transform.position;

        LethalMagicTrapDeathContext.SetTrapFloorWorld(anchor);

        float scale = ResolveConfiguredScale();
        if (!LethalMagicTrapDeathDisplay.TryApplyAtTrapAnchor(
                player,
                frames,
                anchor,
                scale,
                LethalDeathClipPlaybackProfile.LightningTrapFixed))
        {
            Plugin.Log?.LogWarning("[LethalLightningTrapDeathDisplay] Death clip failed to start.");
            return;
        }

        LethalLightningTrapDeathContext.MarkCustomDeathActive();

        Plugin.Log?.LogInfo(
            "[LethalLightningTrapDeathDisplay] Playing trap-anchored death clip @ trap="
            + anchor
            + " offsetY="
            + LethalLightningTrapDeathTuning.ClipOffsetY.ToString("0.##")
            + " => y="
            + (anchor.y + LethalLightningTrapDeathTuning.ClipOffsetY).ToString("0.##"));
    }

    internal static void ScheduleDeferredApply(playercon player)
    {
        if (player == null || LethalLightningTrapDeathContext.IsCustomDeathActive)
            return;

        LethalLightningTrapDeathApplyHost host =
            player.GetComponent<LethalLightningTrapDeathApplyHost>();
        if (host == null)
            host = player.gameObject.AddComponent<LethalLightningTrapDeathApplyHost>();

        host.Schedule(player);
    }

    internal static void ForceCleanupForRespawn(playercon player = null)
    {
        if (player != null)
        {
            LethalLightningTrapDeathApplyHost host =
                player.GetComponent<LethalLightningTrapDeathApplyHost>();
            if (host != null)
                Object.Destroy(host);
        }

        LethalLightningTrapDeathContext.ClearCustomDeathActive();
        LethalMagicTrapDeathDisplay.ForceCleanupForRespawn(player);
    }

    private static float ResolveConfiguredScale()
    {
        float scale = Plugin.lethalLightningTrapDeathClipDisplayScale != null
            ? Plugin.lethalLightningTrapDeathClipDisplayScale.Value
            : Plugin.lethalCocoonTrapDeathClipDisplayScale != null
                ? Plugin.lethalCocoonTrapDeathClipDisplayScale.Value
                : LethalMagicTrapDeathTuning.DisplayScale;
        return Mathf.Max(0.01f, scale);
    }
}

internal sealed class LethalLightningTrapDeathApplyHost : MonoBehaviour
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

        if (_player == null || !Plugin.IsLethalLightningTrapActive)
            yield break;

        if (LethalLightningTrapDeathContext.IsCustomDeathActive)
            yield break;

        LethalLightningTrapDeathDisplay.TryApply(_player);
    }
}
