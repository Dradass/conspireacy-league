using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Passives;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Золотой миллиард»: применяется, чтобы получить <see cref="Reward"/>$.
/// Пассивно — профессионалы владельца приносят больше (<see cref="GoldenBillionPassive"/>).
/// </summary>
public sealed class GoldenBillionCard : TheoryCard
{
  /// <summary>
  /// Сколько $ получает владелец при применении карточки.
  /// </summary>
  public const int Reward = 3;

  public GoldenBillionCard()
    : base("Золотой миллиард", TheoryCatalog.GoldenBillion, new GainMoneyEffect(Reward), new GoldenBillionPassive())
  {
  }
}
