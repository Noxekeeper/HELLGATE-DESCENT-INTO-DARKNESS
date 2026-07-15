using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Patches.Trap;

/// <summary>
/// Diagnostics only. Logs the exact gate state for Black Ooze trap entry without changing behavior.
/// </summary>
internal static class BlackOozeTrapDiagnosticsPatch
{
    private const bool Enabled = false;

    private static float _lastNearbyDamageLogTime;
    private static float _lastProximityLogTime;

    [HarmonyPatch(typeof(BlackOozetrap), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void BlackOozetrap_Enter_Prefix(BlackOozetrap __instance, Collider2D col)
    {
        playercon pc = __instance != null ? __instance.com_player : null;
        bool passGate = col != null &&
                        col.gameObject != null &&
                        col.gameObject.tag == "playerDAMAGEcol" &&
                        pc != null &&
                        !pc.eroflag &&
                        !__instance.eroflag &&
                        pc.erodown != 0;

        LogTrap("BlackOozetrap.Enter", __instance, col, pc, passGate, extra: null);
    }

    [HarmonyPatch(typeof(BlackOozeTrapTypeB), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void BlackOozeTrapTypeB_Enter_Prefix(BlackOozeTrapTypeB __instance, Collider2D col)
    {
        playercon pc = __instance != null ? __instance.com_player : null;
        bool trapflag = __instance != null && Traverse.Create(__instance).Field<bool>("trapflag").Value;
        bool passGate = col != null &&
                        col.gameObject != null &&
                        col.gameObject.tag == "playerDAMAGEcol" &&
                        !trapflag &&
                        pc != null &&
                        !pc.eroflag &&
                        !__instance.eroflag &&
                        !pc.stepfrag;

        LogTrap("BlackOozeTrapTypeB.Enter", __instance, col, pc, passGate, $"trapflag={trapflag}");
    }

    [HarmonyPatch(typeof(playercon), "fun_damage")]
    [HarmonyPrefix]
    private static void FunDamage_Prefix(playercon __instance, int kickbackkind, int getdamedir)
    {
        LogNearbyDamage("fun_damage.Prefix", __instance, kickbackkind, getdamedir);
    }

    [HarmonyPatch(typeof(playercon), "fun_damage_Improvement")]
    [HarmonyPrefix]
    private static void FunDamageImprovement_Prefix(playercon __instance, int kickbackkind, int getdamedir)
    {
        LogNearbyDamage("fun_damage_Improvement.Prefix", __instance, kickbackkind, getdamedir);
    }

    [HarmonyPatch(typeof(playercon), "Update")]
    [HarmonyPostfix]
    private static void PlayerUpdate_Postfix(playercon __instance)
    {
        if (__instance == null || __instance.eroflag)
            return;

        if (!TryGetNearestOozeTrap(__instance.transform.position, out string trapName, out Vector3 trapPos, out float sqrDistance))
            return;
        if (sqrDistance > 16f)
            return;

        float now = Time.realtimeSinceStartup;
        if (now - _lastProximityLogTime < 0.5f)
            return;
        _lastProximityLogTime = now;

        if (!Enabled)
            return;

        Plugin.Log?.LogInfo(
            $"[OozeTrapDiag] Player.NearTrap: near={trapName}, trapPos={FormatVec(trapPos)}, sqrDist={sqrDistance:0.00}; " +
            $"{DescribePlayer(__instance)}; damageCols={DescribePlayerDamageColliders(__instance)}");
    }

    private static void LogTrap(string source, Trapdata trap, Collider2D col, playercon pc, bool passGate, string extra)
    {
        if (!Enabled)
            return;

        string colInfo = DescribeCollider(col);
        string pcInfo = DescribePlayer(pc);
        string trapInfo = trap != null
            ? $"trapEroflag={trap.eroflag}, trapPos={FormatVec(trap.transform.position)}"
            : "trap=null";

        if (!string.IsNullOrEmpty(extra))
            trapInfo += ", " + extra;

        Plugin.Log?.LogInfo($"[OozeTrapDiag] {source}: passGate={passGate}; {colInfo}; {pcInfo}; {trapInfo}");
    }

    private static void LogNearbyDamage(string source, playercon pc, int kickbackkind, int getdamedir)
    {
        if (!Enabled)
            return;

        if (pc == null || pc.eroflag)
            return;

        if (!TryGetNearestOozeTrap(pc.transform.position, out string trapName, out _, out float sqrDistance))
            return;

        float now = Time.realtimeSinceStartup;
        if (now - _lastNearbyDamageLogTime < 0.15f)
            return;
        _lastNearbyDamageLogTime = now;

        Plugin.Log?.LogInfo(
            $"[OozeTrapDiag] {source}: near={trapName}, sqrDist={sqrDistance:0.00}, kickback={kickbackkind}, dir={getdamedir}; {DescribePlayer(pc)}");
    }

    private static bool TryGetNearestOozeTrap(Vector3 playerPos, out string trapName, out Vector3 trapPos, out float bestSqrDistance)
    {
        trapName = null;
        trapPos = default;
        bestSqrDistance = float.MaxValue;

        foreach (BlackOozetrap trap in Object.FindObjectsOfType<BlackOozetrap>())
            ConsiderTrap(playerPos, trap, ref trapName, ref trapPos, ref bestSqrDistance);

        foreach (BlackOozeTrapTypeB trap in Object.FindObjectsOfType<BlackOozeTrapTypeB>())
            ConsiderTrap(playerPos, trap, ref trapName, ref trapPos, ref bestSqrDistance);

        return trapName != null;
    }

    private static void ConsiderTrap(Vector3 playerPos, Trapdata trap, ref string trapName, ref Vector3 trapPos, ref float bestSqrDistance)
    {
        if (trap == null)
            return;

        float sqrDistance = (trap.transform.position - playerPos).sqrMagnitude;
        if (sqrDistance >= bestSqrDistance)
            return;

        bestSqrDistance = sqrDistance;
        trapName = trap.GetType().Name;
        trapPos = trap.transform.position;
    }

    private static string DescribeCollider(Collider2D col)
    {
        if (col == null)
            return "col=null";

        GameObject go = col.gameObject;
        if (go == null)
            return "col.go=null";

        return $"colName={go.name}, tag={go.tag}, layer={LayerMask.LayerToName(go.layer)}({go.layer}), " +
               $"isTrigger={col.isTrigger}, enabled={col.enabled}, colType={col.GetType().Name}, colPos={FormatVec(go.transform.position)}";
    }

    private static string DescribePlayer(playercon pc)
    {
        if (pc == null)
            return "player=null";

        Rigidbody2D rb = pc.rigi2d;
        string rbInfo = rb != null
            ? $"rbSim={rb.simulated}, rbVel={FormatVec(rb.velocity)}"
            : "rb=null";

        return $"playerState={pc.state}, eroflag={pc.eroflag}, erodown={pc.erodown}, stepfrag={pc.stepfrag}, " +
               $"nowdamage={pc.nowdamage}, grapfrag={pc.grapfrag}, grounded={pc.m_Grounded}, stab={pc._stabnow}, " +
               $"playerLayer={LayerMask.LayerToName(pc.gameObject.layer)}({pc.gameObject.layer}), playerPos={FormatVec(pc.transform.position)}, {rbInfo}";
    }

    private static string DescribePlayerDamageColliders(playercon pc)
    {
        Collider2D[] colliders = pc.GetComponentsInChildren<Collider2D>(true);
        if (colliders == null || colliders.Length == 0)
            return "none";

        string result = "";
        int count = 0;
        foreach (Collider2D col in colliders)
        {
            if (col == null || col.gameObject == null || col.gameObject.tag != "playerDAMAGEcol")
                continue;

            if (count > 0)
                result += " | ";

            Bounds b = col.bounds;
            result += $"{col.gameObject.name}: active={col.gameObject.activeInHierarchy}, enabled={col.enabled}, " +
                      $"layer={LayerMask.LayerToName(col.gameObject.layer)}({col.gameObject.layer}), " +
                      $"pos={FormatVec(col.transform.position)}, boundsCenter={FormatVec(b.center)}, boundsSize={FormatVec(b.size)}";
            count++;
        }

        return count == 0 ? "none" : result;
    }

    private static string FormatVec(Vector2 value)
    {
        return $"({value.x:0.00},{value.y:0.00})";
    }

    private static string FormatVec(Vector3 value)
    {
        return $"({value.x:0.00},{value.y:0.00},{value.z:0.00})";
    }
}
