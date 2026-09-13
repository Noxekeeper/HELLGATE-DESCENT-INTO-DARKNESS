using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace NoREroMod.Patches.UI.MindBroken;

/// <summary>
/// Gates real-time MindBroken progress: Unity player window must be foreground,
/// and post-unpause unscaledDeltaTime hitches (full minimize gap) are discarded.
/// </summary>
internal static class MindBrokenRealtimeGate
{
    private const string UnityWindowClass = "UnityWndClass";
    private const float MaxAdvanceStep = 0.1f;

    private static readonly uint OurProcessId = (uint)Process.GetCurrentProcess().Id;
    private static IntPtr _unityHwnd = IntPtr.Zero;
    private static float _nextResolveUnscaledTime;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    internal static bool ApplicationPaused { get; set; }

    internal static bool CanAdvance
    {
        get
        {
            try
            {
                if (ApplicationPaused)
                    return false;

                IntPtr foreground = GetForegroundWindow();
                if (foreground == IntPtr.Zero)
                    return false;

                GetWindowThreadProcessId(foreground, out uint foregroundPid);
                if (foregroundPid != OurProcessId)
                    return false;

                if (IsIconic(foreground) || IsConsoleWindow(foreground))
                    return false;

                IntPtr unity = ResolveUnityWindow();
                if (unity == IntPtr.Zero)
                    return IsWindowVisible(foreground);

                return !IsIconic(unity) && foreground == unity;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Unscaled delta for countdown / unscaled MB ticks. Zero while unfocused;
    /// hitch frames after minimize (dt &gt; MaxAdvanceStep) are dropped.
    /// </summary>
    internal static float GetClampedUnscaledDelta()
    {
        if (!CanAdvance)
            return 0f;

        float dt = Time.unscaledDeltaTime;
        return dt > MaxAdvanceStep ? 0f : dt;
    }

    /// <summary>
    /// Scaled delta for H-scene MB ticks, with the same focus / hitch rules.
    /// </summary>
    internal static float GetClampedDeltaTime()
    {
        if (!CanAdvance)
            return 0f;

        float dt = Time.deltaTime;
        return dt > MaxAdvanceStep ? 0f : dt;
    }

    private static IntPtr ResolveUnityWindow()
    {
        if (_unityHwnd != IntPtr.Zero
            && IsWindow(_unityHwnd)
            && IsUnityClass(_unityHwnd)
            && Time.unscaledTime < _nextResolveUnscaledTime)
        {
            return _unityHwnd;
        }

        IntPtr best = IntPtr.Zero;
        IntPtr fallback = IntPtr.Zero;

        EnumWindows((hWnd, _) =>
        {
            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid != OurProcessId || !IsUnityClass(hWnd))
                return true;

            if (fallback == IntPtr.Zero)
                fallback = hWnd;

            if (IsWindowVisible(hWnd) && !IsIconic(hWnd))
            {
                best = hWnd;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        _unityHwnd = best != IntPtr.Zero ? best : fallback;
        _nextResolveUnscaledTime = Time.unscaledTime + 1f;
        return _unityHwnd;
    }

    private static bool IsUnityClass(IntPtr hWnd)
    {
        return string.Equals(GetWindowClass(hWnd), UnityWindowClass, StringComparison.Ordinal);
    }

    private static bool IsConsoleWindow(IntPtr hWnd)
    {
        return string.Equals(GetWindowClass(hWnd), "ConsoleWindowClass", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetWindowClass(IntPtr hWnd)
    {
        var sb = new StringBuilder(64);
        GetClassName(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }
}
