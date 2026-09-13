using System;
using System.Collections.Generic;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// F11 Spawn System Editor V2.0 UI strings.
/// Follows <c>HellGateLanguage</c> (EN RU JP CN KR FR DE PT BR ES); missing keys stay EN.
/// </summary>
internal static partial class SpawnAuthoringLoc
{
    private static string cachedLang = string.Empty;
    private static Dictionary<string, string> map;

    internal static string T(string key)
    {
        Ensure();
        if (map != null && map.TryGetValue(key, out string value) && !string.IsNullOrEmpty(value))
            return value;
        return key;
    }

    internal static string Tf(string key, params object[] args)
    {
        string fmt = T(key);
        if (args == null || args.Length == 0)
            return fmt;
        try
        {
            return string.Format(fmt, args);
        }
        catch (FormatException)
        {
            return fmt;
        }
    }

    internal static void Invalidate()
    {
        cachedLang = string.Empty;
        map = null;
    }

    private static void Ensure()
    {
        string lang = "EN";
        try
        {
            if (Plugin.hellGateLanguage != null && !string.IsNullOrEmpty(Plugin.hellGateLanguage.Value))
                lang = Plugin.hellGateLanguage.Value.Trim().ToUpperInvariant();
        }
        catch
        {
        }

        if (map != null && string.Equals(cachedLang, lang, StringComparison.OrdinalIgnoreCase))
            return;

        cachedLang = lang;
        map = Build(lang);
    }

    private static Dictionary<string, string> Build(string lang)
    {
        Dictionary<string, string> d = BuildEn();
        if (string.Equals(lang, "RU", StringComparison.OrdinalIgnoreCase))
            ApplyRu(d);
        else if (string.Equals(lang, "JP", StringComparison.OrdinalIgnoreCase))
            ApplyJp(d);
        else if (string.Equals(lang, "CN", StringComparison.OrdinalIgnoreCase))
            ApplyCn(d);
        else if (string.Equals(lang, "KR", StringComparison.OrdinalIgnoreCase))
            ApplyKr(d);
        else if (string.Equals(lang, "FR", StringComparison.OrdinalIgnoreCase))
            ApplyFr(d);
        else if (string.Equals(lang, "DE", StringComparison.OrdinalIgnoreCase))
            ApplyDe(d);
        else if (string.Equals(lang, "PT", StringComparison.OrdinalIgnoreCase))
            ApplyPt(d);
        else if (string.Equals(lang, "BR", StringComparison.OrdinalIgnoreCase))
            ApplyBr(d);
        else if (string.Equals(lang, "ES", StringComparison.OrdinalIgnoreCase))
            ApplyEs(d);
        ApplyStatusExtras(d, lang);
        return d;
    }

    private static Dictionary<string, string> BuildEn()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["banner.title"] = "Spawn System Editor V2.0",
            ["coords.title"] = "Placement",
            ["coords.lastLmb"] = "Point",
            ["coords.activePack"] = "Pack",
            ["coords.pack"] = "Pack",
            ["coords.pending"] = "Pending",
            ["coords.clipboard"] = "Copied",
            ["coords.selected"] = "Selected",
            ["coords.line"] = "Line",
            ["coords.hotkeys"] = "LMB click Point · LMB-drag box · Ctrl+C/X/V/D · arrows · Shift+arrows · E H T R G N M C F B V U · Home camera · Space place · RMB save+reload · F12 shot · F1 help · Q close",
            ["status.dragging"] = "Dragging…",
            ["click.ready"] = "Spawn Point Ready",
            ["click.updated"] = "Coordinates updated",
            ["click.marker"] = "SPAWN",
            ["toast.copied"] = "Object Copied",
            ["toast.cut"] = "Cut",
            ["toast.pasted"] = "Pasted",
            ["toast.duplicated"] = "Duplicated",
            ["toast.deleted"] = "Deleted",
            ["toast.screenshot"] = "Screenshot saved",
            ["toast.screenshotFail"] = "Screenshot failed",
            ["btn.screenshot"] = "Shot [F12]",
            ["btn.help"] = "Help [F1]",

