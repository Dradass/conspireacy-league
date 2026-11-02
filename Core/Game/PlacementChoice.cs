using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Field;

namespace ConspiracyLeague.Core.Game;

/// <summary>
/// Карточка выложена в штаб теории, и игрок выбирает цель её действия при выкладывании
/// (<see cref="Effect.RequiresTargetCell"/>). Пока выбор не сделан (<see cref="GameSession.ResolvePlacement"/>),
/// других действий сделать нельзя.
/// </summary>
/// <param name="Cell">Ячейка выложенной карточки.</param>
/// <param name="Effect">Действие при выкладывании.</param>
/// <param name="Theory">Штаб теории, куда выложена карточка.</param>
public sealed record PlacementChoice(Cell Cell, Effect Effect, TheoryColumn Theory);
