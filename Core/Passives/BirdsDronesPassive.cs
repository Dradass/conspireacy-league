using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Passives;

/// <summary>
/// Пассивная способность карточки теории «Птицы — это дроны»: владелец может смотреть любые закрытые карты в любой момент.
/// </summary>
/// <remarks>
/// Проверяет <see cref="Field.PlayingField.CanSeeAllHidden"/>.
/// </remarks>
public sealed class BirdsDronesPassive : PassiveAbility
{
  public override string Description => "Вы можете смотреть любые закрытые карты в любой момент";

  public override void Handle(GameEvent gameEvent, GameContext context)
  {
  }
}
