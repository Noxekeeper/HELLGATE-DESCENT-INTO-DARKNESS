using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using NoREroMod;

namespace NoREroMod.Systems.CombatAi.Patches;

/// <summary>
/// Prefix/Postfix on EnemyDate.OndamageSend. When tag == "ATKweapon" and player is in the middle of a combo
/// (Attacknow and Atkcount &lt; weaponcount), with configurable chance we temporarily raise Avoidcount so that
/// fun_enedamage() is more likely to choose STEP2 (dodge). Original Avoidcount is restored in Postfix.
/// Uses Traverse for playerstatus (may be private in game) and Avoidcount (protected in EnemyDate).
/// Config: HellGateJson/CombatAi/CombatAi.json (ReactToCombo*, ReactToComboMinAvoidcount).
/// </summary>
[HarmonyPatch(typeof(EnemyDate), "OndamageSend")]
internal static class EnemyDateOndamageSendPatch
{
    private static readonly Dictionary<EnemyDate, int> OriginalAvoidcount = new Dictionary<EnemyDate, int>();

    [HarmonyPrefix]
    public static void Prefix(EnemyDate __instance, string tag)
    {
        if (tag != "ATKweapon" || !CombatAiConfig.Enable || !CombatAiConfig.ReactToCombo)
            return;

        playercon player = __instance.com_player;
        if (player == null)
            return;

        PlayerStatus status = Traverse.Create(player).Field("playerstatus").GetValue<PlayerStatus>();
        if (status == null)
            return;

        bool attackNow = player.Attacknow;
        int atkCount = player.Atkcount;
        int weaponCount = status.weaponcount;
        if (weaponCount <= 0) weaponCount = 3;

        bool comboOngoing = attackNow && atkCount < weaponCount;
        if (CombatAiConfig.ReactToComboOnlyFirstHit)
            comboOngoing = comboOngoing && atkCount <= 1;

        if (!comboOngoing)
            return;

        var avoidField = Traverse.Create(__instance).Field("Avoidcount");
        int currentAvoid = avoidField.GetValue<int>();
        if (currentAvoid < CombatAiConfig.ReactToComboMinAvoidcount)
            return;

        if (Random.value > CombatAiConfig.ReactToComboDodgeChance)
            return;

        int maxAvoid = Mathf.Clamp(CombatAiConfig.ReactToComboMaxAvoidcount, 2, 5);
        OriginalAvoidcount[__instance] = currentAvoid;
        avoidField.SetValue(maxAvoid);

        if (CombatAiConfig.DebugLogging)
            Plugin.Log?.LogInfo($"[CombatAi] Combo react: Avoidcount {currentAvoid} -> {maxAvoid} (Atkcount={atkCount} weaponcount={weaponCount})");
    }

    [HarmonyPostfix]
    public static void Postfix(EnemyDate __instance)
    {
        if (!OriginalAvoidcount.TryGetValue(__instance, out int original))
            return;
        OriginalAvoidcount.Remove(__instance);
        Traverse.Create(__instance).Field("Avoidcount").SetValue(original);
    }
}
