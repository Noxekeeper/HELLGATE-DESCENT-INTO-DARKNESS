using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.PlayerRespawn;

/// <summary>
/// Vanilla death soul ("Idea"): <c>IdeaFall</c> from <see cref="IdeaLost.Dropidea"/> (HARD, difficulty 2).
/// Rises <see cref="RiseHeight"/> world units above the death spot and stays there (kinematic until pickup).
/// </summary>
[HarmonyPatch(typeof(PlayerStatus), nameof(PlayerStatus.REstart_menu))]
internal static class PlayerDeathSoulRestartMenuPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        int diff = StaticMng.GameDifficulty;
        if (diff != 2 && diff != 3) return;
        if (Plugin.Instance == null) return;

        Plugin.Instance.StartCoroutine(PlayerDeathSoulRise.RiseAfterDeath());
    }
}

internal static class PlayerDeathSoulRise
{
    internal const float RiseHeight = 3.5f;
    private const float RiseDuration = 0.35f;

    internal static IEnumerator RiseAfterDeath()
    {
        // Let vanilla Instantiate + Start() finish (FallIdeaMovePlayer, colliders).
        yield return null;
        yield return null;

        var soul = FindDeathSoul();
        if (soul == null)
        {
            Plugin.Log?.LogWarning("[PlayerDeathSoul] No soul object found after death.");
            yield break;
        }

        var rb = soul.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
        }

        Vector3 start = soul.transform.position;
        Vector3 end = start + Vector3.up * RiseHeight;
        float elapsed = 0f;

        while (elapsed < RiseDuration && soul != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / RiseDuration);
            soul.transform.position = Vector3.Lerp(start, end, t);
            if (rb != null)
                rb.velocity = Vector2.zero;
            yield return null;
        }

        if (soul == null) yield break;

        soul.transform.position = end;
        if (rb != null)
            rb.velocity = Vector2.zero;

        if (StaticMng.Idea_fall)
            StaticMng.IdeaPos = new Vector2(end.x, end.y);

        Plugin.Log?.LogInfo($"[PlayerDeathSoul] Soul rose to y={end.y:F2} (+{RiseHeight} from death).");
    }

    private static GameObject FindDeathSoul()
    {
        var named = GameObject.Find("IdeaFall");
        if (named != null) return named;

        FallIdeaMovePlayer[] movers = Object.FindObjectsOfType<FallIdeaMovePlayer>();
        if (movers.Length == 1) return movers[0].gameObject;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector3 refPos = player != null ? player.transform.position : Vector3.zero;

        FallIdeaMovePlayer best = null;
        float bestDist = float.MaxValue;
        foreach (var m in movers)
        {
            float d = Vector2.Distance(m.transform.position, refPos);
            if (d >= bestDist) continue;
            bestDist = d;
            best = m;
        }

        return best != null ? best.gameObject : null;
    }
}
