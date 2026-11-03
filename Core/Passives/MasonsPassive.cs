namespace ConspiracyLeague.Core.Passives;

/// <summary>
/// Пассивная способность карточки теории «Масоны правят миром»: все закрытые персонажи владельца
/// имеют <see cref="HiddenInfluence"/> влияния.
/// </summary>
/// <remarks>
/// Влияние закрытых карточек пересчитывает <see cref="Field.PlayingField"/>.
/// </remarks>
public sealed class MasonsPassive : PassiveAbility
{
  /// <summary>
  /// Влияние каждого закрытого персонажа владельца.
  /// </summary>
  public const int HiddenInfluence = 3;

  public override string Description => $"Все ваши закрытые персонажи имеют {HiddenInfluence} влияния";

  public override void Handle(Game.GameEvent gameEvent, Game.GameContext context)
  {
  }
}
