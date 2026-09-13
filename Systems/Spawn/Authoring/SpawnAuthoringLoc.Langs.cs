using System.Collections.Generic;

namespace NoREroMod.Systems.Spawn;

internal static partial class SpawnAuthoringLoc
{
    private static void ApplyJp(Dictionary<string, string> d)
    {
        ApplyEastAsian(d,
            coordsTitle: "配置",
            point: "ポイント",
            pack: "パック",
            pending: "選択中",
            clipboard: "コピー済",
            selected: "選択",
            line: "行",
            hotkeys: "LMB=ポイント · LMBドラッグ=範囲 · Ctrl+C/X/V/D · 矢印 · Shift+矢印 · E H T R G N M C F B V U · Home · Space · RMB reload · F12 · F1 · Q",
            dragging: "ドラッグ中…",
            clickReady: "スポーンポイント準備完了",
            clickUpdated: "座標を更新しました",
            marker: "SPAWN",
            copied: "コピーしました",
            cut: "切り取り",
            pasted: "貼り付け",
            duplicated: "複製",
            deleted: "削除",
            shotOk: "スクリーンショット保存",
            shotFail: "スクリーンショット失敗",
            btnShot: "スクショ [F12]",
            btnHelp: "ヘルプ [F1]",
            enemies: "敵 [E]",
            enemiesOpen: "敵 ▲ [E]",
            hostage: "人質 [H]",
            hostageOpen: "人質 ▲ [H]",
            hostageTitle: "人質 & OtherScenes",
            helpHostage: "人質、続けて Spine 演出（Look_Dorei, gob_look, …）。|faction= は救出後の戦闘員に継承。Random は RANDOM_HOSTAGE。",
            trap: "罠 [T]",
            trapOpen: "罠 ▲ [T]",
            lethal: "致死 [R]",
            lethalOpen: "致死 ▲ [R]",
            decor: "装飾 [N]",
            decorOpen: "装飾 ▲ [N]",
            gold: "ゴールド [G]",
            goldOpen: "ゴールド ▲ [G]",
            goldTitle: "ゴールド",
            helpGold: "ゴールドの山。固定または範囲。Random は RANDOM,chance,X,Y,gold=…",
            helpGoldOpt: "Random = RANDOM,chance。固定 = GOLD,X,Y,amount,1。",
            etTitle: "EventTrap アンカー",
            helpEt: "EVENTTRAP,folder,X,Y と任意の count/dist/sides/faction/max/r/delay。空欄は pack の config.json。F11 ではラベル付きギズモ（プレイでは非表示）。",
            helpEtGizmo: "編集専用の待ち伏せ／増援アンカー。ゲーム中は非表示。XY をドラッグまたは入力し、位置を使う／保存。",
            helpEtOpt: "空欄 = パック初期値。距離はノックダウン時のプレイヤー基準（アンカーではない）。派閥は待ち伏せ敵に適用。Sides: pack / both / right-only。",
            ecNone: "なし",
            ecAttach: "イベント p=",
            helpEc: "会話NPC: TouzokuNormal|ec_event=<id>。Broker / FSP bandits は常に TouzokuNormal。ポイント→配置。配置済みNPCをLMBで編集。",
            helpEcOpt: "Random = NPC出現率 (RANDOM,chance)。イベント p= = |ec_chance=。NPCは必ず出る。1 = 会話を必ず付与。派閥初期値はイベント遭遇。",
            helpPrevEc: "紐づいたNPCプレハブのプレビュー。プレビューまたはリストからマップへドラッグで配置＆保存。",
            count: "数",
            dist: "距離",
            max: "最大",
            zone: "範囲",
            delay: "遅延",
            sidesPack: "方向: pack",
            sidesBoth: "方向: 両側",
            sidesRight: "方向: 右のみ",
            decorTitle: "装飾",
            spine: "— Spine アニメ —",
            helpDecor: "箱・樽・死体・小物。Spine 演出は人質 & OtherScenes。(?) = まだキャッシュなし。",
            fav: "お気に入り ★ [B]",
            favOpen: "お気に入り ▲ [B]",
            btnDel: "削除 [Del]",
            btnUndo: "元に戻す [Ctrl+Z]",
            btnCopy: "コピー [Ctrl+C]",
            btnCut: "切り取り [Ctrl+X]",
            btnPaste: "貼付 [Ctrl+V]",
            viewOff: "俯瞰 [V]",
            viewOn: "俯瞰 ON [V]",
            confirm: "配置  [Space]",
            confirmFull: "配置して保存  [Space]",
            addFav: "★ お気に入りへ [Ctrl+B]",
            favDel: "削",
            btnSave: "保存  [Ctrl+S]",
            btnSaved: "✓ 保存済",
            closeEdit: "閉じる",
            usePos: "位置を使う [U]",
            reset: "リセット",
            barUndo: "戻す",
            barSave: "保存",
            barFlip: "反転 [F]",
            barFlipOn: "反転 ON [F]",
            barCopy: "コピー",
            barCut: "切取",
            barPaste: "貼付",
            barDelete: "削除",
            barPlace: "配置 [␣]",
            trapsTitle: "罠",
            lethalTitle: "致死トラップ",
            helpTraps: "キーを選び、反転/回転/ソート、ポイントをクリック、配置。配置済みをLMBで編集/削除。(?) = 未キャッシュ。",
            helpLethal: "致死トラップ（Gore）。反転/ソートのみ（回転なし）。Gore Content が必要。",
            flipOn: "左向き [F]",
            flipOff: "右向き [F]",
            ready: "準備完了",
            enemiesTitle: "敵",
            favTitle: "お気に入り",
            favEmpty: "空 — カタログでキーを選び ★。",
            favAdd: "★ 現在を追加",
            factionTitle: "派閥",
            elite: "エリート",
            flip: "左に反転",
            rot: "回転",
            degrees: "度",
            apply: "設定",
            sort: "ソート",
            random: "ランダム出現",
            options: "オプション",
            preview: "プレビュー",
            pickEnemy: "敵を選択",
            pickTrap: "罠を選択",
            noPrefab: "プレハブなし",
            noMesh: "メッシュなし",
            unavailable: "利用不可",
            na: "[不可]",
            enemyNa: "この敵はオーサリング対象外です。",
            helpTitle: "ヘルプ",
            guideTitle: "操作",
            cam: "カメラ / 移動",
            camBody: "WASD — カメラを動かす\n中ボタンを押しながらドラッグ — マウスでカメラを動かす\nホイール — ズーム（俯瞰ON）\nV — 俯瞰 / Default\nHome — プレイヤーへ（俯瞰は維持、ズームは戻す）",
            mouse: "マウス",
            mouseBody: "空き地をLMB — ポイントを置く\n空き地をドラッグ — 範囲選択\nスプライトをLMB — 選択 / 編集（精密。Altで広め）\nオブジェクトをLMB長押し — 移動\n矢印 — 選択を微調整（Shift+矢印 ×5）\nプレビューまたはリストからマップへドラッグ — 配置して保存\nRMB — パック保存とシーン再読込",
            catalogs: "カタログ",
            catalogsBody: "E — 敵\nH — 人質\nT — 罠\nR — 致死トラップ\nG — ゴールド\nN — 装飾\nM — EventTrap\nC — EventCore\nB — お気に入り\nF — 反転\nSpace / Enter — 配置\nU — 位置を使う\nQ — パネル（とこのヘルプ）を閉じる",
            editClip: "編集とクリップボード",
            editBody: "Ctrl+C — コピー\nCtrl+X — 切り取り\nCtrl+V — ポイントへ貼付\nCtrl+D — 複製\nCtrl+Z — 戻す\nCtrl+S — 保存\nCtrl+B — お気に入りへ\nDel — 削除（範囲内をすべて削除）\nF12 — スクショ\nF1 — このヘルプ\nEsc — ヘルプ、範囲、編集、選択の順に閉じる",
            footer: "各パネルの ? に追加ヒント。",
            helpCoords: "空き地をLMB = ポイント。空き地をドラッグ = 範囲（Ctrl+C/X、Ctrl+Vでポイントへ）。スプライトをLMB = 選択/編集（Altで広め）。オブジェクトを長押し = 移動。MMB / WASD = カメラ移動。ホイール = ズーム。矢印 = 微調整（Shift+矢印 ×5）。Home = プレイヤー。F1ヘルプ。F12スクショ。− でこのパネルを畳む。",
            helpFaction: "パック行に書く派閥。紋章は HellGate アイコン。< > で切替。表示名のみ。パックは技術トークン。",
            helpOpt: "エリート = ForceElite。反転 = −X。Random = RANDOM,chance。",
            helpTrapOpt: "反転=水平。回転: +30° [R] または度数＋設定。ソート = sort±N（SortingOrder。near/farではない）。",
            helpHostOpt: "反転=水平。ソート = sort±N。Random は RANDOM_HOSTAGE。人質と OtherScenes は回転なし。",
            helpLetOpt: "反転=水平。ソート = sort±N。致死トラップは回転なし。",
            helpPrev: "選択中の敵プレハブ。プレビューまたはリストからマップへドラッグで配置＆保存。",
            helpPrevTrap: "トラップ雛形。ドラッグで配置＆保存。(?) = 先にその罠があるマップへ行く。",
            dragPlace: "プレビューまたはリストからマップへドラッグ",
            dragRelease: "離すと配置＆保存",
            facNone: "なし",
            facBandits: "山賊",
            facInq: "山賊（異端審問）",
            facMafia: "山賊（マフィア）",
            facDemons: "山賊（悪魔）",
            facChurch: "教会",
            facDemons2: "悪魔",
            facMafia2: "マフィア",
            facUndead: "不死",
            facMonsters: "モンスター",
            facWitch: "魔女",
            facEvent: "イベント遭遇",
            editTitle: "選択中",
            editKey: "キー",
            editFaction: "派閥",
            editRandom: "ランダム出現",
            editChance: "確率",
            editVanilla: "パック未リンク — 編集不可。",
            panelsClosed: "パネルを閉じました [Q]",
            pickKey: "先にキーを選んでください。",
            selectEnemy: "先に敵を選択（ホバー＋LMB）。",
            editDisabled: "パック未リンク — 編集不可。",
            pasteNeed: "マップをクリックしてから Ctrl+V。",
            camHome: "カメラ → プレイヤー",
            camHomeFail: "プレイヤーが見つかりません。",
            badSort: "ソート値が不正です。",
            badRot: "回転の度数が不正です。",
            done: "完了。",
            noLink: "パック未リンク。",
            noLinkXy: "パック未リンク — XY未保存。",
            keyReq: "キーが必要です。",
            badXy: "X/Yが不正です（12.34形式）。",
            goldReq: "ゴールド量が必要です。",
            delFail: "削除に失敗。",
            cutFail: "切り取りに失敗。",
            copyFail: "コピーに失敗。",
            pasteFail: "貼り付けに失敗。",
            undoEmpty: "戻すものがありません。",
            savedXy: "XYをパックへ保存",
            cutCount: "切取 × {0}  → ポイントで Ctrl+V",
            deletedCount: "削除 × {0}  (Ctrl+Z で戻す)",
            copyDelFail: "{0}  (削除失敗: {1})",
            savedKeyAt: "保存 {0} @ {1},{2} → 行 {3}",
            selectPack: "先に HellGate パックのオブジェクトを選択（バニラは編集/削除不可）。",
            deletedLine: "パック行 {0}{1} を削除  (Ctrl+Z で戻す)",
            regionEmpty: "範囲が空です。",
            delRegionFail: "範囲削除に失敗。",
            needPoint: "先にマップをクリック（LMB）。",
            noEnemy: "敵が未確定です。",
            noTrap: "罠が未確定です。",
            noGold: "ゴールド量がありません。",
            noFolder: "EventTrap フォルダがありません。",
            camMissing: "カメラが見つかりません。",
            camSize: "カメラ基準サイズを読めません。",
            overviewOn: "俯瞰 ON — WASD / MMBドラッグでカメラ移動、ホイールでズーム。Vで Default。",
            viewDefault: "俯瞰 Default（標準）。");
    }

