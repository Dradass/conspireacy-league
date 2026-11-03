using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Passives;

/// <summary>
/// Пассивная способность карточки теории «Чипирование»: поставивший фишку может использовать чипированного персонажа
/// как своего; при использовании получает 1$ за каждого чипированного персонажа в игре.
/// </summary>
/// <remarks>
/// Правила ведёт <see cref="Field.PlayingField"/>, пока у игрока есть эта карточка.
/// </remarks>
public sealed class ChipPassive : PassiveAbility
{
  public override string Description =>
    "Вы можете использовать чипированного персонажа как своего; при использовании получите 1$ за каждого чипированного персонажа в игре";

  public override void Handle(GameEvent gameEvent, GameContext context)
  {
  }
}
