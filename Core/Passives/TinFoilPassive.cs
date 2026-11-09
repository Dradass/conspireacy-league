using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Passives;

/// <summary>
/// Пассивная способность карточки теории «Шапочка из фольги»: шапочка лежит на персонаже в личном штабе до конца раунда
/// (он имеет 100% успеха и защищён от любых эффектов), а на адептов в этой теории не действуют никакие эффекты.
/// </summary>
/// <remarks>
/// Прикрепление и защиту ведёт <see cref="Field.PlayingField"/> (<see cref="Field.OwnedTheoryCard.AttachedTo"/>),
/// защиту адептов — <see cref="Field.TheoryColumn.AdeptsShielded"/>.
/// </remarks>
public sealed class TinFoilPassive : PassiveAbility
{
  public override string Description =>
    "Лежит на персонаже в личном штабе до конца раунда: он имеет 100% успеха и защищён от любых эффектов. "
    + "На адептов в этой теории не действуют никакие эффекты";

  public override void Handle(GameEvent gameEvent, GameContext context)
  {
  }
}
