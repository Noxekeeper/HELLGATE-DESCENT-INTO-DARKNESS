using UnityEngine;

namespace NoREroMod.Systems.Economy;

/// <summary>
/// Souls-style "lost gold pile" runtime state, parallel to vanilla <c>StaticMng.Idea_*</c>.
/// We never touch <see cref="StaticMng"/> — keeping the gold module fully isolated.
///
/// On player death the wallet is debited and the lost amount + position + scene are recorded
/// here. On the next scene load that matches <see cref="LostScene"/>, a special "lost"
/// pickup is spawned at <see cref="LostPos"/> so the player can reclaim the gold.
/// Persisted to <c>PlayerGold_Slot{NN}.json</c> together with the wallet balance.
/// </summary>
internal static class GoldStaticMng
{
    public static bool LostFlag;
    public static long LostAmount;
    public static string LostScene;
    public static Vector2 LostPos;

    public static void Clear()
    {
        LostFlag = false;
        LostAmount = 0;
        LostScene = null;
        LostPos = Vector2.zero;
    }

    public static void Set(long amount, string sceneName, Vector2 pos)
    {
        if (amount <= 0)
        {
            Clear();
            return;
        }
        LostFlag = true;
        LostAmount = amount;
        LostScene = sceneName ?? string.Empty;
        LostPos = pos;
    }
}