    private static void ApplyCn(Dictionary<string, string> d)
    {
        ApplyEastAsian(d,
            coordsTitle: "放置",
            point: "点",
            pack: "包",
            pending: "待放置",
            clipboard: "已复制",
            selected: "已选",
            line: "行",
            hotkeys: "LMB=点 · LMB拖=框选 · Ctrl+C/X/V/D · 方向键 · Shift+方向 · E H T R G N M C F B V U · Home · Space · RMB reload · F12 · F1 · Q",
            dragging: "拖动中…",
            clickReady: "生成点已就绪",
            clickUpdated: "坐标已更新",
            marker: "SPAWN",
            copied: "已复制",
            cut: "已剪切",
            pasted: "已粘贴",
            duplicated: "已复制副本",
            deleted: "已删除",
            shotOk: "截图已保存",
            shotFail: "截图失败",
            btnShot: "截图 [F12]",
            btnHelp: "帮助 [F1]",
            enemies: "敌人 [E]",
            enemiesOpen: "敌人 ▲ [E]",
            hostage: "人质 [H]",
            hostageOpen: "人质 ▲ [H]",
            hostageTitle: "人质 & OtherScenes",
            helpHostage: "人质，然后是 Spine 演出（Look_Dorei, gob_look, …）。|faction= 会在解救后继承到战斗单位。Random 写入 RANDOM_HOSTAGE。",
            trap: "陷阱 [T]",
            trapOpen: "陷阱 ▲ [T]",
            lethal: "致命 [R]",
            lethalOpen: "致命 ▲ [R]",
            decor: "装饰 [N]",
            decorOpen: "装饰 ▲ [N]",
            gold: "金币 [G]",
            goldOpen: "金币 ▲ [G]",
            goldTitle: "金币堆",
            helpGold: "放置金币堆。固定或区间。Random 写入 RANDOM,chance,X,Y,gold=…",
            helpGoldOpt: "Random = RANDOM,chance。固定 = GOLD,X,Y,amount,1。",
            etTitle: "EventTrap 锚点",
            helpEt: "写入 EVENTTRAP,folder,X,Y 及可选 count/dist/sides/faction/max/r/delay。空字段保留 pack 的 config.json。F11 显示标签（游戏中不可见）。",
            helpEtGizmo: "仅编辑器用的伏击/增援锚点。游戏中不可见。拖动或输入 XY，然后使用位置 / 保存。",
            helpEtOpt: "空 = 包默认。距离以倒地时玩家为准，不是锚点。派系应用到伏击敌人。Sides: pack / both / right-only。",
            ecNone: "无",
            ecAttach: "事件 p=",
            helpEc: "放置对话 NPC：TouzokuNormal|ec_event=<id>。Broker / FSP bandits 固定 TouzokuNormal。先点，再放置。LMB 已放 NPC 可编辑。",
            helpEcOpt: "Random = NPC 出现率 (RANDOM,chance)。事件 p= = |ec_chance=。NPC 总会刷出；1 = 始终挂对话。派系默认为事件遭遇。",
            helpPrevEc: "绑定 NPC 预制体预览。从预览或列表拖到地图即可放置并保存。",
            count: "数量",
            dist: "距离",
            max: "上限",
            zone: "范围",
            delay: "延迟",
            sidesPack: "方向: pack",
            sidesBoth: "方向: 两侧",
            sidesRight: "方向: 仅右",
            decorTitle: "装饰",
            spine: "— Spine 动画 —",
            helpDecor: "箱子、桶、尸体、场景道具。Spine 演出在人质 & OtherScenes。(?) = 尚未缓存。",
            fav: "收藏 ★ [B]",
            favOpen: "收藏 ▲ [B]",
            btnDel: "删除 [Del]",
            btnUndo: "撤销  [Ctrl+Z]",
            btnCopy: "复制 [Ctrl+C]",
            btnCut: "剪切 [Ctrl+X]",
            btnPaste: "粘贴 [Ctrl+V]",
            viewOff: "俯视 [V]",
            viewOn: "俯视开 [V]",
            confirm: "放置  [Space]",
            confirmFull: "放置并保存  [Space]",
            addFav: "★ 加入收藏 [Ctrl+B]",
            favDel: "删",
            btnSave: "保存  [Ctrl+S]",
            btnSaved: "✓ 已保存",
            closeEdit: "关闭",
            usePos: "使用位置 [U]",
            reset: "重置",
            barUndo: "撤销",
            barSave: "保存",
            barFlip: "翻转 [F]",
            barFlipOn: "翻转开 [F]",
            barCopy: "复制",
            barCut: "剪切",
            barPaste: "粘贴",
            barDelete: "删除",
            barPlace: "放置 [␣]",
            trapsTitle: "陷阱",
            lethalTitle: "致命陷阱",
            helpTraps: "选键、翻转/旋转/层、点地图、放置。LMB 已放陷阱可编辑/删除。(?) = 未缓存。",
            helpLethal: "致命陷阱（Gore）。仅翻转/层（无旋转）。需要 Gore Content。",
            flipOn: "朝左 [F]",
            flipOff: "朝右 [F]",
            ready: "就绪",
            enemiesTitle: "敌人",
            favTitle: "收藏",
            favEmpty: "空 — 在任意目录选键，然后 ★。",
            favAdd: "★ 添加当前",
            factionTitle: "派系",
            elite: "精英",
            flip: "向左翻转",
            rot: "旋转",
            degrees: "角度",
            apply: "应用",
            sort: "层",
            random: "随机出现",
            options: "选项",
            preview: "预览",
            pickEnemy: "选择敌人",
            pickTrap: "选择陷阱",
            noPrefab: "无预制体",
            noMesh: "无网格",
            unavailable: "不可用",
            na: "[不可]",
            enemyNa: "该敌人不可用于编辑。",
            helpTitle: "帮助",
            guideTitle: "操作",
            cam: "镜头 / 移动",
            camBody: "WASD — 移动镜头\n按住中键拖动 — 用鼠标移动镜头\n滚轮 — 缩放（开启俯视）\nV — 俯视 / Default\nHome — 镜头到玩家（保留俯视，缩放复位）",
            mouse: "鼠标",
            mouseBody: "空白处点 LMB — 设点\n空白处拖动 — 框选\n点到精灵 — 选择 / 编辑（精确；Alt 扩大范围）\n按住物体 LMB — 拖动\n方向键 — 微调所选（Shift+方向 ×5）\n从预览或列表拖到地图 — 放置并保存\nRMB — 保存包并重载场景",
            catalogs: "目录",
            catalogsBody: "E — 敌人\nH — 人质\nT — 陷阱\nR — 致命陷阱\nG — 金币\nN — 装饰\nM — EventTrap\nC — EventCore\nB — 收藏\nF — 翻转\nSpace / Enter — 放置\nU — 使用位置\nQ — 关闭面板（和本帮助）",
            editClip: "编辑与剪贴板",
            editBody: "Ctrl+C — 复制\nCtrl+X — 剪切\nCtrl+V — 粘贴到点\nCtrl+D — 再放一份\nCtrl+Z — 撤销\nCtrl+S — 保存编辑\nCtrl+B — 加入收藏\nDel — 删除（框选会删掉框内全部）\nF12 — 截图\nF1 — 本帮助\nEsc — 先关帮助，再关框选，再关编辑，再取消选择",
            footer: "将鼠标移到面板 ? 查看更多提示。",
            helpCoords: "空白处 LMB = 设点。空白处拖动 = 框选（Ctrl+C/X，Ctrl+V 粘贴到点）。点到精灵 = 选择/编辑（Alt 扩大范围）。按住物体 = 拖动。中键 / WASD = 移动镜头。滚轮 = 缩放。方向键微调（Shift+方向 ×5）。Home = 镜头到玩家。F1 帮助。F12 截图。− 折叠本面板。",
            helpFaction: "写入包行的派系。纹章是 HellGate 图标。< > 循环。显示名仅供 UI；包仍存技术令牌。",
            helpOpt: "精英 = ForceElite。翻转 = −X。Random = RANDOM,chance。",
            helpTrapOpt: "翻转=水平。旋转: +30° [R] 或输入角度+应用。层 = sort±N（SortingOrder，不是 near/far）。",
            helpHostOpt: "翻转=水平。层 = sort±N。Random 写入 RANDOM_HOSTAGE。人质和 OtherScenes 不旋转。",
            helpLetOpt: "翻转=水平。层 = sort±N。致命陷阱不旋转。",
            helpPrev: "当前敌人预览。从预览或列表拖到地图即可放置并保存。",
            helpPrevTrap: "陷阱模板预览。拖到地图放置并保存。(?) = 先去包含该陷阱的地图。",
            dragPlace: "从预览或列表拖到地图放置",
            dragRelease: "松开即可放置并保存",
            facNone: "无",
            facBandits: "盗匪",
            facInq: "盗匪（异端审判）",
            facMafia: "盗匪（黑帮）",
            facDemons: "盗匪（恶魔）",
            facChurch: "教会",
            facDemons2: "恶魔",
            facMafia2: "黑帮",
            facUndead: "亡灵",
            facMonsters: "怪物",
            facWitch: "女巫",
            facEvent: "事件遭遇",
            editTitle: "已选",
            editKey: "键",
            editFaction: "派系",
            editRandom: "随机刷新",
            editChance: "概率",
            editVanilla: "未链接到包 — 无法编辑。",
            panelsClosed: "面板已关闭 [Q]",
            pickKey: "请先选择键。",
            selectEnemy: "请先选择敌人（悬停 + LMB）。",
            editDisabled: "未链接到包 — 无法编辑。",
            pasteNeed: "先在地图上点击，再 Ctrl+V。",
            camHome: "镜头 → 玩家",
            camHomeFail: "未找到玩家。",
            badSort: "层数值无效。",
            badRot: "旋转角度无效。",
            done: "完成。",
            noLink: "未链接到包。",
            noLinkXy: "未链接到包 — 未写入 XY。",
            keyReq: "需要键。",
            badXy: "X/Y 无效（格式 12.34）。",
            goldReq: "需要金币数量。",
            delFail: "删除失败。",
            cutFail: "剪切失败。",
            copyFail: "复制失败。",
            pasteFail: "粘贴失败。",
            undoEmpty: "没有可撤销的操作。",
            savedXy: "XY 已写入包",
            cutCount: "已剪切 × {0}  → 在点处 Ctrl+V",
            deletedCount: "已删除 × {0}  (Ctrl+Z 撤销)",
            copyDelFail: "{0}  (删除失败: {1})",
            savedKeyAt: "已保存 {0} @ {1},{2} → 第 {3} 行",
            selectPack: "请先选择 HellGate 包对象（原版不可编辑/删除）。",
            deletedLine: "已删除包行 {0}{1}  (Ctrl+Z 撤销)",
            regionEmpty: "框选为空。",
            delRegionFail: "框选删除失败。",
            needPoint: "请先在地图上点击（LMB）。",
            noEnemy: "尚未确认敌人。",
            noTrap: "尚未确认陷阱。",
            noGold: "没有金币数量。",
            noFolder: "没有 EventTrap 文件夹。",
            camMissing: "未找到镜头。",
            camSize: "无法读取镜头基准尺寸。",
            overviewOn: "俯视开 — WASD / 中键拖动镜头，滚轮缩放。再按 V = Default。",
            viewDefault: "俯视 Default（标准）。");
    }

