using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Passives;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Коммунисты в Антарктиде»: получить 2$ за каждого лидера во всех закрытых теориях на поле.
/// Пассивно — ваши бездомные влияют на закрытие теории как два очка (<see cref="AntarcticaPassive"/>).
/// </summary>
public sealed class CommunistsInAntarcticaCard : TheoryCard
{
  public CommunistsInAntarcticaCard()
    : base(
      "Коммунисты в Антарктиде",
      TheoryCatalog.CommunistsInAntarctica,
      new GainMoneyPerLeaderInClosedTheoriesEffect(2),
      new AntarcticaPassive())
  {
  }
}
