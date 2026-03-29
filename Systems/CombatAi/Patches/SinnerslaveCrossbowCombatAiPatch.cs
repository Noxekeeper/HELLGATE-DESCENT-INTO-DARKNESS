using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using NoREroMod.Systems.CombatAi;

namespace NoREroMod.Systems.CombatAi.Patches;

/// <summary>
/// Dorei AI (SinnerslaveCrossbow): no flee, on approach goes to melee attacks.
/// Type and methods from in-game Assembly-CSharp so patch targets correctly.
/// Config: BepInEx/plugins/HellGateJson/CombatAi/CombatAi.json (DoreiEnable, DoreiDisableFlee, ...).
/// </summary>
internal static class SinnerslaveCrossbowCombatAiPatch
{
    private static bool _loggedStartup;

    private static Type _gameDoreiType;
    private static Type _gameStateEnumType;

    private static bool _applied;

    internal static void Apply(Harmony harmony)
    {
        if (_applied) return;
        Assembly gameAsm = null;
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            try { if (a.GetName().Name == "Assembly-CSharp") { gameAsm = a; break; } } catch { }
        }
        if (gameAsm == null)
        {
            Plugin.Log?.LogWarning("[CombatAi.Dorei] Assembly-CSharp not found (will retry on scene load).");
            return;
        }

        _gameDoreiType = gameAsm.GetType("SinnerslaveCrossbow");
        if (_gameDoreiType == null)
        {
            try { _gameDoreiType = gameAsm.GetTypes().First(t => t.Name == "SinnerslaveCrossbow"); }
            catch { }
        }
        if (_gameDoreiType == null)
        {
            Plugin.Log?.LogWarning("[CombatAi.Dorei] Type SinnerslaveCrossbow not found. Dorei patches skipped.");
            return;
        }

        _gameStateEnumType = _gameDoreiType.GetNestedType("enemystate", BindingFlags.Public | BindingFlags.NonPublic);
        if (_gameStateEnumType == null)
        {
            Plugin.Log?.LogWarning("[CombatAi.Dorei] enemystate enum not found. Dorei patches skipped.");
            return;
        }

        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var allMethods = _gameDoreiType.GetMethods(flags).Select(m => m.Name).ToList();
        Plugin.Log?.LogInfo($"[CombatAi.Dorei] SinnerslaveCrossbow methods (sample): " + string.Join(", ", allMethods.Where(n => n.Contains("fun") || n.Contains("Run") || n.Contains("Move") || n.Contains("Idle")).Take(20).ToArray()));

        MethodInfo runMethod = null, moveMethod = null, idleMethod = null, move2Method = null, moveoutMethod = null, attackKindMethod = null, updateMethod = null;
        foreach (var m in _gameDoreiType.GetMethods(flags))
        {
            if (runMethod == null && (m.Name == "fun_Run" || m.Name == "Fun_Run")) runMethod = m;
            if (moveMethod == null && (m.Name == "fun_move" || m.Name == "Fun_move")) moveMethod = m;
            if (idleMethod == null && (m.Name == "fun_Idle" || m.Name == "Fun_Idle")) idleMethod = m;
            if (move2Method == null && (m.Name == "fun_MOVE" || m.Name == "Fun_MOVE")) move2Method = m;
            if (moveoutMethod == null && (m.Name == "moveout" || m.Name == "Moveout")) moveoutMethod = m;
            if (attackKindMethod == null && m.Name == "AttackKind") attackKindMethod = m;
            if (updateMethod == null && m.Name == "Update") updateMethod = m;
        }
        if (runMethod == null) runMethod = AccessTools.Method(_gameDoreiType, "fun_Run");
        if (moveMethod == null) moveMethod = AccessTools.Method(_gameDoreiType, "fun_move");
        if (idleMethod == null) idleMethod = AccessTools.Method(_gameDoreiType, "fun_Idle");
        if (move2Method == null) move2Method = AccessTools.Method(_gameDoreiType, "fun_MOVE");
        if (moveoutMethod == null) moveoutMethod = AccessTools.Method(_gameDoreiType, "moveout");
        if (attackKindMethod == null) attackKindMethod = AccessTools.Method(_gameDoreiType, "AttackKind");
        if (updateMethod == null) updateMethod = AccessTools.Method(_gameDoreiType, "Update");

