namespace ConspiracyLeague.Core.Game;

/// <summary>
/// Игровое событие, на которое могут реагировать пассивные способности карт
/// (начало хода, розыгрыш карты, приход адепта и т.п.).
/// </summary>
/// <remarks>
/// TODO: конкретные события описываются наследниками, например
/// <c>public sealed record TurnStarted(int PlayerIndex) : GameEvent;</c>
/// </remarks>
public abstract record GameEvent;
