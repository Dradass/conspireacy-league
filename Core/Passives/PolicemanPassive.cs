using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Passives;

/// <summary>
/// Пассивная способность полицейского в личном штабе: соседние карточки (по горизонтали, вертикали или диагонали)
/// защищены от действий других игроков, которые на них влияют. Сам полицейский не защищён;
/// перевёрнутый (сыгранный) полицейский не защищает.
/// </summary>
/// <remarks>
/// Защиту проверяет <see cref="Field.PlayingField.IsProtected"/>.
/// </remarks>
public sealed class PolicemanPassive : PassiveAbility
{
  public override string Description => "Соседние карточки в личном штабе защищены от действий других игроков";

  public override void Handle(GameEvent gameEvent, GameContext context)
  {
  }
}
