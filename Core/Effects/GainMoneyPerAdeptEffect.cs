using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Получить столько $, сколько адептов в выбранной теории, где у игрока есть лидер,
/// или на своей карточке в личном штабе, на которую приводят адептов (<see cref="GameContext.TargetAdeptCard"/>).
/// Теорию или карточку без адептов выбрать нельзя.
/// </summary>
public sealed class GainMoneyPerAdeptEffect : Effect
{
  public override string Description => "Получить $ по числу адептов в своей теории";

  public override bool RequiresTheory => true;

  public override bool CanTargetTheory(TheoryColumn column, Player player) =>
    base.CanTargetTheory(column, player) && column.MembersOf(player).Any() && column.Adepts > 0;

  public override bool CanTargetAdeptCard(Cell cell, PlayingField field) => field.AdeptsAt(cell) > 0;

  public override void Execute(GameContext context)
  {
    var adepts = context.TargetAdeptCard is { } card
      ? context.Field.AdeptsAt(card)
      : (context.TargetTheory ?? throw new InvalidOperationException("Не выбрана теория.")).Adepts;
    context.CurrentPlayer.Earn(adepts);
  }
}
