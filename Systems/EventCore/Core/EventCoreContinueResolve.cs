namespace NoREroMod.Systems.EventCore.Core;

/// <summary>
/// Single exit path for a completed modal step. Hooks run every time; gameplay freeze is released only when the session ends.
/// </summary>
internal static class EventCoreContinueResolve
{
    internal delegate void ContinueHook();

    internal static event ContinueHook OnStepContinue;

    internal static void RaiseStepContinue()
    {
        OnStepContinue?.Invoke();
    }

    /// <summary>
    /// Call when the entire EventCore session has no further modal steps.
    /// </summary>
    internal static void CompleteSession()
    {
        EventCorePause.EndSessionFreeze();
    }
}
