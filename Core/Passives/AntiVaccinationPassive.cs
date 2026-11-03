using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Passives;

/// <summary>
/// Пассивная способность карточки теории «Антипрививочники»: все штабы теорий владельца сложнее закрыть
/// на <see cref="SolvencyBonus"/>.
/// </summary>
/// <remarks>
/// Поправку учитывает <see cref="Field.TheoryColumn"/> при проверке состоятельности штаба.
/// </remarks>
public sealed class AntiVaccinationPassive : PassiveAbility
{
  /// <summary>
  /// На сколько сложнее закрыть штаб владельца.
  /// </summary>
  public const int SolvencyBonus = 1;

  public override string Description => $"Все ваши штабы теорий сложнее закрыть на {SolvencyBonus}";

  public override void Handle(GameEvent gameEvent, GameContext context)
  {
  }
}
