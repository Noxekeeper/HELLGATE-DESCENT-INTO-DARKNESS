using System.Collections.Generic;

namespace NoREroMod.Systems.UI;

/// <summary>Localized supporter-block header only (all other splash labels stay English).</summary>
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

    internal static string GetSupportersHeader(string languageCode)
    {
        if (SupportersHeaderByLanguage.TryGetValue(languageCode, out string header))
            return header;
        return SupportersHeaderByLanguage["EN"];
    }
}
