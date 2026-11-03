using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Passives;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Чипирование»: положить фишку на любого персонажа в личных штабах, заплатив владельцу его стоимость.
/// Пассивно — чипированного персонажа можно использовать как своего; за каждое такое использование 1$ за каждого
/// чипированного персонажа в игре (<see cref="ChipPassive"/>).
/// </summary>
public sealed class MicrochippingCard : TheoryCard
{
  public MicrochippingCard()
    : base("Чипирование", TheoryCatalog.Microchipping, new ChipPersonEffect(), new ChipPassive())
  {
  }
}