    private static void ApplyKr(Dictionary<string, string> d)
    {
        ApplyEastAsian(d,
            coordsTitle: "배치",
            point: "포인트",
            pack: "팩",
            pending: "대기",
            clipboard: "복사됨",
            selected: "선택",
            line: "줄",
            hotkeys: "LMB=포인트 · LMB드래그=영역 · Ctrl+C/X/V/D · 화살표 · Shift+화살표 · E H T R G N M C F B V U · Home · Space · RMB reload · F12 · F1 · Q",
            dragging: "드래그 중…",
            clickReady: "스폰 포인트 준비됨",
            clickUpdated: "좌표가 갱신됨",
            marker: "SPAWN",
            copied: "복사됨",
            cut: "잘라냄",
            pasted: "붙여넣음",
            duplicated: "복제됨",
            deleted: "삭제됨",
            shotOk: "스크린샷 저장됨",
            shotFail: "스크린샷 실패",
            btnShot: "샷 [F12]",
            btnHelp: "도움말 [F1]",
            enemies: "적 [E]",
            enemiesOpen: "적 ▲ [E]",
            hostage: "인질 [H]",
            hostageOpen: "인질 ▲ [H]",
            hostageTitle: "인질 & OtherScenes",
            helpHostage: "인질, 이어서 Spine 연출(Look_Dorei, gob_look, …). |faction= 은 구출 후 전투원에게 상속. Random 은 RANDOM_HOSTAGE.",
            trap: "함정 [T]",
            trapOpen: "함정 ▲ [T]",
            lethal: "치명 [R]",
            lethalOpen: "치명 ▲ [R]",
            decor: "장식 [N]",
            decorOpen: "장식 ▲ [N]",
            gold: "골드 [G]",
            goldOpen: "골드 ▲ [G]",
            goldTitle: "골드 더미",
            helpGold: "골드 더미. 고정 또는 범위. Random 은 RANDOM,chance,X,Y,gold=…",
            helpGoldOpt: "Random = RANDOM,chance. 고정 = GOLD,X,Y,amount,1.",
            etTitle: "EventTrap 앵커",
            helpEt: "EVENTTRAP,folder,X,Y 와 선택 count/dist/sides/faction/max/r/delay. 빈 칸은 pack config.json. F11 은 라벨 기즈모(플레이에서는 숨김).",
            helpEtGizmo: "편집 전용 매복/증원 앵커. 게임에서는 안 보임. XY 드래그 또는 입력 후 위치 사용 / 저장.",
            helpEtOpt: "빈 칸 = 팩 기본값. 거리는 넉다운 시점 플레이어 기준(앵커 아님). 진영은 매복 적에게 적용. Sides: pack / both / right-only.",
            ecNone: "없음",
            ecAttach: "이벤트 p=",
            helpEc: "대화 NPC: TouzokuNormal|ec_event=<id>. Broker / FSP bandits 는 항상 TouzokuNormal. 포인트 후 배치. 배치된 NPC 를 LMB 로 편집.",
            helpEcOpt: "Random = NPC 등장 확률 (RANDOM,chance). 이벤트 p= = |ec_chance=. NPC 는 항상 스폰. 1 = 대화 항상 부착. 진영 기본값은 이벤트 조우.",
            helpPrevEc: "연결된 NPC 프리팹 미리보기. 미리보기/목록에서 맵으로 드래그하면 배치+저장.",
            count: "수",
            dist: "거리",
            max: "최대",
            zone: "범위",
            delay: "지연",
            sidesPack: "방향: pack",
            sidesBoth: "방향: 양쪽",
            sidesRight: "방향: 오른쪽만",
            decorTitle: "장식",
            spine: "— Spine 애니메이션 —",
            helpDecor: "상자, 통, 시체, 소품. Spine 연출은 인질 & OtherScenes. (?) = 아직 캐시 없음.",
            fav: "즐겨찾기 ★ [B]",
            favOpen: "즐겨찾기 ▲ [B]",
            btnDel: "삭제 [Del]",
            btnUndo: "실행 취소 [Ctrl+Z]",
            btnCopy: "복사 [Ctrl+C]",
            btnCut: "자르기 [Ctrl+X]",
            btnPaste: "붙여넣기 [Ctrl+V]",
            viewOff: "오버뷰 [V]",
            viewOn: "오버뷰 ON [V]",
            confirm: "배치  [Space]",
            confirmFull: "배치하고 저장  [Space]",
            addFav: "★ 즐겨찾기에 [Ctrl+B]",
            favDel: "삭",
            btnSave: "저장  [Ctrl+S]",
            btnSaved: "✓ 저장됨",
            closeEdit: "닫기",
            usePos: "위치 사용 [U]",
            reset: "초기화",
            barUndo: "취소",
            barSave: "저장",
            barFlip: "반전 [F]",
            barFlipOn: "반전 ON [F]",
            barCopy: "복사",
            barCut: "자르기",
            barPaste: "붙여넣기",
            barDelete: "삭제",
            barPlace: "배치 [␣]",
            trapsTitle: "함정",
            lethalTitle: "치명 함정",
            helpTraps: "키 선택, 반전/회전/정렬, 포인트 클릭, 배치. 배치된 함정 LMB = 편집/삭제. (?) = 미캐시.",
            helpLethal: "치명 함정(Gore). 반전/정렬만(회전 없음). Gore Content 필요.",
            flipOn: "왼쪽 [F]",
            flipOff: "오른쪽 [F]",
            ready: "준비",
            enemiesTitle: "적",
            favTitle: "즐겨찾기",
            favEmpty: "비어 있음 — 카탈로그에서 키를 고른 뒤 ★.",
            favAdd: "★ 현재 추가",
            factionTitle: "진영",
            elite: "엘리트",
            flip: "왼쪽 반전",
            rot: "회전",
            degrees: "도",
            apply: "적용",
            sort: "정렬",
            random: "랜덤 등장",
            options: "옵션",
            preview: "미리보기",
            pickEnemy: "적을 고르세요",
            pickTrap: "함정을 고르세요",
            noPrefab: "프리팹 없음",
            noMesh: "메시 없음",
            unavailable: "사용 불가",
            na: "[불가]",
            enemyNa: "이 적은 편집 대상이 아닙니다.",
            helpTitle: "도움말",
            guideTitle: "조작",
            cam: "카메라 / 이동",
            camBody: "WASD — 카메라 이동\nMMB를 누른 채 드래그 — 마우스로 카메라 이동\n휠 — 줌(오버뷰 ON)\nV — 오버뷰 / Default\nHome — 플레이어로(오버뷰는 유지, 줌은 되돌림)",
            mouse: "마우스",
            mouseBody: "빈 곳을 LMB — 포인트 지정\n빈 곳을 드래그 — 영역 선택\n스프라이트를 LMB — 선택 / 편집(정밀, Alt면 더 넓게)\n물체를 LMB로 누른 채 — 이동\n화살표 — 선택 미세 이동(Shift+화살표 ×5)\n미리보기나 목록에서 맵으로 드래그 — 배치하고 저장\nRMB — 팩 저장 후 씬 다시 읽기",
            catalogs: "카탈로그",
            catalogsBody: "E — 적\nH — 인질\nT — 함정\nR — 치명 함정\nG — 골드\nN — 장식\nM — EventTrap\nC — EventCore\nB — 즐겨찾기\nF — 반전\nSpace / Enter — 배치\nU — 위치 사용\nQ — 패널(및 이 도움말) 닫기",
            editClip: "편집과 클립보드",
            editBody: "Ctrl+C — 복사\nCtrl+X — 자르기\nCtrl+V — 포인트에 붙여넣기\nCtrl+D — 복제\nCtrl+Z — 실행 취소\nCtrl+S — 편집 저장\nCtrl+B — 즐겨찾기에 추가\nDel — 삭제(영역 안을 모두 삭제)\nF12 — 스크린샷\nF1 — 이 도움말\nEsc — 도움말, 영역, 편집, 선택 순으로 닫기",
            footer: "패널 ? 에 추가 팁.",
            helpCoords: "빈 곳 LMB = 포인트. 빈 곳 드래그 = 영역(Ctrl+C/X, Ctrl+V 로 포인트에 붙여넣기). 스프라이트 LMB = 선택/편집(Alt면 더 넓게). 물체를 누른 채 = 이동. MMB / WASD = 카메라 이동. 휠 = 줌. 화살표 미세 이동(Shift+화살표 ×5). Home = 플레이어. F1 도움말. F12 샷. − 이 패널 접기.",
            helpFaction: "팩 줄에 쓰는 진영. 문장은 HellGate 아이콘. < > 순환. 표시 이름만. 팩은 기술 토큰.",
            helpOpt: "엘리트 = ForceElite. 반전 = −X. Random = RANDOM,chance.",
            helpTrapOpt: "반전=수평. 회전: +30° [R] 또는 각도+적용. 정렬 = sort±N (SortingOrder, near/far 아님).",
            helpHostOpt: "반전=수평. 정렬 = sort±N. Random 은 RANDOM_HOSTAGE. 인질과 OtherScenes 는 회전 없음.",
            helpLetOpt: "반전=수평. 정렬 = sort±N. 치명 함정은 회전 없음.",
            helpPrev: "선택한 적 미리보기. 미리보기/목록에서 맵으로 드래그하면 배치+저장.",
            helpPrevTrap: "함정 템플릿. 드래그로 배치+저장. (?) = 해당 함정이 있는 맵을 먼저 방문.",
            dragPlace: "미리보기 또는 목록에서 맵으로 드래그",
            dragRelease: "놓으면 배치+저장",
            facNone: "없음",
            facBandits: "산적",
            facInq: "산적(인퀴지션)",
            facMafia: "산적(마피아)",
            facDemons: "산적(악마)",
            facChurch: "교회",
            facDemons2: "악마",
            facMafia2: "마피아",
            facUndead: "언데드",
            facMonsters: "몬스터",
            facWitch: "마녀",
            facEvent: "이벤트 조우",
            editTitle: "선택됨",
            editKey: "키",
            editFaction: "진영",
            editRandom: "랜덤 스폰",
            editChance: "확률",
            editVanilla: "팩 링크 없음 — 편집 불가.",
            panelsClosed: "패널 닫힘 [Q]",
            pickKey: "먼저 키를 고르세요.",
            selectEnemy: "먼저 적을 선택(호버 + LMB).",
            editDisabled: "팩 링크 없음 — 편집 불가.",
            pasteNeed: "맵을 클릭한 뒤 Ctrl+V.",
            camHome: "카메라 → 플레이어",
            camHomeFail: "플레이어를 찾을 수 없음.",
            badSort: "정렬 값이 잘못됨.",
            badRot: "회전 각도가 잘못됨.",
            done: "완료.",
            noLink: "팩 링크 없음.",
            noLinkXy: "팩 링크 없음 — XY 미저장.",
            keyReq: "키가 필요함.",
            badXy: "X/Y 가 잘못됨 (12.34 형식).",
            goldReq: "골드 양이 필요함.",
            delFail: "삭제 실패.",
            cutFail: "자르기 실패.",
            copyFail: "복사 실패.",
            pasteFail: "붙여넣기 실패.",
            undoEmpty: "취소할 작업 없음.",
            savedXy: "XY 를 팩에 저장",
            cutCount: "잘라냄 × {0}  → 포인트에서 Ctrl+V",
            deletedCount: "삭제 × {0}  (Ctrl+Z 취소)",
            copyDelFail: "{0}  (삭제 실패: {1})",
            savedKeyAt: "저장 {0} @ {1},{2} → 줄 {3}",
            selectPack: "먼저 HellGate 팩 오브젝트를 선택(바닐라는 편집/삭제 불가).",
            deletedLine: "팩 줄 {0}{1} 삭제  (Ctrl+Z 취소)",
            regionEmpty: "영역이 비어 있음.",
            delRegionFail: "영역 삭제 실패.",
            needPoint: "먼저 맵을 클릭(LMB).",
            noEnemy: "확정된 적이 없음.",
            noTrap: "확정된 함정이 없음.",
            noGold: "골드 양이 없음.",
            noFolder: "EventTrap 폴더가 없음.",
            camMissing: "카메라를 찾을 수 없음.",
            camSize: "카메라 기준 크기를 읽을 수 없음.",
            overviewOn: "오버뷰 ON — WASD / MMB 드래그로 카메라 이동, 휠 줌. V 다시 = Default.",
            viewDefault: "오버뷰 Default (표준).");
    }

