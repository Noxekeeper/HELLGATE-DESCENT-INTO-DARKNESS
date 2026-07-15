using System;
using System.Reflection;

namespace NoREroMod.Systems.Compatibility;

/// <summary>
/// Copies HellGate <c>NoREroMod_HellGate.cfg</c> values into NoREroMod scaffold runtime fields
/// so players edit one cfg file. NoREroMod.cfg should contain only [Enemies] and [Elites].
/// </summary>
internal static class NoREroModScaffoldConfigPush
{
    private sealed class FieldPair
    {
        internal readonly string HellGateField;
        internal readonly string NoREroModField;
        internal FieldPair(string hellGateField, string noREroModField)
        {
            HellGateField = hellGateField;
            NoREroModField = noREroModField;
        }
    }

    private static readonly FieldPair[] FieldMap =
    {
        new FieldPair("pleasureAfterOrgasm", "pleasureAfterOrgasm"),
        new FieldPair("pleasureEnemyAttackMax", "pleasureEnemyAttackMax"),
        new FieldPair("pleasureEnemyAttackMin", "pleasureEnemyAttackMin"),
        new FieldPair("pleasurePlayerAttackMax", "pleasurePlayerAttackMax"),
        new FieldPair("pleasurePlayerAttackMin", "pleasurePlayerAttackMin"),
        new FieldPair("pleasureAttackSpeedMax", "pleasureAttackSpeedMax"),
        new FieldPair("pleasureAttackSpeedMin", "pleasureAttackSpeedMin"),
        new FieldPair("pleasureGainOnEro", "pleasureGainOnEro"),
        new FieldPair("pleasureGainOnHit", "pleasureGainOnHit"),
        new FieldPair("pleasureLossOnHit", "pleasureLossOnHit"),
        new FieldPair("pleasureGainOnBlock", "pleasureGainOnBlock"),
        new FieldPair("pleasureGainOnDown", "pleasureGainOnDown"),
        new FieldPair("enablePregnancy", "enablePregnancy"),
        new FieldPair("enableAnyPregnancy", "enableAnyPregnancy"),
        new FieldPair("extraBirthChance", "extraBirthChance"),
        new FieldPair("disablePleasureParalysis", "disablePleasureParalysis"),
        new FieldPair("orgasmFlashStrength", "orgasmFlashStrength"),
        new FieldPair("hpLosePerSec", "hpLosePerSec"),
        new FieldPair("hpLoseOnCreampie", "hpLoseOnCreampie"),
        new FieldPair("enableDelevel", "enableDelevel"),
        new FieldPair("expLosePerSec", "expLosePerSec"),
        new FieldPair("expLoseOnCreampie", "expLoseOnCreampie"),
        new FieldPair("animationExpLoseMulti", "animationExpLoseMulti"),
        new FieldPair("expDelevelRefundPercent", "expDelevelRefundPercent"),
        new FieldPair("pleasureSPRegenMax", "pleasureSPRegenMax"),
        new FieldPair("pleasureSPRegenMin", "pleasureSPRegenMin"),
        new FieldPair("spLosePercentOnEroEvent", "spLosePercentOnEroEvent"),
        new FieldPair("spPercentGainOnStruggleDown", "spPercentGainOnStruggleDown"),
        new FieldPair("spPercentGainOnStruggleEro", "spPercentGainOnStruggleEro"),
        new FieldPair("spPercentLoseOnBadStruggleEro", "spPercentLoseOnBadStruggleEro"),
        new FieldPair("animationHPDamageMulti", "animationHPDamageMulti"),
        new FieldPair("animationPleasureDamageMulti", "animationPleasureDamageMulti"),
        new FieldPair("easyStruggleCount", "easyStruggleCount"),
        new FieldPair("fatalityDifficulty", "fatalityDifficulty"),
        new FieldPair("fatalityEasyStruggles", "fatalityEasyStruggles"),
        new FieldPair("bossEasyStruggles", "bossEasyStruggles"),
        new FieldPair("bossStruggleFatigue", "bossStruggleFatigue"),
        new FieldPair("enemyHealthEffectiveness", "enemyHealthEffectiveness"),
        new FieldPair("playerHealthEffectiveness", "playerHealthEffectiveness"),
        new FieldPair("spFactorEffectiveness", "spFactorEffectiveness"),
        new FieldPair("playerMpEffectiveness", "playerMpEffectiveness"),
        new FieldPair("playerPleasureEffectiveness", "playerPleasureEffectiveness"),
        new FieldPair("allowStrugglePotion", "allowStrugglePotion"),
        new FieldPair("mpGainPerHit", "mpGainPerHit"),
        new FieldPair("spCostPerGuard", "spCostPerGuard"),
        new FieldPair("spCostPerDash", "spCostPerDash"),
        new FieldPair("spRegenIdle", "spRegenIdle"),
        new FieldPair("spRegenGuard", "spRegenGuard"),
        new FieldPair("hiddenHPBars", "hiddenHPBars"),
        new FieldPair("enableFoV", "enableFoV"),
        new FieldPair("frontViewDistance", "frontViewDistance"),
        new FieldPair("backViewDistance", "backViewDistance"),
        new FieldPair("isHardcoreMode", "isHardcoreMode"),
        new FieldPair("trappedSavePoints", "trappedSavePoints"),
        new FieldPair("shrinesRetoreVirginity", "shrinesRetoreVirginity"),
        new FieldPair("enableAradiaScenePOV", "enableAradiaScenePOV"),
    };

    internal static void Apply()
    {
        try
        {
            Type norType = Type.GetType("NoREroMod.Plugin, NoREroMod");
            if (norType == null)
            {
                Plugin.Log?.LogWarning("[NoREroModScaffold] NoREroMod.Plugin not found — scaffold push skipped.");
                return;
            }

            Type hgType = typeof(Plugin);
            int pushed = 0;

            foreach (FieldPair pair in FieldMap)
            {
                if (!TryPushField(hgType, norType, pair.HellGateField, pair.NoREroModField))
                    continue;
                pushed++;
            }

            Plugin.Log?.LogInfo($"[NoREroModScaffold] Pushed {pushed} cfg values from NoREroMod_HellGate.cfg → NoREroMod runtime.");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[NoREroModScaffold] Push failed: {ex.Message}");
        }
    }

    private static bool TryPushField(Type hgType, Type norType, string hgFieldName, string norFieldName)
    {
        FieldInfo hgField = hgType.GetField(hgFieldName, BindingFlags.Public | BindingFlags.Static);
        FieldInfo norField = norType.GetField(norFieldName, BindingFlags.Public | BindingFlags.Static);
        if (hgField == null || norField == null)
            return false;

        object hgBox = hgField.GetValue(null);
        if (hgBox == null)
            return false;

        object value = ReadConfigOrScalar(hgBox);
        if (value == null || norField.FieldType != value.GetType())
            return false;

        norField.SetValue(null, value);
        return true;
    }

    private static object ReadConfigOrScalar(object hgBox)
    {
        PropertyInfo valueProp = hgBox.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
        return valueProp != null ? valueProp.GetValue(hgBox, null) : hgBox;
    }
}