            ["btn.enemies"] = "Enemies [E]",
            ["btn.enemiesOpen"] = "Enemies ▲ [E]",
            ["btn.hostage"] = "Hostage [H]",
            ["btn.hostageOpen"] = "Hostage ▲ [H]",
            ["hostage.title"] = "Hostage & OtherScenes",
            ["help.hostage"] = "Hostages, then Spine animations (Look_Dorei, gob_look, …). Faction is written as |faction= and inherited when the look/hostage becomes a combatant. Random writes RANDOM_HOSTAGE.",
            ["btn.trap"] = "Trap [T]",
            ["btn.trapOpen"] = "Trap ▲ [T]",
            ["btn.lethalTrap"] = "Lethal [R]",
            ["btn.lethalTrapOpen"] = "Lethal ▲ [R]",
            ["btn.decor"] = "Decorations [N]",
            ["btn.decorOpen"] = "Decorations ▲ [N]",
            ["btn.gold"] = "Gold [G]",
            ["btn.goldOpen"] = "Gold ▲ [G]",
            ["gold.title"] = "Gold piles",
            ["help.gold"] = "Place a gold pile. Presets are fixed or ranged amounts. Random writes RANDOM,chance,X,Y,gold=…",
            ["help.goldOptions"] = "Random = RANDOM,chance gold line. Static = GOLD,X,Y,amount,1.",
            ["btn.eventTrap"] = "EventTrap [M]",
            ["btn.eventTrapOpen"] = "EventTrap ▲ [M]",
            ["eventTrap.title"] = "EventTrap anchors",
            ["help.eventTrap"] = "Writes EVENTTRAP,folder,X,Y plus optional count/dist/sides/faction/max/r/delay. Empty fields keep the pack config.json. F11 shows a labeled gizmo (invisible in play) — pick/drag/edit like other spawns.",
            ["help.eventTrapGizmo"] = "Editor-only ambush/reinforcement anchor. Invisible in the game. Drag or type XY, then Use position / Save.",
            ["help.eventTrapOptions"] = "Empty = pack defaults. Distances are from the player at knockdown, not from the anchor. Faction is applied to ambush enemies. Sides: pack / both / right-only.",
            ["btn.eventCore"] = "EventCore [C]",
            ["btn.eventCoreOpen"] = "EventCore ▲ [C]",
            ["eventcore.title"] = "EventCore",
            ["eventcore.none"] = "None",
            ["eventcore.attach"] = "Event p=",
            ["help.eventCore"] = "Places a dialogue NPC: TouzokuNormal|ec_event=<id>. Broker / FSP bandits always use TouzokuNormal. Click Point, then Place. LMB on a placed NPC to Edit (event, p=, flip, faction, XY).",
            ["help.eventCoreOptions"] = "Random = NPC spawn chance (RANDOM,chance). Event p= = |ec_chance= — the NPC always spawns; 1 = dialogue always attaches. Faction defaults to Event encounter.",
            ["help.previewEventCore"] = "Preview of the bound NPC prefab. Drag from preview or list onto the map to Place & save.",
            ["etrap.count"] = "Count",
            ["etrap.dist"] = "Dist",
            ["etrap.max"] = "Max",
            ["etrap.zone"] = "Zone",
            ["etrap.delay"] = "Delay",
            ["etrap.sidesPack"] = "Sides: pack",
            ["etrap.sidesBoth"] = "Sides: both",
            ["etrap.sidesRight"] = "Sides: right",
            ["decor.title"] = "Decorations",
            ["spine.title"] = "— Spine animations —",
            ["help.decor"] = "Boxes, barrels, static corpses, scene props. Spine look scenes are under Hostage & OtherScenes. (?) = template not cached yet.",
            ["btn.favorites"] = "Favorites ★ [B]",
            ["btn.favoritesOpen"] = "Favorites ▲ [B]",
            ["btn.delete"] = "Delete [Del]",
            ["btn.undo"] = "Undo  [Ctrl+Z]",
            ["btn.copy"] = "Copy  [Ctrl+C]",
            ["btn.cut"] = "Cut   [Ctrl+X]",
            ["btn.paste"] = "Paste [Ctrl+V]",
            ["btn.viewModOff"] = "Overview [V]",
            ["btn.viewModOn"] = "Overview ON [V]",
            ["btn.confirm"] = "Place  [Space]",
            ["btn.confirmFull"] = "Place & save  [Space]",
            ["btn.addFavorite"] = "★ Save to Favorites [Ctrl+B]",
            ["btn.favDelete"] = "Del",
            ["btn.save"] = "Save  [Ctrl+S]",
            ["btn.saved"] = "✓ Saved",
            ["btn.closeEdit"] = "Close",
            ["btn.useCurrentPos"] = "Use position [U]",
            ["btn.default"] = "Reset",

            ["bar.undo"] = "Undo",
            ["bar.save"] = "Save",
            ["bar.flip"] = "Flip [F]",
            ["bar.flipOn"] = "Flip ON [F]",
            ["bar.copy"] = "Copy",
            ["bar.cut"] = "Cut",
            ["bar.paste"] = "Paste",
            ["bar.delete"] = "Delete",
            ["bar.place"] = "Place [␣]",
            ["traps.title"] = "Traps",
            ["lethal.title"] = "Lethal traps",
            ["help.traps"] = "Pick a trap key, set flip/rot/sort, click map Point, Place/Space. LMB on a placed trap to select → Edit/Delete. (?) = template not cached yet.",
            ["help.lethal"] = "Lethal HellGate traps (gore). Place with flip/sort (no rotation). Requires Gore Content enabled.",
            ["status.flipOn"] = "Facing left [F]",
            ["status.flipOff"] = "Facing right [F]",