        var prefixRun = AccessTools.Method(typeof(SinnerslaveCrossbowCombatAiPatch), nameof(fun_Run_Prefix));
        var postfixMove = AccessTools.Method(typeof(SinnerslaveCrossbowCombatAiPatch), nameof(fun_move_Postfix));
        var postfixIdle = AccessTools.Method(typeof(SinnerslaveCrossbowCombatAiPatch), nameof(fun_Idle_Postfix));
        var postfixMove2 = AccessTools.Method(typeof(SinnerslaveCrossbowCombatAiPatch), nameof(fun_MOVE_Postfix));
        var prefixAttackKind = AccessTools.Method(typeof(SinnerslaveCrossbowCombatAiPatch), nameof(AttackKind_Prefix));
        var postfixMoveout = AccessTools.Method(typeof(SinnerslaveCrossbowCombatAiPatch), nameof(moveout_Postfix));
        var postfixUpdate = AccessTools.Method(typeof(SinnerslaveCrossbowCombatAiPatch), nameof(Update_Postfix));

        int patched = 0;
        if (runMethod != null && prefixRun != null) { harmony.Patch(runMethod, prefix: new HarmonyMethod(prefixRun)); patched++; }
        if (moveMethod != null && postfixMove != null) { harmony.Patch(moveMethod, postfix: new HarmonyMethod(postfixMove)); patched++; }
        if (idleMethod != null && postfixIdle != null) { harmony.Patch(idleMethod, postfix: new HarmonyMethod(postfixIdle)); patched++; }
        if (move2Method != null && postfixMove2 != null) { harmony.Patch(move2Method, postfix: new HarmonyMethod(postfixMove2)); patched++; }
        if (attackKindMethod != null && prefixAttackKind != null) { harmony.Patch(attackKindMethod, prefix: new HarmonyMethod(prefixAttackKind)); patched++; }
        if (moveoutMethod != null && postfixMoveout != null) { harmony.Patch(moveoutMethod, postfix: new HarmonyMethod(postfixMoveout)); patched++; }
        if (updateMethod != null && postfixUpdate != null) { harmony.Patch(updateMethod, postfix: new HarmonyMethod(postfixUpdate)); patched++; }

