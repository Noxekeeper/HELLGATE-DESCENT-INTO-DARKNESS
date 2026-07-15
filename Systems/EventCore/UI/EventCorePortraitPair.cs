namespace NoREroMod.Systems.EventCore.UI;

/// <summary>Expression folder names under AradiaAva (left) and TouzokuAva (right). Null or empty hides that side.</summary>
internal readonly struct EventCorePortraitPair
{
    internal readonly string LeftExpression;
    internal readonly string RightExpression;

    internal EventCorePortraitPair(string leftExpression, string rightExpression)
    {
        LeftExpression = leftExpression;
        RightExpression = rightExpression;
    }

    internal static readonly EventCorePortraitPair Hidden = new EventCorePortraitPair(null, null);
}