    private static void ApplyEastAsian(
        Dictionary<string, string> d,
        string coordsTitle, string point, string pack, string pending, string clipboard, string selected, string line,
        string hotkeys, string dragging, string clickReady, string clickUpdated, string marker,
        string copied, string cut, string pasted, string duplicated, string deleted, string shotOk, string shotFail,
        string btnShot, string btnHelp,
        string enemies, string enemiesOpen, string hostage, string hostageOpen, string hostageTitle, string helpHostage,
        string trap, string trapOpen, string lethal, string lethalOpen, string decor, string decorOpen,
        string gold, string goldOpen, string goldTitle, string helpGold, string helpGoldOpt,
        string etTitle, string helpEt, string helpEtGizmo, string helpEtOpt,
        string ecNone, string ecAttach, string helpEc, string helpEcOpt, string helpPrevEc,
        string count, string dist, string max, string zone, string delay,
        string sidesPack, string sidesBoth, string sidesRight,
        string decorTitle, string spine, string helpDecor,
        string fav, string favOpen, string btnDel, string btnUndo, string btnCopy, string btnCut, string btnPaste,
        string viewOff, string viewOn, string confirm, string confirmFull, string addFav, string favDel,
        string btnSave, string btnSaved, string closeEdit, string usePos, string reset,
        string barUndo, string barSave, string barFlip, string barFlipOn, string barCopy, string barCut, string barPaste,
        string barDelete, string barPlace, string trapsTitle, string lethalTitle, string helpTraps, string helpLethal,
        string flipOn, string flipOff, string ready, string enemiesTitle, string favTitle, string favEmpty, string favAdd,
        string factionTitle, string elite, string flip, string rot, string degrees, string apply, string sort, string random,
        string options, string preview, string pickEnemy, string pickTrap, string noPrefab, string noMesh, string unavailable,
        string na, string enemyNa, string helpTitle, string guideTitle, string cam, string camBody, string mouse, string mouseBody,
        string catalogs, string catalogsBody, string editClip, string editBody, string footer, string helpCoords,
        string helpFaction, string helpOpt, string helpTrapOpt, string helpHostOpt, string helpLetOpt, string helpPrev,
        string helpPrevTrap, string dragPlace, string dragRelease,
        string facNone, string facBandits, string facInq, string facMafia, string facDemons, string facChurch,
        string facDemons2, string facMafia2, string facUndead, string facMonsters, string facWitch, string facEvent,
        string editTitle, string editKey, string editFaction, string editRandom, string editChance, string editVanilla,
        string panelsClosed, string pickKey, string selectEnemy, string editDisabled, string pasteNeed,
        string camHome, string camHomeFail, string badSort, string badRot, string done, string noLink, string noLinkXy,
        string keyReq, string badXy, string goldReq, string delFail, string cutFail, string copyFail, string pasteFail,
        string undoEmpty, string savedXy, string cutCount, string deletedCount, string copyDelFail, string savedKeyAt,
        string selectPack, string deletedLine, string regionEmpty, string delRegionFail, string needPoint,
        string noEnemy, string noTrap, string noGold, string noFolder, string camMissing, string camSize,
        string overviewOn, string viewDefault)
    {
        d["coords.title"] = coordsTitle;
        d["coords.lastLmb"] = point;
        d["coords.activePack"] = pack;
        d["coords.pack"] = pack;
        d["coords.pending"] = pending;
        d["coords.clipboard"] = clipboard;
        d["coords.selected"] = selected;
        d["coords.line"] = line;
        d["coords.hotkeys"] = hotkeys;
        d["status.dragging"] = dragging;
        d["click.ready"] = clickReady;
        d["click.updated"] = clickUpdated;
        d["click.marker"] = marker;
        d["toast.copied"] = copied;
        d["toast.cut"] = cut;
        d["toast.pasted"] = pasted;
        d["toast.duplicated"] = duplicated;
        d["toast.deleted"] = deleted;
        d["toast.screenshot"] = shotOk;
        d["toast.screenshotFail"] = shotFail;
        d["btn.screenshot"] = btnShot;
        d["btn.help"] = btnHelp;
        d["btn.enemies"] = enemies;
        d["btn.enemiesOpen"] = enemiesOpen;
        d["btn.hostage"] = hostage;
        d["btn.hostageOpen"] = hostageOpen;
        d["hostage.title"] = hostageTitle;
        d["help.hostage"] = helpHostage;
        d["btn.trap"] = trap;
        d["btn.trapOpen"] = trapOpen;
        d["btn.lethalTrap"] = lethal;
        d["btn.lethalTrapOpen"] = lethalOpen;
        d["btn.decor"] = decor;
        d["btn.decorOpen"] = decorOpen;
        d["btn.gold"] = gold;
        d["btn.goldOpen"] = goldOpen;
        d["gold.title"] = goldTitle;
        d["help.gold"] = helpGold;
        d["help.goldOptions"] = helpGoldOpt;
        d["eventTrap.title"] = etTitle;
        d["help.eventTrap"] = helpEt;
        d["help.eventTrapGizmo"] = helpEtGizmo;
        d["help.eventTrapOptions"] = helpEtOpt;
        d["eventcore.none"] = ecNone;
        d["eventcore.attach"] = ecAttach;
        d["help.eventCore"] = helpEc;
        d["help.eventCoreOptions"] = helpEcOpt;
        d["help.previewEventCore"] = helpPrevEc;
        d["etrap.count"] = count;
        d["etrap.dist"] = dist;
        d["etrap.max"] = max;
        d["etrap.zone"] = zone;
        d["etrap.delay"] = delay;
        d["etrap.sidesPack"] = sidesPack;
        d["etrap.sidesBoth"] = sidesBoth;
        d["etrap.sidesRight"] = sidesRight;
        d["decor.title"] = decorTitle;
        d["spine.title"] = spine;
        d["help.decor"] = helpDecor;
        d["btn.favorites"] = fav;
        d["btn.favoritesOpen"] = favOpen;
        d["btn.delete"] = btnDel;
        d["btn.undo"] = btnUndo;
        d["btn.copy"] = btnCopy;
        d["btn.cut"] = btnCut;
        d["btn.paste"] = btnPaste;
        d["btn.viewModOff"] = viewOff;
        d["btn.viewModOn"] = viewOn;
        d["btn.confirm"] = confirm;
        d["btn.confirmFull"] = confirmFull;
        d["btn.addFavorite"] = addFav;
        d["btn.favDelete"] = favDel;
        d["btn.save"] = btnSave;
        d["btn.saved"] = btnSaved;
        d["btn.closeEdit"] = closeEdit;
        d["btn.useCurrentPos"] = usePos;
        d["btn.default"] = reset;
        d["bar.undo"] = barUndo;
        d["bar.save"] = barSave;
        d["bar.flip"] = barFlip;
        d["bar.flipOn"] = barFlipOn;
        d["bar.copy"] = barCopy;
        d["bar.cut"] = barCut;
        d["bar.paste"] = barPaste;
        d["bar.delete"] = barDelete;
        d["bar.place"] = barPlace;
        d["traps.title"] = trapsTitle;
        d["lethal.title"] = lethalTitle;
        d["help.traps"] = helpTraps;
        d["help.lethal"] = helpLethal;
        d["status.flipOn"] = flipOn;
        d["status.flipOff"] = flipOff;
        d["ready.title"] = ready;
        d["enemies.title"] = enemiesTitle;
        d["favorites.title"] = favTitle;
        d["favorites.empty"] = favEmpty;
        d["favorites.addHere"] = favAdd;
        d["faction.title"] = factionTitle;
        d["elite"] = elite;
        d["flip"] = flip;
        d["rot"] = rot;
        d["rot.degrees"] = degrees;
        d["rot.apply"] = apply;
        d["sort"] = sort;
        d["random"] = random;
        d["options.title"] = options;
        d["preview.title"] = preview;
        d["preview.empty"] = pickEnemy;
        d["preview.emptyTrap"] = pickTrap;
        d["preview.missing"] = noPrefab;
        d["preview.blank"] = noMesh;
        d["preview.unavailable"] = unavailable;
        d["enemies.na"] = na;
        d["status.enemyUnavailable"] = enemyNa;
        d["help.title"] = helpTitle;
        d["help.guide.title"] = guideTitle;
        d["help.guide.camera"] = cam;
        d["help.guide.camera.body"] = camBody;
        d["help.guide.mouse"] = mouse;
        d["help.guide.mouse.body"] = mouseBody;
        d["help.guide.catalogs"] = catalogs;
        d["help.guide.catalogs.body"] = catalogsBody;
        d["help.guide.edit"] = editClip;
        d["help.guide.edit.body"] = editBody;
        d["help.guide.footer"] = footer;
        d["help.coords"] = helpCoords;
        d["help.faction"] = helpFaction;
        d["help.options"] = helpOpt;
        d["help.trapOptions"] = helpTrapOpt;
        d["help.hostageOptions"] = helpHostOpt;
        d["help.lethalOptions"] = helpLetOpt;
        d["help.preview"] = helpPrev;
        d["help.previewTrap"] = helpPrevTrap;
        d["preview.dragPlace"] = dragPlace;
        d["preview.dragRelease"] = dragRelease;
        d["faction.none"] = facNone;
        d["faction.bandits"] = facBandits;
        d["faction.banditsInq"] = facInq;
        d["faction.banditsMafia"] = facMafia;
        d["faction.banditsDemons"] = facDemons;
        d["faction.church"] = facChurch;
        d["faction.demons"] = facDemons2;
        d["faction.mafia"] = facMafia2;
        d["faction.undead"] = facUndead;
        d["faction.monsters"] = facMonsters;
        d["faction.witch"] = facWitch;
        d["faction.event"] = facEvent;
        d["edit.title"] = editTitle;
        d["edit.key"] = editKey;
        d["edit.faction"] = editFaction;
        d["edit.random"] = editRandom;
        d["edit.chance"] = editChance;
        d["edit.vanilla"] = editVanilla;
        d["status.panelsClosed"] = panelsClosed;
        d["status.pickEnemy"] = pickKey;
        d["status.selectEnemy"] = selectEnemy;
        d["status.editDisabled"] = editDisabled;
        d["status.pasteNeedPos"] = pasteNeed;
        d["status.cameraHome"] = camHome;
        d["status.cameraHomeFail"] = camHomeFail;
        d["status.badSort"] = badSort;
        d["status.badRot"] = badRot;
        d["status.done"] = done;
        d["status.noPackLink"] = noLink;
        d["status.noPackLinkXy"] = noLinkXy;
        d["status.keyRequired"] = keyReq;
        d["status.badXy"] = badXy;
        d["status.goldRequired"] = goldReq;
        d["status.deleteFailed"] = delFail;
        d["status.cutFailed"] = cutFail;
        d["status.copyFailed"] = copyFail;
        d["status.pasteFailed"] = pasteFail;
        d["status.undoEmpty"] = undoEmpty;
        d["status.savedXy"] = savedXy;
        d["status.cutCount"] = cutCount;
        d["status.deletedCount"] = deletedCount;
        d["status.copyButDeleteFailed"] = copyDelFail;
        d["status.savedKeyAt"] = savedKeyAt;
        d["status.selectPackSpawn"] = selectPack;
        d["status.deletedLine"] = deletedLine;
        d["status.regionEmpty"] = regionEmpty;
        d["status.deleteRegionFailed"] = delRegionFail;
        d["status.needPoint"] = needPoint;
        d["status.noConfirmedEnemy"] = noEnemy;
        d["status.noConfirmedTrap"] = noTrap;
        d["status.noGold"] = noGold;
        d["status.noEventTrapFolder"] = noFolder;
        d["status.cameraMissing"] = camMissing;
        d["status.cameraSizeFail"] = camSize;
        d["status.overviewOn"] = overviewOn;
        d["status.viewModDefault"] = viewDefault;
    }
}
