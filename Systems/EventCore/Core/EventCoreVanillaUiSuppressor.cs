using NoREroMod;
using UnityEngine;

namespace NoREroMod.Systems.EventCore.Core;

/// <summary>
/// Temporarily hides the vanilla HUD canvas while an EventCore session is active.
/// The base game resolves the main HUD via <c>GameObject.Find("Canvas")</c> and <see cref="Canvas"/>.
/// </summary>
internal static class EventCoreVanillaUiSuppressor
{
    /// <summary>
    /// Root canvas name used by the base Night of Revenge HUD.
    /// </summary>
    private const string VanillaHudRootName = "Canvas";

    private static Canvas _storedVanillaCanvas;
    private static bool _storedVanillaCanvasEnabled;
    private static bool _suppressActive;

    internal static void Begin()
    {
        if (_suppressActive)
            return;

        bool hideVanilla = Plugin.eventCoreHideVanillaHud?.Value ?? true;
        if (!hideVanilla)
            return;

        GameObject go = GameObject.Find(VanillaHudRootName);
        if (go == null)
            return;

        var canvas = go.GetComponent<Canvas>();
        if (canvas == null)
            return;

        _storedVanillaCanvas = canvas;
        _storedVanillaCanvasEnabled = canvas.enabled;
        canvas.enabled = false;
        _suppressActive = true;

        Plugin.Log?.LogDebug("[EventCore] Vanilla HUD Canvas disabled for modal session.");
    }

    internal static void End()
    {
        if (!_suppressActive || _storedVanillaCanvas == null)
        {
            _suppressActive = false;
            _storedVanillaCanvas = null;
            return;
        }

        _storedVanillaCanvas.enabled = _storedVanillaCanvasEnabled;
        _storedVanillaCanvas = null;
        _suppressActive = false;
    }
}
