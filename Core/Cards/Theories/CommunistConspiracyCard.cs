using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Заговор коммунистов»: снизить успех любого персонажа в личном штабе на 10% и получить его влияние.
/// </summary>
public sealed class CommunistConspiracyCard : TheoryCard
{
  public CommunistConspiracyCard()
    : base("Заговор коммунистов", TheoryCatalog.CommunistConspiracy, new WeakenCharacterEffect())
  {
  }
}
