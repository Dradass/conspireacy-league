using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Passives;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Эболу создали в ЦРУ»: действия нет. Пассивно — когда другой игрок добавляет лидера в теорию с вашим
/// персонажем, вы получаете 3$, а он платит вам 1$ (<see cref="EbolaPassive"/>).
/// </summary>
public sealed class CiaEbolaCard : TheoryCard
{
  public CiaEbolaCard()
    : base("Эболу создали в ЦРУ", TheoryCatalog.CiaEbola, Effect.None, new EbolaPassive())
  {
  }
}
