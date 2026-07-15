using System;

namespace NoREroMod.Systems.BadEndPlayer;

/// <summary>
/// Manifest for BadEnd Player prototype. Loaded from manifest.json in BadEndPlayer_Proto folder.
/// </summary>
[Serializable]
internal sealed class BadEndPlayerManifest
{
    public string id;
    public string title;
    public float autoPlayDelay = 5f;
    /// <summary>Diary title shown before slideshow (e.g. "Raul's Diary").</summary>
    public string diaryTitle;
    /// <summary>Diary intro paragraph (from Raul's POV, etc.).</summary>
    public string diaryIntro;
    /// <summary>Optional background audio file name in Proto folder (e.g. "bgm.ogg", "ambient.mp3"). Plays via Unity AudioSource (not MasterAudio).</summary>
    public string backgroundAudio;
    /// <summary>Optional credits shown under "The End" (e.g. "Text by: X. Illustrations by: Y (AI-generated).").</summary>
    public string credits;
    /// <summary>Wrapper for Unity JsonUtility (nested array workaround).</summary>
    public BadEndPlayerScenesWrapper scenesWrapper;
    /// <summary>Root-level array (Unity 5.6 may fill this).</summary>
    public BadEndPlayerScene[] scenes;
}

[Serializable]
internal sealed class BadEndPlayerScenesWrapper
{
    public BadEndPlayerScene[] scenes;
}

/// <summary>Fallback: parse only root-level "scenes" array when main manifest.scenes is null.</summary>
[Serializable]
internal sealed class BadEndPlayerManifestScenesOnly
{
    public BadEndPlayerScene[] scenes;
}

[Serializable]
internal sealed class BadEndPlayerScene
{
    public int id;
    public string file;
    public float duration = 5f;
    public string text;
}
