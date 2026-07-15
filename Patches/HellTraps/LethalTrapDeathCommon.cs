using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Patches.HellTraps;

/// <summary>Shared forced death + slow-mo for HellGate lethal trap variants.</summary>
internal static class LethalTrapDeathCommon
{
    internal static void ApplyDeathSlowMo(playercon player)
    {
        ApplyDeathSlowMo(
            player,
            LethalMagicTrapDeathTuning.SlowMoScale,
            LethalMagicTrapDeathTuning.SlowMoRealSeconds);
    }

    internal static void ApplyDeathSlowMo(playercon player, float scale, float realSeconds)
    {
        scale = Mathf.Clamp(scale, 0.05f, 1f);
        realSeconds = Mathf.Max(0f, realSeconds);

        if (player != null)
        {
            try
            {
                player.CancelInvoke("timescale");
            }
            catch (System.Exception ex)
            {
                Plugin.Log?.LogWarning("[LethalTrap] CancelInvoke(timescale) failed: " + ex.Message);
            }
        }

        if (realSeconds <= 0f || scale >= 0.999f)
            return;

        Time.timeScale = scale;

        if (player == null)
            return;

        LethalMagicTrapDeathSlowMoHost host =
            player.GetComponent<LethalMagicTrapDeathSlowMoHost>();
        if (host == null)
            host = player.gameObject.AddComponent<LethalMagicTrapDeathSlowMoHost>();

        host.ScheduleRestore(realSeconds);
    }

    internal static void ClearDeathSlowMo(playercon player)
    {
        if (player != null)
        {
            LethalMagicTrapDeathSlowMoHost host =
                player.GetComponent<LethalMagicTrapDeathSlowMoHost>();
            if (host != null)
                Object.Destroy(host);
        }

        if (Time.timeScale < 1f)
            Time.timeScale = 1f;
    }

    internal static void ForcePlayerDeath(playercon player, string logTag, bool applySlowMo = true)
    {
        if (player == null)
            return;

        PlayerStatus status =
            Traverse.Create(player).Field("playerstatus").GetValue<PlayerStatus>();

        if (player._Death)
        {
            if (status != null && status.Hp > 0f)
                status.Hp = 0f;

            if (applySlowMo)
                ApplyDeathSlowMo(player);
            LethalMagicTrapEroSuppression.PinPlayerBody(player);
            return;
        }

        if (status != null)
            status.Hp = 0f;

        player.erodown = 0;
        Traverse.Create(player).Field("nowdamage").SetValue(false);
        Traverse.Create(player).Field("Death").SetValue(true);
        Traverse.Create(player).Field("tough").SetValue(-999f);
        player.state = "IDLE";

        if (status != null)
        {
            status.REstart_menu();
            status._SOUSA = false;
            status._SOUSAMNG = false;
        }

        GameObject magicCanvas =
            Traverse.Create(player).Field("MagicSpellCanvas").GetValue<GameObject>();
        if (magicCanvas != null)
            magicCanvas.SetActive(false);

        if (applySlowMo)
            ApplyDeathSlowMo(player);

        LethalMagicTrapEroSuppression.PinPlayerBody(player);
        LethalMagicTrapEroSuppression.SuppressEnemyEroApproach(forceImmediate: true);

        Plugin.Log?.LogInfo("[" + logTag + "] Forced lethal trap death (HP -> 0).");
    }

    /// <summary>Shared post-hit flow for lethal magic and lethal cocoon (death menu + audio + clip).</summary>
    internal static void FinalizeLethalDeathWithClip(
        playercon player,
        string logTag,
        System.Action clearHitState,
        System.Action playDeathClip,
        bool applySlowMoImmediately = true)
    {
        if (player == null)
            return;

        PlayerStatus status =
            Traverse.Create(player).Field("playerstatus").GetValue<PlayerStatus>();
        float hpBefore = status != null ? status.Hp : 0f;

        Plugin.Log?.LogInfo(
            "[" + logTag + "] Hit finalize — HP="
            + hpBefore.ToString("0.##")
            + ", Death="
            + player._Death
            + ", erodown="
            + player.erodown);

        if (hpBefore > 0f || !player._Death)
            ForcePlayerDeath(player, logTag, applySlowMoImmediately);
        else
        {
            if (applySlowMoImmediately)
                ApplyDeathSlowMo(player);
            LethalMagicTrapEroSuppression.PinPlayerBody(player);
        }

        LethalMagicTrapDeathAudio.OnCustomDeathStarted();
        LethalTrapVengeanceShockSession.MarkLethalTrapDeath();
        NoREroMod.Patches.Player.VanillaCutsceneSceneGuard.NotifyPotentialNoAltarZoneDeath();

        if (clearHitState != null)
            clearHitState();

        if (playDeathClip == null)
            return;

        try
        {
            playDeathClip();
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogError("[" + logTag + "] Death clip apply failed: " + ex.Message);
        }
    }
}
