using System.Collections.Generic;

namespace NoREroMod.Systems.UI;

/// <summary>Localized splash labels (supporter header + Options menu).</summary>
internal static class SplashScreenUILabels
{
    private static readonly Dictionary<string, string> SupportersHeaderByLanguage = new Dictionary<string, string>
    {
        { "EN", "With Deepest Gratitude to My Supporters" },
        { "RU", "С глубокой признательностью моим спонсорам" },
        { "JP", "支援者の皆様へ、心からの感謝を" },
        { "CN", "致我的支持者——最深切的谢意" },
        { "KR", "지지해 주신 분들께 깊은 감사를 드립니다" },
        { "FR", "Ma profonde gratitude à mes supporters" },
        { "DE", "Meine tiefste Dankbarkeit an meine Unterstützer" },
        { "PT", "A minha mais profunda gratidão aos meus apoiantes" },
        { "BR", "Minha mais profunda gratidão aos meus apoiadores" },
        { "ES", "Mi más profunda gratitud a mis seguidores" },
    };

    private static readonly Dictionary<string, string> OptionsLabelByLanguage = new Dictionary<string, string>
    {
        { "EN", "Options" },
        { "RU", "Опции" },
        { "JP", "オプション" },
        { "CN", "选项" },
        { "KR", "옵션" },
        { "FR", "Options" },
        { "DE", "Optionen" },
        { "PT", "Opções" },
        { "BR", "Opções" },
        { "ES", "Opciones" },
    };

    private static readonly Dictionary<string, string> SimpleQteLabelByLanguage = new Dictionary<string, string>
    {
        { "EN", "Simple QTE" },
        { "RU", "Простой QTE" },
        { "JP", "シンプルQTE" },
        { "CN", "简易QTE" },
        { "KR", "심플 QTE" },
        { "FR", "QTE simple" },
        { "DE", "Einfaches QTE" },
        { "PT", "QTE simples" },
        { "BR", "QTE simples" },
        { "ES", "QTE simple" },
    };

    private static readonly Dictionary<string, string> DifficultyLabelByLanguage = new Dictionary<string, string>
    {
        { "EN", "Difficulty" },
        { "RU", "Сложность" },
        { "JP", "難易度" },
        { "CN", "难度" },
        { "KR", "난이도" },
        { "FR", "Difficulté" },
        { "DE", "Schwierigkeit" },
        { "PT", "Dificuldade" },
        { "BR", "Dificuldade" },
        { "ES", "Dificultad" },
    };

    private static readonly Dictionary<string, string> DifficultyRestartByLanguage = new Dictionary<string, string>
    {
        { "EN", "Restart the game to apply the difficulty preset." },
        { "RU", "Перезапустите игру, чтобы применить пресет сложности." },
        { "JP", "難易度プリセットを適用するにはゲームを再起動してください。" },
        { "CN", "请重启游戏以应用难度预设。" },
        { "KR", "난이도 프리셋을 적용하려면 게임을 재시작하세요." },
        { "FR", "Redémarrez le jeu pour appliquer le préréglage de difficulté." },
        { "DE", "Starte das Spiel neu, um das Schwierigkeits-Preset zu übernehmen." },
        { "PT", "Reinicie o jogo para aplicar o preset de dificuldade." },
        { "BR", "Reinicie o jogo para aplicar o preset de dificuldade." },
        { "ES", "Reinicia el juego para aplicar el preset de dificultad." },
    };

    private static readonly Dictionary<string, string> DoneLabelByLanguage = new Dictionary<string, string>
    {
        { "EN", "Done" },
        { "RU", "Готово" },
        { "JP", "完了" },
        { "CN", "完成" },
        { "KR", "완료" },
        { "FR", "OK" },
        { "DE", "Fertig" },
        { "PT", "Concluído" },
        { "BR", "Concluído" },
        { "ES", "Listo" },
    };

    private static readonly Dictionary<string, string> ExitLabelByLanguage = new Dictionary<string, string>
    {
        { "EN", "Exit" },
        { "RU", "Выход" },
        { "JP", "終了" },
        { "CN", "退出" },
        { "KR", "종료" },
        { "FR", "Quitter" },
        { "DE", "Beenden" },
        { "PT", "Sair" },
        { "BR", "Sair" },
        { "ES", "Salir" },
    };

    private static readonly Dictionary<string, string> LanguageLabelByLanguage = new Dictionary<string, string>
    {
        { "EN", "Language" },
        { "RU", "Язык" },
        { "JP", "言語" },
        { "CN", "语言" },
        { "KR", "언어" },
        { "FR", "Langue" },
        { "DE", "Sprache" },
        { "PT", "Idioma" },
        { "BR", "Idioma" },
        { "ES", "Idioma" },
    };

