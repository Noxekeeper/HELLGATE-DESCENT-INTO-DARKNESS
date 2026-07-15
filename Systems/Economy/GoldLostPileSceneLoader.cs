using UnityEngine;
using UnityEngine.SceneManagement;

namespace NoREroMod.Systems.Economy;

/// <summary>
/// Re-spawns the souls-style "lost gold pile" when the player re-enters the scene
/// where the death occurred. Mirrors the vanilla <c>IdeaLost.Sceneget</c> contract.
/// Subscribed to <see cref="SceneManager.sceneLoaded"/> from <see cref="Plugin"/>.
/// </summary>
internal static class GoldLostPileSceneLoader
{
    private static bool _subscribed;

    public static void Initialize()
    {
        if (_subscribed) return;
        _subscribed = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        try
        {
            if (!EconomicConfig.Enable) return;
            if (!GoldStaticMng.LostFlag) return;
            if (GoldStaticMng.LostAmount <= 0) return;

            string target = GoldStaticMng.LostScene ?? string.Empty;
            string actual = scene.name ?? string.Empty;
            if (!string.Equals(target, actual, System.StringComparison.Ordinal))
                return;

            // We schedule the spawn for the next frame so the scene's GameObjects
            // (player, colliders) are fully initialized.
            new GameObject("GoldLostPileSpawner_XUAIGNORE")
                .AddComponent<GoldLostPileSpawnerHost>();
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning("[GoldLostPile] sceneLoaded handler threw: " + ex.Message);
        }
    }
}

internal sealed class GoldLostPileSpawnerHost : MonoBehaviour
{
    private void Start()
    {
        try
        {
            if (!GoldStaticMng.LostFlag || GoldStaticMng.LostAmount <= 0)
            {
                Destroy(gameObject);
                return;
            }
            GoldDropAwarder.TrySpawnLostPile(GoldStaticMng.LostPos, GoldStaticMng.LostAmount);
            if (EconomicConfig.DebugLogging)
                Plugin.Log?.LogInfo($"[GoldLostPile] Re-spawned at {GoldStaticMng.LostPos} amount={GoldStaticMng.LostAmount}");
        }
        catch { }
        Destroy(gameObject);
    }
}
