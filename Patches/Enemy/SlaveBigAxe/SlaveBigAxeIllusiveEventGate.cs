using UnityEngine;
using UnityEngine.SceneManagement;

namespace NoREroMod.Patches.Enemy;

/// <summary>
/// Illusive rescue / Rodenia Underground Church event
/// (<c>EvuChurch</c> → battle → <c>EvuChurchAfter</c> / bad path <c>EvuChurchSP</c>).
/// The two scripted global::<see cref="global::SlaveBigAxe"/> in those scenes must stay on the
/// vanilla combat / post-H path — HellGate MeatArmor, DeadArmor, Fatality,
/// H-dialogue overlays, Church auto-faction, etc. are skipped while this gate is active.
/// </summary>
internal static class SlaveBigAxeIllusiveEventGate
{
    private static string _resolvedKey = string.Empty;
    private static bool _skipHellGate;
    private static bool _haveResult;

    /// <summary>
    /// True while the Illusive / SlaveBigAxe church event is the active context.
    /// </summary>
    internal static bool ShouldSkipHellGateLogic()
    {
        string key = BuildContextKey();
        if (_haveResult && string.Equals(key, _resolvedKey, System.StringComparison.Ordinal))
        {
            return _skipHellGate;
        }

        _resolvedKey = key;
        _haveResult = true;
        _skipHellGate = MatchesEventName(GetIdeaNowScene())
            || MatchesEventName(GetActiveSceneName())
            || HasEventManagers();
        return _skipHellGate;
    }

    /// <summary>
    /// Battle scene only — lose H must hand off to <c>EvuChurchSP</c>.
    /// Uses <see cref="StaticMng.Idea_Nowscene"/> (same as spawn zone logs) plus active scene /
    /// battle managers. Excludes SP (<see cref="EVslaveBigAxwMng"/>) and After.
    /// </summary>
    internal static bool IsChurchBattleLoseHandoffScene()
    {
        if (HasSlaveBigAxeSpManager())
        {
            return false;
        }

        if (IsExactBattleSceneName(GetIdeaNowScene()) || IsExactBattleSceneName(GetActiveSceneName()))
        {
            return true;
        }

        // Additive / rename: battle still running with Talk+Battle managers, not SP.
        return Object.FindObjectOfType<EvBigAxeBattleMng>() != null
            && Object.FindObjectOfType<EvBigAxeTalkMng>() != null;
    }

    /// <summary>Call on scene load so the next query re-resolves.</summary>
    internal static void Invalidate()
    {
        _haveResult = false;
        _resolvedKey = string.Empty;
        _skipHellGate = false;
    }

    private static string BuildContextKey()
    {
        return GetActiveSceneName() + "|" + GetIdeaNowScene();
    }

    private static string GetActiveSceneName()
    {
        try
        {
            return SceneManager.GetActiveScene().name ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetIdeaNowScene()
    {
        try
        {
            return StaticMng.Idea_Nowscene ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool MatchesEventName(string scene)
    {
        if (string.IsNullOrEmpty(scene))
        {
            return false;
        }

        return scene.IndexOf("EvuChurch", System.StringComparison.OrdinalIgnoreCase) >= 0
            || scene.IndexOf("EVuChurch", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>True for EvuChurch / EVuChurch only — not SP / After.</summary>
    private static bool IsExactBattleSceneName(string scene)
    {
        if (string.IsNullOrEmpty(scene))
        {
            return false;
        }

        return scene.Equals("EvuChurch", System.StringComparison.OrdinalIgnoreCase)
            || scene.Equals("EVuChurch", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasSlaveBigAxeSpManager()
    {
        try
        {
            return Object.FindObjectOfType<EVslaveBigAxwMng>() != null;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasEventManagers()
    {
        try
        {
            if (Object.FindObjectOfType<EvBigAxeTalkMng>() != null)
            {
                return true;
            }

            if (Object.FindObjectOfType<EvBigAxeBattleMng>() != null)
            {
                return true;
            }

            if (Object.FindObjectOfType<EVslaveBigAxwMng>() != null)
            {
                return true;
            }
        }
        catch
        {
        }

        return false;
    }
}
