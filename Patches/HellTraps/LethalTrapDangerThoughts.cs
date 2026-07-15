using System.Collections;
using NoREroMod.Systems.Dialogue;
using UnityEngine;

namespace NoREroMod.Patches.HellTraps;

/// <summary>
/// Proximity anchor around lethal traps: shows Aradia danger thoughts near trap.
/// Phrases: <c>EventCore/&lt;Lang&gt;/event_trap_gate/phrases.json</c> → <c>lethalTrapThoughts</c>.
/// </summary>
internal static class LethalTrapDangerThoughts
{
    internal const float ThoughtDurationSeconds = 3f;
    internal const float ThoughtVerticalOffsetPx = 32f;
    /// <summary>Lift along Spine bone Y (skeleton space), not screen pixels.</summary>
    internal const float ThoughtBoneWorldOffsetY = 0.3f;

    /// <summary>Dusty white text (#EAE3D2) for lethal-trap anxiety thoughts.</summary>
    internal static readonly Color ThoughtTextColor = new Color(234f / 255f, 227f / 255f, 210f / 255f, 1f);

    internal static readonly Color ThoughtOutlineColor = new Color(0f, 0f, 0f, 1f);
    internal const float ThoughtCooldownSeconds = 10f;
    internal const float TriggerRadius = 8f;

    internal static void EnsureAnchor(GameObject trap, string sourceTag)
    {
        if (trap == null)
            return;

        var anchor = trap.GetComponent<LethalTrapDangerThoughtAnchor>();
        if (anchor == null)
            anchor = trap.AddComponent<LethalTrapDangerThoughtAnchor>();
        anchor.SourceTag = sourceTag;
    }

    internal static string ResolveThoughtLine()
    {
        if (LethalTrapThoughtPhrases.TryGetRandomLine(out string line) &&
            !string.IsNullOrEmpty(line))
        {
            return NormalizeLine(line);
        }

        return string.Empty;
    }

    private static string NormalizeLine(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        string s = raw.Trim();
        if (s.Length == 0)
            return string.Empty;

        int cut = -1;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '.' || c == '!' || c == '?')
            {
                cut = i + 1;
                break;
            }
        }

        if (cut > 0)
            s = s.Substring(0, cut).Trim();

        const int maxLen = 180;
        if (s.Length > maxLen)
            s = s.Substring(0, maxLen).TrimEnd() + "...";

        return s;
    }
}

internal sealed class LethalTrapDangerThoughtAnchor : MonoBehaviour
{
    internal string SourceTag = "LethalTrap";
    private float _nextCheckAt = -9999f;
    private float _nextThoughtAt = -9999f;
    private Coroutine _heartBeatReleaseCoroutine;
    private const float CheckIntervalSeconds = 0.2f;

    private void Update()
    {
        if (Time.time < _nextCheckAt)
            return;
        _nextCheckAt = Time.time + CheckIntervalSeconds;

        if (Time.time < _nextThoughtAt)
            return;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
            return;

        playercon player = playerObj.GetComponent<playercon>();
        if (player == null || player._Death || player.eroflag || player._eroflag2)
            return;

        float d = Vector2.Distance(
            new Vector2(player.transform.position.x, player.transform.position.y),
            new Vector2(transform.position.x, transform.position.y));
        if (d > LethalTrapDangerThoughts.TriggerRadius)
            return;

        string line = LethalTrapDangerThoughts.ResolveThoughtLine();
        if (string.IsNullOrEmpty(line))
            return;

        try
        {
            if (!DialogueFramework.IsInitialized)
                DialogueFramework.Initialize();
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning("[LethalTrapThought] DialogueFramework init failed: " + ex.Message);
        }

        DialogueDisplay display = DialogueFramework.GetDisplay();
        if (display == null)
            return;

        DialogueStyle style = DialogueDisplay.BuildAradiaThoughtStyle(
            LethalTrapDangerThoughts.ThoughtVerticalOffsetPx,
            0f,
            true);

        float thoughtDuration = LethalTrapDangerThoughts.ThoughtDurationSeconds;
        LethalTrapHeartBeatLoop.AcquireLoop();

        if (_heartBeatReleaseCoroutine != null)
            StopCoroutine(_heartBeatReleaseCoroutine);
        _heartBeatReleaseCoroutine = StartCoroutine(ReleaseHeartBeatAfterThought(thoughtDuration));

        display.ShowAradiaThought(
            playerObj,
            line,
            "hair1",
            style,
            thoughtDuration,
            disableBoneFallbacks: false,
            boneWorldOffsetY: LethalTrapDangerThoughts.ThoughtBoneWorldOffsetY,
            textColor: LethalTrapDangerThoughts.ThoughtTextColor,
            outlineColor: LethalTrapDangerThoughts.ThoughtOutlineColor);

        _nextThoughtAt = Time.time + LethalTrapDangerThoughts.ThoughtCooldownSeconds;
        Plugin.Log?.LogInfo(
            "[LethalTrapThought] "
            + SourceTag
            + ": r<="
            + LethalTrapDangerThoughts.TriggerRadius.ToString("0.#")
            + ", next in "
            + LethalTrapDangerThoughts.ThoughtCooldownSeconds.ToString("0.#")
            + "s, line='"
            + line
            + "'");
    }

    private IEnumerator ReleaseHeartBeatAfterThought(float durationSeconds)
    {
        if (durationSeconds > 0f)
            yield return new WaitForSeconds(durationSeconds);

        LethalTrapHeartBeatLoop.ReleaseLoop();
        _heartBeatReleaseCoroutine = null;
    }

    private void OnDisable()
    {
        if (_heartBeatReleaseCoroutine != null)
        {
            StopCoroutine(_heartBeatReleaseCoroutine);
            _heartBeatReleaseCoroutine = null;
        }

        LethalTrapHeartBeatLoop.ReleaseLoop();
    }
}
