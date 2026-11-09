using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Получить 1$ за каждые <see cref="AdeptsPerDollar"/> адепта в выбранной теории, где у игрока есть лидер,
/// или на своей карточке в личном штабе, на которую приводят адептов (<see cref="GameContext.TargetAdeptCard"/>).
/// Теорию или карточку, где адептов меньше <see cref="AdeptsPerDollar"/>, выбрать нельзя.
/// </summary>
public sealed class GainMoneyPerAdeptEffect : Effect
{
  /// <summary>
  /// За сколько адептов дают 1$.
  /// </summary>
  public const int AdeptsPerDollar = 2;

  public override string Description => $"Получить 1$ за каждые {AdeptsPerDollar} адепта в своей теории";

  public override bool RequiresTheory => true;

  public override bool CanTargetTheory(TheoryColumn column, Player player) =>
    base.CanTargetTheory(column, player) && column.IsLeader(player) && column.Adepts >= AdeptsPerDollar;

  public override bool CanTargetAdeptCard(Cell cell, PlayingField field) => field.AdeptsAt(cell) >= AdeptsPerDollar;

  public override void Execute(GameContext context)
  {
    var adepts = context.TargetAdeptCard is { } card
      ? context.Field.AdeptsAt(card)
      : (context.TargetTheory ?? throw new InvalidOperationException("Не выбрана теория.")).Adepts;
    context.CurrentPlayer.Earn(adepts / AdeptsPerDollar);
  }
}
