using System;
using System.Collections.Generic;
using System.Globalization;
using NoREroMod.Systems.EventCore.Content;
using NoREroMod.Systems.EventCore.Host;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// IMGUI overlay while F11 spawn authoring is active:
/// coords, catalogs (Enemies / Hostage / Trap / Lethal / Decorations / Gold /
/// EventTrap / EventCore), Confirm, world hover outline, Edit for HellGate-linked spawns.
/// </summary>
[DefaultExecutionOrder(-32000)]
internal sealed class SpawnAuthoringOverlayHost : MonoBehaviour
{
    private const float PanelWidth = 400f;
    private const float CoordsHeightExpanded = 168f;
    private const float CoordsHeightExpandedSolo = 214f;
    private const float CoordsHeightCollapsed = 32f;
    private const float EnemiesListHeight = 270f;
    private const float ConfirmRowHeight = 40f;
    private const float FactionPanelHeight = 76f;
    private const float OptionsPanelHeight = 188f;
    private const float TrapOptionsPanelHeight = 200f;
    private const float TrapOptionsPanelHeightHostage = 248f;
    private const float GoldOptionsPanelHeight = 120f;
    private const float EventTrapOptionsPanelHeight = 228f;
    private const float EventCoreOptionsPanelHeight = 216f;
    private const float EliteRowHeight = 28f;
    private const float ExtraOptsRowHeight = 28f;
    private const float RandomRowHeight = 28f;
    private const float FavAddRowHeight = 34f;
    private const float StickyConfirmHeight = 56f;
    private const float RowHeight = 28f;
    private const float FavoritesPanelHeight = 250f;
    private const float EditPanelHeight = 400f;
    private const float EditPanelHeightTrap = 490f;
    private const float EditPanelHeightHostage = 560f;
    private const float MenuBtnH = 34f;
    private const float ActionBarHeight = 52f;
    private const float ActionBtnW = 92f;
    private const float ActionBtnH = 36f;
    private const float ActionGap = 6f;
    private const float ShotBtnW = 118f;
    private const float ShotBtnH = 34f;
    private const float HelpBtnW = 108f;
    private const float HelpGuideW = 400f;
    private const float HeaderToolBtn = 22f;
    private const int PanelFontSize = 15;
    private const int PanelTitleFontSize = 16;

    private static SpawnAuthoringOverlayHost instance;
    private static Rect uiBlockRect;
    private static Rect actionBarRect;
    private static Rect centerDockRect;
    private static Rect favoritesFloatRect;
    private static Rect screenshotBtnRect;
    private static Rect helpBtnRect;
    private static Rect helpGuideRect;
    private static Rect enemyPreviewRect;
    private static Rect catalogListViewRect;
    private Vector2 enemiesScroll;
    private Vector2 favoritesScroll;
    private string statusLine = string.Empty;

    private string[] cachedKeys = new string[0];
    private bool[] cachedReady = new bool[0];
    private bool[] cachedRowHeader = new bool[0];
    private bool listCacheWarm;

    private bool editPanelOpen;
    private string editKey = string.Empty;
    private int editFactionIndex;
    private bool editRandom;
    private string editChanceStr = "0.5";
    private bool editFlip;
    private bool editForceElite;
    private float editRotationZ;
    private int editSortOffset;
    private bool editIsTrap;
    private bool editIsGold;
    private string editRotStr = "0";
    private string editSortStr = "0";
    private string editXStr = "0";
    private string editYStr = "0";
    private string editStatus = string.Empty;
    private SpawnManagedInstance editTarget;
    private float saveFlashUntil;
    private bool saveFlashOk;
    private bool placementCollapsed;
    private bool previewCollapsed;
    private bool previewGrabArmed;
    private bool previewGrabMoved;
    private bool editIsAnchor;
    private bool editIsEventCore;
    private string editEventCoreId = string.Empty;
    private string editEventCoreChanceStr = "1";
    private string hoverHelpKey;
    private bool helpGuideOpen;
    private Vector2 helpGuideScroll;
    private float lastHelpToggleUnscaled = -10f;

    private float centerToastUntil;
    private string centerToastTitle = string.Empty;
    private string centerToastDetail = string.Empty;
    private Color centerToastAccent = new Color(1f, 0.55f, 0.1f, 1f);
    private GUIStyle centerToastTitleStyle;
    private GUIStyle centerToastDetailStyle;
    private GUIStyle panelLabelStyle;
    private GUIStyle panelTitleStyle;
    private GUIStyle panelMutedStyle;
    private GUIStyle panelBoxStyle;
    private GUIStyle panelButtonStyle;
    private GUIStyle panelToggleStyle;
    private GUIStyle panelTextFieldStyle;
    private Texture2D texPanelFill;
    private Texture2D texPanelHeader;
    private Texture2D texBtnNormal;
    private Texture2D texBtnHover;
    private Texture2D texBtnActive;
    private static readonly Color BrandOrange = new Color(1f, 0.55f, 0.1f, 1f);

    internal static bool IsPointerOverUi
    {
        get
        {
            Vector2 m = GuiMouse();
            if (uiBlockRect.width > 0f && uiBlockRect.height > 0f && uiBlockRect.Contains(m))
                return true;
            if (actionBarRect.width > 0f && actionBarRect.height > 0f && actionBarRect.Contains(m))
                return true;
            if (centerDockRect.width > 0f && centerDockRect.height > 0f && centerDockRect.Contains(m))
                return true;
            if (favoritesFloatRect.width > 0f && favoritesFloatRect.height > 0f && favoritesFloatRect.Contains(m))
                return true;
            if (screenshotBtnRect.width > 0f && screenshotBtnRect.height > 0f && screenshotBtnRect.Contains(m))
                return true;
            if (helpBtnRect.width > 0f && helpBtnRect.height > 0f && helpBtnRect.Contains(m))
                return true;
            if (helpGuideRect.width > 0f && helpGuideRect.height > 0f && helpGuideRect.Contains(m))
                return true;
            if (enemyPreviewRect.width > 0f && enemyPreviewRect.height > 0f && enemyPreviewRect.Contains(m))
                return true;
            return false;
        }
    }

    private static Vector2 GuiMouse()
    {
        if (Event.current != null)
            return Event.current.mousePosition;
        return new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
    }

    internal static void EnsureExists()
    {
        if (instance == null)
        {
            GameObject go = new GameObject("HellGate_SpawnAuthoringOverlay");
            Object.DontDestroyOnLoad(go);
            instance = go.AddComponent<SpawnAuthoringOverlayHost>();
        }

        SpawnAuthoringNudge.OnPositionNudged = instance.SyncEditFromNudge;
    }

    private void SyncEditFromNudge(float x, float y)
    {
        if (!editPanelOpen || editTarget == null)
            return;
        editXStr = x.ToString("F2", CultureInfo.InvariantCulture);
        editYStr = y.ToString("F2", CultureInfo.InvariantCulture);
    }

    internal static void SetActive(bool active)
    {
        EnsureExists();
        if (instance != null)
            instance.gameObject.SetActive(active);

        if (active)
        {
            if (SpawnAuthoringConfig.UiEnable == null || SpawnAuthoringConfig.UiEnable.Value)
            {
                SpawnAuthoringPause.Begin();
                SpawnAuthoringUiSuppressor.Begin();
            }

            if (instance != null)
            {
                instance.listCacheWarm = false;
                instance.statusLine = string.Empty;
                instance.CloseEditPanel();
            }
        }
        else
        {
            SpawnAuthoringClickCue.Clear();
            SpawnAuthoringCamera.EndSession();
            SpawnAuthoringUiSuppressor.End();
            SpawnAuthoringPause.End();
            SpawnAuthoringWorldPick.Clear();
            SpawnAuthoringOutline.Destroy();
            SpawnAuthoringState.ClearSessionUi();
            uiBlockRect = default;
            helpBtnRect = default;
            helpGuideRect = default;
            if (instance != null)
            {
                instance.listCacheWarm = false;
                instance.statusLine = string.Empty;
                instance.helpGuideOpen = false;
                instance.CloseEditPanel();
            }
        }
    }

    private void Update()
    {
        if (!global::NoREroMod.SpawnPointAnalyzer.IsRecordingModeActive)
            return;
        if (SpawnAuthoringConfig.UiEnable != null && !SpawnAuthoringConfig.UiEnable.Value)
            return;

        SpawnAuthoringWorldPick.Tick(IsPointerOverUi);
        SpawnAuthoringOutline.TickFollow();
        SpawnAuthoringCamera.Tick(IsPointerOverUi);
        SpawnAuthoringNudge.Tick();
        SpawnAuthoringDrag.Tick(IsPointerOverUi);
        SpawnAuthoringRegion.Tick(IsPointerOverUi);
        if (SpawnAuthoringRegion.ConsumeClickForPoint() && !IsPointerOverUi)
        {
            CloseEditPanel();
            global::NoREroMod.SpawnPointAnalyzer.TryRecordWorldClickFromAuthoringUi();
        }

        if (Input.GetKeyDown(KeyCode.Delete))
            TryDeleteSelected();

        // Ctrl+Z undo · Ctrl+C copy · Ctrl+X cut · Ctrl+V paste · Ctrl+D duplicate · Ctrl+B favorite · Ctrl+S save edit
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            if (Input.GetKeyDown(KeyCode.Z))
                TryUndo();
            else if (Input.GetKeyDown(KeyCode.C))
                TryCopySelected();
            else if (Input.GetKeyDown(KeyCode.X))
                TryCutSelected();
            else if (Input.GetKeyDown(KeyCode.V))
                TryPasteClipboard(true);
            else if (Input.GetKeyDown(KeyCode.D))
                TryDuplicateSelected();
            else if (Input.GetKeyDown(KeyCode.B))
                AddPendingToFavorites();
            else if (editPanelOpen && Input.GetKeyDown(KeyCode.S))
                SaveEdit();
        }

        TickMenuHotkeys();

        if (Input.GetKeyDown(KeyCode.F12) &&
            !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
            TryTakeScreenshot();
    }

    /// <summary>Esc: help → region → Edit → selection.</summary>
    internal static bool TryHandleEscape()
    {
        if (instance == null)
            return false;
        return instance.HandleEscape();
    }

    private bool HandleEscape()
    {
        if (helpGuideOpen)
        {
            helpGuideOpen = false;
            return true;
        }

        if (SpawnAuthoringRegion.HasRect || SpawnAuthoringRegion.IsDrawing || SpawnAuthoringRegion.IsArmed)
        {
            SpawnAuthoringRegion.Clear();
            return true;
        }

        if (editPanelOpen)
        {
            CloseEditPanel();
            return true;
        }

        if (SpawnAuthoringWorldPick.Locked != null)
        {
            SpawnAuthoringWorldPick.ClearLock();
            return true;
        }

        return false;
    }

    /// <summary>
    /// F1 is authoring Help while F11 is on. Only KeyDown/KeyUp are consumed —
    /// never Layout/Repaint (Use() there blanks the whole overlay).
    /// </summary>
    private void ConsumeAuthoringF1()
    {
        Event e = Event.current;
        if (e == null || e.keyCode != KeyCode.F1)
            return;

        if (e.type == EventType.KeyDown)
        {
            TryToggleHelpGuide();
            e.Use();
            return;
        }

        if (e.type == EventType.KeyUp)
            e.Use();
    }

    private void TryToggleHelpGuide()
    {
        float now = Time.unscaledTime;
        if (now - lastHelpToggleUnscaled < 0.25f)
            return;
        lastHelpToggleUnscaled = now;
        helpGuideOpen = !helpGuideOpen;
    }

    /// <summary>
    /// Hotkeys (mnemonic):
    /// E Enemies · H Hostage · T Trap · R Lethal · G Gold · N Decor · M EventTrap · C EventCore
    /// F Flip · B Favorites · V View · U Use position · Home camera
    /// Space Place · Q close · Ctrl+C/X/V/D/Z/B/S
    /// </summary>
    private void TickMenuHotkeys()
    {
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
            Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            return;

        if (Input.GetKeyDown(KeyCode.Home) && GUIUtility.keyboardControl == 0)
        {
            string camStatus;
            SpawnAuthoringCamera.TryRecenterOnPlayer(out camStatus);
            statusLine = camStatus;
            return;
        }

        bool typing = GUIUtility.keyboardControl != 0;
        if (editPanelOpen && IsPointerOverUi)
        {
            if (!typing && Input.GetKeyDown(KeyCode.U))
                TryUseCurrentPositionHotkey();
            if (editIsTrap && SpawnAuthoringState.KeyAllowsRotation(editKey) && Input.GetKeyDown(KeyCode.R))
                BumpEditRotation90();
            return;
        }

        if (typing)
            return;

        if (Input.GetKeyDown(KeyCode.E))
            ToggleEnemiesPanel();
        else if (Input.GetKeyDown(KeyCode.H))
            ToggleHostagePanel();
        else if (Input.GetKeyDown(KeyCode.F))
            ToggleQuickFlip();
        else if (Input.GetKeyDown(KeyCode.G))
            ToggleGoldPanel();
        else if (Input.GetKeyDown(KeyCode.B))
            ToggleFavoritesPanel();
        else if (Input.GetKeyDown(KeyCode.N))
            ToggleDecorPanel();
        else if (Input.GetKeyDown(KeyCode.M))
            ToggleEventTrapPanel();
        else if (Input.GetKeyDown(KeyCode.C))
            ToggleEventCorePanel();
        else if (Input.GetKeyDown(KeyCode.U))
            TryUseCurrentPositionHotkey();
        else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            RunConfirm();
        else if (Input.GetKeyDown(KeyCode.Q))
            CloseAllPanels();
        else if (Input.GetKeyDown(KeyCode.V))
            ToggleViewMod();
        else if (Input.GetKeyDown(KeyCode.T))
            ToggleTrapPanel();
        else if (Input.GetKeyDown(KeyCode.R))
        {
            if (editPanelOpen && editIsTrap && SpawnAuthoringState.KeyAllowsRotation(editKey))
                BumpEditRotation90();
            else
                ToggleLethalTrapPanel();
        }
    }

    private void ToggleEnemiesPanel()
    {
        if (SpawnAuthoringState.ActiveCatalog == SpawnAuthoringState.CatalogKind.Enemies)
            SpawnAuthoringState.ActiveCatalog = SpawnAuthoringState.CatalogKind.None;
        else
        {
            SpawnAuthoringState.ActiveCatalog = SpawnAuthoringState.CatalogKind.Enemies;
            WarmListCache();
            CloseEditPanel();
            SpawnAuthoringFavorites.PanelOpen = false;
        }
    }

    private void ToggleTrapPanel()
    {
        if (SpawnAuthoringState.ActiveCatalog == SpawnAuthoringState.CatalogKind.Trap)
            SpawnAuthoringState.ActiveCatalog = SpawnAuthoringState.CatalogKind.None;
        else
        {
            SpawnAuthoringState.ActiveCatalog = SpawnAuthoringState.CatalogKind.Trap;
            WarmTrapListCache(lethal: false);
            CloseEditPanel();
            SpawnAuthoringFavorites.PanelOpen = false;
        }
    }

    private void ToggleLethalTrapPanel()
    {
        if (SpawnAuthoringState.ActiveCatalog == SpawnAuthoringState.CatalogKind.LethalTrap)
            SpawnAuthoringState.ActiveCatalog = SpawnAuthoringState.CatalogKind.None;
        else
        {
            SpawnAuthoringState.ActiveCatalog = SpawnAuthoringState.CatalogKind.LethalTrap;
            SpawnAuthoringState.PendingRotationZ = 0f;
            SpawnAuthoringState.SelectedRotationZ = 0f;
            WarmTrapListCache(lethal: true);
            CloseEditPanel();
            SpawnAuthoringFavorites.PanelOpen = false;
        }
    }

    private void ToggleDecorPanel()
    {
        if (SpawnAuthoringState.ActiveCatalog == SpawnAuthoringState.CatalogKind.Decor)
            SpawnAuthoringState.ActiveCatalog = SpawnAuthoringState.CatalogKind.None;
        else
        {
            SpawnAuthoringState.ActiveCatalog = SpawnAuthoringState.CatalogKind.Decor;
            WarmDecorListCache();
            CloseEditPanel();
            SpawnAuthoringFavorites.PanelOpen = false;
        }
    }

    private void ToggleHostagePanel()
    {
        if (SpawnAuthoringState.ActiveCatalog == SpawnAuthoringState.CatalogKind.Hostage)
            SpawnAuthoringState.ActiveCatalog = SpawnAuthoringState.CatalogKind.None;
        else
        {
            SpawnAuthoringState.ActiveCatalog = SpawnAuthoringState.CatalogKind.Hostage;
            SpawnAuthoringState.PendingRotationZ = 0f;
            SpawnAuthoringState.SelectedRotationZ = 0f;
            WarmHostageListCache();
            CloseEditPanel();
            SpawnAuthoringFavorites.PanelOpen = false;
        }
    }

    private void ToggleGoldPanel()
    {
        if (SpawnAuthoringState.ActiveCatalog == SpawnAuthoringState.CatalogKind.Gold)
            SpawnAuthoringState.ActiveCatalog = SpawnAuthoringState.CatalogKind.None;
        else
        {
            SpawnAuthoringState.ActiveCatalog = SpawnAuthoringState.CatalogKind.Gold;
            WarmGoldListCache();
            CloseEditPanel();
            SpawnAuthoringFavorites.PanelOpen = false;
        }
    }

    private void ToggleEventTrapPanel()
    {
        if (SpawnAuthoringState.ActiveCatalog == SpawnAuthoringState.CatalogKind.EventTrap)
            SpawnAuthoringState.ActiveCatalog = SpawnAuthoringState.CatalogKind.None;
        else
        {
            SpawnAuthoringState.ActiveCatalog = SpawnAuthoringState.CatalogKind.EventTrap;
            WarmEventTrapListCache();
            CloseEditPanel();
            SpawnAuthoringFavorites.PanelOpen = false;
        }
    }