        _applied = true;
        Plugin.Log?.LogInfo($"[CombatAi] Dorei patches applied: {patched} methods total (Run, move, Idle, MOVE, AttackKind, moveout, Update). Config: DoreiEnable={DoreiCombatAiConfig.Enable}, DoreiDisableFlee={DoreiCombatAiConfig.DisableFlee}");
    }

    private static object GetState(string name)
    {
        if (_gameStateEnumType == null) return null;
        try { return Enum.Parse(_gameStateEnumType, name); } catch { return null; }
    }

    /// <summary>Blocks flee (RUN): substitute with WALK + Choose=1.</summary>
    [HarmonyPrefix]
    public static bool fun_Run_Prefix(object __instance)
    {
        if (!DoreiCombatAiConfig.Enable || !DoreiCombatAiConfig.DisableFlee)
            return true;

        if (!_loggedStartup) { _loggedStartup = true; Plugin.Log?.LogInfo("[CombatAi.Dorei] fun_Run_Prefix called — Dorei AI active (no flee, prefer melee)."); }
        var t = Traverse.Create(__instance);
        t.Field("state").SetValue(GetState("WALK"));
        t.Field("Choose").SetValue(1);
        return false;
    }

    /// <summary>Prevent choosing ranged in melee: in melee zone — WALK or ATK3.</summary>
    [HarmonyPrefix]
    public static bool AttackKind_Prefix(object __instance)
    {
        if (!DoreiCombatAiConfig.Enable || !DoreiCombatAiConfig.DisableFlee)
            return true;

        var tr = Traverse.Create(__instance);
        float dist = Mathf.Abs(tr.Field("distance").GetValue<float>());
        float distY = Mathf.Abs(tr.Field("distance_y").GetValue<float>());
        float atk = tr.Field("Atkdistance").GetValue<float>();
        float threshold = DoreiCombatAiConfig.MeleeRangeThreshold;

        if (dist > threshold)
            return true;

        if (dist < 2f && distY < atk + 1f && UnityEngine.Random.value <= DoreiCombatAiConfig.PreferMeleeOverFleeChance)
        {
            tr.Field("state").SetValue(GetState("ATK3"));
            tr.Field("Choose").SetValue(10);
        }
        else
        {
            tr.Field("state").SetValue(GetState("WALK"));
            tr.Field("Choose").SetValue(1);
        }
        return false;
    }

    /// <summary>After moveout(): if just chose ranged (ATK/ATKDOWN/ATKUP) but in melee zone — switch to WALK or ATK3.</summary>
    [HarmonyPostfix]
    public static void moveout_Postfix(object __instance)
    {
        if (!DoreiCombatAiConfig.Enable || !DoreiCombatAiConfig.DisableFlee)
            return;

        var tr = Traverse.Create(__instance);
        object state = tr.Field("state").GetValue();
        object atk = GetState("ATK"), atkDown = GetState("ATKDOWN"), atkUp = GetState("ATKUP");
        bool isRanged = (atk != null && state != null && state.Equals(atk)) ||
            (atkDown != null && state != null && state.Equals(atkDown)) ||
            (atkUp != null && state != null && state.Equals(atkUp));
        if (!isRanged) return;

        float dist = Mathf.Abs(tr.Field("distance").GetValue<float>());
        float distY = Mathf.Abs(tr.Field("distance_y").GetValue<float>());
        float atkDist = tr.Field("Atkdistance").GetValue<float>();
        float threshold = DoreiCombatAiConfig.MeleeRangeThreshold;

        if (dist >= threshold) return;

        if (dist < 2f && distY < atkDist + 1f && UnityEngine.Random.value <= DoreiCombatAiConfig.PreferMeleeOverFleeChance)
        {
            tr.Field("state").SetValue(GetState("ATK3"));
            tr.Field("Choose").SetValue(10);
        }
        else
        {
            tr.Field("state").SetValue(GetState("WALK"));
            tr.Field("Choose").SetValue(1);
        }
    }

    [HarmonyPostfix]
    public static void fun_move_Postfix(object __instance)
    {
        if (!DoreiCombatAiConfig.Enable || !DoreiCombatAiConfig.DisableFlee)
            return;

        var tr = Traverse.Create(__instance);
        float dist = Mathf.Abs(tr.Field("distance").GetValue<float>());
        if (dist <= 8f) return;

        object state = tr.Field("state").GetValue();
        object runState = GetState("RUN");
        object blankState = GetState("BLANK");
        if (state == null || (runState != null && state.Equals(runState)) || (blankState != null && state.Equals(blankState)))
        {
            float distY = Mathf.Abs(tr.Field("distance_y").GetValue<float>());
            float atk = tr.Field("Atkdistance").GetValue<float>();
            if (dist <= DoreiCombatAiConfig.MeleeRangeThreshold && distY <= atk + 2f &&
                UnityEngine.Random.value <= DoreiCombatAiConfig.PreferMeleeOverFleeChance)
            {
                tr.Field("state").SetValue(GetState("ATK3"));
                tr.Field("Choose").SetValue(10);
            }
            else
            {
                tr.Field("state").SetValue(GetState("WALK"));
                tr.Field("Choose").SetValue(1);
            }
        }
    }

    [HarmonyPostfix]
    public static void fun_Idle_Postfix(object __instance)
    {
        if (!DoreiCombatAiConfig.Enable) return;

        object state = Traverse.Create(__instance).Field("state").GetValue();
        object atk = GetState("ATK"), atkDown = GetState("ATKDOWN"), atkUp = GetState("ATKUP");
        bool isRanged = (atk != null && state != null && state.Equals(atk)) ||
            (atkDown != null && state != null && state.Equals(atkDown)) ||
            (atkUp != null && state != null && state.Equals(atkUp));
        if (!isRanged) return;

        var tr = Traverse.Create(__instance);
        float dist = Mathf.Abs(tr.Field("distance").GetValue<float>());
        if (dist > DoreiCombatAiConfig.MeleeRangeThreshold) return;

        tr.Field("state").SetValue(GetState("WALK"));
    }

    [HarmonyPostfix]
    public static void fun_MOVE_Postfix(object __instance)
    {
        if (!DoreiCombatAiConfig.Enable) return;

        float mult = DoreiCombatAiConfig.MeleeAttackRateMultiplier;
        if (mult <= 1f) return;

        var tr = Traverse.Create(__instance);
        float dist = Mathf.Abs(tr.Field("distance").GetValue<float>());
        float distY = Mathf.Abs(tr.Field("distance_y").GetValue<float>());
        float atk = tr.Field("Atkdistance").GetValue<float>();
        if (dist > DoreiCombatAiConfig.MeleeRangeThreshold || distY > atk + 2f) return;

        float extra = (mult - 1f) * Time.deltaTime;
        tr.Field("statecount").SetValue(tr.Field("statecount").GetValue<float>() + extra);
        var sc2 = tr.Field("statecount2");
        var sc3 = tr.Field("statecount3");
        if (sc2 != null) sc2.SetValue(sc2.GetValue<float>() + extra);
        if (sc3 != null) sc3.SetValue(sc3.GetValue<float>() + extra);
    }

    /// <summary>
    /// Hard Dorei control: if closer than threshold — no flee, no kite, go melee.
    /// Runs after original Update every frame.
    /// </summary>
    [HarmonyPostfix]
    public static void Update_Postfix(object __instance)
    {
        if (!DoreiCombatAiConfig.Enable || !DoreiCombatAiConfig.DisableFlee)
            return;

        var tr = Traverse.Create(__instance);

        float dist  = Mathf.Abs(tr.Field("distance").GetValue<float>());
        float distY = Mathf.Abs(tr.Field("distance_y").GetValue<float>());
        float atk   = tr.Field("Atkdistance").GetValue<float>();
        float thr   = DoreiCombatAiConfig.MeleeRangeThreshold;

        // Beyond threshold — stay back.
        if (dist >= thr)
            return;

        object state    = tr.Field("state").GetValue();
        object idle     = GetState("IDLE");
        object walk     = GetState("WALK");
        object run      = GetState("RUN");
        object blank    = GetState("BLANK");
        object death    = GetState("DEATH");
        object dmg      = GetState("DAMAGE");
        object dmg2     = GetState("DAMAGE2");
        object dmg3     = GetState("DAMAGE3");
        object erowalk  = GetState("EROWALK");
        object stabbl   = GetState("STABBLANK");
        object step     = GetState("STEP");
        object atkState = GetState("ATK");
        object atk3     = GetState("ATK3");
        object atkDown  = GetState("ATKDOWN");
        object atkUp    = GetState("ATKUP");
        object atk4     = GetState("ATK4");
        object atk2     = GetState("ATK2");

        // Don't break death/damage/ero/grab/already-running attack.
        if (state != null && (
            state.Equals(death)   || state.Equals(dmg)   || state.Equals(dmg2)  || state.Equals(dmg3) ||
            state.Equals(erowalk) || state.Equals(stabbl)|| state.Equals(step)  ||
            state.Equals(atkState)|| state.Equals(atk3)  || state.Equals(atkDown) ||
            state.Equals(atkUp)   || state.Equals(atk4)  || state.Equals(atk2)))
            return;

        // Very close — force melee attack.
        if (dist < Mathf.Min(thr, atk + 1f) && distY < atk + 2f)
        {
            tr.Field("state").SetValue(atk3);
            tr.Field("Choose").SetValue(10);
            tr.Field("enmATKnow").SetValue(true);
            return;
        }

        // In zone <threshold but not point-blank — no RUN/BLANK/IDLE, force approach step by step.
        if (state != null && (state.Equals(run) || state.Equals(blank) || state.Equals(idle)))
        {
            tr.Field("state").SetValue(walk);
            tr.Field("Choose").SetValue(1);
            tr.Field("enmATKnow").SetValue(false);
        }
    }
}
