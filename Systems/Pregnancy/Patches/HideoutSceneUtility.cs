using UnityEngine;
using UnityEngine.SceneManagement;

namespace NoREroMod.Systems.Pregnancy.Patches;

internal static class HideoutSceneUtility
{
    private static readonly Vector2[] HideoutNodes =
    {
        new Vector2(-176.07f, -40.63f),
        new Vector2(-172.86f, -40.40f),
        new Vector2(-168.77f, -40.21f),
        new Vector2(-166.32f, -40.39f),
        new Vector2(-162.42f, -40.34f),
        new Vector2(-157.63f, -40.41f),
        new Vector2(-150.80f, -40.91f),
        new Vector2(-158.49f, -38.38f),
        new Vector2(-163.07f, -36.99f)
    };

    /// <summary>
    /// True only when the Parishchurch scene is physically loaded.
    /// Do NOT trust <c>_re_Scenename</c> / <c>Idea_Nowscene</c> — altar teleport
    /// writes those names as soon as a destination is clicked, before the scene loads.
    /// </summary>
    internal static bool IsParishHideoutActive()
    {
        try
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                if (IsParishChurchSceneName(scene.name))
                    return true;
            }
        }
        catch { }

        return false;
    }

    internal static bool IsParishChurchSceneName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;

        // Exact hideout scene only — avoid loose "parish" matches (UndergroundChurch, etc.).
        return sceneName.IndexOf("Parishchurch", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    internal static Vector2 GetNodePosition(int nodeIndex)
    {
        if (nodeIndex >= 0 && nodeIndex < HideoutNodes.Length)
            return HideoutNodes[nodeIndex];
        return HideoutNodes[0];
    }
}
