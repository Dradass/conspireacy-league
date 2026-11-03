using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Passives;

/// <summary>
/// Пассивная способность карточки теории «Коммунисты в Антарктиде»: бездомные владельца влияют на закрытие штаба
/// как два очка вместо одного.
/// </summary>
/// <remarks>
/// Учитывает <see cref="Field.TheoryColumn.IsInsolvent"/>.
/// </remarks>
public sealed class AntarcticaPassive : PassiveAbility
{
  public override string Description => "Ваши бездомные влияют на закрытие теории как 2 очка";

  public override void Handle(GameEvent gameEvent, GameContext context)
  {
  }
}