    private void ToggleEventCorePanel()
    {
        if (SpawnAuthoringState.ActiveCatalog == SpawnAuthoringState.CatalogKind.EventCore)
            SpawnAuthoringState.ActiveCatalog = SpawnAuthoringState.CatalogKind.None;
        else
        {
            SpawnAuthoringState.ActiveCatalog = SpawnAuthoringState.CatalogKind.EventCore;
            if (string.IsNullOrEmpty(SpawnAuthoringState.PendingEventCoreChanceStr))
                SpawnAuthoringState.PendingEventCoreChanceStr = "1";
            WarmEventCoreListCache();
            if (cachedKeys != null && cachedKeys.Length > 0)
            {
                bool known = false;
                for (int i = 0; i < cachedKeys.Length; i++)
                {
                    if (string.Equals(cachedKeys[i], SpawnAuthoringState.PendingEnemyKey,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        known = true;
                        break;
                    }
                }

                if (!known)
                    SpawnAuthoringState.PendingEnemyKey = cachedKeys[0];
            }

            CloseEditPanel();
            SpawnAuthoringFavorites.PanelOpen = false;
        }
    }

    private void ToggleFavoritesPanel()
    {
        SpawnAuthoringFavorites.PanelOpen = !SpawnAuthoringFavorites.PanelOpen;
        if (SpawnAuthoringFavorites.PanelOpen)
        {
            SpawnAuthoringState.ActiveCatalog = SpawnAuthoringState.CatalogKind.None;
            CloseEditPanel();
            SpawnAuthoringFavorites.Invalidate();
        }
    }

    /// <summary>V / View Mod: first press Overview ON, second press Default (like combat V).</summary>
    private void ToggleViewMod()
    {
        string status;
        SpawnAuthoringCamera.ToggleOverview(out status);
        statusLine = status;
    }

    private void CloseAllPanels()
    {
        SpawnAuthoringState.ActiveCatalog = SpawnAuthoringState.CatalogKind.None;
        SpawnAuthoringFavorites.PanelOpen = false;
        helpGuideOpen = false;
        CloseEditPanel();
        statusLine = SpawnAuthoringLoc.T("status.panelsClosed");
    }

    private void AddPendingToFavorites()
    {
        string status;
        bool ok = SpawnAuthoringFavorites.TryAddFromPending(out status);
        statusLine = status ?? string.Empty;
        if (ok)
            ShowCenterToast(statusLine, string.Empty, new Color(0.95f, 0.75f, 0.25f, 1f));
    }

    private void WarmListCache()
    {
        cachedKeys = SpawnAuthoringEnemyCatalog.GetEnemyKeys();
        cachedReady = new bool[cachedKeys.Length];
        cachedRowHeader = new bool[cachedKeys.Length];
        EnemyPrefabRegistry.Initialize();
        for (int i = 0; i < cachedKeys.Length; i++)
            cachedReady[i] = EnemyPrefabRegistry.TryGetPrefab(cachedKeys[i], out _);
        listCacheWarm = true;
    }

    private void CloseEditPanel()
    {
        editPanelOpen = false;
        editTarget = null;
        editStatus = string.Empty;
        SpawnAuthoringOutline.ClearEditPreview();
    }

    private void SyncEditRotStr()
    {
        editRotStr = Mathf.RoundToInt(SpawnRotationUtility.NormalizeAngle(editRotationZ))
            .ToString(CultureInfo.InvariantCulture);
    }

    private void SyncEditSortStr()
    {
        editSortStr = editSortOffset.ToString(CultureInfo.InvariantCulture);
    }

    private void TryApplyEditSortFromField()
    {
        if (!editIsTrap)
            return;
        if (!int.TryParse(editSortStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sort))
        {
            statusLine = SpawnAuthoringLoc.T("status.badSort");
            return;
        }

        editSortOffset = sort;
        SyncEditSortStr();
        statusLine = SpawnAuthoringLoc.T("sort") + ": " +
                     (SpawnAuthoringPackEdit.FormatSortToken(editSortOffset) ?? "sort0");
    }

    private void BumpEditRotation90()
    {
        if (!editIsTrap)
            return;
        editRotationZ = SpawnAuthoringPackEdit.CycleRotationZ(editRotationZ);
        SyncEditRotStr();
        ApplyEditTrapRotationLive();
        statusLine = SpawnAuthoringLoc.T("rot") + ": " + editRotStr + "°";
    }

    private void TryApplyEditRotationFromField()
    {
        if (!editIsTrap)
            return;
        if (!float.TryParse(editRotStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float deg))
        {
            statusLine = SpawnAuthoringLoc.T("status.badRot");
            return;
        }

        editRotationZ = SpawnRotationUtility.NormalizeAngle(deg);
        SyncEditRotStr();
        ApplyEditTrapRotationLive();
        statusLine = SpawnAuthoringLoc.T("rot") + ": " + editRotStr + "°";
    }

    private void ApplyEditTrapRotationLive()
    {
        if (editTarget == null)
            return;

        SpawnRotationUtility.LockAuthoringRotation(editTarget.gameObject, editRotationZ);
    }

    private void ApplyEditTrapOrEnemyFlipLive()
    {
        if (editTarget == null)
            return;

        editTarget.AuthoringFlipX = editFlip;
        SpawnFlipUtility.ApplyAuthoringFlip(editTarget.gameObject, editFlip);
    }

    private void ApplyEditEventCoreHostLive()
    {
        if (editTarget == null)
            return;

        EventCoreHost host = editTarget.GetComponent<EventCoreHost>() ??
                             editTarget.GetComponentInChildren<EventCoreHost>(true);
        string eventId = editIsEventCore ? (editEventCoreId ?? string.Empty).Trim() : string.Empty;
        if (string.IsNullOrEmpty(eventId))
        {
            if (host != null)
                host.Configure(string.Empty);
            editIsEventCore = false;
            return;
        }

        if (host == null)
            host = editTarget.gameObject.AddComponent<EventCoreHost>();
        host.Configure(eventId);
        editIsEventCore = true;
    }

    private void TryOpenEditForLocked()
    {
        GameObject locked = SpawnAuthoringWorldPick.Locked;
        if (locked == null)
        {
            statusLine = SpawnAuthoringLoc.T("status.selectEnemy");
            return;
        }

        SpawnManagedInstance managed = locked.GetComponent<SpawnManagedInstance>()
            ?? locked.GetComponentInChildren<SpawnManagedInstance>(true);
        if (managed == null || !managed.HasAuthoringLink)
        {
            statusLine = SpawnAuthoringLoc.T("status.editDisabled");
            CloseEditPanel();
            return;
        }

        editTarget = managed;
        editKey = managed.AuthoringEnemyKey;
        editFactionIndex = IndexOfFaction(managed.AuthoringFactionIdRaw);
        if (editFactionIndex == 0 && string.IsNullOrEmpty(managed.AuthoringFactionIdRaw))
            editFactionIndex = IndexOfFaction(
                SpawnAuthoringPackEdit.ExtractFactionFromSourceLine(managed.AuthoringSourceLineRaw));
        editRandom = managed.AuthoringChance > 0f && managed.AuthoringChance < 0.999f;
        editChanceStr = managed.AuthoringChance.ToString("0.##", CultureInfo.InvariantCulture);
        editFlip = managed.AuthoringFlipX;
        editForceElite = managed.AuthoringForceElite || managed.ForceElite;
        editRotationZ = managed.AuthoringRotationZ;
        editSortOffset = managed.AuthoringSortOffset;
        editIsGold = SpawnAuthoringPackEdit.IsGoldManagedInstance(managed);
        editIsAnchor = !editIsGold && SpawnAuthoringPackEdit.IsAnchorManagedInstance(managed);
        editIsTrap = !editIsGold && !editIsAnchor && SpawnAuthoringPackEdit.IsTrapManagedInstance(managed);
        editIsEventCore = false;
        editEventCoreId = string.Empty;
        editEventCoreChanceStr = "0.5";
        if (!editIsGold && !editIsAnchor && !editIsTrap)
        {
            SpawnAuthoringPackEdit.ExtractEventCoreFromSourceLine(
                managed.AuthoringSourceLineRaw, out string ecId, out float ecChance);
            if (string.IsNullOrEmpty(ecId))
            {
                EventCoreHost host = managed.GetComponent<EventCoreHost>() ??
                                     managed.GetComponentInChildren<EventCoreHost>(true);
                if (host != null && !string.IsNullOrEmpty(host.EventId))
                    ecId = host.EventId;
            }

            if (!string.IsNullOrEmpty(ecId))
            {
                editIsEventCore = true;
                editEventCoreId = ecId;
                if (ecChance > 0f && ecChance < 0.999f)
                    editEventCoreChanceStr = ecChance.ToString("0.##", CultureInfo.InvariantCulture);
                else
                    editEventCoreChanceStr = "1";
            }
        }
        if (editIsGold)
        {
            string goldKey = SpawnAuthoringGoldCatalog.NormalizeRangeToken(managed.AuthoringEnemyKey);
            if (string.IsNullOrEmpty(goldKey))
                SpawnAuthoringPackEdit.TryExtractKeyAndCoords(
                    managed.AuthoringSourceLineRaw, out goldKey, out _, out _);
            if (!string.IsNullOrEmpty(goldKey))
                editKey = SpawnAuthoringGoldCatalog.NormalizeRangeToken(goldKey);
        }
        else if (editIsTrap)
        {
            string resolvedKey = SpawnAuthoringPackEdit.ResolveTrapAuthoringKey(managed);
            if (!string.IsNullOrEmpty(resolvedKey))
                editKey = resolvedKey;

            if (Mathf.Abs(editRotationZ) < 0.01f && editSortOffset == 0 && !editFlip)
            {
                SpawnAuthoringPackEdit.TryExtractPlacementExtras(
                    managed.AuthoringSourceLineRaw,
                    out bool lineFlip,
                    out float lineRot,
                    out int lineSort);
                editFlip = lineFlip;
                editRotationZ = lineRot;
                editSortOffset = lineSort;
            }

            if (SpawnAuthoringState.KeyAllowsRotation(editKey))
            {
                if (Mathf.Abs(editRotationZ) < 0.01f)
                    editRotationZ = SpawnRotationUtility.ReadRotationZ(managed.gameObject);
            }
            else
            {
                editRotationZ = 0f;
            }

            SyncEditRotStr();
            SyncEditSortStr();
        }

        Vector3 livePos = managed.transform.position;
        editXStr = livePos.x.ToString("F2", CultureInfo.InvariantCulture);
        editYStr = livePos.y.ToString("F2", CultureInfo.InvariantCulture);
        editStatus = SpawnAuthoringLoc.Tf("status.editingLine", managed.AuthoringLineIndex + 1);
        editPanelOpen = true;
        SpawnAuthoringState.ActiveCatalog = SpawnAuthoringState.CatalogKind.None;
        SpawnAuthoringFavorites.PanelOpen = false;
    }

    private static int IndexOfFaction(string raw)
    {
        string[] tokens = SpawnAuthoringPackEdit.FactionTokens;
        if (string.IsNullOrEmpty(raw))
            return 0;
        for (int i = 0; i < tokens.Length; i++)
        {
            if (string.Equals(tokens[i], raw.Trim(), StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return 0;
    }

    private void OnGUI()
    {
        if (!global::NoREroMod.SpawnPointAnalyzer.IsRecordingModeActive)
            return;

        if (SpawnAuthoringConfig.UiEnable != null && !SpawnAuthoringConfig.UiEnable.Value)
            return;

        ConsumeAuthoringF1();

        if (SpawnAuthoringScreenshot.SuppressChrome)
        {
            uiBlockRect = default;
            actionBarRect = default;
            centerDockRect = default;
            favoritesFloatRect = default;
            screenshotBtnRect = default;
            helpBtnRect = default;
            helpGuideRect = default;
            enemyPreviewRect = default;
            catalogListViewRect = default;
            return;
        }

        PushEditPreviewToOutline();
        SpawnAuthoringOutline.DrawGui();
        SpawnAuthoringAnchorMarker.DrawGui();
        SpawnAuthoringRegion.DrawGui();
        SpawnAuthoringClickCue.DrawGui();
        TickPreviewGrabPlace();
        DrawSaveFlashToast();
        DrawCenterActionToast();

        float margin = 12f;
        float x = Screen.width - PanelWidth - margin;
        float y = margin;

        EnsureRightPanelStyles();
        hoverHelpKey = null;

        float coordsH = placementCollapsed
            ? CoordsHeightCollapsed
            : (SpawnAuthoringState.AnyCatalogOpen ? CoordsHeightExpanded : CoordsHeightExpandedSolo);
        if (!placementCollapsed && SpawnAuthoringRegion.HasRect)
            coordsH += 36f;
        float catBlockH = 4f * (MenuBtnH + 6f) - 6f;
        float totalHeight = coordsH + 8f + catBlockH;
        if (SpawnAuthoringState.EnemiesPanelOpen)
            totalHeight += 8f + EnemiesListHeight + 6f + FactionPanelHeight + 6f + OptionsPanelHeight + 6f +
                           FavAddRowHeight + 6f + ConfirmRowHeight;
        else if (SpawnAuthoringState.IsTemplateCatalog)
        {
            float trapOpts = SpawnAuthoringState.HostagePanelOpen
                ? TrapOptionsPanelHeightHostage
                : TrapOptionsPanelHeight;
            if (!SpawnAuthoringState.CatalogAllowsRotation)
                trapOpts -= ExtraOptsRowHeight + 4f;
            totalHeight += 8f + EnemiesListHeight + 6f + trapOpts + 6f + FavAddRowHeight + 6f + ConfirmRowHeight;
            if (SpawnAuthoringState.HostagePanelOpen)
                totalHeight += FactionPanelHeight + 6f;
        }
        else if (SpawnAuthoringState.GoldPanelOpen)
            totalHeight += 8f + EnemiesListHeight + 6f + GoldOptionsPanelHeight + 6f +
                           FavAddRowHeight + 6f + ConfirmRowHeight;
        else if (SpawnAuthoringState.EventTrapPanelOpen)
            totalHeight += 8f + EnemiesListHeight + 6f + EventTrapOptionsPanelHeight + 6f +
                           FactionPanelHeight + 6f + FavAddRowHeight + 6f + ConfirmRowHeight;
        else if (SpawnAuthoringState.EventCorePanelOpen)
            totalHeight += 8f + EnemiesListHeight + 6f + FactionPanelHeight + 6f +
                           EventCoreOptionsPanelHeight + 6f + FavAddRowHeight + 6f + ConfirmRowHeight;
        if (editPanelOpen)
            totalHeight += 8f + CurrentEditPanelHeight();

        uiBlockRect = new Rect(x, y, PanelWidth, totalHeight);
        favoritesFloatRect = default;

        Event e = Event.current;
        bool overUi = IsPointerOverUi;
        if (e != null && e.type == EventType.MouseDown && e.button == 0 && !overUi &&
            !SpawnAuthoringCamera.IsMiddleMousePanning && !previewGrabArmed)
        {
            if (SpawnAuthoringWorldPick.Hovered != null && SpawnAuthoringWorldPick.TryLockHoveredOnClick())
            {
                statusLine = FormatSelectionStatus(SpawnAuthoringWorldPick.Locked);
                TryOpenEditForLocked();
                SpawnAuthoringDrag.BeginOnLocked();
            }
            else if (!SpawnAuthoringDrag.IsArmed && SpawnAuthoringWorldPick.Hovered == null)
            {
                SpawnAuthoringRegion.TryBeginAtMouse();
            }
        }

        Rect coordsRect = new Rect(x, y, PanelWidth, coordsH);
        DrawChromePanel(coordsRect, SpawnAuthoringLoc.T("coords.title"));
        DrawPanelHeaderTools(coordsRect, "help.coords", collapse: true, ref placementCollapsed);

        if (!placementCollapsed)
        {
            GUILayout.BeginArea(new Rect(x + 14f, y + 32f, PanelWidth - 24f, coordsH - 40f));
            DrawCoordsLine(SpawnAuthoringLoc.T("coords.lastLmb"), SpawnAuthoringState.FormatLastCoords());
            string activePack = SpawnAuthoringPackWriter.GetActivePackFileName();
            if (!string.IsNullOrEmpty(activePack))
                DrawCoordsLine(SpawnAuthoringLoc.T("coords.activePack"), activePack);
            else if (!string.IsNullOrEmpty(SpawnAuthoringState.LastClickPackHint))
                DrawCoordsLine(SpawnAuthoringLoc.T("coords.pack"), SpawnAuthoringState.LastClickPackHint);
            DrawCoordsLine(SpawnAuthoringLoc.T("coords.pending"), OrDash(SpawnAuthoringState.PendingEnemyKey));
            if (!string.IsNullOrEmpty(SpawnAuthoringState.PendingEnemyKey))
                GUILayout.Label(SpawnAuthoringState.FormatPendingFlagsShort(), panelMutedStyle);
            if (SpawnAuthoringRegion.HasRect)
                DrawCoordsLineWrapped(SpawnAuthoringLoc.T("coords.selected"), SpawnAuthoringRegion.FormatSummary());
            if (SpawnAuthoringClipboard.HasClip)
                DrawCoordsLine(SpawnAuthoringLoc.T("coords.clipboard"), SpawnAuthoringClipboard.FormatShort());
            else if (SpawnAuthoringClipboard.HasChunk)
                DrawCoordsLineWrapped(SpawnAuthoringLoc.T("coords.clipboard"), SpawnAuthoringClipboard.FormatShort());
            if (!SpawnAuthoringState.AnyCatalogOpen)
            {
                DrawCoordsLineWrapped(SpawnAuthoringLoc.T("coords.line"), SpawnAuthoringState.FormatPendingLinePreview());
                GUILayout.Space(4f);
                GUILayout.Label(SpawnAuthoringLoc.T("coords.hotkeys"), panelMutedStyle);
            }
            if (SpawnAuthoringDrag.IsDragging)
                GUILayout.Label(SpawnAuthoringLoc.T("status.dragging"), panelTitleStyle);
            else if (!string.IsNullOrEmpty(statusLine))
                GUILayout.Label(statusLine, panelMutedStyle);
            GUILayout.EndArea();
        }

        string hover = SpawnAuthoringWorldPick.FormatHoverLabel();
        if (!string.IsNullOrEmpty(hover) && !overUi)
        {
            Vector2 m = GuiMouse();
            GUI.Label(new Rect(m.x + 14f, m.y + 14f, 480f, 24f), hover, panelLabelStyle);
        }

        y += coordsH + 8f;
        y = DrawSpawnCategoryButtons(x, y);

        if (!SpawnAuthoringState.EnemiesPanelOpen && !SpawnAuthoringState.IsTemplateCatalog &&
            !SpawnAuthoringState.IsSpecialCatalog)
        {
            enemyPreviewRect = default;
            catalogListViewRect = default;
        }

        if (SpawnAuthoringState.EnemiesPanelOpen)
            y = DrawEnemiesPanel(x, y);
        else if (SpawnAuthoringState.IsTemplateCatalog)
            y = DrawTrapCatalogPanel(x, y);
        else if (SpawnAuthoringState.EventCorePanelOpen)
            y = DrawEventCoreCatalogPanel(x, y);
        else if (SpawnAuthoringState.IsSpecialCatalog)
            y = DrawSpecialCatalogPanel(x, y);

        if (editPanelOpen)
            DrawEditPanel(x, y);

        DrawCenterDockAndActionBar();
        DrawTopLeftCornerButtons();

        if (SpawnAuthoringFavorites.PanelOpen)
            DrawFavoritesFloatPanel();

        DrawGrabFollowHint();
        DrawHoverHelpCard();
    }

    private void DrawTopLeftCornerButtons()
    {
        EnsureRightPanelStyles();
        float margin = 12f;
        screenshotBtnRect = new Rect(margin, margin, ShotBtnW, ShotBtnH);
        helpBtnRect = new Rect(margin + ShotBtnW + 6f, margin, HelpBtnW, ShotBtnH);

        Color prev = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.35f, 0.55f, 0.85f, 1f);
        if (GUI.Button(screenshotBtnRect, SpawnAuthoringLoc.T("btn.screenshot"), panelButtonStyle))
            TryTakeScreenshot();

        GUI.backgroundColor = helpGuideOpen
            ? BrandOrange
            : new Color(0.55f, 0.42f, 0.22f, 1f);
        if (GUI.Button(helpBtnRect, SpawnAuthoringLoc.T("btn.help"), panelButtonStyle))
            TryToggleHelpGuide();
        GUI.backgroundColor = prev;

        if (helpGuideOpen)
            DrawHelpGuidePanel(margin);
        else
            helpGuideRect = default;
    }

    private void DrawHelpGuidePanel(float margin)
    {
        EnsureRightPanelStyles();
        float top = margin + ShotBtnH + 8f;
        float bottomLimit = Screen.height - 16f;
        if (centerDockRect.width > 0f && centerDockRect.x < margin + HelpGuideW + 12f)
            bottomLimit = Mathf.Min(bottomLimit, centerDockRect.y - 10f);

        float h = Mathf.Clamp(bottomLimit - top, 220f, 760f);
        helpGuideRect = new Rect(margin, top, HelpGuideW, h);
        DrawChromePanel(helpGuideRect, SpawnAuthoringLoc.T("help.guide.title"));

        Rect closeR = new Rect(helpGuideRect.xMax - 28f, helpGuideRect.y + 3f, 22f, 22f);
        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.45f, 0.28f, 0.22f, 1f);
        if (GUI.Button(closeR, "×", panelButtonStyle))
            helpGuideOpen = false;
        GUI.backgroundColor = prevBg;

        GUIStyle section = new GUIStyle(panelLabelStyle)
        {
            fontStyle = FontStyle.Bold,
            wordWrap = false,
            fontSize = PanelFontSize
        };
        section.normal.textColor = BrandOrange;

        float contentW = HelpGuideW - 40f;
        float y = 4f;
        y += MeasureHelpSection(contentW, "help.guide.camera.body");
        y += MeasureHelpSection(contentW, "help.guide.mouse.body");
        y += MeasureHelpSection(contentW, "help.guide.catalogs.body");
        y += MeasureHelpSection(contentW, "help.guide.edit.body");
        y += MeasureHelpSection(contentW, "help.guide.footer");
        float contentH = y + 8f;

        Rect view = new Rect(helpGuideRect.x + 12f, helpGuideRect.y + 34f, HelpGuideW - 20f, h - 42f);
        helpGuideScroll = GUI.BeginScrollView(view, helpGuideScroll, new Rect(0f, 0f, contentW, contentH));
        y = 4f;
        DrawHelpSection(ref y, contentW, section, "help.guide.camera", "help.guide.camera.body");
        DrawHelpSection(ref y, contentW, section, "help.guide.mouse", "help.guide.mouse.body");
        DrawHelpSection(ref y, contentW, section, "help.guide.catalogs", "help.guide.catalogs.body");
        DrawHelpSection(ref y, contentW, section, "help.guide.edit", "help.guide.edit.body");
        string footer = SpawnAuthoringLoc.T("help.guide.footer");
        float fh = panelMutedStyle.CalcHeight(new GUIContent(footer), contentW);
        GUI.Label(new Rect(0f, y, contentW, fh), footer, panelMutedStyle);
        GUI.EndScrollView();
    }

    private float MeasureHelpSection(float w, string bodyKey)
    {
        string body = SpawnAuthoringLoc.T(bodyKey);
        return 20f + panelMutedStyle.CalcHeight(new GUIContent(body), w) + 10f;
    }

    private void DrawHelpSection(ref float y, float w, GUIStyle section, string titleKey, string bodyKey)
    {
        GUI.Label(new Rect(0f, y, w, 20f), SpawnAuthoringLoc.T(titleKey), section);
        y += 20f;
        string body = SpawnAuthoringLoc.T(bodyKey);
        float bh = panelMutedStyle.CalcHeight(new GUIContent(body), w);
        GUI.Label(new Rect(0f, y, w, bh), body, panelMutedStyle);
        y += bh + 10f;
    }

    private void TryTakeScreenshot()
    {
        if (!SpawnAuthoringScreenshot.TryBeginCapture(this, out string status) &&
            !string.IsNullOrEmpty(status))
        {
            statusLine = status;
        }
    }

    /// <summary>Called after F12 screenshot coroutine finishes.</summary>
    internal void NotifyScreenshotResult(bool ok, string detail)
    {
        if (ok)
        {
            ShowCenterToast(SpawnAuthoringLoc.T("toast.screenshot"), detail, new Color(0.4f, 0.75f, 1f, 1f));
            statusLine = SpawnAuthoringLoc.T("toast.screenshot") + ": " + detail;
        }
        else
        {
            ShowCenterToast(SpawnAuthoringLoc.T("toast.screenshotFail"), detail, new Color(0.9f, 0.35f, 0.3f, 1f));
            statusLine = detail;
        }
    }

    /// <summary>Right column: Enemies / Hostage / Trap / Lethal / Decorations / Gold / EventTrap / EventCore.</summary>
    private float DrawSpawnCategoryButtons(float x, float y)
    {
        EnsureRightPanelStyles();
        float gap = 6f;
        float half = (PanelWidth - gap) * 0.5f;

        // Row 1: Enemies | Hostage & OtherScenes
        if (GUI.Button(new Rect(x, y, half, MenuBtnH),
                SpawnAuthoringState.EnemiesPanelOpen
                    ? SpawnAuthoringLoc.T("btn.enemiesOpen")
                    : SpawnAuthoringLoc.T("btn.enemies"), panelButtonStyle))
            ToggleEnemiesPanel();

        if (GUI.Button(new Rect(x + half + gap, y, half, MenuBtnH),
                SpawnAuthoringState.HostagePanelOpen
                    ? SpawnAuthoringLoc.T("btn.hostageOpen")
                    : SpawnAuthoringLoc.T("btn.hostage"), panelButtonStyle))
            ToggleHostagePanel();
        y += MenuBtnH + gap;

        // Row 2: Trap | LethalTrap
        if (GUI.Button(new Rect(x, y, half, MenuBtnH),
                SpawnAuthoringState.TrapPanelOpen
                    ? SpawnAuthoringLoc.T("btn.trapOpen")
                    : SpawnAuthoringLoc.T("btn.trap"), panelButtonStyle))
            ToggleTrapPanel();

        if (GUI.Button(new Rect(x + half + gap, y, half, MenuBtnH),
                SpawnAuthoringState.LethalTrapPanelOpen
                    ? SpawnAuthoringLoc.T("btn.lethalTrapOpen")
                    : SpawnAuthoringLoc.T("btn.lethalTrap"), panelButtonStyle))
            ToggleLethalTrapPanel();
        y += MenuBtnH + gap;

        // Row 3: Decorations | Gold
        if (GUI.Button(new Rect(x, y, half, MenuBtnH),
                SpawnAuthoringState.DecorPanelOpen
                    ? SpawnAuthoringLoc.T("btn.decorOpen")
                    : SpawnAuthoringLoc.T("btn.decor"), panelButtonStyle))
            ToggleDecorPanel();

        if (GUI.Button(new Rect(x + half + gap, y, half, MenuBtnH),
                SpawnAuthoringState.GoldPanelOpen
                    ? SpawnAuthoringLoc.T("btn.goldOpen")
                    : SpawnAuthoringLoc.T("btn.gold"), panelButtonStyle))
            ToggleGoldPanel();
        y += MenuBtnH + gap;

        // Row 4: EventTrap | EventCore
        if (GUI.Button(new Rect(x, y, half, MenuBtnH),
                SpawnAuthoringState.EventTrapPanelOpen
                    ? SpawnAuthoringLoc.T("btn.eventTrapOpen")
                    : SpawnAuthoringLoc.T("btn.eventTrap"), panelButtonStyle))
            ToggleEventTrapPanel();
        if (GUI.Button(new Rect(x + half + gap, y, half, MenuBtnH),
                SpawnAuthoringState.EventCorePanelOpen
                    ? SpawnAuthoringLoc.T("btn.eventCoreOpen")
                    : SpawnAuthoringLoc.T("btn.eventCore"), panelButtonStyle))
            ToggleEventCorePanel();
        y += MenuBtnH;

        return y;
    }

    /// <summary>View Mod + Favorites docked above the quick action strip.</summary>
    private void DrawCenterDockAndActionBar()
    {
        EnsureRightPanelStyles();

        const int slots = 8;
        float barW = slots * ActionBtnW + (slots - 1) * ActionGap + 24f;
        float barH = ActionBarHeight;
        float dockH = MenuBtnH + 10f;
        float totalH = dockH + 6f + barH;
        float bx = (Screen.width - barW) * 0.5f;
        float by = Screen.height - totalH - 12f;

        centerDockRect = new Rect(bx, by, barW, dockH);
        actionBarRect = new Rect(bx, by + dockH + 6f, barW, barH);

        // Dock + action chrome (no title header)
        DrawChromeBar(centerDockRect);
        Color prevBg = GUI.backgroundColor;
        Color prevGui = GUI.color;

        float dockHalf = (barW - 24f - ActionGap) * 0.5f;
        float dx = bx + 12f;
        float dy = by + 5f;

        string viewLabel = SpawnAuthoringCamera.IsOverviewArmed
            ? SpawnAuthoringLoc.T("btn.viewModOn") + " ×" + SpawnAuthoringCamera.CurrentMultiplier.ToString("0.0")
            : SpawnAuthoringLoc.T("btn.viewModOff");
        if (SpawnAuthoringCamera.IsOverviewArmed)
            GUI.backgroundColor = new Color(1f, 0.55f, 0.1f, 1f);
        else
            GUI.backgroundColor = new Color(0.45f, 0.45f, 0.5f, 1f);
        if (GUI.Button(new Rect(dx, dy, dockHalf, MenuBtnH), viewLabel, panelButtonStyle))
            ToggleViewMod();

        GUI.backgroundColor = SpawnAuthoringFavorites.PanelOpen
            ? new Color(0.95f, 0.75f, 0.25f, 1f)
            : new Color(0.55f, 0.5f, 0.35f, 1f);
        string favLabel = SpawnAuthoringFavorites.PanelOpen
            ? SpawnAuthoringLoc.T("btn.favoritesOpen")
            : SpawnAuthoringLoc.T("btn.favorites");
        if (GUI.Button(new Rect(dx + dockHalf + ActionGap, dy, dockHalf, MenuBtnH), favLabel, panelButtonStyle))
            ToggleFavoritesPanel();
        GUI.backgroundColor = prevBg;

        DrawChromeBar(actionBarRect);
        GUI.color = prevGui;

        float ax = bx + 12f;
        float ay = actionBarRect.y + (barH - ActionBtnH) * 0.5f;

        GUI.enabled = SpawnAuthoringUndo.CanUndo;
        GUI.backgroundColor = new Color(0.55f, 0.55f, 0.6f, 1f);
        string undoLabel = SpawnAuthoringLoc.T("bar.undo") +
                           (SpawnAuthoringUndo.CanUndo ? " " + SpawnAuthoringUndo.Count : "");
        if (GUI.Button(new Rect(ax, ay, ActionBtnW, ActionBtnH), undoLabel, panelButtonStyle))
            TryUndo();
        ax += ActionBtnW + ActionGap;

        GUI.enabled = editPanelOpen && editTarget != null && editTarget.HasAuthoringLink;
        bool flashing = Time.unscaledTime < saveFlashUntil && saveFlashOk;
        GUI.backgroundColor = flashing
            ? new Color(0.2f, 1f, 0.45f, 1f)
            : new Color(0.3f, 0.75f, 0.4f, 1f);
        string saveLabel = flashing ? SpawnAuthoringLoc.T("btn.saved") : SpawnAuthoringLoc.T("bar.save");
        if (GUI.Button(new Rect(ax, ay, ActionBtnW, ActionBtnH), saveLabel, panelButtonStyle))
            SaveEdit();
        ax += ActionBtnW + ActionGap;

        bool flipOn = GetQuickFlipState();
        GUI.enabled = CanToggleQuickFlip();
        GUI.backgroundColor = flipOn
            ? new Color(0.95f, 0.7f, 0.25f, 1f)
            : new Color(0.45f, 0.45f, 0.5f, 1f);
        if (GUI.Button(new Rect(ax, ay, ActionBtnW, ActionBtnH),
                flipOn ? SpawnAuthoringLoc.T("bar.flipOn") : SpawnAuthoringLoc.T("bar.flip"), panelButtonStyle))
            ToggleQuickFlip();
        ax += ActionBtnW + ActionGap;

        GUI.enabled = SpawnAuthoringRegion.HasRect || ResolveCopyTarget() != null;
        GUI.backgroundColor = new Color(0.35f, 0.7f, 0.9f, 1f);
        if (GUI.Button(new Rect(ax, ay, ActionBtnW, ActionBtnH), SpawnAuthoringLoc.T("bar.copy"), panelButtonStyle))
            TryCopySelected();
        ax += ActionBtnW + ActionGap;

        GUI.enabled = SpawnAuthoringRegion.HasRect || ResolveCopyTarget() != null;
        GUI.backgroundColor = new Color(0.75f, 0.45f, 0.85f, 1f);
        if (GUI.Button(new Rect(ax, ay, ActionBtnW, ActionBtnH), SpawnAuthoringLoc.T("bar.cut"), panelButtonStyle))
            TryCutSelected();
        ax += ActionBtnW + ActionGap;

        GUI.enabled = SpawnAuthoringClipboard.HasClip || SpawnAuthoringClipboard.HasChunk;
        GUI.backgroundColor = new Color(1f, 0.55f, 0.1f, 1f);
        if (GUI.Button(new Rect(ax, ay, ActionBtnW, ActionBtnH), SpawnAuthoringLoc.T("bar.paste"), panelButtonStyle))
            TryPasteClipboard(true);
        ax += ActionBtnW + ActionGap;

        GUI.enabled = SpawnAuthoringRegion.HasRect || ResolveCopyTarget() != null;
        GUI.backgroundColor = new Color(0.85f, 0.35f, 0.3f, 1f);
        if (GUI.Button(new Rect(ax, ay, ActionBtnW, ActionBtnH), SpawnAuthoringLoc.T("bar.delete"), panelButtonStyle))
            TryDeleteSelected();
        ax += ActionBtnW + ActionGap;

        bool canPlace = !string.IsNullOrEmpty(SpawnAuthoringState.PendingEnemyKey) &&
                        SpawnAuthoringState.HasLastClick;
        GUI.enabled = canPlace;
        GUI.backgroundColor = canPlace
            ? new Color(0.45f, 0.85f, 0.4f, 1f)
            : new Color(0.4f, 0.4f, 0.42f, 1f);
        if (GUI.Button(new Rect(ax, ay, ActionBtnW, ActionBtnH), SpawnAuthoringLoc.T("bar.place"), panelButtonStyle))
            RunConfirm();

        GUI.enabled = true;
        GUI.backgroundColor = prevBg;
    }

    private void DrawFavoritesFloatPanel()
    {
        EnsureRightPanelStyles();
        float w = Mathf.Min(PanelWidth + 40f, Screen.width - 40f);
        float h = FavoritesPanelHeight;
        float px = (Screen.width - w) * 0.5f;
        float dockTop = centerDockRect.height > 0f
            ? centerDockRect.y
            : (Screen.height - ActionBarHeight - MenuBtnH - 30f);
        float py = dockTop - h - 10f;
        if (py < 8f)
            py = 8f;

        favoritesFloatRect = new Rect(px, py, w, h);
        DrawFavoritesPanel(px, py);
    }

    private bool CanToggleQuickFlip()
    {
        if (editPanelOpen && editTarget != null && editTarget.HasAuthoringLink)
            return true;
        return !string.IsNullOrEmpty(SpawnAuthoringState.PendingEnemyKey);
    }

    private bool GetQuickFlipState()
    {
        if (editPanelOpen && editTarget != null && editTarget.HasAuthoringLink)
            return editFlip;
        return SpawnAuthoringState.PendingFlipX;
    }

    private void ToggleQuickFlip()
    {
        if (editPanelOpen && editTarget != null && editTarget.HasAuthoringLink)
        {
            editFlip = !editFlip;
            ApplyEditTrapOrEnemyFlipLive();
            SaveEdit();
            statusLine = editFlip
                ? SpawnAuthoringLoc.T("status.flipOn")
                : SpawnAuthoringLoc.T("status.flipOff");
            return;
        }

        if (string.IsNullOrEmpty(SpawnAuthoringState.PendingEnemyKey))
            return;

        SpawnAuthoringState.PendingFlipX = !SpawnAuthoringState.PendingFlipX;
        statusLine = SpawnAuthoringState.PendingFlipX
            ? SpawnAuthoringLoc.T("status.flipOn")
            : SpawnAuthoringLoc.T("status.flipOff");
    }

    private float DrawStickyConfirm(float x, float y)
    {
        GUI.Box(new Rect(x, y, PanelWidth, StickyConfirmHeight), SpawnAuthoringLoc.T("ready.title"));
        GUI.Label(new Rect(x + 8f, y + 18f, PanelWidth - 16f, 16f),
            SpawnAuthoringState.FormatPendingFlagsShort());
        SpawnAuthoringState.PendingFlipX = GUI.Toggle(
            new Rect(x + 8f, y + 34f, 110f, 16f),
            SpawnAuthoringState.PendingFlipX,
            SpawnAuthoringLoc.T("flip"));
        GUI.enabled = SpawnAuthoringState.HasLastClick;
        if (GUI.Button(new Rect(x + 120f, y + 32f, PanelWidth - 128f, 18f), SpawnAuthoringLoc.T("btn.confirm")))
            RunConfirm();
        GUI.enabled = true;
        return y + StickyConfirmHeight + 8f;
    }

    private void RunConfirm()
    {
        if (!SpawnAuthoringState.ConfirmPendingEnemy())
        {
            statusLine = SpawnAuthoringLoc.T("status.pickEnemy");
            return;
        }

        string status;
        if (SpawnAuthoringState.ActiveCatalog == SpawnAuthoringState.CatalogKind.Gold)
            SpawnAuthoringPreviewSpawn.TryConfirmGoldSpawnAndSave(out status);
        else if (SpawnAuthoringState.ActiveCatalog == SpawnAuthoringState.CatalogKind.EventTrap)
            SpawnAuthoringPreviewSpawn.TryConfirmEventTrapAndSave(out status);
        else if (SpawnAuthoringState.IsTemplateCatalog)
            SpawnAuthoringPreviewSpawn.TryConfirmTrapSpawnAndSave(out status);
        else
            SpawnAuthoringPreviewSpawn.TryConfirmSpawnAndSave(out status);
        statusLine = status ?? SpawnAuthoringLoc.T("status.done");
    }

    private void WarmTrapListCache(bool lethal)
    {
        cachedKeys = lethal
            ? SpawnAuthoringTrapCatalog.GetLethalKeys()
            : SpawnAuthoringTrapCatalog.GetTrapKeys();
        cachedReady = new bool[cachedKeys.Length];
        cachedRowHeader = new bool[cachedKeys.Length];
        for (int i = 0; i < cachedKeys.Length; i++)
            cachedReady[i] = SpawnAuthoringTrapCatalog.IsReady(cachedKeys[i]);
        listCacheWarm = true;
    }

    private void WarmDecorListCache()
    {
        cachedKeys = SpawnDecorCatalog.GetAuthoringKeys();
        cachedReady = new bool[cachedKeys.Length];
        cachedRowHeader = new bool[cachedKeys.Length];
        for (int i = 0; i < cachedKeys.Length; i++)
            cachedReady[i] = SpawnAuthoringTrapCatalog.IsReady(cachedKeys[i]);
        listCacheWarm = true;
    }

    private void WarmHostageListCache()
    {
        string[] all = SpawnAuthoringHostageCatalog.GetKeys();
        var hostages = new List<string>();
        var spine = new List<string>();
        for (int i = 0; i < all.Length; i++)
        {
            if (SpawnAuthoringHostageCatalog.IsOtherSceneKey(all[i]))
                spine.Add(all[i]);
            else
                hostages.Add(all[i]);
        }

        int extra = spine.Count > 0 ? 1 : 0;
        cachedKeys = new string[hostages.Count + extra + spine.Count];
        cachedReady = new bool[cachedKeys.Length];
        cachedRowHeader = new bool[cachedKeys.Length];

        int n = 0;
        for (int i = 0; i < hostages.Count; i++)
        {
            cachedKeys[n] = hostages[i];
            cachedReady[n] = SpawnAuthoringHostageCatalog.IsReady(hostages[i]);
            n++;
        }

        if (spine.Count > 0)
        {
            cachedKeys[n] = SpawnAuthoringLoc.T("spine.title");
            cachedRowHeader[n] = true;
            n++;
            for (int i = 0; i < spine.Count; i++)
            {
                cachedKeys[n] = spine[i];
                cachedReady[n] = SpawnAuthoringHostageCatalog.IsReady(spine[i]);
                n++;
            }
        }

        listCacheWarm = true;
    }

    private float DrawTrapCatalogPanel(float x, float y)
    {
        EnsureRightPanelStyles();
        if (!listCacheWarm)
        {
            if (SpawnAuthoringState.HostagePanelOpen)
                WarmHostageListCache();
            else if (SpawnAuthoringState.DecorPanelOpen)
                WarmDecorListCache();
            else
                WarmTrapListCache(SpawnAuthoringState.LethalTrapPanelOpen);
        }

        string title = SpawnAuthoringState.HostagePanelOpen
            ? SpawnAuthoringLoc.T("hostage.title")
            : (SpawnAuthoringState.DecorPanelOpen
                ? SpawnAuthoringLoc.T("decor.title")
                : (SpawnAuthoringState.LethalTrapPanelOpen
                    ? SpawnAuthoringLoc.T("lethal.title")
                    : SpawnAuthoringLoc.T("traps.title")));

        Rect listRect = new Rect(x, y, PanelWidth, EnemiesListHeight);
        DrawChromePanel(listRect, title);
        bool unusedHelp = false;
        string helpKey = SpawnAuthoringState.HostagePanelOpen
            ? "help.hostage"
            : (SpawnAuthoringState.DecorPanelOpen
                ? "help.decor"
                : (SpawnAuthoringState.LethalTrapPanelOpen ? "help.lethal" : "help.traps"));
        DrawPanelHeaderTools(
            listRect,
            helpKey,
            collapse: false,
            ref unusedHelp);

        float previewW = 280f;
        float previewH = previewCollapsed ? 32f : EnemiesListHeight;
        enemyPreviewRect = new Rect(x - previewW - 10f, y, previewW, previewH);
        DrawChromePanel(enemyPreviewRect, SpawnAuthoringLoc.T("preview.title"));
        DrawPanelHeaderTools(enemyPreviewRect, "help.previewTrap", collapse: true, ref previewCollapsed);
        if (!previewCollapsed)
        {
            Rect previewContent = new Rect(
                enemyPreviewRect.x + 8f,
                enemyPreviewRect.y + 32f,
                previewW - 16f,
                previewH - 40f);
            SpawnAuthoringEnemyPreview.Draw(previewContent, SpawnAuthoringState.PendingEnemyKey, trapMode: true);
            DrawPreviewPlaceHint(previewContent);
        }
        else
            enemyPreviewRect = new Rect(x - previewW - 10f, y, previewW, previewH);

        Rect view = new Rect(listRect.x + 8f, listRect.y + 34f, PanelWidth - 16f, EnemiesListHeight - 42f);
        catalogListViewRect = view;
        float contentH = Mathf.Max(view.height, cachedKeys.Length * RowHeight);
        Rect content = new Rect(0f, 0f, view.width - 18f, contentH);
        enemiesScroll = GUI.BeginScrollView(view, enemiesScroll, content);

        int first = Mathf.Max(0, Mathf.FloorToInt(enemiesScroll.y / RowHeight) - 1);
        int visible = Mathf.CeilToInt(view.height / RowHeight) + 3;
        int last = Mathf.Min(cachedKeys.Length, first + visible);

        for (int i = first; i < last; i++)
        {
            string key = cachedKeys[i];
            Rect row = new Rect(0f, i * RowHeight, content.width, RowHeight - 2f);
            if (i < cachedRowHeader.Length && cachedRowHeader[i])
            {
                GUI.Label(row, key, panelMutedStyle);
                continue;
            }

            bool pending = string.Equals(key, SpawnAuthoringState.PendingEnemyKey, StringComparison.OrdinalIgnoreCase);
            bool confirmed = string.Equals(key, SpawnAuthoringState.SelectedEnemyKey, StringComparison.OrdinalIgnoreCase);
            bool ready = i < cachedReady.Length && cachedReady[i];

            string label = key;
            if (pending)
                label = "> " + label;
            if (confirmed)
                label = label + "  [OK]";
            if (!ready)
                label = label + "  (?)";

            Color prev = GUI.backgroundColor;
            if (pending)
                GUI.backgroundColor = new Color(0.95f, 0.85f, 0.35f, 1f);
            else if (confirmed)
                GUI.backgroundColor = new Color(0.45f, 0.85f, 0.45f, 1f);
            else if (!ready)
                GUI.backgroundColor = new Color(0.35f, 0.35f, 0.38f, 1f);

            if (GUI.Button(row, label, panelButtonStyle))
                SpawnAuthoringState.PendingEnemyKey = key;

            GUI.backgroundColor = prev;
        }

        GUI.EndScrollView();
        y += EnemiesListHeight + 6f;
        if (SpawnAuthoringState.HostagePanelOpen)
            y = DrawFactionPickerRow(x, y, ref SpawnAuthoringState.PendingFactionIndex);
        y = DrawTrapOptionsPanel(x, y);
        y = DrawFavoriteAddButton(x, y);

        Rect confirmRect = new Rect(x, y, PanelWidth, ConfirmRowHeight - 4f);
        GUI.enabled = !string.IsNullOrEmpty(SpawnAuthoringState.PendingEnemyKey);
        if (GUI.Button(confirmRect, SpawnAuthoringLoc.T("btn.confirmFull"), panelButtonStyle))
            RunConfirm();
        GUI.enabled = true;
        return y + ConfirmRowHeight;
    }

    private float DrawTrapOptionsPanel(float x, float y)
    {
        EnsureRightPanelStyles();
        bool hostage = SpawnAuthoringState.HostagePanelOpen;
        bool showRot = SpawnAuthoringState.CatalogAllowsRotation;
        float height = hostage ? TrapOptionsPanelHeightHostage : TrapOptionsPanelHeight;
        if (!showRot)
            height -= ExtraOptsRowHeight + 4f;
        Rect box = new Rect(x, y, PanelWidth, height);
        DrawChromePanel(box, SpawnAuthoringLoc.T("options.title"));
        bool unusedCollapse = false;
        DrawPanelHeaderTools(
            box,
            hostage ? "help.hostageOptions" : (showRot ? "help.trapOptions" : "help.lethalOptions"),
            collapse: false,
            ref unusedCollapse);

        float ax = x + 12f;
        float ay = y + 34f;
        float rowW = PanelWidth - 24f;

        SpawnAuthoringState.PendingFlipX = DrawOptionToggle(
            new Rect(ax, ay, rowW, ExtraOptsRowHeight),
            SpawnAuthoringState.PendingFlipX,
            SpawnAuthoringLoc.T("flip"));
        ay += ExtraOptsRowHeight + 4f;

        if (showRot)
        {
            if (GUI.Button(new Rect(ax, ay, 90f, ExtraOptsRowHeight), SpawnAuthoringLoc.T("rot.plus90"), panelButtonStyle))
            {
                SpawnAuthoringState.PendingRotationZ =
                    SpawnAuthoringPackEdit.CycleRotationZ(SpawnAuthoringState.PendingRotationZ);
                SpawnAuthoringState.SelectedRotationZ = SpawnAuthoringState.PendingRotationZ;
            }
            GUI.Label(new Rect(ax + 96f, ay, 70f, ExtraOptsRowHeight), SpawnAuthoringLoc.T("rot.degrees"), panelLabelStyle);
            string rotField = GUI.TextField(
                new Rect(ax + 168f, ay, 70f, ExtraOptsRowHeight),
                Mathf.RoundToInt(SpawnAuthoringState.PendingRotationZ).ToString(CultureInfo.InvariantCulture),
                panelTextFieldStyle);
            if (float.TryParse(rotField, NumberStyles.Float, CultureInfo.InvariantCulture, out float pendingDeg))
            {
                SpawnAuthoringState.PendingRotationZ = SpawnRotationUtility.NormalizeAngle(pendingDeg);
                SpawnAuthoringState.SelectedRotationZ = SpawnAuthoringState.PendingRotationZ;
            }
            if (GUI.Button(new Rect(ax + 242f, ay, 44f, ExtraOptsRowHeight), SpawnAuthoringLoc.T("rot.apply"), panelButtonStyle))
            {
                if (float.TryParse(rotField, NumberStyles.Float, CultureInfo.InvariantCulture, out float applyDeg))
                {
                    SpawnAuthoringState.PendingRotationZ = SpawnRotationUtility.NormalizeAngle(applyDeg);
                    SpawnAuthoringState.SelectedRotationZ = SpawnAuthoringState.PendingRotationZ;
                }
            }
            ay += ExtraOptsRowHeight + 4f;
        }

        string sortTok = SpawnAuthoringPackEdit.FormatSortToken(SpawnAuthoringState.PendingSortOffset)
                         ?? "sort0";
        GUI.Label(new Rect(ax, ay, 48f, ExtraOptsRowHeight), SpawnAuthoringLoc.T("sort"), panelLabelStyle);
        if (GUI.Button(new Rect(ax + 50f, ay, 40f, ExtraOptsRowHeight), "-10", panelButtonStyle))
            SpawnAuthoringState.PendingSortOffset -= 10;
        string sortField = GUI.TextField(
            new Rect(ax + 94f, ay, 64f, ExtraOptsRowHeight),
            SpawnAuthoringState.PendingSortOffset.ToString(CultureInfo.InvariantCulture),
            panelTextFieldStyle);
        if (int.TryParse(sortField, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pendingSort))
            SpawnAuthoringState.PendingSortOffset = pendingSort;
        if (GUI.Button(new Rect(ax + 162f, ay, 40f, ExtraOptsRowHeight), "+10", panelButtonStyle))
            SpawnAuthoringState.PendingSortOffset += 10;
        GUI.Label(new Rect(ax + 208f, ay, rowW - 208f, ExtraOptsRowHeight), sortTok, panelLabelStyle);
        ay += ExtraOptsRowHeight + 4f;

        if (hostage)
        {
            SpawnAuthoringState.PendingRandom = DrawOptionToggle(
                new Rect(ax, ay, 190f, RandomRowHeight),
                SpawnAuthoringState.PendingRandom,
                SpawnAuthoringLoc.T("random"));
            if (SpawnAuthoringState.PendingRandom)
            {
                GUI.Label(new Rect(ax + 200f, ay + 4f, 36f, 22f), "p=", panelLabelStyle);
                SpawnAuthoringState.PendingChanceStr = GUI.TextField(
                    new Rect(ax + 232f, ay + 2f, 80f, 24f),
                    SpawnAuthoringState.PendingChanceStr ?? "0.5",
                    panelTextFieldStyle);
            }
        }

        DrawPendingLineInOptions(box);
        return y + height + 6f;
    }

    private float DrawFavoritesPanel(float x, float y)
    {
        EnsureRightPanelStyles();
        Rect box = new Rect(x, y, PanelWidth, FavoritesPanelHeight);
        DrawChromePanel(box, SpawnAuthoringLoc.T("favorites.title"));

        float ax = x + 12f;
        float ay = y + 34f;
        float aw = PanelWidth - 20f;

        Color prevAdd = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.95f, 0.75f, 0.25f, 1f);
        if (GUI.Button(new Rect(ax, ay, aw, 28f), SpawnAuthoringLoc.T("favorites.addHere"), panelButtonStyle))
            AddPendingToFavorites();
        GUI.backgroundColor = prevAdd;

        var list = SpawnAuthoringFavorites.GetEntries();
        Rect view = new Rect(ax, ay + 34f, aw, FavoritesPanelHeight - 70f);
        float contentH = Mathf.Max(view.height, list.Count * RowHeight + 4f);
        favoritesScroll = GUI.BeginScrollView(view, favoritesScroll, new Rect(0f, 0f, view.width - 18f, contentH));

        for (int i = 0; i < list.Count; i++)
        {
            SpawnAuthoringFavorites.Entry e = list[i];
            float delW = 52f;
            Rect row = new Rect(0f, i * RowHeight, view.width - delW - 10f, RowHeight - 2f);
            Color prev = GUI.backgroundColor;
            if (string.Equals(e.EnemyKey, SpawnAuthoringState.PendingEnemyKey, StringComparison.OrdinalIgnoreCase))
                GUI.backgroundColor = new Color(0.95f, 0.85f, 0.35f, 1f);

            if (GUI.Button(row, SpawnAuthoringFavorites.FormatRow(e), panelButtonStyle))
            {
                string status;
                SpawnAuthoringFavorites.TryApplyAt(i, out status);
                statusLine = status ?? string.Empty;
                WarmCatalogForActive();
            }

            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.35f, 1f);
            if (GUI.Button(new Rect(view.width - delW - 2f, i * RowHeight, delW, RowHeight - 2f),
                    SpawnAuthoringLoc.T("btn.favDelete"), panelButtonStyle))
            {
                string status;
                SpawnAuthoringFavorites.TryRemoveAt(i, out status);
                statusLine = status ?? string.Empty;
                GUI.backgroundColor = prev;
                break;
            }

            GUI.backgroundColor = prev;
        }

        GUI.EndScrollView();

        if (list.Count == 0)
            GUI.Label(new Rect(ax, ay + 70f, aw, 44f), SpawnAuthoringLoc.T("favorites.empty"), panelLabelStyle);

        return y + FavoritesPanelHeight + 8f;
    }

