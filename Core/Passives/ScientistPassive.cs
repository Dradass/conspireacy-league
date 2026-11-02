using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Passives;

/// <summary>
/// Пассивная способность учёного в личном штабе: соседние персонажи (по горизонтали, вертикали или диагонали)
/// получают +<see cref="NeighborSuccessBonus"/>% к вероятности успеха, а каждая карточка личного штаба,
/// которая приносит деньги, приносит ещё +<see cref="MoneyBonus"/>$. Бонусы нескольких учёных складываются.
/// </summary>
/// <remarks>
/// Бонусы считает <see cref="Field.PlayingField"/>: <see cref="Field.PlayingField.SuccessBonusAt"/>
/// и <see cref="Field.PlayingField.MoneyBonusOf"/>.
/// </remarks>
public sealed class ScientistPassive : PassiveAbility
{
  /// <summary>
  /// На сколько процентов учёный повышает вероятность успеха соседних персонажей в личном штабе.
  /// </summary>
  public const int NeighborSuccessBonus = 10;

  /// <summary>
  /// Сколько $ учёный добавляет к деньгам, которые приносит карточка личного штаба.
  /// </summary>
  public const int MoneyBonus = 1;

  public override string Description =>
    $"Соседи в личном штабе: +{NeighborSuccessBonus}% успеха; карточки личного штаба приносят +{MoneyBonus}$";

  public override void Handle(GameEvent gameEvent, GameContext context)
  {
  }
}
