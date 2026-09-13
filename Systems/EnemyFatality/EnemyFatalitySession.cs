using UnityEngine.SceneManagement;

namespace NoREroMod.Systems.EnemyFatality;

/// <summary>Shared runtime lock for one active enemy fatality at a time.</summary>
internal static class EnemyFatalitySession
{
    private static bool _sceneHooked;

    public static bool IsActive { get; private set; }

    /// <summary>Clip folder relative path chosen for the active fatality (empty when idle).</summary>
    public static string ActiveClipRelative { get; private set; }

    internal static void EnsureSceneHook()
    {
        if (_sceneHooked)
            return;

        _sceneHooked = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    internal static void Begin(string clipRelative = null)
    {
        IsActive = true;
        ActiveClipRelative = string.IsNullOrEmpty(clipRelative) ? null : clipRelative.Trim();
        EnemyFatalityConfig.LogDebug(
            "[EnemyFatality] Session begin"
            + (string.IsNullOrEmpty(ActiveClipRelative)
                ? string.Empty
                : " clip=" + ActiveClipRelative));
    }

    internal static void End()
    {
        EnemyFatalityTaunts.CancelPending();
        EnemyFatalityPlayback.DestroyActiveFatalityIcon();
        EnemyFatalityEroSuppression.RestoreCombatAi();
        EnemyFatalityVanillaDeathMute.End();
        IsActive = false;
        ActiveClipRelative = null;
        EnemyFatalityConfig.LogDebug("[EnemyFatality] Session end");
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        End();
    }
}