    private float DrawEnemiesPanel(float x, float y)
    {
        EnsureRightPanelStyles();
        if (!listCacheWarm)
            WarmListCache();

        Rect listRect = new Rect(x, y, PanelWidth, EnemiesListHeight);
        DrawChromePanel(listRect, SpawnAuthoringLoc.T("enemies.title"));

        // Large portrait preview, same height as enemies list (collapsible).
        float previewW = 280f;
        float previewH = previewCollapsed ? 32f : EnemiesListHeight;
        enemyPreviewRect = new Rect(x - previewW - 10f, y, previewW, previewH);
        DrawChromePanel(enemyPreviewRect, SpawnAuthoringLoc.T("preview.title"));
        DrawPanelHeaderTools(enemyPreviewRect, "help.preview", collapse: true, ref previewCollapsed);
        if (!previewCollapsed)
        {
            Rect previewContent = new Rect(
                enemyPreviewRect.x + 8f,
                enemyPreviewRect.y + 32f,
                previewW - 16f,
                previewH - 40f);
            SpawnAuthoringEnemyPreview.Draw(previewContent, SpawnAuthoringState.PendingEnemyKey);
            DrawPreviewPlaceHint(previewContent);
        }
        else
            enemyPreviewRect = new Rect(x - previewW - 10f, y, previewW, previewH);

        Rect view = new Rect(listRect.x + 8f, listRect.y + 34f, PanelWidth - 16f, EnemiesListHeight - 42f);
        catalogListViewRect = view;
        float contentH = Mathf.Max(view.height, cachedKeys.Length * RowHeight);
        Rect content = new Rect(0f, 0f, view.width - 18f, contentH);
        enemiesScroll = GUI.BeginScrollView(view, enemiesScroll, content);

        int first = Mathf.Max(0, Mathf.FloorToInt(enemiesScroll.y / RowHeight) - 1);
        int visible = Mathf.CeilToInt(view.height / RowHeight) + 3;
        int last = Mathf.Min(cachedKeys.Length, first + visible);

        for (int i = first; i < last; i++)
        {
            string key = cachedKeys[i];
            bool unavailable = SpawnAuthoringEnemyCatalog.IsUnavailable(key);
            bool pending = !unavailable &&
                           string.Equals(key, SpawnAuthoringState.PendingEnemyKey, StringComparison.OrdinalIgnoreCase);
            bool confirmed = string.Equals(key, SpawnAuthoringState.SelectedEnemyKey, StringComparison.OrdinalIgnoreCase);
            bool ready = i < cachedReady.Length && cachedReady[i];

            string label = key;
            if (unavailable)
                label = label + "  " + SpawnAuthoringLoc.T("enemies.na");
            else
            {
                if (pending)
                    label = "> " + label;
                if (confirmed)
                    label = label + "  [OK]";
                if (!ready)
                    label = label + "  (?)";
            }

            Rect row = new Rect(0f, i * RowHeight, content.width, RowHeight - 2f);
            Color prev = GUI.backgroundColor;
            if (unavailable)
                GUI.backgroundColor = new Color(0.28f, 0.28f, 0.3f, 1f);
            else if (pending)
                GUI.backgroundColor = new Color(0.95f, 0.85f, 0.35f, 1f);
            else if (confirmed)
                GUI.backgroundColor = new Color(0.45f, 0.85f, 0.45f, 1f);

            if (GUI.Button(row, label, panelButtonStyle))
            {
                if (unavailable)
                    statusLine = SpawnAuthoringLoc.T("status.enemyUnavailable");
                else
                    SpawnAuthoringState.PendingEnemyKey = key;
            }

            GUI.backgroundColor = prev;
        }

        GUI.EndScrollView();

        y += EnemiesListHeight + 6f;
        y = DrawFactionPickerRow(x, y, ref SpawnAuthoringState.PendingFactionIndex);
        y = DrawPendingOptionsPanel(x, y);
        y = DrawFavoriteAddButton(x, y);

        Rect confirmRect = new Rect(x, y, PanelWidth, ConfirmRowHeight - 4f);
        GUI.enabled = !string.IsNullOrEmpty(SpawnAuthoringState.PendingEnemyKey);
        if (GUI.Button(confirmRect, SpawnAuthoringLoc.T("btn.confirmFull"), panelButtonStyle))
            RunConfirm();

        GUI.enabled = true;
        return y + ConfirmRowHeight;
    }

