using HarmonyLib;
using NoREroMod.Systems.Cache;
using NoREroMod.Systems.Spawn;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NoREroMod.Systems.UI;

/// <summary>
/// Hides the OS mouse cursor during combat and H-scenes ([General] HideCursorInGameplay).
/// Any menu keeps it: vanilla pause sets <see cref="Time.timeScale"/> to 0, shops / altar / chests
/// select an interactable uGUI button, the altar / teleport menu is detected by its Savepoint_menu,
/// the defeat menu follows the player's death flag, and the
/// F11 spawn editor is checked explicitly.
/// </summary>
internal sealed class GameplayCursorHider : MonoBehaviour
{
    private static GameplayCursorHider instance;
    private bool hidden;

    internal static void EnsureExists()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject("HellGate_GameplayCursorHider");
        Object.DontDestroyOnLoad(go);
        instance = go.AddComponent<GameplayCursorHider>();
    }

    private void LateUpdate()
    {
        bool hide = Plugin.IsHideCursorInGameplayEnabled && IsGameplayWithoutMenu();
        if (hide == hidden)
            return;

        hidden = hide;
        Cursor.visible = !hide;
    }

    private void OnDisable()
    {
        if (!hidden)
            return;

        hidden = false;
        Cursor.visible = true;
    }

    private static bool IsGameplayWithoutMenu()
    {
        if (Time.timeScale < 0.01f)
            return false;
        if (SceneManager.GetActiveScene().name != "Common")
            return false;
        playercon player = UnifiedPlayerCacheManager.GetPlayer();
        if (player == null)
            return false;
        // Defeat menu (Vengeance / Nightmare) is not focused in the EventSystem; Death stays set until restart.
        if (player._Death)
            return false;
        PlayerStatus status = UnifiedPlayerCacheManager.GetPlayerStatus();
        if (status != null && status.Hp <= 0f)
            return false;
        if (SpawnAuthoringOverlayHost.IsOpen)
            return false;
        if (IsAltarMenuOpen())
            return false;
        return !IsUiButtonFocused();
    }

    private static Savepoint_menu altarMenu;

    internal static void RegisterAltarMenu(Savepoint_menu menu)
    {
        if (menu != null)
            altarMenu = menu;
    }

    // Clicking empty space in the altar / teleport list clears the EventSystem selection,
    // so the menu object itself is the reliable signal.
    private static bool IsAltarMenuOpen() => altarMenu != null && altarMenu.isActiveAndEnabled;

    private static bool IsUiButtonFocused()
    {
        EventSystem es = EventSystem.current;
        if (es == null)
            return false;

        GameObject selected = es.currentSelectedGameObject;
        if (selected == null || !selected.activeInHierarchy)
            return false;

        Selectable selectable = selected.GetComponent<Selectable>();
        if (selectable == null || !selectable.IsInteractable())
            return false;

        Canvas[] canvases = selected.GetComponentsInParent<Canvas>();
        if (canvases.Length == 0)
            return false;
        foreach (Canvas canvas in canvases)
        {
            if (!canvas.enabled)
                return false;
        }

        return true;
    }
}

[HarmonyPatch]
internal static class GameplayCursorHiderAltarMenuPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Savepoint_menu), "Start")]
    private static void Start_Postfix(Savepoint_menu __instance) =>
        GameplayCursorHider.RegisterAltarMenu(__instance);
}
