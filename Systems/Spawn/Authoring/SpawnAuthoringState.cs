using System.Globalization;
using NoREroMod.Systems.EventCore.Content;
using NoREroMod.Systems.EventCore.EventTrap;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>Shared F11 authoring session state (coords + catalog selection).</summary>
internal static class SpawnAuthoringState
{
    internal enum CatalogKind
    {
        None = 0,
        Enemies = 1,
        Trap = 2,
        LethalTrap = 3,
        Decor = 4,
        Hostage = 5,
        Gold = 6,
        EventTrap = 7,
        EventCore = 8
    }

    private static CatalogKind activeCatalog;
    private static int factionIndexBeforeEventCore;
    private static string pendingKeyBeforeEventCore = string.Empty;

    internal static CatalogKind ActiveCatalog
    {
        get { return activeCatalog; }
        set
        {
            if (activeCatalog == CatalogKind.EventCore && value != CatalogKind.EventCore)
                RestoreFactionAfterEventCore();
            else if (value == CatalogKind.EventCore && activeCatalog != CatalogKind.EventCore)
                ApplyEventCoreDefaultFaction();
            activeCatalog = value;
        }
    }

    internal static bool EnemiesPanelOpen
    {
        get => ActiveCatalog == CatalogKind.Enemies;
        set
        {
            if (value)
                ActiveCatalog = CatalogKind.Enemies;
            else if (ActiveCatalog == CatalogKind.Enemies)
                ActiveCatalog = CatalogKind.None;
        }
    }

    internal static bool TrapPanelOpen => ActiveCatalog == CatalogKind.Trap;
    internal static bool LethalTrapPanelOpen => ActiveCatalog == CatalogKind.LethalTrap;
    internal static bool DecorPanelOpen => ActiveCatalog == CatalogKind.Decor;
    internal static bool HostagePanelOpen => ActiveCatalog == CatalogKind.Hostage;
    internal static bool GoldPanelOpen => ActiveCatalog == CatalogKind.Gold;
    internal static bool EventTrapPanelOpen => ActiveCatalog == CatalogKind.EventTrap;
    internal static bool EventCorePanelOpen => ActiveCatalog == CatalogKind.EventCore;
    internal static bool IsTrapCatalog =>
        ActiveCatalog == CatalogKind.Trap || ActiveCatalog == CatalogKind.LethalTrap;
    internal static bool IsTemplateCatalog =>
        IsTrapCatalog || ActiveCatalog == CatalogKind.Decor || ActiveCatalog == CatalogKind.Hostage;
    internal static bool IsSpecialCatalog =>
        ActiveCatalog == CatalogKind.Gold ||
        ActiveCatalog == CatalogKind.EventTrap ||
        ActiveCatalog == CatalogKind.EventCore;
    internal static bool AnyCatalogOpen => ActiveCatalog != CatalogKind.None;

    /// <summary>F11 Place rotation is for Trap / Decor only — not Lethal or Hostage &amp; OtherScenes.</summary>
    internal static bool CatalogAllowsRotation =>
        ActiveCatalog != CatalogKind.LethalTrap && ActiveCatalog != CatalogKind.Hostage;

    internal static bool KeyAllowsRotation(string key)
    {
        if (SpawnAuthoringTrapCatalog.IsLethalPickerKey(key))
            return false;
        if (SpawnAuthoringHostageCatalog.IsPickerKey(key) ||
            SpawnAuthoringHostageCatalog.IsHostageLineKey(key) ||
            SpawnAuthoringHostageCatalog.IsOtherSceneKey(key))
            return false;
        return true;
    }

    /// <summary>Highlighted in the list, not yet confirmed (enemy or trap key).</summary>
    internal static string PendingEnemyKey = string.Empty;

    /// <summary>Index into <see cref="SpawnAuthoringPackEdit.FactionTokens"/> for pending confirm.</summary>
    internal static int PendingFactionIndex;

    /// <summary>Pending confirm: force NoREroMod elite (|elite=1).</summary>
    internal static bool PendingForceElite;

    /// <summary>Pending confirm: append ,flip.</summary>
    internal static bool PendingFlipX;