            ["ready.title"] = "Ready",
            ["enemies.title"] = "Enemies",
            ["favorites.title"] = "Favorites",
            ["favorites.empty"] = "Empty — pick any catalog key, then ★ Add.",
            ["favorites.addHere"] = "★ Add current",
            ["faction.title"] = "Faction",
            ["elite"] = "Elite",
            ["flip"] = "Flip left",
            ["rot"] = "Rotate",
            ["rot.plus90"] = "+30° [R]",
            ["rot.degrees"] = "Degrees",
            ["rot.apply"] = "Set",
            ["sort"] = "Sort",
            ["random"] = "Random chance",
            ["options.title"] = "Options",
            ["preview.title"] = "Preview",
            ["preview.empty"] = "Pick an enemy",
            ["preview.emptyTrap"] = "Pick a trap",
            ["preview.missing"] = "No prefab",
            ["preview.blank"] = "No mesh",
            ["preview.unavailable"] = "Unavailable",
            ["enemies.na"] = "[N/A]",
            ["status.enemyUnavailable"] = "This enemy is marked unavailable for authoring.",
            ["help.title"] = "Help",
            ["help.guide.title"] = "Controls",
            ["help.guide.camera"] = "Camera / move",
            ["help.guide.camera.body"] = "WASD — move the camera\nHold MMB and drag — move the camera with the mouse\nMouse wheel — zoom (turns on Overview)\nV — Overview / Default\nHome — camera to the player (Overview stays on, zoom resets)",
            ["help.guide.mouse"] = "Mouse",
            ["help.guide.mouse.body"] = "LMB on empty ground — set Point\nLMB-drag on empty ground — box-select\nLMB on a sprite — select / edit (precise; Alt = wider pick)\nHold LMB on an object — drag it\nArrow keys — nudge the selection (Shift+arrows ×5)\nDrag from preview or list onto the map — place and save\nRMB — save the pack and reload the scene",
            ["help.guide.catalogs"] = "Catalogs",
            ["help.guide.catalogs.body"] = "E — Enemies\nH — Hostages\nT — Traps\nR — Lethal traps\nG — Gold\nN — Decor\nM — EventTrap\nC — EventCore\nB — Favorites\nF — Flip\nSpace / Enter — Place\nU — Use position\nQ — close panels (and this help)",
            ["help.guide.edit"] = "Edit & clipboard",
            ["help.guide.edit.body"] = "Ctrl+C — copy\nCtrl+X — cut\nCtrl+V — paste at Point\nCtrl+D — duplicate\nCtrl+Z — undo\nCtrl+S — save edit\nCtrl+B — add to Favorites\nDel — delete (a box-select deletes everything inside)\nF12 — screenshot\nF1 — this help\nEsc — close help, then box, then Edit, then selection",
            ["help.guide.footer"] = "Hover ? on any panel for extra tips.",
            ["help.coords"] = "LMB on empty ground = set Point. LMB-drag on empty ground = box-select (Ctrl+C copy / Ctrl+X cut / Ctrl+V paste at Point). LMB on a sprite = select/edit (Alt = wider pick). Hold LMB on an object to drag. Hold MMB / WASD to move the camera. Wheel = zoom. Arrows nudge (Shift+arrows ×5). Home = camera to the player. F1 help. F12 screenshot. − collapses this panel.",
            ["help.faction"] = "Faction written into the pack line. Emblem = HellGate faction icon. Use < > to cycle. Names are display-only; pack still stores the technical token.",
            ["help.options"] = "Elite = ForceElite. Flip = face −X. Random = RANDOM,chance.",
            ["help.trapOptions"] = "Flip = horizontal. Rotate: +30° [R] or type degrees + Set. Sort = pack sort±N (SortingOrder delta, not near/far).",
            ["help.hostageOptions"] = "Flip = horizontal. Sort = pack sort±N. Random writes RANDOM_HOSTAGE,chance,Key,X,Y,1. Hostages and OtherScenes do not rotate.",
            ["help.lethalOptions"] = "Flip = horizontal. Sort = pack sort±N (SortingOrder delta). Lethal traps do not rotate.",
            ["help.preview"] = "Live thumbnail of the pending enemy prefab (Spine snapshot). Drag from this panel or from the list onto the map to Place & save.",
            ["help.previewTrap"] = "Live thumbnail of the pending trap template. Drag from this panel or from the list onto the map to Place & save. (?) in the list = visit a map that contains that trap first.",
            ["preview.dragPlace"] = "Drag from preview or list onto the map to place",
            ["preview.dragRelease"] = "Release to Place & save",
            ["faction.none"] = "None",
            ["faction.bandits"] = "Bandits",
            ["faction.banditsInq"] = "Bandits (Inquisition)",
            ["faction.banditsMafia"] = "Bandits (Mafia)",
            ["faction.banditsDemons"] = "Bandits (Demons)",
            ["faction.church"] = "Church",
            ["faction.demons"] = "Demons",
            ["faction.mafia"] = "Mafia",
            ["faction.undead"] = "Undead",
            ["faction.monsters"] = "Monsters",
            ["faction.witch"] = "Witch",
            ["faction.event"] = "Event encounter",
            ["edit.title"] = "Selected",
            ["edit.key"] = "Key",
            ["edit.faction"] = "Faction",
            ["edit.random"] = "Random spawn",
            ["edit.chance"] = "Chance",
            ["edit.vanilla"] = "Not linked to a pack — edit unavailable.",
            ["status.panelsClosed"] = "Panels closed [Q]",
            ["status.pickEnemy"] = "Pick a key first.",
            ["status.selectEnemy"] = "Select an enemy first (hover + LMB).",
            ["status.editDisabled"] = "Vanilla / no pack link — edit unavailable.",
            ["status.pasteNeedPos"] = "Click a map point (Last LMB) then Ctrl+V.",
            ["status.cameraHome"] = "Camera → player",
            ["status.cameraHomeFail"] = "Player not found.",
            ["status.badSort"] = "Bad sort offset.",
            ["status.badRot"] = "Bad rotation degrees.",
            ["status.done"] = "Done.",
            ["status.noPackLink"] = "Vanilla / no pack link.",
            ["status.noPackLinkXy"] = "Vanilla / no pack link — XY not written.",
            ["status.keyRequired"] = "Key required.",
            ["status.badXy"] = "Bad X/Y (use 12.34 format).",
            ["status.goldRequired"] = "Gold amount required.",
            ["status.deleteFailed"] = "Delete failed.",
            ["status.cutFailed"] = "Cut failed.",
            ["status.copyFailed"] = "Copy failed.",
            ["status.pasteFailed"] = "Paste failed.",
            ["status.undoEmpty"] = "Nothing to undo.",
            ["status.savedXy"] = "Saved XY → pack",
            ["status.cutCount"] = "Cut × {0}  → Ctrl+V at Point",
            ["status.deletedCount"] = "Deleted × {0}  (Ctrl+Z undo)",
            ["status.copyButDeleteFailed"] = "{0}  (delete failed: {1})",
            ["status.savedKeyAt"] = "Saved {0} @ {1},{2} → line {3}",
            ["status.selectPackSpawn"] = "Select a HellGate pack spawn first (Edit/Delete disabled for vanilla).",
            ["status.deletedLine"] = "Deleted pack line {0}{1}  (Ctrl+Z undo)",
            ["status.regionEmpty"] = "Region empty.",
            ["status.deleteRegionFailed"] = "Delete region failed.",
            ["status.needPoint"] = "Click a world point first (Last LMB).",
            ["status.noConfirmedEnemy"] = "No confirmed enemy.",
            ["status.noConfirmedTrap"] = "No confirmed trap.",
            ["status.noGold"] = "No gold amount.",
            ["status.noEventTrapFolder"] = "No EventTrap folder.",
            ["status.cameraMissing"] = "Camera not found.",
            ["status.cameraSizeFail"] = "Could not read camera base size.",
            ["status.overviewOn"] = "Overview ON — WASD / MMB-drag move the camera, wheel zooms. V again = Default.",
            ["status.viewModDefault"] = "Overview Default (Standard).",
            ["status.copyNeedSpawn"] = "Select a HellGate pack spawn to copy.",
            ["status.goldUnresolved"] = "Gold amount unresolved — RMB reload then Ctrl+C.",
            ["status.trapKeyUnresolved"] = "Trap key unresolved — re-save the spike (Ctrl+S / RMB) then Ctrl+C.",
            ["status.copiedAtPoint"] = "Copied {0}  → Ctrl+V at Point",
            ["status.regionEmptyBox"] = "Region empty — LMB-drag a box over HellGate objects.",
            ["status.copiedChunk"] = "Copied chunk × {0}  → Ctrl+V at Point (also OS clipboard)",
            ["status.chunkEmpty"] = "Chunk clipboard empty — box-select, then Ctrl+C.",
            ["status.chunkWriteFailed"] = "Chunk paste write failed.",
            ["status.chunkPasteFailed"] = "Chunk paste failed.",
            ["status.pastedChunk"] = "Pasted chunk × {0} @ {1},{2} → {3}  (preview {4})",
            ["status.clipboardEmpty"] = "Clipboard empty — Ctrl+C a spawn first.",
            ["status.pastedAt"] = "Pasted {0} @ {1},{2} → {3}{4}",
            ["status.pastedPreviewFail"] = " (pack ok, preview failed)",
            ["status.pastedNudge"] = "  (arrows to nudge)",
            ["status.favAlready"] = "Already in Favorites.",
            ["status.favAdded"] = "★ Added: {0}",
            ["status.favBadIndex"] = "Bad favorite index.",
            ["status.favRemoved"] = "Removed: {0}",
            ["status.favLoaded"] = "Loaded ★ {0}",
            ["status.saveFailed"] = "Save failed.",
            ["status.undoUnknown"] = "Unknown undo kind.",
            ["status.undoRemovedLast"] = "Undo: removed last spawn line.",
            ["status.undoRestoredLine"] = "Undo: restored previous pack line.",
            ["status.undoRestoredDeleted"] = "Undo: restored deleted pack line{0}",
            ["status.undoReplaceFailed"] = "Undo replace failed.",
            ["status.undoDeleteFailed"] = "Undo delete failed.",
            ["status.savedPrefabNotReady"] = "Saved {0} but prefab (?) not ready — RMB reload after cache.",
            ["status.savedSpawned"] = "Saved + spawned{0} → {1}",
            ["status.savedTemplateNotReady"] = "Saved {0} but template (?) not ready — visit trap map / RMB reload.",
            ["status.savedSpawnedTrap"] = "Saved + spawned trap{0} → {1}",
            ["status.savedGoldDisabled"] = "Saved gold line (economy disabled — no preview pile) → {0}",
            ["status.savedGoldOk"] = "Saved + spawned gold → {0}",
            ["status.savedGoldPreviewFail"] = "Saved gold line but preview pile failed → {0}",
            ["status.savedEventTrap"] = "Saved EVENTTRAP (gizmo in F11) → {0}",
            ["status.noHost"] = "No host.",
            ["status.screenshotBusy"] = "Screenshot busy…",
            ["status.packWriteFailed"] = "Pack write failed.",
            ["status.pasteWriteFailed"] = "Paste write failed.",
            ["status.undoAppendFailed"] = "Undo append failed.",
            ["status.noSpawnPack"] = "No spawn pack for this zone.",
            ["status.writeFailed"] = "Write failed: {0}",
            ["status.editingLine"] = "Editing pack line {0}",
            ["status.savedEdit"] = "✓ Saved → {0}  line {1}{2}",
            ["status.commitPosFailed"] = "Commit position failed.",
            ["status.selectedHellGate"] = "Selected HellGate: {0} (line {1})",
            ["status.selectedVanilla"] = "Selected: {0} — Vanilla / no pack link",
            ["edit.packXy"] = "Pack XY: {0},{1}",
            ["edit.currentXy"] = "Current XY: {0},{1}",
            ["edit.x"] = "Edit X",
        };
    }

    private static void ApplyRu(Dictionary<string, string> d)
    {
        d["coords.title"] = "Размещение";
        d["coords.lastLmb"] = "Точка";
        d["coords.activePack"] = "Пак";
        d["coords.pack"] = "Пак";
        d["coords.pending"] = "Выбрано";
        d["coords.clipboard"] = "Буфер";
        d["coords.selected"] = "Выделено";
        d["coords.line"] = "Строка";
        d["coords.hotkeys"] = "ЛКМ = Точка · ЛКМ-drag = рамка · Ctrl+C/X/V/D · стрелки · Shift+стрелки · E H T R G N M C F B V U · Home · Space · ПКМ reload · F12 · F1 · Q";
        d["status.dragging"] = "Перетаскивание…";
        d["click.ready"] = "Точка спавна готова";
        d["click.updated"] = "Координаты перезаписаны";
        d["click.marker"] = "СПАВН";
        d["toast.copied"] = "Объект скопирован";
        d["toast.cut"] = "Вырезано";
        d["toast.pasted"] = "Вставлено";
        d["toast.duplicated"] = "Дубликат";
        d["toast.deleted"] = "Удалено";
        d["toast.screenshot"] = "Скриншот сохранён";
        d["toast.screenshotFail"] = "Скриншот не удался";
        d["btn.screenshot"] = "Скрин [F12]";
        d["btn.help"] = "Справка [F1]";

        d["btn.enemies"] = "Враги [E]";
        d["btn.enemiesOpen"] = "Враги ▲ [E]";
        d["btn.hostage"] = "Пленники [H]";
        d["btn.hostageOpen"] = "Пленники ▲ [H]";
        d["hostage.title"] = "Пленники и OtherScenes";
        d["help.hostage"] = "Пленники, затем Spine-анимации (Look_Dorei, gob_look, …). Фракция пишется как |faction= и переходит на бойца после H/спасения. Random пишет RANDOM_HOSTAGE.";
        d["btn.trap"] = "Ловушки [T]";
        d["btn.trapOpen"] = "Ловушки ▲ [T]";
        d["btn.lethalTrap"] = "Летальн. [R]";
        d["btn.lethalTrapOpen"] = "Летальн. ▲ [R]";
        d["btn.decor"] = "Декор [N]";
        d["btn.decorOpen"] = "Декор ▲ [N]";
        d["btn.gold"] = "Золото [G]";
        d["btn.goldOpen"] = "Золото ▲ [G]";
        d["gold.title"] = "Золото";
        d["help.gold"] = "Куча золота. Пресеты — фикс или диапазон. Random пишет RANDOM,chance,X,Y,gold=…";
        d["help.goldOptions"] = "Random = RANDOM,chance. Статика = GOLD,X,Y,amount,1.";
        d["btn.eventTrap"] = "EventTrap [M]";
        d["btn.eventTrapOpen"] = "EventTrap ▲ [M]";
        d["eventTrap.title"] = "Якоря EventTrap";
        d["help.eventTrap"] = "Пишет EVENTTRAP,folder,X,Y и опционально count/dist/sides/faction/max/r/delay. Пустые поля = config.json пака. В F11 якорь — подпись (в игре невидим): можно выбрать, перетащить, сохранить.";
        d["help.eventTrapGizmo"] = "Якорь засады/подкрепления только для редактора. В игре невидим. Перетащи или введи XY, затем «Взять позицию» / Сохранить.";
        d["help.eventTrapOptions"] = "Пусто = дефолты пака. Дистанция — от игрока в момент нокдауна, не от якоря. Фракция вешается на засаду. Стороны: пак / обе / только справа.";
        d["btn.eventCore"] = "EventCore [C]";
        d["btn.eventCoreOpen"] = "EventCore ▲ [C]";
        d["eventcore.title"] = "EventCore";
        d["eventcore.none"] = "Нет";
        d["eventcore.attach"] = "Событие p=";
        d["help.eventCore"] = "Ставит NPC диалога: TouzokuNormal|ec_event=<id>. Broker / FSP bandits всегда TouzokuNormal. Кликни Точку, потом Поставить. ЛКМ по NPC — правка (событие, p=, разворот, фракция, XY).";
        d["help.eventCoreOptions"] = "Random = шанс появления NPC (RANDOM,chance). Событие p= = |ec_chance= — NPC всегда есть; 1 = диалог всегда вешается. Фракция по умолчанию — Событие.";
        d["help.previewEventCore"] = "Превью префаба NPC. Перетащи из превью или списка на карту = поставить и сохранить.";
        d["etrap.count"] = "Кол-во";
        d["etrap.dist"] = "Дист";
        d["etrap.max"] = "Макс";
        d["etrap.zone"] = "Зона";
        d["etrap.delay"] = "Задерж.";
        d["etrap.sidesPack"] = "Стороны: пак";
        d["etrap.sidesBoth"] = "Стороны: обе";
        d["etrap.sidesRight"] = "Стороны: справа";
        d["decor.title"] = "Декорации";
        d["spine.title"] = "— Spine-анимации —";
        d["help.decor"] = "Ящики, бочки, статичные трупы, пропы. Spine-сцены — в Пленники и OtherScenes. (?) = шаблон ещё не в кэше.";
        d["btn.favorites"] = "Избранное ★ [B]";
        d["btn.favoritesOpen"] = "Избранное ▲ [B]";
        d["btn.delete"] = "Удалить [Del]";
        d["btn.undo"] = "Отмена  [Ctrl+Z]";
        d["btn.copy"] = "Копир. [Ctrl+C]";
        d["btn.cut"] = "Вырезать [Ctrl+X]";
        d["btn.paste"] = "Вставить [Ctrl+V]";
        d["btn.viewModOff"] = "Обзор [V]";
        d["btn.viewModOn"] = "Обзор ВКЛ [V]";
        d["btn.confirm"] = "Поставить  [Space]";
        d["btn.confirmFull"] = "Поставить и сохранить  [Space]";
        d["btn.addFavorite"] = "★ В избранное [Ctrl+B]";
        d["btn.favDelete"] = "Удал.";
        d["btn.save"] = "Сохранить  [Ctrl+S]";
        d["btn.saved"] = "✓ Сохранено";
        d["btn.closeEdit"] = "Закрыть";
        d["btn.useCurrentPos"] = "Взять позицию [U]";
        d["btn.default"] = "Сброс";

        d["bar.undo"] = "Отмена";
        d["bar.save"] = "Сохранить";
        d["bar.flip"] = "Разворот [F]";
        d["bar.flipOn"] = "Разворот ВКЛ [F]";
        d["bar.copy"] = "Копир.";
        d["bar.cut"] = "Вырезать";
        d["bar.paste"] = "Вставить";
        d["bar.delete"] = "Удалить";
        d["bar.place"] = "Поставить [␣]";
        d["traps.title"] = "Ловушки";
        d["lethal.title"] = "Летальные ловушки";
        d["help.traps"] = "Выбери ключ, разворот/поворот/слой, кликни Точку, Поставить/Space. ЛКМ по ловушке = выбрать → правка/удалить. (?) = шаблон ещё не в кэше.";
        d["help.lethal"] = "Летальные ловушки HellGate (gore). Поставить: разворот/слой, без поворота. Нужен Gore Content.";
        d["status.flipOn"] = "Смотрит влево [F]";
        d["status.flipOff"] = "Смотрит вправо [F]";

        d["ready.title"] = "Готово";
        d["enemies.title"] = "Враги";
        d["favorites.title"] = "Избранное";
        d["favorites.empty"] = "Пусто — выбери ключ в любом каталоге, затем ★.";
        d["favorites.addHere"] = "★ Добавить текущий";
        d["faction.title"] = "Фракция";
        d["elite"] = "Элита";
        d["flip"] = "Разворот влево";
        d["rot"] = "Поворот";
        d["rot.plus90"] = "+30° [R]";
        d["rot.degrees"] = "Градусы";
        d["rot.apply"] = "Ок";
        d["sort"] = "Слой";
        d["random"] = "Случайный шанс";
        d["options.title"] = "Опции";
        d["preview.title"] = "Превью";
        d["preview.empty"] = "Выбери врага";
        d["preview.emptyTrap"] = "Выбери ловушку";
        d["preview.missing"] = "Нет префаба";
        d["preview.blank"] = "Нет меша";
        d["preview.unavailable"] = "Недоступен";
        d["enemies.na"] = "[недоступен]";
        d["status.enemyUnavailable"] = "Этот враг помечен как недоступный для автора.";
        d["help.title"] = "Подсказка";
        d["help.guide.title"] = "Управление";
        d["help.guide.camera"] = "Камера / перемещение";
        d["help.guide.camera.body"] = "WASD — двигать камеру\nЗажать СКМ и тянуть — двигать камеру мышью\nКолёсико — зум (включает Обзор)\nV — Обзор / Default\nHome — камера к игроку (Обзор остаётся, зум сбрасывается)";
        d["help.guide.mouse"] = "Мышь";
        d["help.guide.mouse.body"] = "ЛКМ по пустому месту — поставить Точку\nЛКМ и тянуть по земле — выделить рамкой\nЛКМ по спрайту — выбрать / править (точно; Alt — шире область)\nЗажать ЛКМ на объекте — перетащить\nСтрелки — сдвинуть выбранное (Shift+стрелки ×5)\nПеретащить из превью или списка на карту — поставить и сохранить\nПКМ — сохранить пак и перезагрузить сцену";
        d["help.guide.catalogs"] = "Каталоги";
        d["help.guide.catalogs.body"] = "E — Враги\nH — Пленники\nT — Ловушки\nR — Летальные ловушки\nG — Золото\nN — Декор\nM — EventTrap\nC — EventCore\nB — Избранное\nF — Разворот\nSpace / Enter — Поставить\nU — взять позицию\nQ — закрыть панели (и эту справку)";
        d["help.guide.edit"] = "Правка и буфер";
        d["help.guide.edit.body"] = "Ctrl+C — копировать\nCtrl+X — вырезать\nCtrl+V — вставить в Точку\nCtrl+D — дублировать\nCtrl+Z — отменить\nCtrl+S — сохранить правку\nCtrl+B — в избранное\nDel — удалить (рамка удаляет всё внутри)\nF12 — скриншот\nF1 — эта справка\nEsc — закрыть справку, затем рамку, затем правку, затем выбор";
        d["help.guide.footer"] = "Наведи ? на панели — доп. подсказки.";
        d["help.coords"] = "ЛКМ по пустому = Точка. ЛКМ и тянуть по земле = рамка (Ctrl+C копировать, Ctrl+X вырезать, Ctrl+V в Точку). ЛКМ по спрайту = выбрать/править (Alt — шире). Зажать ЛКМ на объекте = перетащить. СКМ / WASD = двигать камеру. Колёсико = зум. Стрелки = сдвиг (Shift+стрелки ×5). Home = камера к игроку. F1 справка. F12 скрин. − сворачивает панель.";
        d["help.faction"] = "Фракция в строке пака. Значок — эмблема HellGate. < > листают. Название для UI; в пак пишется технический токен.";
        d["help.options"] = "Элита = ForceElite. Разворот = влево. Случайный = RANDOM,chance.";
        d["help.trapOptions"] = "Разворот = горизонталь. Поворот: +30° [R] или градусы + Ок. Слой = sort±N в паке (SortingOrder, не near/far).";
        d["help.hostageOptions"] = "Разворот = горизонталь. Слой = sort±N. Random пишет RANDOM_HOSTAGE,chance,Key,X,Y,1. Пленники и OtherScenes без поворота.";
        d["help.lethalOptions"] = "Разворот = горизонталь. Слой = sort±N (SortingOrder). Летальные ловушки без поворота.";
        d["help.preview"] = "Миниатюра выбранного врага. Перетащи из превью или из списка на карту = поставить и сохранить.";
        d["help.previewTrap"] = "Миниатюра шаблона. Перетащи из превью или из списка на карту = поставить и сохранить. (?) = сначала зайди на карту с этим шаблоном.";
        d["preview.dragPlace"] = "Перетащи из превью или списка на карту";
        d["preview.dragRelease"] = "Отпусти = поставить и сохранить";
        d["faction.none"] = "Нет";
        d["faction.bandits"] = "Бандиты";
        d["faction.banditsInq"] = "Бандиты (Инквизиция)";
        d["faction.banditsMafia"] = "Бандиты (Мафия)";
        d["faction.banditsDemons"] = "Бандиты (Демоны)";
        d["faction.church"] = "Церковь";
        d["faction.demons"] = "Демоны";
        d["faction.mafia"] = "Мафия";
        d["faction.undead"] = "Нежить";
        d["faction.monsters"] = "Монстры";
        d["faction.witch"] = "Ведьма";
        d["faction.event"] = "Событие";
        d["edit.title"] = "Выбрано";
        d["edit.key"] = "Ключ";
        d["edit.faction"] = "Фракция";
        d["edit.random"] = "Случайный спавн";
        d["edit.chance"] = "Шанс";
        d["edit.vanilla"] = "Нет связи с паком — правка недоступна.";
        d["status.panelsClosed"] = "Панели закрыты [Q]";
        d["status.pickEnemy"] = "Сначала выбери ключ.";
        d["status.selectEnemy"] = "Сначала выдели врага (наведение + ЛКМ).";
        d["status.editDisabled"] = "Нет связи с паком — правка недоступна.";
        d["status.pasteNeedPos"] = "Кликни точку на карте, потом Ctrl+V.";
        d["status.cameraHome"] = "Камера → игрок";
        d["status.cameraHomeFail"] = "Игрок не найден.";
        d["status.badSort"] = "Неверный слой (sort).";
        d["status.badRot"] = "Неверные градусы поворота.";
        d["status.done"] = "Готово.";
        d["status.noPackLink"] = "Нет связи с паком.";
        d["status.noPackLinkXy"] = "Нет связи с паком — XY не записаны.";
        d["status.keyRequired"] = "Нужен ключ.";
        d["status.badXy"] = "Неверные X/Y (формат 12.34).";
        d["status.goldRequired"] = "Нужна сумма золота.";
        d["status.deleteFailed"] = "Удаление не удалось.";
        d["status.cutFailed"] = "Вырезать не удалось.";
        d["status.copyFailed"] = "Копирование не удалось.";
        d["status.pasteFailed"] = "Вставка не удалась.";
        d["status.undoEmpty"] = "Нечего отменять.";
        d["status.savedXy"] = "XY записаны в пак";
        d["status.cutCount"] = "Вырезано × {0}  → Ctrl+V в Точку";
        d["status.deletedCount"] = "Удалено × {0}  (Ctrl+Z отмена)";
        d["status.copyButDeleteFailed"] = "{0}  (удаление не удалось: {1})";
        d["status.savedKeyAt"] = "Сохранено {0} @ {1},{2} → строка {3}";
        d["status.selectPackSpawn"] = "Сначала выбери объект HellGate (правка/удаление недоступны для ванили).";
        d["status.deletedLine"] = "Удалена строка пака {0}{1}  (Ctrl+Z отмена)";
        d["status.regionEmpty"] = "Рамка пуста.";
        d["status.deleteRegionFailed"] = "Удаление рамки не удалось.";
        d["status.needPoint"] = "Сначала кликни точку на карте (ЛКМ).";
        d["status.noConfirmedEnemy"] = "Сначала выбери врага.";
        d["status.noConfirmedTrap"] = "Сначала выбери ловушку.";
        d["status.noGold"] = "Нет суммы золота.";
        d["status.noEventTrapFolder"] = "Нет папки EventTrap.";
        d["status.cameraMissing"] = "Камера не найдена.";
        d["status.cameraSizeFail"] = "Не удалось прочитать базовый зум камеры.";
        d["status.overviewOn"] = "Обзор ВКЛ — WASD / СКМ-drag двигают камеру, колёсико зум. V снова = Default.";
        d["status.viewModDefault"] = "Обзор Default (стандарт).";
        d["status.copyNeedSpawn"] = "Сначала выбери объект HellGate для копирования.";
        d["status.goldUnresolved"] = "Сумма золота неясна — ПКМ reload, потом Ctrl+C.";
        d["status.trapKeyUnresolved"] = "Ключ ловушки неясен — сохрани шип (Ctrl+S / ПКМ), потом Ctrl+C.";
        d["status.copiedAtPoint"] = "Скопировано {0}  → Ctrl+V в Точку";
        d["status.regionEmptyBox"] = "Рамка пуста — ЛКМ-drag по объектам HellGate.";
        d["status.copiedChunk"] = "Скопирован кусок × {0}  → Ctrl+V в Точку (и в буфер ОС)";
        d["status.chunkEmpty"] = "Буфер куска пуст — рамка, потом Ctrl+C.";
        d["status.chunkWriteFailed"] = "Запись куска в пак не удалась.";
        d["status.chunkPasteFailed"] = "Вставка куска не удалась.";
        d["status.pastedChunk"] = "Вставлен кусок × {0} @ {1},{2} → {3}  (превью {4})";
        d["status.clipboardEmpty"] = "Буфер пуст — сначала Ctrl+C.";
        d["status.pastedAt"] = "Вставлено {0} @ {1},{2} → {3}{4}";
        d["status.pastedPreviewFail"] = " (пак ок, превью не удалось)";
        d["status.pastedNudge"] = "  (стрелки — сдвиг)";
        d["status.favAlready"] = "Уже в избранном.";
        d["status.favAdded"] = "★ Добавлено: {0}";
        d["status.favBadIndex"] = "Неверный индекс избранного.";
        d["status.favRemoved"] = "Удалено: {0}";
        d["status.favLoaded"] = "Загружено ★ {0}";
        d["status.saveFailed"] = "Сохранение не удалось.";
        d["status.undoUnknown"] = "Неизвестный тип отмены.";
        d["status.undoRemovedLast"] = "Отмена: убрана последняя строка спавна.";
        d["status.undoRestoredLine"] = "Отмена: восстановлена предыдущая строка пака.";
        d["status.undoRestoredDeleted"] = "Отмена: восстановлена удалённая строка{0}";
        d["status.undoReplaceFailed"] = "Отмена замены не удалась.";
        d["status.undoDeleteFailed"] = "Отмена удаления не удалась.";
        d["status.savedPrefabNotReady"] = "Сохранено {0}, но префаб (?) не готов — ПКМ reload после кэша.";
        d["status.savedSpawned"] = "Сохранено и заспавнено{0} → {1}";
        d["status.savedTemplateNotReady"] = "Сохранено {0}, но шаблон (?) не готов — зайди на карту с ловушкой / ПКМ reload.";
        d["status.savedSpawnedTrap"] = "Сохранена и заспавнена ловушка{0} → {1}";
        d["status.savedGoldDisabled"] = "Строка золота сохранена (экономика выкл. — без превью) → {0}";
        d["status.savedGoldOk"] = "Золото сохранено и заспавнено → {0}";
        d["status.savedGoldPreviewFail"] = "Строка золота сохранена, превью кучи не удалось → {0}";
        d["status.savedEventTrap"] = "EVENTTRAP сохранён (гизмо в F11) → {0}";
        d["status.noHost"] = "Нет хоста.";
        d["status.screenshotBusy"] = "Скриншот занят…";
        d["status.packWriteFailed"] = "Запись пака не удалась.";
        d["status.pasteWriteFailed"] = "Запись вставки не удалась.";
        d["status.undoAppendFailed"] = "Отмена добавления не удалась.";
        d["status.noSpawnPack"] = "Нет спавн-пака для этой зоны.";
        d["status.writeFailed"] = "Запись не удалась: {0}";
        d["status.editingLine"] = "Правка строки пака {0}";
        d["status.savedEdit"] = "✓ Сохранено → {0}  строка {1}{2}";
        d["status.commitPosFailed"] = "Сохранение позиции не удалось.";
        d["status.selectedHellGate"] = "Выбрано HellGate: {0} (строка {1})";
        d["status.selectedVanilla"] = "Выбрано: {0} — ваниль / нет связи с паком";
        d["edit.packXy"] = "Пак XY: {0},{1}";
        d["edit.currentXy"] = "Сейчас XY: {0},{1}";
        d["edit.x"] = "Правка X";
    }
}
