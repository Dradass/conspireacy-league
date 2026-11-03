namespace ConspiracyLeague.Core.Passives;

/// <summary>
/// Пассивная способность карточки теории «Рептилоиды у власти»: персонажей владельца нельзя смотреть,
/// раскрывать и подменять никакими эффектами.
/// </summary>
/// <remarks>
/// Защиту проверяет <see cref="Field.PlayingField"/>.
/// </remarks>
public sealed class ReptiliansPassive : PassiveAbility
{
  public override string Description => "Ваших персонажей нельзя смотреть, раскрывать и подменять никакими эффектами";

  public override void Handle(Game.GameEvent gameEvent, Game.GameContext context)
  {
  }
}