    private float DrawPendingOptionsPanel(float x, float y)
    {
        EnsureRightPanelStyles();
        Rect box = new Rect(x, y, PanelWidth, OptionsPanelHeight);
        DrawChromePanel(box, SpawnAuthoringLoc.T("options.title"));
        bool unusedCollapse = false;
        DrawPanelHeaderTools(box, "help.options", collapse: false, ref unusedCollapse);

        float ax = x + 12f;
        float ay = y + 34f;
        float rowW = PanelWidth - 24f;

        SpawnAuthoringState.PendingForceElite = DrawOptionToggle(
            new Rect(ax, ay, rowW, EliteRowHeight),
            SpawnAuthoringState.PendingForceElite,
            SpawnAuthoringLoc.T("elite"));
        ay += EliteRowHeight + 4f;

        SpawnAuthoringState.PendingFlipX = DrawOptionToggle(
            new Rect(ax, ay, rowW, ExtraOptsRowHeight),
            SpawnAuthoringState.PendingFlipX,
            SpawnAuthoringLoc.T("flip"));
        ay += ExtraOptsRowHeight + 4f;

        SpawnAuthoringState.PendingRandom = DrawOptionToggle(
            new Rect(ax, ay, 190f, RandomRowHeight),
            SpawnAuthoringState.PendingRandom,
            SpawnAuthoringLoc.T("random"));
        if (SpawnAuthoringState.PendingRandom)
        {
            GUI.Label(new Rect(ax + 200f, ay + 4f, 36f, 22f), "p=", panelLabelStyle);
            SpawnAuthoringState.PendingChanceStr = GUI.TextField(
                new Rect(ax + 232f, ay + 2f, 80f, 24f),
                SpawnAuthoringState.PendingChanceStr ?? "0.5",
                panelTextFieldStyle);
        }

        ay += RandomRowHeight + 4f;
        DrawPendingLineInOptions(box);
        return y + OptionsPanelHeight + 6f;
    }

