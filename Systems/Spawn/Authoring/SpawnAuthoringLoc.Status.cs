using System;
using System.Collections.Generic;

namespace NoREroMod.Systems.Spawn;

internal static partial class SpawnAuthoringLoc
{
    /// <summary>
    /// Clipboard / undo / favorites / save-status strings omitted from <see cref="ApplyEastAsian"/>.
    /// EN and RU are already filled by <see cref="BuildEn"/> / <see cref="ApplyRu"/>.
    /// </summary>
    private static void ApplyStatusExtras(Dictionary<string, string> d, string lang)
    {
        if (d == null || string.IsNullOrEmpty(lang))
            return;
        if (string.Equals(lang, "EN", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(lang, "RU", StringComparison.OrdinalIgnoreCase))
            return;

        if (string.Equals(lang, "JP", StringComparison.OrdinalIgnoreCase))
            FillStatusJp(d);
        else if (string.Equals(lang, "CN", StringComparison.OrdinalIgnoreCase))
            FillStatusCn(d);
        else if (string.Equals(lang, "KR", StringComparison.OrdinalIgnoreCase))
            FillStatusKr(d);
        else if (string.Equals(lang, "FR", StringComparison.OrdinalIgnoreCase))
            FillStatusFr(d);
        else if (string.Equals(lang, "DE", StringComparison.OrdinalIgnoreCase))
            FillStatusDe(d);
        else if (string.Equals(lang, "PT", StringComparison.OrdinalIgnoreCase))
            FillStatusPt(d, brazilian: false);
        else if (string.Equals(lang, "BR", StringComparison.OrdinalIgnoreCase))
            FillStatusPt(d, brazilian: true);
        else if (string.Equals(lang, "ES", StringComparison.OrdinalIgnoreCase))
            FillStatusEs(d);
    }

    private static void FillStatusJp(Dictionary<string, string> d)
    {
        d["status.copyNeedSpawn"] = "コピーする HellGate パックのオブジェクトを選択。";
        d["status.goldUnresolved"] = "ゴールド量が不明 — RMB reload のあと Ctrl+C。";
        d["status.trapKeyUnresolved"] = "罠キーが不明 — スパイクを再保存（Ctrl+S / RMB）して Ctrl+C。";
        d["status.copiedAtPoint"] = "コピー {0}  → ポイントで Ctrl+V";
        d["status.regionEmptyBox"] = "範囲が空 — HellGate オブジェクト上を LMB ドラッグ。";
        d["status.copiedChunk"] = "チャンク × {0} をコピー  → ポイントで Ctrl+V（OSクリップボードにも）";
        d["status.chunkEmpty"] = "チャンクが空 — 範囲選択して Ctrl+C。";
        d["status.chunkWriteFailed"] = "チャンク貼付の書き込み失敗。";
        d["status.chunkPasteFailed"] = "チャンク貼付失敗。";
        d["status.pastedChunk"] = "チャンク × {0} を貼付 @ {1},{2} → {3}  （プレビュー {4}）";
        d["status.clipboardEmpty"] = "クリップボード空 — 先に Ctrl+C。";
        d["status.pastedAt"] = "貼付 {0} @ {1},{2} → {3}{4}";
        d["status.pastedPreviewFail"] = " （パック成功、プレビュー失敗）";
        d["status.pastedNudge"] = " （矢印で微調整）";
        d["status.favAlready"] = "すでにお気に入り。";
        d["status.favAdded"] = "★ 追加: {0}";
        d["status.favBadIndex"] = "お気に入りの番号が不正。";
        d["status.favRemoved"] = "削除: {0}";
        d["status.favLoaded"] = "読込 ★ {0}";
        d["status.saveFailed"] = "保存失敗。";
        d["status.undoUnknown"] = "不明な戻す種別。";
        d["status.undoRemovedLast"] = "戻す: 最後のスポーン行を削除。";
        d["status.undoRestoredLine"] = "戻す: 前のパック行を復元。";
        d["status.undoRestoredDeleted"] = "戻す: 削除したパック行を復元{0}";
        d["status.undoReplaceFailed"] = "戻す（置換）失敗。";
        d["status.undoDeleteFailed"] = "戻す（削除）失敗。";
        d["status.undoAppendFailed"] = "戻す（追加）失敗。";
        d["status.savedPrefabNotReady"] = "保存 {0} したがプレハブ未準備 — キャッシュ後に RMB reload。";
        d["status.savedSpawned"] = "保存＋スポーン{0} → {1}";
        d["status.savedTemplateNotReady"] = "保存 {0} したが雛形未準備 — 罠マップへ行く / RMB reload。";
        d["status.savedSpawnedTrap"] = "保存＋トラップスポーン{0} → {1}";
        d["status.savedGoldDisabled"] = "ゴールド行を保存（経済オフ — プレビューなし） → {0}";
        d["status.savedGoldOk"] = "ゴールド保存＋スポーン → {0}";
        d["status.savedGoldPreviewFail"] = "ゴールド行保存、プレビュー失敗 → {0}";
        d["status.savedEventTrap"] = "EVENTTRAP 保存（F11 ギズモ） → {0}";
        d["status.noHost"] = "ホストなし。";
        d["status.screenshotBusy"] = "スクショ処理中…";
        d["status.packWriteFailed"] = "パック書き込み失敗。";
        d["status.pasteWriteFailed"] = "貼付の書き込み失敗。";
        d["status.noSpawnPack"] = "このゾーンにスポーンパックがありません。";
        d["status.writeFailed"] = "書き込み失敗: {0}";
        d["status.editingLine"] = "パック行 {0} を編集中";
        d["status.savedEdit"] = "✓ 保存 → {0}  行 {1}{2}";
        d["status.commitPosFailed"] = "位置の確定失敗。";
        d["status.selectedHellGate"] = "選択 HellGate: {0} （行 {1}）";
        d["status.selectedVanilla"] = "選択: {0} — バニラ / パック未リンク";
        d["edit.packXy"] = "パック XY: {0},{1}";
        d["edit.currentXy"] = "現在 XY: {0},{1}";
        d["edit.x"] = "編集 X";
    }

    private static void FillStatusCn(Dictionary<string, string> d)
    {
        d["status.copyNeedSpawn"] = "先选择要复制的 HellGate 包物体。";
        d["status.goldUnresolved"] = "金币数量不明 — RMB reload 后再 Ctrl+C。";
        d["status.trapKeyUnresolved"] = "陷阱键不明 — 先保存尖刺（Ctrl+S / RMB）再 Ctrl+C。";
        d["status.copiedAtPoint"] = "已复制 {0}  → 在点处 Ctrl+V";
        d["status.regionEmptyBox"] = "框选为空 — 在 HellGate 物体上 LMB 拖框。";
        d["status.copiedChunk"] = "已复制块 × {0}  → 在点处 Ctrl+V（也进系统剪贴板）";
        d["status.chunkEmpty"] = "块剪贴板为空 — 先框选再 Ctrl+C。";
        d["status.chunkWriteFailed"] = "块粘贴写入失败。";
        d["status.chunkPasteFailed"] = "块粘贴失败。";
        d["status.pastedChunk"] = "已粘贴块 × {0} @ {1},{2} → {3}  （预览 {4}）";
        d["status.clipboardEmpty"] = "剪贴板为空 — 先 Ctrl+C。";
        d["status.pastedAt"] = "已粘贴 {0} @ {1},{2} → {3}{4}";
        d["status.pastedPreviewFail"] = " （包已写入，预览失败）";
        d["status.pastedNudge"] = " （方向键微调）";
        d["status.favAlready"] = "已在收藏中。";
        d["status.favAdded"] = "★ 已添加: {0}";
        d["status.favBadIndex"] = "收藏索引无效。";
        d["status.favRemoved"] = "已移除: {0}";
        d["status.favLoaded"] = "已加载 ★ {0}";
        d["status.saveFailed"] = "保存失败。";
        d["status.undoUnknown"] = "未知撤销类型。";
        d["status.undoRemovedLast"] = "撤销: 已删除最后一行生成。";
        d["status.undoRestoredLine"] = "撤销: 已恢复上一包行。";
        d["status.undoRestoredDeleted"] = "撤销: 已恢复删除的包行{0}";
        d["status.undoReplaceFailed"] = "撤销替换失败。";
        d["status.undoDeleteFailed"] = "撤销删除失败。";
        d["status.undoAppendFailed"] = "撤销添加失败。";
        d["status.savedPrefabNotReady"] = "已保存 {0}，但预制体未就绪 — 缓存后 RMB reload。";
        d["status.savedSpawned"] = "已保存并生成{0} → {1}";
        d["status.savedTemplateNotReady"] = "已保存 {0}，但模板未就绪 — 先去有该陷阱的地图 / RMB reload。";
        d["status.savedSpawnedTrap"] = "已保存并生成陷阱{0} → {1}";
        d["status.savedGoldDisabled"] = "已保存金币行（经济关闭 — 无预览） → {0}";
        d["status.savedGoldOk"] = "金币已保存并生成 → {0}";
        d["status.savedGoldPreviewFail"] = "金币行已保存，预览失败 → {0}";
        d["status.savedEventTrap"] = "已保存 EVENTTRAP（F11 小工具） → {0}";
        d["status.noHost"] = "无宿主。";
        d["status.screenshotBusy"] = "截图进行中…";
        d["status.packWriteFailed"] = "包写入失败。";
        d["status.pasteWriteFailed"] = "粘贴写入失败。";
        d["status.noSpawnPack"] = "此区域没有生成包。";
        d["status.writeFailed"] = "写入失败: {0}";
        d["status.editingLine"] = "正在编辑包行 {0}";
        d["status.savedEdit"] = "✓ 已保存 → {0}  行 {1}{2}";
        d["status.commitPosFailed"] = "提交坐标失败。";
        d["status.selectedHellGate"] = "已选 HellGate: {0}（行 {1}）";
        d["status.selectedVanilla"] = "已选: {0} — 原版 / 未链接包";
        d["edit.packXy"] = "包 XY: {0},{1}";
        d["edit.currentXy"] = "当前 XY: {0},{1}";
        d["edit.x"] = "编辑 X";
    }

    private static void FillStatusKr(Dictionary<string, string> d)
    {
        d["status.copyNeedSpawn"] = "복사할 HellGate 팩 오브젝트를 선택하세요.";
        d["status.goldUnresolved"] = "골드 수량이 불명 — RMB reload 후 Ctrl+C.";
        d["status.trapKeyUnresolved"] = "함정 키가 불명 — 스파이크를 다시 저장(Ctrl+S / RMB)한 뒤 Ctrl+C.";
        d["status.copiedAtPoint"] = "복사됨 {0}  → 포인트에서 Ctrl+V";
        d["status.regionEmptyBox"] = "범위가 비어 있음 — HellGate 오브젝트 위로 LMB 드래그.";
        d["status.copiedChunk"] = "청크 × {0} 복사  → 포인트에서 Ctrl+V (OS 클립보드에도)";
        d["status.chunkEmpty"] = "청크 클립보드가 비어 있음 — 박스 선택 후 Ctrl+C.";
        d["status.chunkWriteFailed"] = "청크 붙여넣기 쓰기 실패.";
        d["status.chunkPasteFailed"] = "청크 붙여넣기 실패.";
        d["status.pastedChunk"] = "청크 × {0} 붙여넣음 @ {1},{2} → {3}  (미리보기 {4})";
        d["status.clipboardEmpty"] = "클립보드가 비어 있음 — 먼저 Ctrl+C.";
        d["status.pastedAt"] = "붙여넣음 {0} @ {1},{2} → {3}{4}";
        d["status.pastedPreviewFail"] = " (팩 성공, 미리보기 실패)";
        d["status.pastedNudge"] = " (화살표로 미세이동)";
        d["status.favAlready"] = "이미 즐겨찾기에 있습니다.";
        d["status.favAdded"] = "★ 추가됨: {0}";
        d["status.favBadIndex"] = "잘못된 즐겨찾기 인덱스.";
        d["status.favRemoved"] = "제거됨: {0}";
        d["status.favLoaded"] = "불러옴 ★ {0}";
        d["status.saveFailed"] = "저장 실패.";
        d["status.undoUnknown"] = "알 수 없는 실행 취소 종류.";
        d["status.undoRemovedLast"] = "실행 취소: 마지막 스폰 줄을 삭제했습니다.";
        d["status.undoRestoredLine"] = "실행 취소: 이전 팩 줄을 복원했습니다.";
        d["status.undoRestoredDeleted"] = "실행 취소: 삭제한 팩 줄을 복원{0}";
        d["status.undoReplaceFailed"] = "실행 취소(교체) 실패.";
        d["status.undoDeleteFailed"] = "실행 취소(삭제) 실패.";
        d["status.undoAppendFailed"] = "실행 취소(추가) 실패.";
        d["status.savedPrefabNotReady"] = "{0} 저장했지만 프리팹 미준비 — 캐시 후 RMB reload.";
        d["status.savedSpawned"] = "저장+스폰{0} → {1}";
        d["status.savedTemplateNotReady"] = "{0} 저장했지만 템플릿 미준비 — 해당 함정 맵으로 이동 / RMB reload.";
        d["status.savedSpawnedTrap"] = "저장+함정 스폰{0} → {1}";
        d["status.savedGoldDisabled"] = "골드 줄 저장(경제 꺼짐 — 미리보기 없음) → {0}";
        d["status.savedGoldOk"] = "골드 저장+스폰 → {0}";
        d["status.savedGoldPreviewFail"] = "골드 줄 저장, 미리보기 실패 → {0}";
        d["status.savedEventTrap"] = "EVENTTRAP 저장 (F11 기즈모) → {0}";
        d["status.noHost"] = "호스트 없음.";
        d["status.screenshotBusy"] = "스크린샷 처리 중…";
        d["status.packWriteFailed"] = "팩 쓰기 실패.";
        d["status.pasteWriteFailed"] = "붙여넣기 쓰기 실패.";
        d["status.noSpawnPack"] = "이 구역에 스폰 팩이 없습니다.";
        d["status.writeFailed"] = "쓰기 실패: {0}";
        d["status.editingLine"] = "팩 줄 {0} 편집 중";
        d["status.savedEdit"] = "✓ 저장됨 → {0}  줄 {1}{2}";
        d["status.commitPosFailed"] = "좌표 확정 실패.";
        d["status.selectedHellGate"] = "선택 HellGate: {0} (줄 {1})";
        d["status.selectedVanilla"] = "선택: {0} — 바닐라 / 팩 링크 없음";
        d["edit.packXy"] = "팩 XY: {0},{1}";
        d["edit.currentXy"] = "현재 XY: {0},{1}";
        d["edit.x"] = "편집 X";
    }

    private static void FillStatusFr(Dictionary<string, string> d)
    {
        d["status.copyNeedSpawn"] = "Sélectionne un objet de pack HellGate à copier.";
        d["status.goldUnresolved"] = "Montant d’or indéterminé — RMB reload puis Ctrl+C.";
        d["status.trapKeyUnresolved"] = "Clé de piège indéterminée — réenregistre le spike (Ctrl+S / RMB) puis Ctrl+C.";
        d["status.copiedAtPoint"] = "Copié {0}  → Ctrl+V au Point";
        d["status.regionEmptyBox"] = "Cadre vide — glisse LMB sur des objets HellGate.";
        d["status.copiedChunk"] = "Bloc × {0} copié  → Ctrl+V au Point (aussi presse-papiers OS)";
        d["status.chunkEmpty"] = "Presse-papiers de bloc vide — cadre puis Ctrl+C.";
        d["status.chunkWriteFailed"] = "Écriture du collage de bloc échouée.";
        d["status.chunkPasteFailed"] = "Collage de bloc échoué.";
        d["status.pastedChunk"] = "Bloc × {0} collé @ {1},{2} → {3}  (aperçu {4})";
        d["status.clipboardEmpty"] = "Presse-papiers vide — Ctrl+C d’abord.";
        d["status.pastedAt"] = "Collé {0} @ {1},{2} → {3}{4}";
        d["status.pastedPreviewFail"] = " (pack OK, aperçu échoué)";
        d["status.pastedNudge"] = "  (flèches pour déplacer)";
        d["status.favAlready"] = "Déjà dans les favoris.";
        d["status.favAdded"] = "★ Ajouté : {0}";
        d["status.favBadIndex"] = "Index de favori invalide.";
        d["status.favRemoved"] = "Retiré : {0}";
        d["status.favLoaded"] = "Chargé ★ {0}";
        d["status.saveFailed"] = "Échec de l’enregistrement.";
        d["status.undoUnknown"] = "Type d’annulation inconnu.";
        d["status.undoRemovedLast"] = "Annuler : dernière ligne de spawn retirée.";
        d["status.undoRestoredLine"] = "Annuler : ligne de pack précédente restaurée.";
        d["status.undoRestoredDeleted"] = "Annuler : ligne de pack supprimée restaurée{0}";
        d["status.undoReplaceFailed"] = "Annulation du remplacement échouée.";
        d["status.undoDeleteFailed"] = "Annulation de la suppression échouée.";
        d["status.undoAppendFailed"] = "Annulation de l’ajout échouée.";
        d["status.savedPrefabNotReady"] = "Enregistré {0} mais prefab (?) pas prêt — RMB reload après cache.";
        d["status.savedSpawned"] = "Enregistré + spawné{0} → {1}";
        d["status.savedTemplateNotReady"] = "Enregistré {0} mais modèle (?) pas prêt — visite une carte avec ce piège / RMB reload.";
        d["status.savedSpawnedTrap"] = "Enregistré + piège spawné{0} → {1}";
        d["status.savedGoldDisabled"] = "Ligne d’or enregistrée (économie off — pas d’aperçu) → {0}";
        d["status.savedGoldOk"] = "Or enregistré + spawné → {0}";
        d["status.savedGoldPreviewFail"] = "Ligne d’or enregistrée, aperçu échoué → {0}";
        d["status.savedEventTrap"] = "EVENTTRAP enregistré (gizmo F11) → {0}";
        d["status.noHost"] = "Pas d’hôte.";
        d["status.screenshotBusy"] = "Capture en cours…";
        d["status.packWriteFailed"] = "Écriture du pack échouée.";
        d["status.pasteWriteFailed"] = "Écriture du collage échouée.";
        d["status.noSpawnPack"] = "Pas de pack de spawn pour cette zone.";
        d["status.writeFailed"] = "Écriture échouée : {0}";
        d["status.editingLine"] = "Édition de la ligne de pack {0}";
        d["status.savedEdit"] = "✓ Enregistré → {0}  ligne {1}{2}";
        d["status.commitPosFailed"] = "Validation de la position échouée.";
        d["status.selectedHellGate"] = "HellGate sélectionné : {0} (ligne {1})";
        d["status.selectedVanilla"] = "Sélection : {0} — vanilla / pas de lien pack";
        d["edit.packXy"] = "Pack XY : {0},{1}";
        d["edit.currentXy"] = "XY actuel : {0},{1}";
        d["edit.x"] = "Édit. X";
    }

    private static void FillStatusDe(Dictionary<string, string> d)
    {
        d["status.copyNeedSpawn"] = "Wähle ein HellGate-Pack-Objekt zum Kopieren.";
        d["status.goldUnresolved"] = "Goldmenge unklar — RMB reload, dann Ctrl+C.";
        d["status.trapKeyUnresolved"] = "Fallen-Key unklar — Spike neu speichern (Ctrl+S / RMB), dann Ctrl+C.";
        d["status.copiedAtPoint"] = "Kopiert {0}  → Ctrl+V am Punkt";
        d["status.regionEmptyBox"] = "Rahmen leer — LMB über HellGate-Objekte ziehen.";
        d["status.copiedChunk"] = "Chunk × {0} kopiert  → Ctrl+V am Punkt (auch OS-Zwischenablage)";
        d["status.chunkEmpty"] = "Chunk-Zwischenablage leer — Rahmen, dann Ctrl+C.";
        d["status.chunkWriteFailed"] = "Chunk-Einfügen schreiben fehlgeschlagen.";
        d["status.chunkPasteFailed"] = "Chunk-Einfügen fehlgeschlagen.";
        d["status.pastedChunk"] = "Chunk × {0} eingefügt @ {1},{2} → {3}  (Vorschau {4})";
        d["status.clipboardEmpty"] = "Zwischenablage leer — zuerst Ctrl+C.";
        d["status.pastedAt"] = "Eingefügt {0} @ {1},{2} → {3}{4}";
        d["status.pastedPreviewFail"] = " (Pack OK, Vorschau fehlgeschlagen)";
        d["status.pastedNudge"] = "  (Pfeile zum Verschieben)";
        d["status.favAlready"] = "Bereits in Favoriten.";
        d["status.favAdded"] = "★ Hinzugefügt: {0}";
        d["status.favBadIndex"] = "Ungültiger Favoritenindex.";
        d["status.favRemoved"] = "Entfernt: {0}";
        d["status.favLoaded"] = "Geladen ★ {0}";
        d["status.saveFailed"] = "Speichern fehlgeschlagen.";
        d["status.undoUnknown"] = "Unbekannte Undo-Art.";
        d["status.undoRemovedLast"] = "Undo: letzte Spawn-Zeile entfernt.";
        d["status.undoRestoredLine"] = "Undo: vorherige Pack-Zeile wiederhergestellt.";
        d["status.undoRestoredDeleted"] = "Undo: gelöschte Pack-Zeile wiederhergestellt{0}";
        d["status.undoReplaceFailed"] = "Undo-Ersetzen fehlgeschlagen.";
        d["status.undoDeleteFailed"] = "Undo-Löschen fehlgeschlagen.";
        d["status.undoAppendFailed"] = "Undo-Hinzufügen fehlgeschlagen.";
        d["status.savedPrefabNotReady"] = "{0} gespeichert, Prefab (?) nicht bereit — RMB reload nach Cache.";
        d["status.savedSpawned"] = "Gespeichert + gespawnt{0} → {1}";
        d["status.savedTemplateNotReady"] = "{0} gespeichert, Vorlage (?) nicht bereit — Fallen-Karte betreten / RMB reload.";
        d["status.savedSpawnedTrap"] = "Gespeichert + Falle gespawnt{0} → {1}";
        d["status.savedGoldDisabled"] = "Goldzeile gespeichert (Wirtschaft aus — keine Vorschau) → {0}";
        d["status.savedGoldOk"] = "Gold gespeichert + gespawnt → {0}";
        d["status.savedGoldPreviewFail"] = "Goldzeile gespeichert, Vorschau fehlgeschlagen → {0}";
        d["status.savedEventTrap"] = "EVENTTRAP gespeichert (F11-Gizmo) → {0}";
        d["status.noHost"] = "Kein Host.";
        d["status.screenshotBusy"] = "Screenshot läuft…";
        d["status.packWriteFailed"] = "Pack-Schreiben fehlgeschlagen.";
        d["status.pasteWriteFailed"] = "Einfügen-Schreiben fehlgeschlagen.";
        d["status.noSpawnPack"] = "Kein Spawn-Pack für diese Zone.";
        d["status.writeFailed"] = "Schreiben fehlgeschlagen: {0}";
        d["status.editingLine"] = "Pack-Zeile {0} bearbeiten";
        d["status.savedEdit"] = "✓ Gespeichert → {0}  Zeile {1}{2}";
        d["status.commitPosFailed"] = "Position übernehmen fehlgeschlagen.";
        d["status.selectedHellGate"] = "HellGate gewählt: {0} (Zeile {1})";
        d["status.selectedVanilla"] = "Gewählt: {0} — Vanilla / kein Pack-Link";
        d["edit.packXy"] = "Pack XY: {0},{1}";
        d["edit.currentXy"] = "Aktuell XY: {0},{1}";
        d["edit.x"] = "Edit X";
    }

    private static void FillStatusPt(Dictionary<string, string> d, bool brazilian)
    {
        d["status.copyNeedSpawn"] = "Seleciona um objeto de pack HellGate para copiar.";
        d["status.goldUnresolved"] = "Quantidade de ouro indefinida — RMB reload e depois Ctrl+C.";
        d["status.trapKeyUnresolved"] = "Chave da armadilha indefinida — volta a guardar o spike (Ctrl+S / RMB) e Ctrl+C.";
        d["status.copiedAtPoint"] = "Copiado {0}  → Ctrl+V no Ponto";
        d["status.regionEmptyBox"] = "Caixa vazia — arrasta LMB sobre objetos HellGate.";
        d["status.copiedChunk"] = "Bloco × {0} copiado  → Ctrl+V no Ponto (também área de transferência)";
        d["status.chunkEmpty"] = "Área de bloco vazia — caixa e depois Ctrl+C.";
        d["status.chunkWriteFailed"] = "Falha ao escrever o bloco colado.";
        d["status.chunkPasteFailed"] = "Falha ao colar o bloco.";
        d["status.pastedChunk"] = "Bloco × {0} colado @ {1},{2} → {3}  (pré-visualização {4})";
        d["status.clipboardEmpty"] = "Área de transferência vazia — primeiro Ctrl+C.";
        d["status.pastedAt"] = "Colado {0} @ {1},{2} → {3}{4}";
        d["status.pastedPreviewFail"] = " (pack ok, pré-visualização falhou)";
        d["status.pastedNudge"] = "  (setas para ajustar)";
        d["status.favAlready"] = "Já está nos favoritos.";
        d["status.favAdded"] = "★ Adicionado: {0}";
        d["status.favBadIndex"] = "Índice de favorito inválido.";
        d["status.favRemoved"] = "Removido: {0}";
        d["status.favLoaded"] = "Carregado ★ {0}";
        d["status.saveFailed"] = brazilian ? "Falha ao salvar." : "Falha ao guardar.";
        d["status.undoUnknown"] = "Tipo de anulação desconhecido.";
        d["status.undoRemovedLast"] = "Anular: última linha de spawn removida.";
        d["status.undoRestoredLine"] = "Anular: linha de pack anterior restaurada.";
        d["status.undoRestoredDeleted"] = "Anular: linha de pack apagada restaurada{0}";
        d["status.undoReplaceFailed"] = "Falha ao anular a substituição.";
        d["status.undoDeleteFailed"] = "Falha ao anular a eliminação.";
        d["status.undoAppendFailed"] = "Falha ao anular a adição.";
        d["status.savedPrefabNotReady"] = brazilian
            ? "Salvo {0} mas prefab (?) não pronto — RMB reload após cache."
            : "Guardado {0} mas prefab (?) não pronto — RMB reload após cache.";
        d["status.savedSpawned"] = brazilian ? "Salvo + spawnado{0} → {1}" : "Guardado + spawnado{0} → {1}";
        d["status.savedTemplateNotReady"] = brazilian
            ? "Salvo {0} mas modelo (?) não pronto — visita um mapa com a armadilha / RMB reload."
            : "Guardado {0} mas modelo (?) não pronto — visita um mapa com a armadilha / RMB reload.";
        d["status.savedSpawnedTrap"] = brazilian
            ? "Salvo + armadilha spawnada{0} → {1}"
            : "Guardado + armadilha spawnada{0} → {1}";
        d["status.savedGoldDisabled"] = brazilian
            ? "Linha de ouro salva (economia desligada — sem pré-visualização) → {0}"
            : "Linha de ouro guardada (economia desligada — sem pré-visualização) → {0}";
        d["status.savedGoldOk"] = brazilian ? "Ouro salvo + spawnado → {0}" : "Ouro guardado + spawnado → {0}";
        d["status.savedGoldPreviewFail"] = brazilian
            ? "Linha de ouro salva, pré-visualização falhou → {0}"
            : "Linha de ouro guardada, pré-visualização falhou → {0}";
        d["status.savedEventTrap"] = brazilian
            ? "EVENTTRAP salvo (gizmo F11) → {0}"
            : "EVENTTRAP guardado (gizmo F11) → {0}";
        d["status.noHost"] = "Sem host.";
        d["status.screenshotBusy"] = "Captura ocupada…";
        d["status.packWriteFailed"] = brazilian ? "Falha ao escrever o pack." : "Falha ao escrever o pack.";
        d["status.pasteWriteFailed"] = "Falha ao escrever a colagem.";
        d["status.noSpawnPack"] = "Não há pack de spawn nesta zona.";
        d["status.writeFailed"] = "Falha ao escrever: {0}";
        d["status.editingLine"] = "A editar a linha de pack {0}";
        d["status.savedEdit"] = brazilian
            ? "✓ Salvo → {0}  linha {1}{2}"
            : "✓ Guardado → {0}  linha {1}{2}";
        d["status.commitPosFailed"] = "Falha ao confirmar a posição.";
        d["status.selectedHellGate"] = "HellGate selecionado: {0} (linha {1})";
        d["status.selectedVanilla"] = "Selecionado: {0} — vanilla / sem ligação ao pack";
        d["edit.packXy"] = "Pack XY: {0},{1}";
        d["edit.currentXy"] = "XY atual: {0},{1}";
        d["edit.x"] = "Editar X";
        if (brazilian)
        {
            d["status.copyNeedSpawn"] = "Selecione um objeto de pack HellGate para copiar.";
            d["status.editingLine"] = "Editando a linha de pack {0}";
        }
    }

    private static void FillStatusEs(Dictionary<string, string> d)
    {
        d["status.copyNeedSpawn"] = "Selecciona un objeto de pack HellGate para copiar.";
        d["status.goldUnresolved"] = "Cantidad de oro indefinida — RMB reload y luego Ctrl+C.";
        d["status.trapKeyUnresolved"] = "Clave de trampa indefinida — vuelve a guardar el spike (Ctrl+S / RMB) y Ctrl+C.";
        d["status.copiedAtPoint"] = "Copiado {0}  → Ctrl+V en el Punto";
        d["status.regionEmptyBox"] = "Marco vacío — arrastra LMB sobre objetos HellGate.";
        d["status.copiedChunk"] = "Bloque × {0} copiado  → Ctrl+V en el Punto (también portapapeles OS)";
        d["status.chunkEmpty"] = "Portapapeles de bloque vacío — marco y luego Ctrl+C.";
        d["status.chunkWriteFailed"] = "Fallo al escribir el bloque pegado.";
        d["status.chunkPasteFailed"] = "Fallo al pegar el bloque.";
        d["status.pastedChunk"] = "Bloque × {0} pegado @ {1},{2} → {3}  (vista previa {4})";
        d["status.clipboardEmpty"] = "Portapapeles vacío — primero Ctrl+C.";
        d["status.pastedAt"] = "Pegado {0} @ {1},{2} → {3}{4}";
        d["status.pastedPreviewFail"] = " (pack ok, vista previa falló)";
        d["status.pastedNudge"] = "  (flechas para ajustar)";
        d["status.favAlready"] = "Ya está en favoritos.";
        d["status.favAdded"] = "★ Añadido: {0}";
        d["status.favBadIndex"] = "Índice de favorito inválido.";
        d["status.favRemoved"] = "Eliminado: {0}";
        d["status.favLoaded"] = "Cargado ★ {0}";
        d["status.saveFailed"] = "Fallo al guardar.";
        d["status.undoUnknown"] = "Tipo de deshacer desconocido.";
        d["status.undoRemovedLast"] = "Deshacer: última línea de spawn eliminada.";
        d["status.undoRestoredLine"] = "Deshacer: línea de pack anterior restaurada.";
        d["status.undoRestoredDeleted"] = "Deshacer: línea de pack eliminada restaurada{0}";
        d["status.undoReplaceFailed"] = "Fallo al deshacer el reemplazo.";
        d["status.undoDeleteFailed"] = "Fallo al deshacer el borrado.";
        d["status.undoAppendFailed"] = "Fallo al deshacer el añadido.";
        d["status.savedPrefabNotReady"] = "Guardado {0} pero prefab (?) no listo — RMB reload tras caché.";
        d["status.savedSpawned"] = "Guardado + spawneado{0} → {1}";
        d["status.savedTemplateNotReady"] = "Guardado {0} pero plantilla (?) no lista — visita un mapa con esa trampa / RMB reload.";
        d["status.savedSpawnedTrap"] = "Guardado + trampa spawneada{0} → {1}";
        d["status.savedGoldDisabled"] = "Línea de oro guardada (economía off — sin vista previa) → {0}";
        d["status.savedGoldOk"] = "Oro guardado + spawneado → {0}";
        d["status.savedGoldPreviewFail"] = "Línea de oro guardada, vista previa falló → {0}";
        d["status.savedEventTrap"] = "EVENTTRAP guardado (gizmo F11) → {0}";
        d["status.noHost"] = "Sin host.";
        d["status.screenshotBusy"] = "Captura ocupada…";
        d["status.packWriteFailed"] = "Fallo al escribir el pack.";
        d["status.pasteWriteFailed"] = "Fallo al escribir el pegado.";
        d["status.noSpawnPack"] = "No hay pack de spawn en esta zona.";
        d["status.writeFailed"] = "Fallo al escribir: {0}";
        d["status.editingLine"] = "Editando la línea de pack {0}";
        d["status.savedEdit"] = "✓ Guardado → {0}  línea {1}{2}";
        d["status.commitPosFailed"] = "Fallo al confirmar la posición.";
        d["status.selectedHellGate"] = "HellGate seleccionado: {0} (línea {1})";
        d["status.selectedVanilla"] = "Seleccionado: {0} — vanilla / sin enlace al pack";
        d["edit.packXy"] = "Pack XY: {0},{1}";
        d["edit.currentXy"] = "XY actual: {0},{1}";
        d["edit.x"] = "Editar X";
    }
}
