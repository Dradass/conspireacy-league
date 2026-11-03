using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Passives;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Птицы — это дроны»: действия нет, владелец смотрит любые закрытые карты в любой момент
/// (<see cref="BirdsDronesPassive"/>).
/// </summary>
public sealed class BirdsAreDronesCard : TheoryCard
{
  public BirdsAreDronesCard()
    : base("Птицы — это дроны", TheoryCatalog.BirdsAreDrones, Effect.None, new BirdsDronesPassive())
  {
  }
}