    private float DrawFactionPickerRow(float x, float y, ref int factionIndex, bool allowEventCoreFaction = false)
    {
        EnsureRightPanelStyles();
        string[] factions = SpawnAuthoringPackEdit.FactionTokens;
        factionIndex = SpawnAuthoringState.ClampFactionIndex(factionIndex, allowEventCoreFaction);

        string token = factions[factionIndex];
        string label = SpawnAuthoringFactionUi.DisplayName(token);

        Rect box = new Rect(x, y, PanelWidth, FactionPanelHeight);
        DrawChromePanel(box, SpawnAuthoringLoc.T("faction.title"));
        bool unusedCollapse = false;
        DrawPanelHeaderTools(box, "help.faction", collapse: false, ref unusedCollapse);

        // Content: faction emblem + friendly name between arrows
        float rowY = y + 34f;
        float btnW = 34f;
        float btnH = 30f;
        float gap = 8f;
        float left = x + 12f;
        float right = x + PanelWidth - 12f;

        if (GUI.Button(new Rect(left, rowY, btnW, btnH), "<", panelButtonStyle))
            factionIndex = SpawnAuthoringState.CycleFactionIndex(factionIndex, -1, allowEventCoreFaction);

        if (GUI.Button(new Rect(right - btnW, rowY, btnW, btnH), ">", panelButtonStyle))
            factionIndex = SpawnAuthoringState.CycleFactionIndex(factionIndex, 1, allowEventCoreFaction);

        float iconX = left + btnW + gap;
        float icon = 36f;
        SpawnAuthoringFactionUi.DrawBadge(new Rect(iconX, rowY - 2f, icon, icon), token);

        float labelX = iconX + icon + gap;
        float labelW = (right - btnW - gap) - labelX;
        GUI.Label(new Rect(labelX, rowY + 4f, labelW, 22f), label, panelLabelStyle);

        return y + FactionPanelHeight + 6f;
    }

