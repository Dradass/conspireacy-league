using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Passives;

/// <summary>
/// Пассивная способность учёного в личном штабе: соседние персонажи (по горизонтали, вертикали или диагонали)
/// получают +<see cref="NeighborSuccessBonus"/>% к вероятности успеха, а если соседний персонаж приносит деньги,
/// он приносит ещё +<see cref="MoneyBonus"/>$. Бонусы нескольких учёных складываются.
/// </summary>
/// <remarks>
/// Бонусы считает <see cref="Field.PlayingField"/>: <see cref="Field.PlayingField.SuccessBonusAt"/>
/// и <see cref="Field.PlayingField.MoneyBonusAt"/>.
/// </remarks>
public sealed class ScientistPassive : PassiveAbility
{
  /// <summary>
  /// На сколько процентов учёный повышает вероятность успеха соседних персонажей в личном штабе.
  /// </summary>
  public const int NeighborSuccessBonus = 5;

  /// <summary>
  /// На сколько сложнее закрыть штаб теории за каждого учёного в нём: бездомных должно быть больше остальных
  /// на это число.
  /// </summary>
  public const int SolvencyBonus = 2;

  /// <summary>
  /// Сколько $ учёный добавляет к деньгам, которые приносит соседний персонаж в личном штабе.
  /// </summary>
  public const int MoneyBonus = 1;

  public override string Description =>
    $"Соседи в личном штабе: +{NeighborSuccessBonus}% успеха и +{MoneyBonus}$ к деньгам, которые они приносят (пока учёный открыт)";

  public override void Handle(GameEvent gameEvent, GameContext context)
  {
  }
}