    private static readonly Dictionary<string, string> CancelLabelByLanguage = new Dictionary<string, string>
    {
        { "EN", "Cancel" },
        { "RU", "Отмена" },
        { "JP", "キャンセル" },
        { "CN", "取消" },
        { "KR", "취소" },
        { "FR", "Annuler" },
        { "DE", "Abbrechen" },
        { "PT", "Cancelar" },
        { "BR", "Cancelar" },
        { "ES", "Cancelar" },
    };

    private static readonly Dictionary<string, string> LanguageRestartByLanguage = new Dictionary<string, string>
    {
        { "EN", "Restart the game after selecting a language." },
        { "RU", "Перезапустите игру после выбора языка." },
        { "JP", "言語を選んだらゲームを再起動してください。" },
        { "CN", "选择语言后请重新启动游戏。" },
        { "KR", "언어를 선택한 후 게임을 재시작해 주세요." },
        { "FR", "Redémarrez le jeu après avoir choisi une langue." },
        { "DE", "Starte das Spiel nach der Sprachwahl neu." },
        { "PT", "Reinicie o jogo após selecionar um idioma." },
        { "BR", "Reinicie o jogo após selecionar um idioma." },
        { "ES", "Reinicia el juego después de elegir un idioma." },
    };

    /// <summary>Native display names for the language grid (stable across UI locales).</summary>
    private static readonly Dictionary<string, string> LanguageDisplayNames = new Dictionary<string, string>
    {
        { "EN", "English" },
        { "JP", "日本語" },
        { "RU", "Русский" },
        { "CN", "中文" },
        { "KR", "한국어" },
        { "FR", "Français" },
        { "DE", "Deutsch" },
        { "ES", "Español" },
        { "PT", "Português" },
        { "BR", "Português (BR)" },
    };

    /// <summary>Grid order for Options language submenu (2 columns).</summary>
    internal static readonly string[] LanguageCodesInGridOrder =
    {
        "EN", "JP",
        "RU", "CN",
        "KR", "FR",
        "DE", "ES",
        "PT", "BR",
    };

    internal static string GetSupportersHeader(string languageCode)
    {
        if (SupportersHeaderByLanguage.TryGetValue(languageCode, out string header))
            return header;
        return SupportersHeaderByLanguage["EN"];
    }

    internal static string GetOptionsLabel(string languageCode)
    {
        if (OptionsLabelByLanguage.TryGetValue(languageCode, out string label))
            return label;
        return OptionsLabelByLanguage["EN"];
    }

    internal static string GetSimpleQteLabel(string languageCode)
    {
        if (SimpleQteLabelByLanguage.TryGetValue(languageCode, out string label))
            return label;
        return SimpleQteLabelByLanguage["EN"];
    }

    internal static string GetDifficultyLabel(string languageCode)
    {
        if (DifficultyLabelByLanguage.TryGetValue(languageCode, out string label))
            return label;
        return DifficultyLabelByLanguage["EN"];
    }

    internal static string GetDifficultyRestartNotice(string languageCode)
    {
        if (DifficultyRestartByLanguage.TryGetValue(languageCode, out string notice))
            return notice;
        return DifficultyRestartByLanguage["EN"];
    }

    internal static string GetDoneLabel(string languageCode)
    {
        if (DoneLabelByLanguage.TryGetValue(languageCode, out string label))
            return label;
        return DoneLabelByLanguage["EN"];
    }

    internal static string GetExitLabel(string languageCode)
    {
        if (ExitLabelByLanguage.TryGetValue(languageCode, out string label))
            return label;
        return ExitLabelByLanguage["EN"];
    }

    internal static string GetLanguageLabel(string languageCode)
    {
        if (LanguageLabelByLanguage.TryGetValue(languageCode, out string label))
            return label;
        return LanguageLabelByLanguage["EN"];
    }

    internal static string GetCancelLabel(string languageCode)
    {
        if (CancelLabelByLanguage.TryGetValue(languageCode, out string label))
            return label;
        return CancelLabelByLanguage["EN"];
    }

    internal static string GetLanguageRestartNotice(string languageCode)
    {
        if (LanguageRestartByLanguage.TryGetValue(languageCode, out string notice))
            return notice;
        return LanguageRestartByLanguage["EN"];
    }

    internal static string GetLanguageDisplayName(string languageCode)
    {
        if (LanguageDisplayNames.TryGetValue(languageCode, out string name))
            return name;
        return languageCode;
    }
}
