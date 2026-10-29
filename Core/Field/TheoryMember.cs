using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Field;

/// <summary>
/// Карточка в стопке теории.
/// </summary>
/// <param name="Cell">Ячейка карточки.</param>
/// <param name="Card">Карточка персоны.</param>
/// <param name="Owner">Игрок, который выложил карточку, — её владелец.</param>
public sealed record TheoryMember(Cell Cell, PersonCard Card, Player Owner);
