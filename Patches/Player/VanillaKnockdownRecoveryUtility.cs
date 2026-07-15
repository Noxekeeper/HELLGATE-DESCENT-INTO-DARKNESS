using DarkTonic.MasterAudio;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Patches.Player;

/// <summary>
/// Mirrors vanilla <see cref="playercon.fun_nowdamage"/> mash stand-up physics when HellGate
/// clears <see cref="playercon.erodown"/> without applying <c>vspeed</c> / <c>act_downup</c>.
/// </summary>
internal static class VanillaKnockdownRecoveryUtility
{
    internal static void ApplyStandUpFromKnockdown(playercon player)
    {
        if (player == null || player.erodown == 0)
            return;

        MasterAudio.StopBus("EroVoice");
        MasterAudio.PlaySound("act_downup", 1f, null, 0f, null, false, false);

        Traverse traverse = Traverse.Create(player);
        float keyMove = 0f;
        float gravity = 3f;
        try
        {
            keyMove = traverse.Field<float>("key_move").Value;
            gravity = traverse.Field<float>("gravity").Value;
            traverse.Field("damecount").SetValue(0f);
            traverse.Field("downup").SetValue(0);
            traverse.Field("walldown").SetValue(0f);
        }
        catch
        {
        }

        player.erodown = 0;
        player.tough = player.maxtough;
        player.nowdamage = false;

        if (player.rigi2d != null)
        {
            player.rigi2d.velocity = new Vector2(player.movespeed * keyMove, player.vspeed);
            player.rigi2d.gravityScale = gravity;
        }
    }

    internal static bool NeedsStandUpJump(playercon player, int erodownBefore)
    {
        if (player == null || erodownBefore == 0 || player.erodown != 0)
            return false;

        if (player.eroflag || PlayerEroContextUtility.IsSoloPleasureState(player))
            return false;

        if (player.rigi2d == null)
            return false;

        float expectedY = player.vspeed;
        if (Mathf.Abs(player.rigi2d.velocity.y - expectedY) < 1f)
            return false;

        if (!player.m_Grounded && player.rigi2d.velocity.y > 0.5f)
            return false;

        return true;
    }
}
