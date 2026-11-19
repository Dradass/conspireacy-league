using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Passives;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Рептилоиды у власти»: заменить бездомного или гражданина в любой теории карточкой с руки,
/// а заменённого забрать себе на руку.
/// Пассивно — персонажей владельца нельзя смотреть, раскрывать и подменять (<see cref="ReptiliansPassive"/>).
/// </summary>
public sealed class ReptiliansCard : TheoryCard
{
  public ReptiliansCard()
    : base("Рептилоиды у власти", TheoryCatalog.Reptilians, new ReplaceWithHandCardEffect(), new ReptiliansPassive())
  {
  }
}
