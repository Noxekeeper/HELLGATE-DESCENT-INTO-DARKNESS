# FSP — бандиты (RU)

**Карта папок:** `docs/EventCore_FolderLayout_RU.md`

## Один event = одна папка + свой `*_lang.json`

| Папка | Манифест | Пулы (случайные реплики) | Шаги (кнопки, npcLine) |
|-------|----------|--------------------------|------------------------|
| `sex_paid/` | `eventcore_fsp_bandits_sex_paid.json` | `sex_paid/eventcore_fsp_bandits_sex_paid_lang.json` | `sex_paid/eventcore_fsp_bandits_sex_paid_*.json` |
| `buyher/` | `eventcore_fsp_bandits_buyher.json` | `buyher/eventcore_fsp_bandits_buyher_lang.json` | `buyher/*.json` |
| `sharegold/` | … | `sharegold/*_lang.json` | `sharegold/*.json` |
| `sex_friend/` | … | `sex_friend/*_lang.json` | `sex_friend/*.json` |
| `chat_*` | … | `chat_*/eventcore_fsp_bandits_chat_*_lang.json` | `chat_*/*.json` |

**Общие кнопки игрока:** `../fsp_choice_shared_lang.json` (все FSP-события).

**Не использовать** `eventcore_fsp_bandits_lang.json` в корне `bandits/` — устаревший монолит (бэкап `.bak`).

## sex_paid — что править

1. **Пулы** — только `sex_paid/eventcore_fsp_bandits_sex_paid_lang.json` (prelude, open, react, after_yes, close, rage…).
2. **Шаги** — файлы в `sex_paid/` (например `refuse_c02.json` → поле `npcLine`).
3. **Authoring** — `docs/Factions/authoring/FSP_sex_paid_text_RU.txt`.

Удалённые неиспользуемые пулы sex_paid: `after_refuse`, `after_no_money` (в игре не подключены).

## Загрузка

При старте мод подмешивает все `FactionSocial/bandits/**/*_lang.json` + `fsp_choice_shared_lang.json` (`EventCoreStringRegistry`).
