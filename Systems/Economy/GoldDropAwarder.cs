using UnityEngine;

namespace NoREroMod.Systems.Economy;

/// <summary>
/// Single entry point for spawning a gold pickup in the world. Used by the enemy
/// death patch (live drop) and by the souls-style scene-load handler (lost pile).
/// Large enemy drops can be split into several pickups (see <see cref="EconomicBigDropSplitSettings"/>).
/// </summary>
internal static class GoldDropAwarder
{
    /// <summary>
    /// Live gold drop at the enemy position. Returns true if at least one pickup was created.
    /// </summary>
    public static bool TrySpawnDrop(Vector2 position, long amount)
    {
        if (amount <= 0) return false;

        EconomicBigDropSplitSettings split = EconomicConfig.BigDropSplit;
        if (split != null && split.Enable)
        {
            // Large tier: random MinPiles..MaxPiles (e.g. 8–11).
            if (amount >= split.MinTotalGold)
            {
                int minP = Mathf.Max(2, split.MinPiles);
                int maxP = Mathf.Max(minP, split.MaxPiles);
                int nDesired = Random.Range(minP, maxP + 1);
                int n = (int)Mathf.Min(nDesired, amount);
                if (n > 1)
                    return TrySpawnSplitPiles(position, amount, n, split.HorizontalSpread);
            }
            // Medium tier: fixed pile count (e.g. 2 from 15+ gold until large threshold).
            else if (split.SmallSplitPileCount >= 2 && amount >= split.SmallSplitMinTotalGold)
            {
                int n = (int)Mathf.Min(split.SmallSplitPileCount, amount);
                if (n > 1)
                    return TrySpawnSplitPiles(position, amount, n, split.HorizontalSpread);
            }
        }

        return CreatePickup(position, amount, isLostPile: false, suppressDropSfx: false, staticPlacement: false) != null;
    }

    /// <summary>
    /// Souls-style "lost pile" recreate on scene re-entry. Skips drop SFX and physics.
    /// Always a single pile.
    /// </summary>
    public static bool TrySpawnLostPile(Vector2 position, long amount)
    {
        return CreatePickup(position, amount, isLostPile: true, suppressDropSfx: false, staticPlacement: false) != null;
    }

    /// <summary>
    /// HellGate spawn-config placed pile: no drop arc/SFX, single pickup (no big-drop split).
    /// </summary>
    public static GameObject TrySpawnPlacedPickup(Vector2 position, long amount)
    {
        if (amount <= 0)
            return null;

        return CreatePickup(position, amount, isLostPile: false, suppressDropSfx: true, staticPlacement: true);
    }

    private static bool TrySpawnSplitPiles(Vector2 basePosition, long total, int pileCount, float spread)
    {
        if (pileCount <= 1 || total <= 0)
            return CreatePickup(basePosition, total, isLostPile: false, suppressDropSfx: false, staticPlacement: false) != null;

        long baseEach = total / pileCount;
        long remainder = total % pileCount;
        bool any = false;
        float spreadAbs = Mathf.Max(0f, spread);

        for (int i = 0; i < pileCount; i++)
        {
            long chunk = baseEach + (i < remainder ? 1L : 0L);
            if (chunk <= 0) continue;

            float ox = spreadAbs <= 0.0001f ? 0f : Random.Range(-spreadAbs, spreadAbs);
            float oy = spreadAbs <= 0.0001f ? 0f : Random.Range(-spreadAbs * 0.35f, spreadAbs * 0.35f);
            Vector2 p = new Vector2(basePosition.x + ox, basePosition.y + oy);

            if (CreatePickup(p, chunk, isLostPile: false, suppressDropSfx: i > 0, staticPlacement: false) != null)
                any = true;
        }

        if (EconomicConfig.DebugLogging && any)
            Plugin.Log?.LogInfo($"[GoldDropAwarder] Split drop: total={total} piles={pileCount}");

        return any;
    }

    private static GameObject CreatePickup(
        Vector2 position,
        long amount,
        bool isLostPile,
        bool suppressDropSfx,
        bool staticPlacement)
    {
        if (amount <= 0)
            return null;

        if (!GoldAssetLoader.HasFrames)
        {
            Plugin.Log?.LogWarning("[GoldDropAwarder] Cannot spawn pickup: pickup frames not loaded.");
            return null;
        }

        try
        {
            string objectName = isLostPile ? "GoldPickup_Lost" : staticPlacement ? "GoldPickup_Placed" : "GoldPickup";
            GameObject go = new GameObject(objectName);
            go.transform.position = new Vector3(position.x, position.y, 0f);

            float spriteScale = Mathf.Max(0.05f, EconomicConfig.PickupSpriteScale);
            go.transform.localScale = new Vector3(spriteScale, spriteScale, 1f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GoldAssetLoader.PickupFrames[0];

            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = Mathf.Max(0.1f, EconomicConfig.PickupTriggerRadius);

            GoldPickup pickup = go.AddComponent<GoldPickup>();
            pickup.Initialize(amount, isLostPile, suppressDropSfx, staticPlacement);

            return go;
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning("[GoldDropAwarder] Failed to spawn pickup: " + ex.Message);
            return null;
        }
    }
}