    /// <summary>Pending Z rotation degrees (0 / 90 / 180 / 270) for traps.</summary>
    internal static float PendingRotationZ;

    /// <summary>Pending sortingOrder delta for traps (<c>sort±N</c>).</summary>
    internal static int PendingSortOffset;

    /// <summary>Index into EventCore ids (0 = none) for pending confirm.</summary>
    internal static int PendingEventCoreIndex;

    /// <summary>Pending confirm: write RANDOM,chance,… instead of a static line.</summary>
    internal static bool PendingRandom;

    /// <summary>Pending RANDOM chance text (InvariantCulture, 0–1).</summary>
    internal static string PendingChanceStr = "0.5";

    /// <summary>Pending EventCore attach chance (0–1). Used only when an event is selected. 1 = always attach.</summary>
    internal static string PendingEventCoreChanceStr = "1";

    /// <summary>EventTrap Place overrides. Empty = keep pack <c>config.json</c>.</summary>
    internal static string PendingEventTrapCountStr = string.Empty;
    internal static string PendingEventTrapDistStr = string.Empty;
    /// <summary>0 = pack default, 1 = both sides, 2 = right only.</summary>
    internal static int PendingEventTrapSidesIndex;
    internal static string PendingEventTrapMaxStr = string.Empty;
    internal static string PendingEventTrapRadiusStr = string.Empty;
    internal static string PendingEventTrapDelayStr = string.Empty;

    /// <summary>Confirmed via the Confirm button — used for Append.</summary>
    internal static string SelectedEnemyKey = string.Empty;

    internal static int SelectedFactionIndex;

    internal static bool SelectedForceElite;

    internal static bool SelectedFlipX;

    internal static float SelectedRotationZ;

    internal static int SelectedSortOffset;

    internal static string SelectedEventCoreId = string.Empty;

    internal static float SelectedEventCoreChance = 1f;

    internal static bool SelectedRandom;

    internal static float SelectedChance = 1f;

    internal static float LastClickX;
    internal static float LastClickY;
    internal static bool HasLastClick;
    internal static string LastClickPackHint = string.Empty;

    internal static void SetLastClick(float x, float y, string packHint)
    {
        bool overwrite = HasLastClick;
        LastClickX = x;
        LastClickY = y;
        HasLastClick = true;
        LastClickPackHint = packHint ?? string.Empty;
        SpawnAuthoringClickCue.NotifyClickRecorded(x, y, overwrite);
    }

    /// <summary>Update Last LMB coords without toast (arrow nudge / silent sync).</summary>
    internal static void SetLastClickSilent(float x, float y, string packHint)
    {
        LastClickX = x;
        LastClickY = y;
        HasLastClick = true;
        if (packHint != null)
            LastClickPackHint = packHint;
    }

    internal static void ClearSessionUi()
    {
        ActiveCatalog = CatalogKind.None;
        SpawnAuthoringFavorites.PanelOpen = false;
        PendingEnemyKey = string.Empty;
        SpawnAuthoringClickCue.Clear();
        SpawnAuthoringNudge.Clear();
        SpawnAuthoringDrag.Clear();
        SpawnAuthoringRegion.Clear();
        SpawnAuthoringUndo.Clear();
        SpawnAuthoringEnemyPreview.Clear();
        // Keep last click / confirmed selection across brief F11 toggles.
    }

    internal static bool ConfirmPendingEnemy()
    {
        if (string.IsNullOrEmpty(PendingEnemyKey))
            return false;
        if (ActiveCatalog != CatalogKind.EventCore && IsAuthoringEventId(PendingEnemyKey))
            return false;

        SelectedEnemyKey = PendingEnemyKey;
        SelectedFactionIndex = PendingFactionIndex;
        SelectedForceElite = PendingForceElite;
        SelectedFlipX = PendingFlipX;
        SelectedRotationZ = CatalogAllowsRotation && KeyAllowsRotation(PendingEnemyKey)
            ? PendingRotationZ
            : 0f;
        SelectedSortOffset = PendingSortOffset;
        SelectedEventCoreId = string.Empty;
        SelectedEventCoreChance = 1f;
        if (ActiveCatalog == CatalogKind.EventCore)
        {
            SelectedEventCoreId = PendingEnemyKey ?? string.Empty;
            if (EventCoreDefinitionRegistry.TryGetBoundEnemyKey(SelectedEventCoreId, out string boundKey) &&
                !string.IsNullOrEmpty(boundKey))
                SelectedEnemyKey = boundKey;
            else if (string.IsNullOrEmpty(SelectedEnemyKey) ||
                     string.Equals(SelectedEnemyKey, SelectedEventCoreId, System.StringComparison.OrdinalIgnoreCase))
                SelectedEnemyKey = "TouzokuNormal";
            SelectedEventCoreChance = ResolvePendingEventCoreChance();
        }
        else if (IsEventCoreEncounterToken(TokenAt(SelectedFactionIndex)))
        {
            SelectedFactionIndex = 0;
            PendingFactionIndex = 0;
        }
        SelectedRandom = PendingRandom;
        SelectedChance = ResolvePendingChance();
        return true;
    }