    private float DrawFavoriteAddButton(float x, float y)
    {
        Color prevFav = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.95f, 0.75f, 0.25f, 1f);
        GUI.enabled = !string.IsNullOrEmpty(SpawnAuthoringState.PendingEnemyKey);
        if (GUI.Button(new Rect(x, y, PanelWidth, FavAddRowHeight - 4f), SpawnAuthoringLoc.T("btn.addFavorite"), panelButtonStyle))
            AddPendingToFavorites();
        GUI.enabled = true;
        GUI.backgroundColor = prevFav;
        return y + FavAddRowHeight + 6f;
    }

    private void WarmGoldListCache()
    {
        cachedKeys = SpawnAuthoringGoldCatalog.GetKeys();
        cachedReady = new bool[cachedKeys.Length];
        cachedRowHeader = new bool[cachedKeys.Length];
        for (int i = 0; i < cachedKeys.Length; i++)
            cachedReady[i] = true;
        listCacheWarm = true;
    }

    private void WarmEventTrapListCache()
    {
        cachedKeys = SpawnAuthoringEventTrapCatalog.GetKeys();
        cachedReady = new bool[cachedKeys.Length];
        cachedRowHeader = new bool[cachedKeys.Length];
        for (int i = 0; i < cachedKeys.Length; i++)
            cachedReady[i] = true;
        listCacheWarm = true;
    }

    private void WarmCatalogForActive()
    {
        listCacheWarm = false;
        if (SpawnAuthoringState.EnemiesPanelOpen)
            WarmListCache();
        else if (SpawnAuthoringState.HostagePanelOpen)
            WarmHostageListCache();
        else if (SpawnAuthoringState.DecorPanelOpen)
            WarmDecorListCache();
        else if (SpawnAuthoringState.GoldPanelOpen)
            WarmGoldListCache();
        else if (SpawnAuthoringState.EventTrapPanelOpen)
            WarmEventTrapListCache();
        else if (SpawnAuthoringState.EventCorePanelOpen)
            WarmEventCoreListCache();
        else if (SpawnAuthoringState.IsTrapCatalog)
            WarmTrapListCache(SpawnAuthoringState.LethalTrapPanelOpen);
    }

    private void WarmEventCoreListCache()
    {
        cachedKeys = EventCoreDefinitionRegistry.GetAuthoringEventIds() ?? new string[0];
        cachedReady = new bool[cachedKeys.Length];
        cachedRowHeader = new bool[cachedKeys.Length];
        for (int i = 0; i < cachedKeys.Length; i++)
            cachedReady[i] = true;
        listCacheWarm = true;
    }

    private float DrawEventCoreCatalogPanel(float x, float y)
    {
        EnsureRightPanelStyles();
        if (!listCacheWarm)
            WarmEventCoreListCache();

        Rect listRect = new Rect(x, y, PanelWidth, EnemiesListHeight);
        DrawChromePanel(listRect, SpawnAuthoringLoc.T("eventcore.title"));
        bool unusedHelp = false;
        DrawPanelHeaderTools(listRect, "help.eventCore", collapse: false, ref unusedHelp);

        float previewW = 280f;
        float previewH = previewCollapsed ? 32f : EnemiesListHeight;
        enemyPreviewRect = new Rect(x - previewW - 10f, y, previewW, previewH);
        DrawChromePanel(enemyPreviewRect, SpawnAuthoringLoc.T("preview.title"));
        DrawPanelHeaderTools(enemyPreviewRect, "help.previewEventCore", collapse: true, ref previewCollapsed);
        if (!previewCollapsed)
        {
            Rect previewContent = new Rect(
                enemyPreviewRect.x + 8f,
                enemyPreviewRect.y + 32f,
                previewW - 16f,
                previewH - 40f);
            SpawnAuthoringEnemyPreview.Draw(
                previewContent, SpawnAuthoringState.GetPendingEventCoreBoundEnemyKey());
            DrawPreviewPlaceHint(previewContent);
        }
        else
            enemyPreviewRect = new Rect(x - previewW - 10f, y, previewW, previewH);

        Rect view = new Rect(listRect.x + 8f, listRect.y + 34f, PanelWidth - 16f, EnemiesListHeight - 42f);
        catalogListViewRect = view;
        float contentH = Mathf.Max(view.height, cachedKeys.Length * RowHeight);
        Rect content = new Rect(0f, 0f, view.width - 18f, contentH);
        enemiesScroll = GUI.BeginScrollView(view, enemiesScroll, content);

        int first = Mathf.Max(0, Mathf.FloorToInt(enemiesScroll.y / RowHeight) - 1);
        int visible = Mathf.CeilToInt(view.height / RowHeight) + 3;
        int last = Mathf.Min(cachedKeys.Length, first + visible);

        for (int i = first; i < last; i++)
        {
            string key = cachedKeys[i];
            Rect row = new Rect(0f, i * RowHeight, content.width, RowHeight - 2f);
            bool pending = string.Equals(key, SpawnAuthoringState.PendingEnemyKey, StringComparison.OrdinalIgnoreCase);
            string label = EventCoreDefinitionRegistry.FormatEventIdForUi(key);
            if (pending)
                label = "> " + label;
            Color prev = GUI.backgroundColor;
            if (pending)
                GUI.backgroundColor = new Color(0.95f, 0.85f, 0.35f, 1f);
            if (GUI.Button(row, label, panelButtonStyle))
                SpawnAuthoringState.PendingEnemyKey = key;
            GUI.backgroundColor = prev;
        }

        GUI.EndScrollView();
        y += EnemiesListHeight + 6f;
        y = DrawFactionPickerRow(x, y, ref SpawnAuthoringState.PendingFactionIndex, allowEventCoreFaction: true);
        y = DrawEventCoreOptionsPanel(x, y);
        y = DrawFavoriteAddButton(x, y);

        Rect confirmRect = new Rect(x, y, PanelWidth, ConfirmRowHeight - 4f);
        GUI.enabled = !string.IsNullOrEmpty(SpawnAuthoringState.PendingEnemyKey);
        if (GUI.Button(confirmRect, SpawnAuthoringLoc.T("btn.confirmFull"), panelButtonStyle))
            RunConfirm();
        GUI.enabled = true;
        return y + ConfirmRowHeight;
    }

    private float DrawEventCoreOptionsPanel(float x, float y)
    {
        EnsureRightPanelStyles();
        Rect box = new Rect(x, y, PanelWidth, EventCoreOptionsPanelHeight);
        DrawChromePanel(box, SpawnAuthoringLoc.T("options.title"));
        bool unusedCollapse = false;
        DrawPanelHeaderTools(box, "help.eventCoreOptions", collapse: false, ref unusedCollapse);

        float ax = x + 12f;
        float ay = y + 34f;
        float rowW = PanelWidth - 24f;

        SpawnAuthoringState.PendingForceElite = DrawOptionToggle(
            new Rect(ax, ay, rowW, EliteRowHeight),
            SpawnAuthoringState.PendingForceElite,
            SpawnAuthoringLoc.T("elite"));
        ay += EliteRowHeight + 4f;

        SpawnAuthoringState.PendingFlipX = DrawOptionToggle(
            new Rect(ax, ay, rowW, ExtraOptsRowHeight),
            SpawnAuthoringState.PendingFlipX,
            SpawnAuthoringLoc.T("flip"));
        ay += ExtraOptsRowHeight + 4f;

        SpawnAuthoringState.PendingRandom = DrawOptionToggle(
            new Rect(ax, ay, 190f, RandomRowHeight),
            SpawnAuthoringState.PendingRandom,
            SpawnAuthoringLoc.T("random"));
        if (SpawnAuthoringState.PendingRandom)
        {
            GUI.Label(new Rect(ax + 200f, ay + 4f, 36f, 22f), "p=", panelLabelStyle);
            SpawnAuthoringState.PendingChanceStr = GUI.TextField(
                new Rect(ax + 232f, ay + 2f, 80f, 24f),
                SpawnAuthoringState.PendingChanceStr ?? "0.5",
                panelTextFieldStyle);
        }

        ay += RandomRowHeight + 4f;
        GUI.Label(new Rect(ax, ay, 90f, ExtraOptsRowHeight), SpawnAuthoringLoc.T("eventcore.attach"), panelLabelStyle);
        SpawnAuthoringState.PendingEventCoreChanceStr = GUI.TextField(
            new Rect(ax + 94f, ay + 2f, 80f, 24f),
            SpawnAuthoringState.PendingEventCoreChanceStr ?? "1",
            panelTextFieldStyle);

        DrawPendingLineInOptions(box);
        return y + EventCoreOptionsPanelHeight + 6f;
    }

    private float DrawSpecialCatalogPanel(float x, float y)
    {
        EnsureRightPanelStyles();
        bool gold = SpawnAuthoringState.GoldPanelOpen;
        if (!listCacheWarm)
        {
            if (gold)
                WarmGoldListCache();
            else
                WarmEventTrapListCache();
        }

        string title = gold
            ? SpawnAuthoringLoc.T("gold.title")
            : SpawnAuthoringLoc.T("eventTrap.title");
        string helpKey = gold ? "help.gold" : "help.eventTrap";

        Rect listRect = new Rect(x, y, PanelWidth, EnemiesListHeight);
        DrawChromePanel(listRect, title);
        bool unusedHelp = false;
        DrawPanelHeaderTools(listRect, helpKey, collapse: false, ref unusedHelp);

        Rect view = new Rect(listRect.x + 8f, listRect.y + 34f, PanelWidth - 16f, EnemiesListHeight - 42f);
        catalogListViewRect = view;
        float contentH = Mathf.Max(view.height, cachedKeys.Length * RowHeight);
        Rect content = new Rect(0f, 0f, view.width - 18f, contentH);
        enemiesScroll = GUI.BeginScrollView(view, enemiesScroll, content);

        int first = Mathf.Max(0, Mathf.FloorToInt(enemiesScroll.y / RowHeight) - 1);
        int visible = Mathf.CeilToInt(view.height / RowHeight) + 3;
        int last = Mathf.Min(cachedKeys.Length, first + visible);

        for (int i = first; i < last; i++)
        {
            string key = cachedKeys[i];
            Rect row = new Rect(0f, i * RowHeight, content.width, RowHeight - 2f);
            bool pending = string.Equals(key, SpawnAuthoringState.PendingEnemyKey, StringComparison.OrdinalIgnoreCase);
            string label = pending ? ("> " + key) : key;
            Color prev = GUI.backgroundColor;
            if (pending)
                GUI.backgroundColor = new Color(0.95f, 0.85f, 0.35f, 1f);
            if (GUI.Button(row, label, panelButtonStyle))
                SpawnAuthoringState.PendingEnemyKey = key;
            GUI.backgroundColor = prev;
        }

        GUI.EndScrollView();
        y += EnemiesListHeight + 6f;

        if (gold)
            y = DrawGoldOptionsPanel(x, y);
        else
        {
            y = DrawEventTrapOptionsPanel(x, y);
            y = DrawFactionPickerRow(x, y, ref SpawnAuthoringState.PendingFactionIndex);
        }

        y = DrawFavoriteAddButton(x, y);

        Rect confirmRect = new Rect(x, y, PanelWidth, ConfirmRowHeight - 4f);
        GUI.enabled = !string.IsNullOrEmpty(SpawnAuthoringState.PendingEnemyKey);
        if (GUI.Button(confirmRect, SpawnAuthoringLoc.T("btn.confirmFull"), panelButtonStyle))
            RunConfirm();
        GUI.enabled = true;
        return y + ConfirmRowHeight;
    }

    private float DrawGoldOptionsPanel(float x, float y)
    {
        EnsureRightPanelStyles();
        Rect box = new Rect(x, y, PanelWidth, GoldOptionsPanelHeight);
        DrawChromePanel(box, SpawnAuthoringLoc.T("options.title"));
        bool unusedCollapse = false;
        DrawPanelHeaderTools(box, "help.goldOptions", collapse: false, ref unusedCollapse);

        float ax = x + 12f;
        float ay = y + 34f;
        SpawnAuthoringState.PendingRandom = DrawOptionToggle(
            new Rect(ax, ay, 190f, RandomRowHeight),
            SpawnAuthoringState.PendingRandom,
            SpawnAuthoringLoc.T("random"));
        if (SpawnAuthoringState.PendingRandom)
        {
            GUI.Label(new Rect(ax + 200f, ay + 4f, 36f, 22f), "p=", panelLabelStyle);
            SpawnAuthoringState.PendingChanceStr = GUI.TextField(
                new Rect(ax + 232f, ay + 2f, 80f, 24f),
                SpawnAuthoringState.PendingChanceStr ?? "0.5",
                    panelTextFieldStyle);
        }

        DrawPendingLineInOptions(box);
        return y + GoldOptionsPanelHeight + 6f;
    }

    private float DrawEventTrapOptionsPanel(float x, float y)
    {
        EnsureRightPanelStyles();
        Rect box = new Rect(x, y, PanelWidth, EventTrapOptionsPanelHeight);
        DrawChromePanel(box, SpawnAuthoringLoc.T("options.title"));
        bool unusedCollapse = false;
        DrawPanelHeaderTools(box, "help.eventTrapOptions", collapse: false, ref unusedCollapse);

        float ax = x + 12f;
        float ay = y + 34f;
        float rowW = PanelWidth - 24f;
        float half = (rowW - 8f) * 0.5f;

        GUI.Label(new Rect(ax, ay, 52f, ExtraOptsRowHeight), SpawnAuthoringLoc.T("etrap.count"), panelLabelStyle);
        SpawnAuthoringState.PendingEventTrapCountStr = GUI.TextField(
            new Rect(ax + 54f, ay, half - 54f, ExtraOptsRowHeight),
            SpawnAuthoringState.PendingEventTrapCountStr ?? string.Empty,
            panelTextFieldStyle);
        GUI.Label(new Rect(ax + half + 8f, ay, 40f, ExtraOptsRowHeight), SpawnAuthoringLoc.T("etrap.dist"), panelLabelStyle);
        SpawnAuthoringState.PendingEventTrapDistStr = GUI.TextField(
            new Rect(ax + half + 50f, ay, half - 50f, ExtraOptsRowHeight),
            SpawnAuthoringState.PendingEventTrapDistStr ?? string.Empty,
            panelTextFieldStyle);
        ay += ExtraOptsRowHeight + 4f;

        string sidesLabel = SpawnAuthoringState.PendingEventTrapSidesIndex == 1
            ? SpawnAuthoringLoc.T("etrap.sidesBoth")
            : (SpawnAuthoringState.PendingEventTrapSidesIndex == 2
                ? SpawnAuthoringLoc.T("etrap.sidesRight")
                : SpawnAuthoringLoc.T("etrap.sidesPack"));
        if (GUI.Button(new Rect(ax, ay, half, ExtraOptsRowHeight), sidesLabel, panelButtonStyle))
            SpawnAuthoringState.PendingEventTrapSidesIndex =
                (SpawnAuthoringState.PendingEventTrapSidesIndex + 1) % 3;
        GUI.Label(new Rect(ax + half + 8f, ay, 40f, ExtraOptsRowHeight), SpawnAuthoringLoc.T("etrap.max"), panelLabelStyle);
        SpawnAuthoringState.PendingEventTrapMaxStr = GUI.TextField(
            new Rect(ax + half + 50f, ay, half - 50f, ExtraOptsRowHeight),
            SpawnAuthoringState.PendingEventTrapMaxStr ?? string.Empty,
            panelTextFieldStyle);
        ay += ExtraOptsRowHeight + 4f;

        GUI.Label(new Rect(ax, ay, 52f, ExtraOptsRowHeight), SpawnAuthoringLoc.T("etrap.zone"), panelLabelStyle);
        SpawnAuthoringState.PendingEventTrapRadiusStr = GUI.TextField(
            new Rect(ax + 54f, ay, half - 54f, ExtraOptsRowHeight),
            SpawnAuthoringState.PendingEventTrapRadiusStr ?? string.Empty,
            panelTextFieldStyle);
        GUI.Label(new Rect(ax + half + 8f, ay, 48f, ExtraOptsRowHeight), SpawnAuthoringLoc.T("etrap.delay"), panelLabelStyle);
        SpawnAuthoringState.PendingEventTrapDelayStr = GUI.TextField(
            new Rect(ax + half + 58f, ay, half - 58f, ExtraOptsRowHeight),
            SpawnAuthoringState.PendingEventTrapDelayStr ?? string.Empty,
            panelTextFieldStyle);

        DrawPendingLineInOptions(box);
        return y + EventTrapOptionsPanelHeight + 6f;
    }

    private void DrawPreviewPlaceHint(Rect previewContent)
    {
        EnsureRightPanelStyles();
        string hint = previewGrabArmed
            ? SpawnAuthoringLoc.T("preview.dragRelease")
            : SpawnAuthoringLoc.T("preview.dragPlace");
        GUI.Label(
            new Rect(previewContent.x + 4f, previewContent.yMax - 22f, previewContent.width - 8f, 20f),
            hint,
            panelMutedStyle);
    }

    private void DrawGrabFollowHint()
    {
        if (!previewGrabArmed)
            return;
        EnsureRightPanelStyles();
        Vector2 m = GuiMouse();
        GUI.Label(
            new Rect(m.x + 14f, m.y + 16f, 280f, 20f),
            SpawnAuthoringLoc.T("preview.dragRelease"),
            panelTitleStyle);
    }

    private bool TryGetCatalogListKeyAtMouse(out string key)
    {
        key = null;
        if (catalogListViewRect.width <= 0f || cachedKeys == null || cachedKeys.Length == 0)
            return false;

        Vector2 m = GuiMouse();
        if (!catalogListViewRect.Contains(m))
            return false;
        if (m.x > catalogListViewRect.xMax - 18f)
            return false;

        float localY = m.y - catalogListViewRect.y + enemiesScroll.y;
        int index = Mathf.FloorToInt(localY / RowHeight);
        if (index < 0 || index >= cachedKeys.Length)
            return false;
        if (cachedRowHeader != null && index < cachedRowHeader.Length && cachedRowHeader[index])
            return false;

        key = cachedKeys[index];
        if (string.IsNullOrEmpty(key))
            return false;
        if (SpawnAuthoringState.EnemiesPanelOpen && SpawnAuthoringEnemyCatalog.IsUnavailable(key))
            return false;
        return true;
    }

    private void TickPreviewGrabPlace()
    {
        Event e = Event.current;
        if (e == null)
            return;

        bool hasKey = !string.IsNullOrEmpty(SpawnAuthoringState.PendingEnemyKey);
        bool overPreview = !previewCollapsed &&
                           enemyPreviewRect.width > 0f &&
                           enemyPreviewRect.Contains(GuiMouse());

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            if (overPreview && hasKey)
            {
                previewGrabArmed = true;
                previewGrabMoved = false;
                e.Use();
                return;
            }

            if (TryGetCatalogListKeyAtMouse(out string listKey))
            {
                SpawnAuthoringState.PendingEnemyKey = listKey;
                previewGrabArmed = true;
                previewGrabMoved = false;
                e.Use();
                return;
            }
        }

        if (!previewGrabArmed)
            return;

        if (e.type == EventType.MouseDrag && e.button == 0)
            previewGrabMoved = true;

        if (e.rawType != EventType.MouseUp || e.button != 0)
            return;

        hasKey = !string.IsNullOrEmpty(SpawnAuthoringState.PendingEnemyKey);
        previewGrabArmed = false;
        previewGrabMoved = false;
        if (!hasKey)
            return;

        Vector2 gui = GuiMouse();
        if (uiBlockRect.Contains(gui) || enemyPreviewRect.Contains(gui) || actionBarRect.Contains(gui) ||
            catalogListViewRect.Contains(gui) || helpBtnRect.Contains(gui) || helpGuideRect.Contains(gui))
            return;

        if (!SpawnAuthoringWorldPick.TryGetMouseWorld(out Vector2 world))
            return;

        string packHint = SpawnAuthoringPackWriter.GetActivePackFileName();
        SpawnAuthoringState.SetLastClick(world.x, world.y, packHint);
        RunConfirm();
        e.Use();
    }

    private void DrawCoordsLine(string label, string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, panelMutedStyle, GUILayout.Width(92f));
        GUILayout.Label(value ?? "—", panelLabelStyle);
        GUILayout.EndHorizontal();
    }

    private void DrawCoordsLineWrapped(string label, string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, panelMutedStyle, GUILayout.Width(92f));
        GUILayout.Label(value ?? "—", panelMutedStyle);
        GUILayout.EndHorizontal();
    }

    private void DrawPendingLineInOptions(Rect box)
    {
        EnsureRightPanelStyles();
        string line = SpawnAuthoringState.FormatPendingLinePreview();
        GUI.Label(
            new Rect(box.x + 12f, box.yMax - 50f, box.width - 24f, 46f),
            SpawnAuthoringLoc.T("coords.line") + ": " + (line ?? "—"),
            panelMutedStyle);
    }

    private void DrawChromePanel(Rect r, string title)
    {
        EnsureRightPanelStyles();
        Color prev = GUI.color;

        // Soft shadow
        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.DrawTexture(new Rect(r.x + 3f, r.y + 4f, r.width, r.height), Texture2D.whiteTexture);

        // Body
        GUI.color = Color.white;
        if (texPanelFill != null)
            GUI.DrawTexture(r, texPanelFill);
        else
        {
            GUI.color = new Color(0.07f, 0.07f, 0.09f, 0.94f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
        }

        bool hasTitle = !string.IsNullOrEmpty(title);

        // Header band
        if (hasTitle)
        {
            if (texPanelHeader != null)
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, 28f), texPanelHeader);
            else
            {
                GUI.color = new Color(0.14f, 0.11f, 0.09f, 0.98f);
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, 28f), Texture2D.whiteTexture);
            }
        }

        // Brand accents
        GUI.color = BrandOrange;
        GUI.DrawTexture(new Rect(r.x, r.y, 3f, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), Texture2D.whiteTexture);

        GUI.color = Color.white;
        if (hasTitle)
        {
            // Centered title; leave room for ? / − tools on the right.
            GUI.Label(new Rect(r.x + 28f, r.y + 3f, r.width - 56f, 22f), title, panelTitleStyle);
        }
        GUI.color = prev;
    }

    /// <summary>Compact chrome strip for bottom dock / action bar (no title header).</summary>
    private void DrawChromeBar(Rect r)
    {
        EnsureRightPanelStyles();
        Color prev = GUI.color;

        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.DrawTexture(new Rect(r.x + 2f, r.y + 3f, r.width, r.height), Texture2D.whiteTexture);

        GUI.color = Color.white;
        if (texPanelFill != null)
            GUI.DrawTexture(r, texPanelFill);
        else
        {
            GUI.color = new Color(0.07f, 0.07f, 0.09f, 0.94f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
        }

        GUI.color = BrandOrange;
        GUI.DrawTexture(new Rect(r.x, r.y, 3f, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), Texture2D.whiteTexture);

        GUI.color = prev;
    }

    /// <summary>
    /// Header tools on chrome panels: ? help (hover card) and optional −/+ collapse.
    /// Replaces the old unused faction swatch sitting in the Placement title bar.
    /// </summary>
    private void DrawPanelHeaderTools(Rect panel, string helpKey, bool collapse, ref bool collapsed)
    {
        EnsureRightPanelStyles();
        float btn = HeaderToolBtn;
        float right = panel.xMax - 6f;
        float top = panel.y + 3f;
        Color prevBg = GUI.backgroundColor;

        if (collapse)
        {
            Rect collapseR = new Rect(right - btn, top, btn, btn);
            GUI.backgroundColor = new Color(0.35f, 0.35f, 0.38f, 1f);
            if (GUI.Button(collapseR, collapsed ? "+" : "−", panelButtonStyle))
                collapsed = !collapsed;
            right -= btn + 4f;
        }

        Rect helpR = new Rect(right - btn, top, btn, btn);
        // Hover-only help (no click toast — duplicates the card).
        Color prevGui = GUI.color;
        GUI.color = new Color(0.55f, 0.58f, 0.65f, 1f);
        GUI.DrawTexture(helpR, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUIStyle q = new GUIStyle(panelButtonStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 13
        };
        q.normal.background = null;
        q.hover.background = null;
        q.active.background = null;
        q.normal.textColor = Color.white;
        GUI.Label(helpR, "?", q);
        GUI.color = prevGui;

        if (helpR.Contains(GuiMouse()))
            hoverHelpKey = helpKey;

        GUI.backgroundColor = prevBg;
    }

    private void DrawHoverHelpCard()
    {
        if (string.IsNullOrEmpty(hoverHelpKey))
            return;

        EnsureRightPanelStyles();
        string body = SpawnAuthoringLoc.T(hoverHelpKey);
        if (string.IsNullOrEmpty(body))
            return;

        float maxW = Mathf.Min(420f, Screen.width - 40f);
        GUIContent content = new GUIContent(body);
        float textH = panelMutedStyle.CalcHeight(content, maxW - 28f);
        float w = maxW;
        float h = textH + 36f;
        float px = (Screen.width - w) * 0.5f;
        float py = Mathf.Max(48f, (Screen.height - h) * 0.28f);
        Rect r = new Rect(px, py, w, h);

        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.DrawTexture(new Rect(r.x + 3f, r.y + 4f, r.width, r.height), Texture2D.whiteTexture);
        GUI.color = new Color(0.08f, 0.08f, 0.1f, 0.96f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = BrandOrange;
        GUI.DrawTexture(new Rect(r.x, r.y, 3f, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(r.x + 14f, r.y + 8f, w - 28f, 20f), SpawnAuthoringLoc.T("help.title"), panelTitleStyle);
        GUI.Label(new Rect(r.x + 14f, r.y + 28f, w - 28f, textH + 4f), body, panelMutedStyle);
        GUI.color = prev;
    }

    private void EnsureRightPanelStyles()
    {
        if (panelLabelStyle != null)
            return;

        texPanelFill = MakeSolidTex(new Color(0.07f, 0.07f, 0.09f, 0.94f));
        texPanelHeader = MakeSolidTex(new Color(0.15f, 0.11f, 0.08f, 0.98f));
        texBtnNormal = MakeSolidTex(new Color(0.18f, 0.17f, 0.16f, 0.98f));
        texBtnHover = MakeSolidTex(new Color(0.28f, 0.22f, 0.16f, 1f));
        texBtnActive = MakeSolidTex(new Color(0.55f, 0.32f, 0.1f, 1f));

        panelLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = PanelFontSize,
            wordWrap = true,
            richText = false,
            clipping = TextClipping.Clip
        };
        panelLabelStyle.normal.textColor = new Color(0.96f, 0.95f, 0.93f, 1f);

        panelTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = PanelTitleFontSize,
            fontStyle = FontStyle.Bold,
            wordWrap = false,
            clipping = TextClipping.Clip,
            alignment = TextAnchor.MiddleCenter
        };
        panelTitleStyle.normal.textColor = BrandOrange;

        panelMutedStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = PanelFontSize - 1,
            wordWrap = true,
            clipping = TextClipping.Clip
        };
        panelMutedStyle.normal.textColor = new Color(0.72f, 0.7f, 0.66f, 1f);

        panelBoxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = PanelTitleFontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(10, 10, 6, 6)
        };

        panelButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = PanelFontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Clip,
            border = new RectOffset(4, 4, 4, 4),
            padding = new RectOffset(8, 8, 4, 4)
        };
        panelButtonStyle.normal.background = texBtnNormal;
        panelButtonStyle.hover.background = texBtnHover;
        panelButtonStyle.active.background = texBtnActive;
        panelButtonStyle.focused.background = texBtnHover;
        panelButtonStyle.normal.textColor = Color.white;
        panelButtonStyle.hover.textColor = Color.white;
        panelButtonStyle.active.textColor = Color.white;
        panelButtonStyle.focused.textColor = Color.white;

        panelToggleStyle = new GUIStyle(GUI.skin.toggle)
        {
            fontSize = PanelFontSize,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(4, 4, 2, 2)
        };

        panelTextFieldStyle = new GUIStyle(GUI.skin.textField)
        {
            fontSize = PanelFontSize,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(8, 8, 4, 4)
        };
        panelTextFieldStyle.normal.textColor = Color.white;
        panelTextFieldStyle.focused.textColor = Color.white;
    }

    private static Texture2D MakeSolidTex(Color c)
    {
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        t.SetPixels(new[] { c, c, c, c });
        t.Apply(false, true);
        return t;
    }

    /// <summary>Custom toggle: checkbox left, label right — no overlap with Unity skin quirks.</summary>
    private bool DrawOptionToggle(Rect row, bool value, string label)
    {
        EnsureRightPanelStyles();
        const float box = 18f;
        float midY = row.y + (row.height - box) * 0.5f;
        Rect hit = row;
        Rect boxR = new Rect(row.x + 2f, midY, box, box);
        Rect textR = new Rect(boxR.xMax + 10f, row.y, row.width - box - 14f, row.height);

        Event ev = Event.current;
        if (ev != null && ev.type == EventType.MouseDown && ev.button == 0 && hit.Contains(ev.mousePosition))
        {
            value = !value;
            ev.Use();
        }

        Color prev = GUI.color;
        GUI.color = new Color(0.12f, 0.12f, 0.14f, 0.9f);
        GUI.DrawTexture(boxR, Texture2D.whiteTexture);
        GUI.color = value
            ? new Color(1f, 0.55f, 0.1f, 1f)
            : new Color(0.55f, 0.55f, 0.58f, 1f);
        GUI.DrawTexture(new Rect(boxR.x, boxR.y, boxR.width, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(boxR.x, boxR.yMax - 2f, boxR.width, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(boxR.x, boxR.y, 2f, boxR.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(boxR.xMax - 2f, boxR.y, 2f, boxR.height), Texture2D.whiteTexture);

        if (value)
        {
            GUI.color = new Color(1f, 0.55f, 0.1f, 1f);
            GUI.DrawTexture(new Rect(boxR.x + 4f, boxR.y + 4f, box - 8f, box - 8f), Texture2D.whiteTexture);
        }

        GUI.color = Color.white;
        GUI.Label(textR, label ?? string.Empty, panelLabelStyle);
        GUI.color = prev;
        return value;
    }

    private void PushEditPreviewToOutline()
    {
        if (!editPanelOpen || editTarget == null || !editTarget.HasAuthoringLink)
        {
            SpawnAuthoringOutline.ClearEditPreview();
            return;
        }

        if (!float.TryParse(editXStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float ex) ||
            !float.TryParse(editYStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float ey))
        {
            SpawnAuthoringOutline.ClearEditPreview();
            return;
        }

        SpawnAuthoringOutline.SetEditPreview(true, ex, ey);
    }

    private void DrawEditEventCoreRow()
    {
        string[] ids = EventCoreDefinitionRegistry.GetAuthoringEventIds();
        int max = ids != null ? ids.Length : 0;
        int index = SpawnAuthoringState.IndexOfEventCore(editEventCoreId);
        if (index < 0 || index > max)
            index = 0;

        GUILayout.BeginHorizontal();
        GUILayout.Label(SpawnAuthoringLoc.T("eventcore.title"), panelLabelStyle, GUILayout.Width(80f));
        if (GUILayout.Button("<", panelButtonStyle, GUILayout.Width(28f)))
            index = (index + max) % (max + 1);
        bool hasEvent = index > 0 && ids != null && index - 1 < ids.Length;
        string label = hasEvent
            ? EventCoreDefinitionRegistry.FormatEventIdForUi(ids[index - 1])
            : SpawnAuthoringLoc.T("eventcore.none");
        GUILayout.Label(label, panelLabelStyle);
        if (GUILayout.Button(">", panelButtonStyle, GUILayout.Width(28f)))
            index = (index + 1) % (max + 1);
        GUILayout.EndHorizontal();

        hasEvent = index > 0 && ids != null && index - 1 < ids.Length;

        editEventCoreId = hasEvent ? ids[index - 1] : string.Empty;
        if (hasEvent &&
            EventCoreDefinitionRegistry.TryGetBoundEnemyKey(editEventCoreId, out string boundKey) &&
            !string.IsNullOrEmpty(boundKey))
            editKey = boundKey;

        if (hasEvent)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(SpawnAuthoringLoc.T("eventcore.attach"), panelLabelStyle, GUILayout.Width(90f));
            editEventCoreChanceStr = GUILayout.TextField(
                editEventCoreChanceStr ?? "1", panelTextFieldStyle);
            GUILayout.EndHorizontal();
        }
    }

    private float CurrentEditPanelHeight()
    {
        float h;
        if (editIsTrap && SpawnAuthoringHostageCatalog.AcceptsFaction(editKey))
            h = EditPanelHeightHostage;
        else
            h = editIsTrap ? EditPanelHeightTrap : EditPanelHeight;
        if (editIsTrap && !SpawnAuthoringState.KeyAllowsRotation(editKey))
            h -= ExtraOptsRowHeight * 2f + 8f;
        if (editIsEventCore)
            h += ExtraOptsRowHeight * 2f + 12f;
        return h;
    }

    private void DrawEditPanel(float x, float y)
    {
        EnsureRightPanelStyles();
        float panelH = CurrentEditPanelHeight();
        Rect box = new Rect(x, y, PanelWidth, panelH);
        DrawChromePanel(box, SpawnAuthoringLoc.T("edit.title"));

        float ax = x + 12f;
        float ay = y + 34f;
        float aw = PanelWidth - 20f;

        if (!string.IsNullOrEmpty(editStatus))
        {
            bool flashing = Time.unscaledTime < saveFlashUntil;
            Color prevC = GUI.contentColor;
            if (flashing)
                GUI.contentColor = saveFlashOk
                    ? new Color(0.35f, 1f, 0.45f, 1f)
                    : new Color(1f, 0.45f, 0.35f, 1f);
            GUI.Label(new Rect(ax, ay, aw, 22f), editStatus, panelLabelStyle);
            GUI.contentColor = prevC;
        }

        float fieldsTop = ay + 26f;
        float fieldsH = panelH - (fieldsTop - y) - 40f;
        GUILayout.BeginArea(new Rect(ax, fieldsTop, aw, fieldsH));

        if (editTarget == null || !editTarget.HasAuthoringLink)
        {
            GUILayout.Label(SpawnAuthoringLoc.T("edit.vanilla"), panelLabelStyle);
            GUILayout.EndArea();
            if (GUI.Button(new Rect(ax, y + panelH - 36f, aw, 30f), SpawnAuthoringLoc.T("btn.closeEdit"), panelButtonStyle))
                CloseEditPanel();
            return;
        }

        GUILayout.Label(SpawnAuthoringLoc.T("edit.key"), panelLabelStyle);
        editKey = GUILayout.TextField(editKey ?? string.Empty, panelTextFieldStyle);

        if (editIsEventCore)
            DrawEditEventCoreRow();

        if (!editIsTrap && !editIsGold)
        {
            string[] factions = SpawnAuthoringPackEdit.FactionTokens;
            editFactionIndex = SpawnAuthoringState.ClampFactionIndex(editFactionIndex, editIsEventCore);
            string factionToken = factions[editFactionIndex];
            string factionLabel = SpawnAuthoringFactionUi.DisplayName(factionToken);
            GUILayout.BeginHorizontal();
            GUILayout.Label(SpawnAuthoringLoc.T("edit.faction"), panelLabelStyle, GUILayout.Width(80f));
            Rect swatch = GUILayoutUtility.GetRect(18f, 18f, GUILayout.Width(18f), GUILayout.Height(18f));
            SpawnAuthoringFactionUi.DrawSwatch(swatch, factionToken);
            if (GUILayout.Button("<", panelButtonStyle, GUILayout.Width(28f)))
                editFactionIndex = SpawnAuthoringState.CycleFactionIndex(editFactionIndex, -1, editIsEventCore);
            GUILayout.Label(factionLabel, panelLabelStyle, GUILayout.Width(140f));
            if (GUILayout.Button(">", panelButtonStyle, GUILayout.Width(28f)))
                editFactionIndex = SpawnAuthoringState.CycleFactionIndex(editFactionIndex, 1, editIsEventCore);
            GUILayout.EndHorizontal();

            editRandom = DrawOptionToggle(
                GUILayoutUtility.GetRect(PanelWidth - 40f, ExtraOptsRowHeight),
                editRandom,
                SpawnAuthoringLoc.T("edit.random"));
            if (editRandom)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(SpawnAuthoringLoc.T("edit.chance"), panelLabelStyle, GUILayout.Width(70f));
                editChanceStr = GUILayout.TextField(editChanceStr ?? "0.5", panelTextFieldStyle);
                GUILayout.EndHorizontal();
            }
        }

        if (!editIsGold && !editIsAnchor)
        {
            bool prevFlip = editFlip;
            editFlip = DrawOptionToggle(
                GUILayoutUtility.GetRect(PanelWidth - 40f, ExtraOptsRowHeight),
                editFlip,
                SpawnAuthoringLoc.T("flip"));
            if (editTarget != null && editFlip != prevFlip)
            {
                ApplyEditTrapOrEnemyFlipLive();
                SaveEdit();
            }
        }

        if (editIsAnchor)
        {
            GUILayout.Label(SpawnAuthoringLoc.T("help.eventTrapGizmo"), panelMutedStyle);
        }
        else if (editIsTrap && SpawnAuthoringHostageCatalog.AcceptsFaction(editKey))
        {
            string[] factions = SpawnAuthoringPackEdit.FactionTokens;
            editFactionIndex = SpawnAuthoringState.ClampFactionIndex(editFactionIndex, false);
            string factionToken = factions[editFactionIndex];
            string factionLabel = SpawnAuthoringFactionUi.DisplayName(factionToken);
            GUILayout.BeginHorizontal();
            GUILayout.Label(SpawnAuthoringLoc.T("edit.faction"), panelLabelStyle, GUILayout.Width(80f));
            Rect swatch = GUILayoutUtility.GetRect(18f, 18f, GUILayout.Width(18f), GUILayout.Height(18f));
            SpawnAuthoringFactionUi.DrawSwatch(swatch, factionToken);
            if (GUILayout.Button("<", panelButtonStyle, GUILayout.Width(28f)))
                editFactionIndex = SpawnAuthoringState.CycleFactionIndex(editFactionIndex, -1, false);
            GUILayout.Label(factionLabel, panelLabelStyle, GUILayout.Width(140f));
            if (GUILayout.Button(">", panelButtonStyle, GUILayout.Width(28f)))
                editFactionIndex = SpawnAuthoringState.CycleFactionIndex(editFactionIndex, 1, false);
            GUILayout.EndHorizontal();
        }

        if (editIsGold)
        {
            GUILayout.Label(SpawnAuthoringLoc.T("gold.title"), panelLabelStyle);
        }
        else if (editIsTrap)
        {
            if (SpawnAuthoringState.KeyAllowsRotation(editKey))
            {
                GUILayout.Label(SpawnAuthoringLoc.T("rot"), panelLabelStyle);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(SpawnAuthoringLoc.T("rot.plus90"), panelButtonStyle, GUILayout.Width(90f)))
                    BumpEditRotation90();
                GUILayout.Label(SpawnAuthoringLoc.T("rot.degrees"), panelLabelStyle, GUILayout.Width(70f));
                editRotStr = GUILayout.TextField(editRotStr ?? "0", panelTextFieldStyle);
                if (GUILayout.Button(SpawnAuthoringLoc.T("rot.apply"), panelButtonStyle, GUILayout.Width(44f)))
                    TryApplyEditRotationFromField();
                GUILayout.EndHorizontal();
            }

            GUILayout.Label(SpawnAuthoringLoc.T("sort"), panelLabelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-10", panelButtonStyle, GUILayout.Width(40f)))
            {
                editSortOffset -= 10;
                SyncEditSortStr();
            }
            editSortStr = GUILayout.TextField(editSortStr ?? "0", panelTextFieldStyle);
            if (GUILayout.Button("+10", panelButtonStyle, GUILayout.Width(40f)))
            {
                editSortOffset += 10;
                SyncEditSortStr();
            }
            if (GUILayout.Button(SpawnAuthoringLoc.T("rot.apply"), panelButtonStyle, GUILayout.Width(44f)))
                TryApplyEditSortFromField();
            GUILayout.Label(
                SpawnAuthoringPackEdit.FormatSortToken(editSortOffset) ?? "sort0",
                panelLabelStyle,
                GUILayout.Width(70f));
            GUILayout.EndHorizontal();
        }
        else
        {
            editForceElite = DrawOptionToggle(
                GUILayoutUtility.GetRect(PanelWidth - 40f, ExtraOptsRowHeight),
                editForceElite,
                SpawnAuthoringLoc.T("elite"));
        }

        Vector3 cur = editTarget.transform.position;
        GUILayout.Label(
            SpawnAuthoringLoc.Tf(
                "edit.packXy",
                editTarget.AuthoringSpawnX.ToString("F2", CultureInfo.InvariantCulture),
                editTarget.AuthoringSpawnY.ToString("F2", CultureInfo.InvariantCulture)),
            panelLabelStyle);
        GUILayout.Label(
            SpawnAuthoringLoc.Tf(
                "edit.currentXy",
                cur.x.ToString("F2", CultureInfo.InvariantCulture),
                cur.y.ToString("F2", CultureInfo.InvariantCulture)),
            panelLabelStyle);

        GUILayout.BeginHorizontal();
        GUILayout.Label(SpawnAuthoringLoc.T("edit.x"), panelLabelStyle, GUILayout.Width(50f));
        editXStr = GUILayout.TextField(editXStr ?? "0", panelTextFieldStyle);
        GUILayout.Label("Y", panelLabelStyle, GUILayout.Width(18f));
        editYStr = GUILayout.TextField(editYStr ?? "0", panelTextFieldStyle);
        GUILayout.EndHorizontal();

        if (GUILayout.Button(SpawnAuthoringLoc.T("btn.useCurrentPos"), panelButtonStyle))
            TryUseCurrentPositionHotkey();

        GUILayout.EndArea();

        if (GUI.Button(new Rect(ax, y + panelH - 36f, aw, 30f), SpawnAuthoringLoc.T("btn.closeEdit"), panelButtonStyle))
            CloseEditPanel();
    }

    private void SaveEdit()
    {
        if (editTarget == null || !editTarget.HasAuthoringLink)
        {
            NotifySaveFailed(SpawnAuthoringLoc.T("status.noPackLink"));
            return;
        }

        if (string.IsNullOrEmpty(editKey))
        {
            NotifySaveFailed(SpawnAuthoringLoc.T("status.keyRequired"));
            return;
        }

        if (!float.TryParse(editXStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
            !float.TryParse(editYStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
        {
            NotifySaveFailed(SpawnAuthoringLoc.T("status.badXy"));
            return;
        }

        const int count = 1;

        string line;
        string faction = null;
        float chance = 1f;
        bool elite = false;

        if (editIsGold)
        {
            string goldKey = SpawnAuthoringGoldCatalog.NormalizeRangeToken(editKey);
            if (string.IsNullOrEmpty(goldKey))
            {
                NotifySaveFailed(SpawnAuthoringLoc.T("status.goldRequired"));
                return;
            }

            editKey = goldKey;
            if (editRandom)
            {
                if (!float.TryParse(editChanceStr, NumberStyles.Float, CultureInfo.InvariantCulture, out chance))
                    chance = 0.5f;
                chance = Mathf.Clamp01(chance);
                if (chance >= 0.999f)
                    chance = 0.99f;
            }

            line = SpawnAuthoringPackEdit.BuildGoldLine(x, y, goldKey, chance, count);
        }
        else if (editIsAnchor || SpawnAuthoringPackEdit.IsAnchorManagedInstance(editTarget))
        {
            if (!SpawnAuthoringPackEdit.TryRelocateLine(editTarget.AuthoringSourceLineRaw, x, y, out line) ||
                string.IsNullOrEmpty(line))
            {
                line = SpawnAuthoringPackEdit.BuildEventTrapLine(x, y, editKey.Trim());
            }
        }
        else if (editIsTrap)
        {
            if (float.TryParse(editRotStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float deg))
                editRotationZ = SpawnRotationUtility.NormalizeAngle(deg);
            if (int.TryParse(editSortStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sortParsed))
                editSortOffset = sortParsed;

            string trapKey = editKey.Trim();
            if (!SpawnAuthoringPackEdit.IsUsableTrapKey(trapKey))
            {
                string resolvedTrapKey = SpawnAuthoringPackEdit.ResolveTrapAuthoringKey(editTarget);
                if (!string.IsNullOrEmpty(resolvedTrapKey))
                    trapKey = resolvedTrapKey;
            }
            editKey = trapKey;

            if (SpawnAuthoringHostageCatalog.AcceptsFaction(trapKey) &&
                editFactionIndex >= 0 &&
                editFactionIndex < SpawnAuthoringPackEdit.FactionTokens.Length)
            {
                faction = SpawnAuthoringPackEdit.FactionTokens[editFactionIndex];
                if (string.IsNullOrEmpty(faction))
                    faction = null;
            }

            if (!SpawnAuthoringState.KeyAllowsRotation(trapKey))
                editRotationZ = 0f;

            line = SpawnAuthoringPackEdit.BuildTrapLine(
                x, y, trapKey, count, editFlip, editRotationZ, editSortOffset, faction);
        }
        else
        {
            if (editRandom)
            {
                if (!float.TryParse(editChanceStr, NumberStyles.Float, CultureInfo.InvariantCulture, out chance))
                    chance = 0.5f;
                chance = Mathf.Clamp01(chance);
                if (chance >= 0.999f)
                    chance = 0.99f;
            }

            faction = SpawnAuthoringPackEdit.FactionTokens[editFactionIndex];
            if (!editIsEventCore && SpawnAuthoringState.IsEventCoreEncounterToken(faction))
                faction = null;
            elite = editForceElite;
            string eventCoreId = null;
            float eventCoreChance = 1f;
            if (editIsEventCore && !string.IsNullOrEmpty(editEventCoreId))
            {
                eventCoreId = editEventCoreId.Trim();
                if (!float.TryParse(editEventCoreChanceStr, NumberStyles.Float, CultureInfo.InvariantCulture,
                        out eventCoreChance))
                    eventCoreChance = 1f;
                eventCoreChance = Mathf.Clamp01(eventCoreChance);
                if (eventCoreChance <= 0f)
                    eventCoreChance = 0.01f;
            }

            line = SpawnAuthoringPackEdit.BuildEnemyLine(
                x, y, editKey.Trim(), faction, chance, count, editFlip, elite, eventCoreId, eventCoreChance);
        }

        string err;
        int resolved;
        string previousLine;
        if (!SpawnAuthoringPackEdit.TryReplaceLinkedLine(
                editTarget, line, out resolved, out previousLine, out err))
        {
            NotifySaveFailed(err ?? SpawnAuthoringLoc.T("status.saveFailed"));
            return;
        }

        SpawnAuthoringUndo.RecordReplace(
            editTarget.AuthoringPackPath, resolved, previousLine, line);

        editTarget.ApplyAuthoringLink(
            editTarget.AuthoringPackPath,
            resolved,
            line,
            x,
            y,
            editKey.Trim(),
            faction,
            chance,
            count,
            editFlip,
            elite,
            editIsTrap ? editRotationZ : 0f,
            editIsTrap ? editSortOffset : 0,
            editIsTrap);

        // Spikes: do not keep SpawnFixedFacing unless the pack actually has ,flip.
        // That component rewrites scale every LateUpdate and was collapsing rotated traps.
        if (!editIsAnchor)
            SpawnFlipUtility.ApplyAuthoringFlip(editTarget.gameObject, editFlip);

        editTarget.transform.position = new Vector3(x, y, editTarget.transform.position.z);
        SpawnAuthoringState.SetLastClickSilent(x, y, SpawnAuthoringState.LastClickPackHint);
        if (!editIsTrap && !editIsGold && !editIsAnchor)
            ApplyEditEventCoreHostLive();
        if (editIsTrap && !editIsAnchor && SpawnAuthoringState.KeyAllowsRotation(editKey))
        {
            ApplyEditTrapRotationLive();
            SyncEditRotStr();
            SyncEditSortStr();
        }

        editStatus = SpawnAuthoringLoc.Tf(
            "status.savedEdit",
            System.IO.Path.GetFileName(editTarget.AuthoringPackPath),
            resolved + 1,
            editFlip ? "  [flip]" : "");
        statusLine = editStatus;
        saveFlashOk = true;
        saveFlashUntil = Time.unscaledTime + 2f;
    }

    private void DrawSaveFlashToast()
    {
        if (Time.unscaledTime >= saveFlashUntil || string.IsNullOrEmpty(editStatus))
            return;

        float w = 420f;
        float h = 28f;
        Rect r = new Rect((Screen.width - w) * 0.5f, 44f, w, h);
        Color prev = GUI.color;
        GUI.color = saveFlashOk
            ? new Color(0.12f, 0.45f, 0.22f, 0.92f)
            : new Color(0.5f, 0.15f, 0.12f, 0.92f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(r, "  " + editStatus);
        GUI.color = prev;
    }

    private void ShowCenterToast(string title, string detail, Color accent)
    {
        centerToastTitle = title ?? string.Empty;
        centerToastDetail = detail ?? string.Empty;
        centerToastAccent = accent;
        centerToastUntil = Time.unscaledTime + 1.45f;
    }

    private void DrawCenterActionToast()
    {
        if (Time.unscaledTime >= centerToastUntil || string.IsNullOrEmpty(centerToastTitle))
            return;

        EnsureCenterToastStyles();
        float life = Mathf.Clamp01((centerToastUntil - Time.unscaledTime) / 1.45f);
        float alpha = life > 0.2f ? 0.94f : (life / 0.2f) * 0.94f;

        Vector2 titleSize = centerToastTitleStyle.CalcSize(new GUIContent(centerToastTitle));
        Vector2 detailSize = string.IsNullOrEmpty(centerToastDetail)
            ? Vector2.zero
            : centerToastDetailStyle.CalcSize(new GUIContent(centerToastDetail));

        float innerW = Mathf.Max(titleSize.x, detailSize.x);
        float w = Mathf.Clamp(innerW + 48f, 220f, 520f);
        float h = string.IsNullOrEmpty(centerToastDetail) ? 52f : 72f;
        float px = (Screen.width - w) * 0.5f;
        float py = (Screen.height - h) * 0.5f - 24f;
        Rect r = new Rect(px, py, w, h);

        Color prev = GUI.color;
        GUI.color = new Color(0.06f, 0.06f, 0.07f, alpha * 0.82f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);

        GUI.color = new Color(centerToastAccent.r, centerToastAccent.g, centerToastAccent.b, alpha);
        GUI.DrawTexture(new Rect(r.x, r.y, 4f, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - 3f, r.width, 3f), Texture2D.whiteTexture);

        GUI.color = new Color(1f, 1f, 1f, alpha);
        float textX = r.x + 18f;
        float titleY = string.IsNullOrEmpty(centerToastDetail) ? r.y + (h - titleSize.y) * 0.5f : r.y + 12f;
        GUI.Label(new Rect(textX, titleY, w - 28f, titleSize.y + 4f), centerToastTitle, centerToastTitleStyle);
        if (!string.IsNullOrEmpty(centerToastDetail))
        {
            GUI.color = new Color(0.85f, 0.85f, 0.85f, alpha);
            GUI.Label(
                new Rect(textX, r.y + 38f, w - 28f, detailSize.y + 4f),
                centerToastDetail,
                centerToastDetailStyle);
        }

        GUI.color = prev;
    }

    private void EnsureCenterToastStyles()
    {
        if (centerToastTitleStyle != null)
            return;

        centerToastTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
            clipping = TextClipping.Overflow
        };
        centerToastTitleStyle.normal.textColor = Color.white;

        centerToastDetailStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
            clipping = TextClipping.Overflow
        };
        centerToastDetailStyle.normal.textColor = Color.white;
    }

    private void NotifySaveFailed(string message)
    {
        editStatus = message ?? SpawnAuthoringLoc.T("status.saveFailed");
        statusLine = editStatus;
        saveFlashOk = false;
        saveFlashUntil = Time.unscaledTime + 2.5f;
    }

    /// <summary>
    /// Delete locked (or edit-panel) HellGate pack spawn: remove pack line + destroy instance.
    /// Hotkey: Delete.
    /// </summary>
    private void TryDeleteSelected()
    {
        int deleted;
        string status;
        if (!TryDeleteCurrentSelection(out deleted, out status))
        {
            statusLine = status ?? SpawnAuthoringLoc.T("status.deleteFailed");
            editStatus = statusLine;
            return;
        }

        statusLine = status;
        editStatus = status;
        ShowCenterToast(
            SpawnAuthoringLoc.T("toast.deleted"),
            deleted.ToString(CultureInfo.InvariantCulture),
            new Color(0.85f, 0.35f, 0.3f, 1f));
    }

    private void TryCutSelected()
    {
        string copyStatus;
        bool copied = SpawnAuthoringRegion.HasRect
            ? SpawnAuthoringClipboard.TryCopyRegion(out copyStatus)
            : SpawnAuthoringClipboard.TryCopyFrom(ResolveCopyTarget(), out copyStatus);
        if (!copied)
        {
            statusLine = copyStatus ?? SpawnAuthoringLoc.T("status.cutFailed");
            editStatus = statusLine;
            return;
        }

        int deleted;
        string delStatus;
        if (!TryDeleteCurrentSelection(out deleted, out delStatus))
        {
            statusLine = SpawnAuthoringLoc.Tf("status.copyButDeleteFailed", copyStatus, delStatus ?? "?");
            editStatus = statusLine;
            ShowCenterToast(
                SpawnAuthoringLoc.T("toast.copied"),
                SpawnAuthoringClipboard.FormatShort(),
                new Color(0.35f, 0.85f, 1f, 1f));
            return;
        }

        statusLine = SpawnAuthoringLoc.Tf("status.cutCount", deleted);
        editStatus = statusLine;
        ShowCenterToast(
            SpawnAuthoringLoc.T("toast.cut"),
            SpawnAuthoringClipboard.FormatShort(),
            new Color(0.75f, 0.45f, 0.85f, 1f));
    }

    private bool TryDeleteCurrentSelection(out int deleted, out string status)
    {
        deleted = 0;
        status = null;
        if (SpawnAuthoringRegion.HasRect)
            return TryDeleteRegion(out deleted, out status);

        SpawnManagedInstance target = editTarget;
        if (target == null || !target.HasAuthoringLink)
        {
            GameObject locked = SpawnAuthoringWorldPick.Locked;
            if (locked != null)
            {
                target = locked.GetComponent<SpawnManagedInstance>()
                    ?? locked.GetComponentInChildren<SpawnManagedInstance>(true);
            }
        }

        if (target == null || !target.HasAuthoringLink)
        {
            status = SpawnAuthoringLoc.T("status.selectPackSpawn");
            return false;
        }

        string err;
        int resolved;
        string removed;
        string packPath = target.AuthoringPackPath;
        string enemyKey = target.AuthoringEnemyKey;
        float sx = target.AuthoringSpawnX;
        float sy = target.AuthoringSpawnY;
        string faction = target.AuthoringFactionIdRaw;
        float chance = target.AuthoringChance;
        bool flip = target.AuthoringFlipX;
        bool elite = target.AuthoringForceElite || target.ForceElite;

        if (!SpawnAuthoringPackEdit.TryDeleteLinkedLine(target, out resolved, out removed, out err))
        {
            status = err ?? SpawnAuthoringLoc.T("status.deleteFailed");
            return false;
        }

        SpawnAuthoringUndo.RecordDelete(
            packPath, resolved, removed, enemyKey, sx, sy, faction, chance, flip, elite);

        CloseEditPanel();
        SpawnAuthoringWorldPick.ClearLock();
        SpawnAuthoringOutline.Hide();
        deleted = 1;
        status = SpawnAuthoringLoc.Tf("status.deletedLine", resolved + 1,
                 string.IsNullOrEmpty(removed) ? "" : (": " + removed));
        return true;
    }

    private bool TryDeleteRegion(out int deleted, out string status)
    {
        deleted = 0;
        status = null;
        List<SpawnAuthoringRegion.Item> items;
        SpawnAuthoringRegion.Collect(out items);
        if (items == null || items.Count == 0)
        {
            status = SpawnAuthoringLoc.T("status.regionEmpty");
            return false;
        }

        var live = new List<SpawnManagedInstance>(items.Count);
        SpawnAuthoringRegion.CollectLiveManaged(live);
        live.Sort((a, b) => b.AuthoringLineIndex.CompareTo(a.AuthoringLineIndex));

        for (int i = 0; i < live.Count; i++)
        {
            SpawnManagedInstance target = live[i];
            if (target == null || !target.HasAuthoringLink)
                continue;

            string packPath = target.AuthoringPackPath;
            string enemyKey = target.AuthoringEnemyKey;
            float sx = target.AuthoringSpawnX;
            float sy = target.AuthoringSpawnY;
            string faction = target.AuthoringFactionIdRaw;
            float chance = target.AuthoringChance;
            bool flip = target.AuthoringFlipX;
            bool elite = target.AuthoringForceElite || target.ForceElite;
            int resolved;
            string removed;
            string err;
            if (!SpawnAuthoringPackEdit.TryDeleteLinkedLine(target, out resolved, out removed, out err))
                continue;

            SpawnAuthoringUndo.RecordDelete(
                packPath, resolved, removed, enemyKey, sx, sy, faction, chance, flip, elite);
            deleted++;
        }

        string leftoverPack = HellGateLocationSpawnRefresh.GetActiveSpawnConfigPath();
        if (!string.IsNullOrEmpty(leftoverPack))
        {
            var leftover = new List<KeyValuePair<int, SpawnAuthoringRegion.Item>>();
            var seenIdx = new HashSet<int>();
            for (int i = 0; i < items.Count; i++)
            {
                SpawnAuthoringRegion.Item item = items[i];
                int idx;
                string removed;
                string err;
                if (!SpawnAuthoringPackEdit.TryResolveLineIndex(
                        leftoverPack, -1, item.Line, item.WorldX, item.WorldY, item.Key,
                        out idx, out removed, out err))
                    continue;
                if (!seenIdx.Add(idx))
                    continue;
                leftover.Add(new KeyValuePair<int, SpawnAuthoringRegion.Item>(idx, item));
            }

            leftover.Sort((a, b) => b.Key.CompareTo(a.Key));
            for (int i = 0; i < leftover.Count; i++)
            {
                int idx = leftover[i].Key;
                SpawnAuthoringRegion.Item item = leftover[i].Value;
                string err;
                if (!SpawnAuthoringPackEdit.TryDeleteLine(leftoverPack, idx, out err))
                    continue;
                SpawnAuthoringPackEdit.DestroyInstancesForDeletedLine(leftoverPack, idx, item.Line);
                SpawnAuthoringPackEdit.ReindexAfterDelete(leftoverPack, idx);
                SpawnAuthoringUndo.RecordDelete(
                    leftoverPack, idx, item.Line, item.Key, item.WorldX, item.WorldY,
                    string.Empty, 1f, false, false);
                deleted++;
            }
        }

        SpawnAuthoringRegion.Clear();
        CloseEditPanel();
        SpawnAuthoringWorldPick.ClearLock();
        SpawnAuthoringOutline.Hide();

        if (deleted <= 0)
        {
            status = SpawnAuthoringLoc.T("status.deleteRegionFailed");
            return false;
        }

        status = SpawnAuthoringLoc.Tf("status.deletedCount", deleted);
        return true;
    }

    private void TryUndo()
    {
        string status;
        if (!SpawnAuthoringUndo.TryUndo(out status))
        {
            statusLine = status ?? SpawnAuthoringLoc.T("status.undoEmpty");
            editStatus = statusLine;
            return;
        }

        CloseEditPanel();
        SpawnAuthoringWorldPick.ClearLock();
        SpawnAuthoringOutline.Hide();
        statusLine = status;
        editStatus = status;
    }

    private SpawnManagedInstance ResolveCopyTarget()
    {
        if (editTarget != null && editTarget.HasAuthoringLink)
            return editTarget;

        GameObject locked = SpawnAuthoringWorldPick.Locked;
        if (locked == null)
            return null;

        SpawnManagedInstance managed = locked.GetComponent<SpawnManagedInstance>()
            ?? locked.GetComponentInChildren<SpawnManagedInstance>(true);
        if (managed == null || !managed.HasAuthoringLink)
            return null;
        return managed;
    }

    /// <summary>Write a dragged instance's live XY into the pack immediately.</summary>
    internal static void TryCommitDraggedInstance(GameObject go)
    {
        if (go == null)
            return;

        SpawnManagedInstance managed = go.GetComponent<SpawnManagedInstance>()
            ?? go.GetComponentInChildren<SpawnManagedInstance>(true);
        if (managed == null || !managed.HasAuthoringLink)
        {
            if (instance != null)
            {
                instance.statusLine = SpawnAuthoringLoc.T("status.noPackLinkXy");
                instance.editStatus = instance.statusLine;
            }
            return;
        }

        string status;
        if (TryCommitManagedLivePosition(managed, false, out status) && instance != null)
        {
            instance.statusLine = status ?? SpawnAuthoringLoc.T("status.savedXy");
            instance.editStatus = instance.statusLine;
        }
        else if (instance != null && !string.IsNullOrEmpty(status))
        {
            instance.statusLine = status;
            instance.editStatus = status;
        }
    }

    /// <summary>
    /// Write F11-moved instances into the pack (not in-game AI/physics drift), then
    /// the selected Edit XY if the user typed a new position.
    /// </summary>
    internal static bool TryCommitLivePositionBeforeReload(out string status)
    {
        status = null;
        bool any = TryCommitAuthoringMovedPositions();
        SpawnAuthoringOverlayHost host = instance;
        SpawnManagedInstance selected = host != null ? host.ResolveCopyTarget() : null;
        if (selected != null && selected.HasAuthoringLink && host != null &&
            host.editPanelOpen && host.editTarget == selected &&
            float.TryParse(host.editXStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float ex) &&
            float.TryParse(host.editYStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float ey))
        {
            Vector3 live = selected.transform.position;
            float dx = ex - live.x;
            float dy = ey - live.y;
            if (dx * dx + dy * dy >= 0.0025f)
            {
                string one;
                if (TryCommitManagedLivePosition(selected, true, out one))
                {
                    any = true;
                    status = one;
                }
                else if (!string.IsNullOrEmpty(one))
                    status = one;
            }
        }

        return any;
    }

    private static bool TryCommitAuthoringMovedPositions()
    {
        SpawnManagedInstance[] all = Object.FindObjectsOfType<SpawnManagedInstance>();
        if (all == null || all.Length == 0)
            return false;

        bool any = false;
        for (int i = 0; i < all.Length; i++)
        {
            SpawnManagedInstance managed = all[i];
            if (managed == null || !managed.HasAuthoringLink || !managed.AuthoringMovedInEditor)
                continue;

            string ignored;
            if (TryCommitManagedLivePosition(managed, false, out ignored))
                any = true;
        }

        return any;
    }

    private static bool TryCommitManagedLivePosition(
        SpawnManagedInstance managed,
        bool preferEditFields,
        out string status)
    {
        status = null;
        if (managed == null || !managed.HasAuthoringLink)
            return false;

        SpawnAuthoringOverlayHost host = instance;
        Vector3 live = managed.transform.position;
        float x = Mathf.Round(live.x * 100f) / 100f;
        float y = Mathf.Round(live.y * 100f) / 100f;

        const int count = 1;
        string key = managed.AuthoringEnemyKey ?? string.Empty;
        if (string.IsNullOrEmpty(key) &&
            SpawnAuthoringPackEdit.TryExtractKeyAndCoords(managed.AuthoringSourceLineRaw, out string fromLine, out _, out _))
            key = fromLine;

        if (string.IsNullOrEmpty(key))
        {
            status = SpawnAuthoringLoc.T("status.keyRequired");
            return false;
        }

        bool isGold = SpawnAuthoringPackEdit.IsGoldManagedInstance(managed);
        bool isAnchor = !isGold && SpawnAuthoringPackEdit.IsAnchorManagedInstance(managed);
        bool isTrap = !isGold && !isAnchor && SpawnAuthoringPackEdit.IsTrapManagedInstance(managed);
        string line;
        string faction = managed.AuthoringFactionIdRaw;
        float chance = managed.AuthoringChance > 0f ? managed.AuthoringChance : 1f;
        bool elite = managed.AuthoringForceElite || managed.ForceElite;
        bool flip = managed.AuthoringFlipX;
        float rot = managed.AuthoringRotationZ;
        int sort = managed.AuthoringSortOffset;

        if (preferEditFields &&
            host != null && host.editPanelOpen && host.editTarget == managed &&
            float.TryParse(host.editXStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float ex) &&
            float.TryParse(host.editYStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float ey))
        {
            x = Mathf.Round(ex * 100f) / 100f;
            y = Mathf.Round(ey * 100f) / 100f;
        }

        if (isTrap)
        {
            string resolvedKey = SpawnAuthoringPackEdit.ResolveTrapAuthoringKey(managed);
            if (!string.IsNullOrEmpty(resolvedKey))
                key = resolvedKey;

            if (host != null && host.editPanelOpen && host.editTarget == managed)
            {
                flip = host.editFlip;
                if (float.TryParse(host.editRotStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float deg))
                    rot = SpawnRotationUtility.NormalizeAngle(deg);
                else
                    rot = host.editRotationZ;
                if (int.TryParse(host.editSortStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sortParsed))
                    sort = sortParsed;
                else
                    sort = host.editSortOffset;
            }
            else if (Mathf.Abs(rot) < 0.01f && sort == 0 && !flip)
            {
                SpawnAuthoringPackEdit.TryExtractPlacementExtras(
                    managed.AuthoringSourceLineRaw, out flip, out rot, out sort);
            }

            if (SpawnAuthoringState.KeyAllowsRotation(key))
            {
                if (Mathf.Abs(rot) < 0.01f)
                    rot = SpawnRotationUtility.ReadRotationZ(managed.gameObject);
            }
            else
            {
                rot = 0f;
            }
        }

        if (SpawnAuthoringPackEdit.TryRelocateLine(managed.AuthoringSourceLineRaw, x, y, out string relocated) &&
            !string.IsNullOrEmpty(relocated) &&
            !(host != null && host.editPanelOpen && host.editTarget == managed && isTrap && preferEditFields) &&
            !(isTrap && !SpawnAuthoringState.KeyAllowsRotation(key)))
        {
            line = relocated;
        }
        else if (isGold)
        {
            string goldKey = SpawnAuthoringGoldCatalog.NormalizeRangeToken(key);
            if (string.IsNullOrEmpty(goldKey))
                SpawnAuthoringPackEdit.TryExtractKeyAndCoords(
                    managed.AuthoringSourceLineRaw, out goldKey, out _, out _);
            goldKey = SpawnAuthoringGoldCatalog.NormalizeRangeToken(goldKey);
            if (string.IsNullOrEmpty(goldKey))
            {
                status = SpawnAuthoringLoc.T("status.goldRequired");
                return false;
            }

            key = goldKey;
            line = SpawnAuthoringPackEdit.BuildGoldLine(x, y, goldKey, chance, count);
        }
        else if (isAnchor)
        {
            if (!SpawnAuthoringPackEdit.TryRelocateLine(managed.AuthoringSourceLineRaw, x, y, out line) ||
                string.IsNullOrEmpty(line))
                line = SpawnAuthoringPackEdit.BuildEventTrapLine(x, y, key.Trim());
        }
        else if (isTrap)
        {
            string trapFaction = faction;
            if (host != null && host.editPanelOpen && host.editTarget == managed &&
                SpawnAuthoringHostageCatalog.AcceptsFaction(key) &&
                host.editFactionIndex >= 0 &&
                host.editFactionIndex < SpawnAuthoringPackEdit.FactionTokens.Length)
            {
                trapFaction = SpawnAuthoringPackEdit.FactionTokens[host.editFactionIndex];
                if (string.IsNullOrEmpty(trapFaction))
                    trapFaction = null;
            }

            line = SpawnAuthoringPackEdit.BuildTrapLine(
                x, y, key.Trim(), count, flip, rot, sort, trapFaction);
            faction = trapFaction;
        }
        else
        {
            line = SpawnAuthoringPackEdit.BuildEnemyLine(
                x, y, key.Trim(), faction, chance, count, flip, elite);
        }

        string err;
        int resolved;
        string previousLine;
        if (!SpawnAuthoringPackEdit.TryReplaceLinkedLine(
                managed, line, out resolved, out previousLine, out err))
        {
            status = err ?? SpawnAuthoringLoc.T("status.commitPosFailed");
            return false;
        }

        SpawnAuthoringUndo.RecordReplace(managed.AuthoringPackPath, resolved, previousLine, line);
        managed.ApplyAuthoringLink(
            managed.AuthoringPackPath,
            resolved,
            line,
            x,
            y,
            key.Trim(),
            faction,
            chance,
            count,
            flip,
            elite,
            isTrap ? rot : 0f,
            isTrap ? sort : 0,
            isTrap || isAnchor);

        SpawnAuthoringState.SetLastClickSilent(x, y, SpawnAuthoringState.LastClickPackHint);

        if (host != null && host.editTarget == managed)
        {
            host.editXStr = x.ToString("F2", CultureInfo.InvariantCulture);
            host.editYStr = y.ToString("F2", CultureInfo.InvariantCulture);
            if (isTrap)
            {
                host.editIsTrap = true;
                host.editKey = key.Trim();
                host.editFlip = flip;
                host.editRotationZ = rot;
                host.editSortOffset = sort;
                host.SyncEditRotStr();
                host.SyncEditSortStr();
            }
            if (isAnchor)
                host.editIsAnchor = true;
            host.statusLine = SpawnAuthoringLoc.T("status.savedXy");
            host.editStatus = host.statusLine;
        }

        status = SpawnAuthoringLoc.Tf(
            "status.savedKeyAt",
            key,
            x.ToString("F2", CultureInfo.InvariantCulture),
            y.ToString("F2", CultureInfo.InvariantCulture),
            resolved + 1);
        Plugin.Log?.LogInfo("[SPAWN AUTHORING] Commit position: " + line);
        return true;
    }

    private void TryUseCurrentPositionHotkey()
    {
        if (editTarget == null)
        {
            if (SpawnAuthoringWorldPick.Locked == null)
            {
                statusLine = SpawnAuthoringLoc.T("status.selectEnemy");
                return;
            }
            TryOpenEditForLocked();
        }

        if (editTarget == null || !editTarget.HasAuthoringLink)
        {
            statusLine = SpawnAuthoringLoc.T("status.editDisabled");
            editStatus = statusLine;
            return;
        }

        Vector3 cur = editTarget.transform.position;
        editXStr = cur.x.ToString("F2", CultureInfo.InvariantCulture);
        editYStr = cur.y.ToString("F2", CultureInfo.InvariantCulture);
        SaveEdit();
    }

    private void TryDuplicateSelected()
    {
        string copyStatus;
        bool copied = SpawnAuthoringRegion.HasRect
            ? SpawnAuthoringClipboard.TryCopyRegion(out copyStatus)
            : SpawnAuthoringClipboard.TryCopyFrom(ResolveCopyTarget(), out copyStatus);
        if (!copied)
        {
            statusLine = copyStatus ?? SpawnAuthoringLoc.T("status.copyFailed");
            editStatus = statusLine;
            return;
        }

        if (!TryPasteClipboard(false))
            return;

        ShowCenterToast(
            SpawnAuthoringLoc.T("toast.duplicated"),
            SpawnAuthoringClipboard.FormatShort(),
            new Color(1f, 0.55f, 0.1f, 1f));
    }

    private void TryCopySelected()
    {
        string status;
        if (SpawnAuthoringRegion.HasRect)
        {
            if (!SpawnAuthoringClipboard.TryCopyRegion(out status))
            {
                statusLine = status ?? SpawnAuthoringLoc.T("status.copyFailed");
                editStatus = statusLine;
                return;
            }
        }
        else if (!SpawnAuthoringClipboard.TryCopyFrom(ResolveCopyTarget(), out status))
        {
            statusLine = status ?? SpawnAuthoringLoc.T("status.copyFailed");
            editStatus = statusLine;
            return;
        }

        statusLine = status;
        editStatus = status;
        ShowCenterToast(
            SpawnAuthoringLoc.T("toast.copied"),
            SpawnAuthoringClipboard.FormatShort(),
            new Color(0.35f, 0.85f, 1f, 1f));
    }

    private bool TryPasteClipboard(bool showToast)
    {
        float x;
        float y;
        if (SpawnAuthoringState.HasLastClick)
        {
            x = SpawnAuthoringState.LastClickX;
            y = SpawnAuthoringState.LastClickY;
        }
        else
        {
            SpawnManagedInstance src = ResolveCopyTarget();
            if (src != null)
            {
                Vector3 p = src.transform.position;
                x = p.x + 0.5f;
                y = p.y;
            }
            else
            {
                statusLine = SpawnAuthoringLoc.T("status.pasteNeedPos");
                editStatus = statusLine;
                return false;
            }
        }

        GameObject spawned = null;
        string status;
        bool ok = SpawnAuthoringClipboard.HasChunk
            ? SpawnAuthoringClipboard.TryPasteChunkAt(x, y, out status)
            : SpawnAuthoringClipboard.TryPasteAt(x, y, out spawned, out status);
        if (!ok)
        {
            statusLine = status ?? SpawnAuthoringLoc.T("status.pasteFailed");
            editStatus = statusLine;
            return false;
        }

        statusLine = status;
        editStatus = status;
        if (showToast)
        {
            ShowCenterToast(
                SpawnAuthoringLoc.T("toast.pasted"),
                SpawnAuthoringClipboard.FormatShort() + "  @ " +
                x.ToString("F2", CultureInfo.InvariantCulture) + "," +
                y.ToString("F2", CultureInfo.InvariantCulture),
                new Color(1f, 0.55f, 0.1f, 1f));
        }

        if (spawned != null)
        {
            SpawnAuthoringWorldPick.ForceLock(spawned);
            TryOpenEditForLocked();
        }

        return true;
    }

    private static string FormatSelectionStatus(GameObject locked)
    {
        if (locked == null)
            return string.Empty;

        SpawnManagedInstance managed = locked.GetComponent<SpawnManagedInstance>()
            ?? locked.GetComponentInChildren<SpawnManagedInstance>(true);
        if (managed != null && managed.HasAuthoringLink)
            return SpawnAuthoringLoc.Tf(
                "status.selectedHellGate",
                managed.AuthoringEnemyKey,
                managed.AuthoringLineIndex + 1);

        string name = locked.name ?? "?";
        int clone = name.IndexOf("(Clone)", StringComparison.OrdinalIgnoreCase);
        if (clone >= 0)
            name = name.Substring(0, clone).Trim();
        return SpawnAuthoringLoc.Tf("status.selectedVanilla", name);
    }

    private static string OrDash(string value)
    {
        return string.IsNullOrEmpty(value) ? "—" : value;
    }
}
