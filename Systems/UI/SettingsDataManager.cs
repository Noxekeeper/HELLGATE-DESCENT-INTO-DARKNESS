using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NoREroMod.Systems.UI
{
    /// <summary>
    /// Менеджер данных настроек - загрузка и парсинг JSON with описаниями
    /// </summary>
    public static class SettingsDataManager
    {
        /// <summary>
        /// Структура for описания настройки
        /// </summary>
        public class SettingDescription
        {
            public string description;
            public string defaultValue;
            public string minValue;
            public string maxValue;
            public string unit;
        }

        private static Dictionary<string, SettingDescription> _settingsDescriptions = new();

        /// <summary>
        /// Получить все описания настроек
        /// </summary>
        public static Dictionary<string, SettingDescription> GetSettingsDescriptions()
        {
            return _settingsDescriptions;
        }

        /// <summary>
        /// Получить описание настройки by ключу
        /// </summary>
        public static SettingDescription GetSettingDescription(string key)
        {
            return _settingsDescriptions.ContainsKey(key) ? _settingsDescriptions[key] : null;
        }

        /// <summary>
        /// Загрузить описания настроек from JSON файла
        /// </summary>
        public static void LoadSettingsDescriptions()
        {
            try
            {
                // Путь к JSON файлу - ищем in несколькtheir местах
                string pluginPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string jsonPath = null;

                // 1. Пробуем in BepInEx/plugins/HellGateJson (основное место)
                // pluginPath is typically .../BepInEx/plugins (next to game exe)
                // Target: .../BepInEx/plugins/HellGateJson
                if (pluginPath != null)
                {
                    string hellGateJsonPath = Path.Combine(pluginPath, "HellGateJson");
                    jsonPath = Path.Combine(hellGateJsonPath, "QTESettingsDescriptions.json");
                    if (File.Exists(jsonPath))
                    {
                        Plugin.Log?.LogInfo($"[SettingsDataManager] Found JSON at: {jsonPath}");
                    }
                }

                // 2. Если not найден, пробуем рядом with DLL плагина
                if (jsonPath == null || !File.Exists(jsonPath))
                {
                    jsonPath = Path.Combine(pluginPath, "QTESettingsDescriptions.json");
                }

                // 3. Если not найден, пробуем in подпапке Systems/UI рядом with DLL
                if (!File.Exists(jsonPath))
                {
                    string systemsUIPath = Path.Combine(pluginPath, "Systems");
                    systemsUIPath = Path.Combine(systemsUIPath, "UI");
                    jsonPath = Path.Combine(systemsUIPath, "QTESettingsDescriptions.json");
                }

                // 4. Если not найден, пробуем in корnot проекта (for разработки)
                if (!File.Exists(jsonPath))
                {
                    string projectRoot = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(pluginPath)));
                    if (projectRoot != null)
                    {
                        string rezervniePath = Path.Combine(projectRoot, "REZERVNIE COPY");
                        string noRHellGatePath = Path.Combine(rezervniePath, "NoRHellGate3");
                        string systemsPath = Path.Combine(noRHellGatePath, "Systems");
                        string uiPath = Path.Combine(systemsPath, "UI");
                        jsonPath = Path.Combine(uiPath, "QTESettingsDescriptions.json");
                    }
                }

                if (jsonPath == null || !File.Exists(jsonPath))
                {
                    Plugin.Log?.LogWarning($"[SettingsDataManager] Settings descriptions JSON file not found, tried multiple paths, using default descriptions");
                    return;
                }

                string jsonContent = File.ReadAllText(jsonPath, Encoding.UTF8);

                // Простой парсер JSON (without внешнtheir зависимостей)
                ParseSettingsDescriptionsJson(jsonContent);

                Plugin.Log?.LogInfo($"[SettingsDataManager] Loaded {_settingsDescriptions.Count} setting descriptions from JSON at {jsonPath}");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[SettingsDataManager] Failed to load settings descriptions: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Простой парсер JSON for описаний настроек
        /// </summary>
        private static void ParseSettingsDescriptionsJson(string jsonContent)
        {
            try
            {
                // Убираем пробелы и переносы строк for упрощения парсинга
                jsonContent = jsonContent.Replace("\r", "").Replace("\n", "").Replace("\t", "");

                // Find section "settings"
                int settingsStart = jsonContent.IndexOf("\"settings\"");
                if (settingsStart == -1)
                {
                    Plugin.Log?.LogWarning("[SettingsDataManager] JSON: 'settings' section not found");
                    return;
                }

                // Находим начало объекта settings
                int objectStart = jsonContent.IndexOf('{', settingsStart);
                if (objectStart == -1)
                {
                    Plugin.Log?.LogWarning("[SettingsDataManager] JSON: settings object start not found");
                    return;
                }

                // Парсим каждую настройку
                int pos = objectStart + 1;
                int parsedCount = 0;

                while (pos < jsonContent.Length)
                {
                    // Ищем название настройки
                    int keyStart = jsonContent.IndexOf('"', pos);
                    if (keyStart == -1 || keyStart >= jsonContent.Length - 1) break;

                    // Skip экранированные кавычки
                    while (keyStart > 0 && jsonContent[keyStart - 1] == '\\')
                    {
                        keyStart = jsonContent.IndexOf('"', keyStart + 1);
                        if (keyStart == -1) break;
                    }
                    if (keyStart == -1) break;

                    int keyEnd = jsonContent.IndexOf('"', keyStart + 1);
                    if (keyEnd == -1) break;

                    // Skip экранированные кавычки
                    while (keyEnd > keyStart + 1 && jsonContent[keyEnd - 1] == '\\')
                    {
                        keyEnd = jsonContent.IndexOf('"', keyEnd + 1);
                        if (keyEnd == -1) break;
                    }
                    if (keyEnd == -1) break;

                    string settingName = jsonContent.Substring(keyStart + 1, keyEnd - keyStart - 1);

                    // Ищем начало объекта настройки
                    int settingObjStart = jsonContent.IndexOf('{', keyEnd);
                    if (settingObjStart == -1) break;

                    // Парсим поля настройки
                    var settingDesc = ParseSettingObject(jsonContent, settingObjStart, settingName);
                    if (settingDesc != null)
                    {
                        _settingsDescriptions[settingName] = settingDesc;
                        parsedCount++;
                    }

                    // Ищем конец объекта настройки и переходим к следующей
                    int settingObjEnd = FindObjectEnd(jsonContent, settingObjStart);
                    if (settingObjEnd == -1) break;

                    pos = settingObjEnd + 1;

                    // Skip запятую, if exists
                    while (pos < jsonContent.Length && (char.IsWhiteSpace(jsonContent[pos]) || jsonContent[pos] == ','))
                    {
                        pos++;
                    }
                }

                Plugin.Log?.LogInfo($"[SettingsDataManager] Parsed {parsedCount} settings from JSON");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[SettingsDataManager] Failed to parse JSON: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Парсит объект настройки from JSON
        /// </summary>
        private static SettingDescription ParseSettingObject(string jsonContent, int objStart, string settingName)
        {
            var settingDesc = new SettingDescription();

            // Ищем "description"
            int descStart = jsonContent.IndexOf("\"description\"", objStart);
            if (descStart != -1)
            {
                settingDesc.description = ExtractJsonStringValue(jsonContent, descStart + 12);
            }

            // Ищем "defaultValue"
            int defaultStart = jsonContent.IndexOf("\"defaultValue\"", objStart);
            if (defaultStart != -1)
            {
                settingDesc.defaultValue = ExtractJsonStringValue(jsonContent, defaultStart + 13);
            }

            // Ищем "min"
            int minStart = jsonContent.IndexOf("\"min\"", objStart);
            if (minStart != -1)
            {
                settingDesc.minValue = ExtractJsonStringValue(jsonContent, minStart + 5);
            }

            // Ищем "max"
            int maxStart = jsonContent.IndexOf("\"max\"", objStart);
            if (maxStart != -1)
            {
                settingDesc.maxValue = ExtractJsonStringValue(jsonContent, maxStart + 5);
            }

            // Ищем "unit"
            int unitStart = jsonContent.IndexOf("\"unit\"", objStart);
            if (unitStart != -1)
            {
                settingDesc.unit = ExtractJsonStringValue(jsonContent, unitStart + 6);
            }

            return settingDesc;
        }

        /// <summary>
        /// Извлекает строковое значение from JSON
        /// </summary>
        private static string ExtractJsonStringValue(string jsonContent, int colonPos)
        {
            try
            {
                // Ищем двоеточие after названия поля
                int valueStart = jsonContent.IndexOf(':', colonPos);
                if (valueStart == -1) return "";

                // Skip пробелы after двоеточия
                valueStart++;
                while (valueStart < jsonContent.Length && char.IsWhiteSpace(jsonContent[valueStart]))
                {
                    valueStart++;
                }

                if (valueStart >= jsonContent.Length || jsonContent[valueStart] != '"') return "";

                int valueEnd = jsonContent.IndexOf('"', valueStart + 1);
                if (valueEnd == -1) return "";

                // Skip экранированные кавычки
                while (valueEnd > valueStart + 1 && jsonContent[valueEnd - 1] == '\\')
                {
                    valueEnd = jsonContent.IndexOf('"', valueEnd + 1);
                    if (valueEnd == -1) return "";
                }

                return jsonContent.Substring(valueStart + 1, valueEnd - valueStart - 1);
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Находит конец JSON объекта
        /// </summary>
        private static int FindObjectEnd(string jsonContent, int objStart)
        {
            int braceCount = 0;
            for (int i = objStart; i < jsonContent.Length; i++)
            {
                if (jsonContent[i] == '{') braceCount++;
                else if (jsonContent[i] == '}') braceCount--;

                if (braceCount == 0) return i;
            }
            return -1;
        }
    }
}