    internal static float ResolvePendingChance()
    {
        if (!PendingRandom)
            return 1f;

        if (!float.TryParse(PendingChanceStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float chance))
            chance = 0.5f;
        chance = Mathf.Clamp01(chance);
        if (chance >= 0.999f)
            chance = 0.99f;
        if (chance <= 0f)
            chance = 0.01f;
        return chance;
    }

    internal static float ResolvePendingEventCoreChance()
    {
        if (string.IsNullOrEmpty(GetPendingEventCoreId()))
            return 1f;

        if (!float.TryParse(PendingEventCoreChanceStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float chance))
            chance = 1f;
        chance = Mathf.Clamp01(chance);
        if (chance <= 0f)
            chance = 0.01f;
        return chance;
    }

    internal static string GetPendingEventCoreBoundEnemyKey()
    {
        string id = GetPendingEventCoreId();
        if (EventCoreDefinitionRegistry.TryGetBoundEnemyKey(id, out string boundKey) &&
            !string.IsNullOrEmpty(boundKey))
            return boundKey;
        return "TouzokuNormal";
    }

    internal static int IndexOfEventCoreEncounterFaction()
    {
        string[] tokens = SpawnAuthoringPackEdit.FactionTokens;
        for (int i = 0; i < tokens.Length; i++)
        {
            if (string.Equals(tokens[i], "eventcore_encounter", System.StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return 0;
    }

    internal static bool IsEventCoreEncounterToken(string token)
    {
        return !string.IsNullOrEmpty(token) &&
               string.Equals(token, "eventcore_encounter", System.StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsAuthoringEventId(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
        string[] ids = EventCoreDefinitionRegistry.GetAuthoringEventIds();
        if (ids == null)
            return false;
        for (int i = 0; i < ids.Length; i++)
        {
            if (string.Equals(ids[i], key.Trim(), System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    internal static int ClampFactionIndex(int index, bool allowEventCoreFaction)
    {
        string[] tokens = SpawnAuthoringPackEdit.FactionTokens;
        if (tokens == null || tokens.Length == 0)
            return 0;
        if (index < 0 || index >= tokens.Length)
            index = 0;
        if (!allowEventCoreFaction && IsEventCoreEncounterToken(tokens[index]))
            return 0;
        return index;
    }

    internal static int CycleFactionIndex(int index, int delta, bool allowEventCoreFaction)
    {
        string[] tokens = SpawnAuthoringPackEdit.FactionTokens;
        if (tokens == null || tokens.Length == 0)
            return 0;
        int n = tokens.Length;
        int start = ClampFactionIndex(index, allowEventCoreFaction);
        int next = start;
        for (int i = 0; i < n; i++)
        {
            next = (next + delta % n + n) % n;
            if (allowEventCoreFaction || !IsEventCoreEncounterToken(tokens[next]))
                return next;
        }

        return 0;
    }

    private static void ApplyEventCoreDefaultFaction()
    {
        int encounter = IndexOfEventCoreEncounterFaction();
        if (PendingFactionIndex != encounter)
            factionIndexBeforeEventCore = PendingFactionIndex;
        pendingKeyBeforeEventCore = PendingEnemyKey ?? string.Empty;
        PendingFactionIndex = encounter;
    }

    private static void RestoreFactionAfterEventCore()
    {
        if (PendingFactionIndex == IndexOfEventCoreEncounterFaction())
            PendingFactionIndex = factionIndexBeforeEventCore;
        if (IsAuthoringEventId(PendingEnemyKey))
            PendingEnemyKey = pendingKeyBeforeEventCore ?? string.Empty;
    }

    internal static string GetPendingFactionToken()
    {
        return TokenAt(PendingFactionIndex);
    }

    internal static string BuildPendingEventTrapExtrasSuffix()
    {
        EventTrapLineExtras extras = EventTrapLineExtras.FromAuthoring(
            PendingEventTrapCountStr,
            PendingEventTrapDistStr,
            PendingEventTrapSidesIndex,
            IsEventCoreEncounterToken(GetPendingFactionToken()) ? string.Empty : GetPendingFactionToken(),
            PendingEventTrapMaxStr,
            PendingEventTrapRadiusStr,
            PendingEventTrapDelayStr);
        return extras != null ? extras.ToLineSuffix() : string.Empty;
    }

    internal static string GetSelectedFactionToken()
    {
        return TokenAt(SelectedFactionIndex);
    }

    internal static string GetPendingEventCoreId()
    {
        if (ActiveCatalog == CatalogKind.EventCore)
            return string.IsNullOrEmpty(PendingEnemyKey) ? null : PendingEnemyKey.Trim();
        return null;
    }

    internal static string GetSelectedEventCoreId()
    {
        return string.IsNullOrEmpty(SelectedEventCoreId) ? null : SelectedEventCoreId;
    }

    internal static int IndexOfEventCore(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return 0;
        string[] ids = EventCoreDefinitionRegistry.GetAuthoringEventIds();
        for (int i = 0; i < ids.Length; i++)
        {
            if (string.Equals(ids[i], raw.Trim(), System.StringComparison.OrdinalIgnoreCase))
                return i + 1;
        }

        return 0;
    }

    private static string TokenAt(int index)
    {
        string[] tokens = SpawnAuthoringPackEdit.FactionTokens;
        if (tokens == null || tokens.Length == 0)
            return string.Empty;
        if (index < 0 || index >= tokens.Length)
            return string.Empty;
        return tokens[index] ?? string.Empty;
    }

    internal static string FormatLastCoords()
    {
        if (!HasLastClick)
            return "—";
        return LastClickX.ToString("F2") + "," + LastClickY.ToString("F2");
    }

    /// <summary>Live preview of the pack line Confirm would append (uses Last LMB + pending options).</summary>
    internal static string FormatPendingLinePreview()
    {
        if (string.IsNullOrEmpty(PendingEnemyKey))
            return "—";
        if (!HasLastClick)
            return "(click world for XY) " + PendingEnemyKey;

        if (ActiveCatalog == CatalogKind.Gold)
        {
            return SpawnAuthoringPackEdit.BuildGoldLine(
                LastClickX, LastClickY, PendingEnemyKey, ResolvePendingChance(), 1);
        }

        if (ActiveCatalog == CatalogKind.EventTrap)
            return SpawnAuthoringPackEdit.BuildEventTrapLine(
                LastClickX, LastClickY, PendingEnemyKey, BuildPendingEventTrapExtrasSuffix());

        if (ActiveCatalog == CatalogKind.EventCore)
        {
            string eventFaction = GetPendingFactionToken();
            if (string.IsNullOrEmpty(eventFaction))
                eventFaction = null;
            return SpawnAuthoringPackEdit.BuildEnemyLine(
                LastClickX,
                LastClickY,
                GetPendingEventCoreBoundEnemyKey(),
                eventFaction,
                ResolvePendingChance(),
                1,
                PendingFlipX,
                PendingForceElite,
                GetPendingEventCoreId(),
                ResolvePendingEventCoreChance());
        }

        if (IsTemplateCatalog)
        {
            string templateFaction = null;
            if (ActiveCatalog == CatalogKind.Hostage)
            {
                templateFaction = GetPendingFactionToken();
                if (string.IsNullOrEmpty(templateFaction))
                    templateFaction = null;
            }

            float hostageChance = ActiveCatalog == CatalogKind.Hostage && PendingRandom
                ? ResolvePendingChance()
                : 1f;

            return SpawnAuthoringPackEdit.BuildTrapLine(
                LastClickX,
                LastClickY,
                PendingEnemyKey,
                1,
                PendingFlipX,
                CatalogAllowsRotation ? PendingRotationZ : 0f,
                PendingSortOffset,
                templateFaction,
                hostageChance);
        }

        string faction = GetPendingFactionToken();
        if (string.IsNullOrEmpty(faction))
            faction = null;

        return SpawnAuthoringPackEdit.BuildEnemyLine(
            LastClickX,
            LastClickY,
            PendingEnemyKey,
            faction,
            ResolvePendingChance(),
            1,
            PendingFlipX,
            PendingForceElite,
            GetPendingEventCoreId(),
            ResolvePendingEventCoreChance());
    }

    internal static string FormatPendingFlagsShort()
    {
        if (ActiveCatalog == CatalogKind.Gold)
        {
            string goldRnd = PendingRandom
                ? ("RND " + ResolvePendingChance().ToString("0.##", CultureInfo.InvariantCulture))
                : "static";
            return "gold " + PendingEnemyKey + " · " + goldRnd;
        }

        if (ActiveCatalog == CatalogKind.EventTrap)
        {
            string extras = BuildPendingEventTrapExtrasSuffix();
            return string.IsNullOrEmpty(extras)
                ? ("eventtrap · " + PendingEnemyKey + " · pack defaults")
                : ("eventtrap · " + PendingEnemyKey + extras);
        }

        if (ActiveCatalog == CatalogKind.EventCore)
        {
            string ec = GetPendingEventCoreId();
            string ecLabel = string.IsNullOrEmpty(ec)
                ? "—"
                : EventCoreDefinitionRegistry.FormatEventIdForUi(ec);
            string eventFaction = SpawnAuthoringFactionUi.DisplayName(GetPendingFactionToken());
            string eventElite = PendingForceElite ? "ELITE" : "normal";
            string eventFlip = PendingFlipX ? "flip" : "no-flip";
            string eventRnd = PendingRandom
                ? ("RND " + ResolvePendingChance().ToString("0.##", CultureInfo.InvariantCulture))
                : "static";
            return eventFaction + " · " + eventElite + " · " + eventFlip + " · " + eventRnd +
                   " · " + ecLabel + " p=" +
                   ResolvePendingEventCoreChance().ToString("0.##", CultureInfo.InvariantCulture);
        }

        if (IsTemplateCatalog)
        {
            string flip = PendingFlipX ? "flip" : "no-flip";
            string rot = CatalogAllowsRotation
                ? (SpawnAuthoringPackEdit.FormatRotationToken(PendingRotationZ) ?? "rot0")
                : null;
            string sort = SpawnAuthoringPackEdit.FormatSortToken(PendingSortOffset) ?? "sort0";
            string kind = ActiveCatalog == CatalogKind.LethalTrap
                ? "lethal"
                : (ActiveCatalog == CatalogKind.Decor
                    ? "decor"
                    : (ActiveCatalog == CatalogKind.Hostage ? "hostage" : "trap"));
            if (ActiveCatalog == CatalogKind.Hostage)
            {
                string hostageFaction = SpawnAuthoringFactionUi.DisplayName(GetPendingFactionToken());
                string hostageRnd = PendingRandom
                    ? (" · RND " + ResolvePendingChance().ToString("0.##", CultureInfo.InvariantCulture))
                    : "";
                return kind + " · " + hostageFaction + " · " + flip + " · " + sort + hostageRnd;
            }

            if (string.IsNullOrEmpty(rot))
                return kind + " · " + flip + " · " + sort;
            return kind + " · " + flip + " · " + rot + " · " + sort;
        }

        string faction = SpawnAuthoringFactionUi.DisplayName(GetPendingFactionToken());
        string elite = PendingForceElite ? "ELITE" : "normal";
        string flipFlag = PendingFlipX ? "flip" : "no-flip";
        string random = PendingRandom
            ? ("RND " + ResolvePendingChance().ToString("0.##", CultureInfo.InvariantCulture))
            : "static";
        return faction + " · " + elite + " · " + flipFlag + " · " + random;
    }
}
