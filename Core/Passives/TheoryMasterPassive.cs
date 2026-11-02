using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Passives;

/// <summary>
/// Пассивная способность Мастера теорий в штабе теории: любое привлечение адептов в эту теорию приводит
/// на <see cref="AdeptsBonus"/> адепта больше. Бонусы нескольких мастеров складываются.
/// </summary>
/// <remarks>
/// Бонус добавляет <see cref="Field.PlayingField.AddAdepts(Field.TheoryColumn, int)"/>.
/// </remarks>
public sealed class TheoryMasterPassive : PassiveAbility
{
  /// <summary>
  /// На сколько адептов больше приходит в теорию.
  /// </summary>
  public const int AdeptsBonus = 1;

  public override string Description => $"В штабе теории: любое привлечение адептов в теорию даёт +{AdeptsBonus} адепта";

  public override void Handle(GameEvent gameEvent, GameContext context)
  {
  }
}
