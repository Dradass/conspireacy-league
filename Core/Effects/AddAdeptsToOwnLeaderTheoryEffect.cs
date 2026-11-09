using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Cards.Professionals;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Добавить адепта в любую открытую теорию (<see cref="GameContext.TargetTheory"/>), где игрок — лидер.
/// Если в теории есть гражданин, адепт один; если есть профессионал — два.
/// </summary>
public sealed class AddAdeptsToOwnLeaderTheoryEffect : Effect
{
  /// <summary>
  /// Сколько адептов приходит, если в теории есть профессионал.
  /// </summary>
  public const int AdeptsWithProfessional = 2;

  /// <summary>
  /// Сколько адептов приходит, если в теории есть гражданин (без профессионала).
  /// </summary>
  public const int AdeptsWithCitizen = 1;

  public override string Description =>
    $"Добавить {AdeptsWithCitizen} адепта в теорию со своим лидером и гражданином или {AdeptsWithProfessional} — если там есть профессионал";

  public override bool RequiresTheory => true;

  public override bool CanTargetTheory(TheoryColumn column, Player player) =>
    base.CanTargetTheory(column, player) && column.IsLeader(player) && AdeptsFor(column) > 0;

  public override void Execute(GameContext context)
  {
    var theory = context.TargetTheory ?? throw new InvalidOperationException("Не выбрана теория.");
    context.Field.AddAdepts(theory, AdeptsFor(theory));
  }

  /// <summary>
  /// Сколько адептов добавит действие в эту теорию: 0, если там нет ни гражданина, ни профессионала.
  /// </summary>
  private static int AdeptsFor(TheoryColumn column)
  {
    if (column.Members.Any(member => member.Card is ProfessionalCard))
      return AdeptsWithProfessional;
    if (column.Members.Any(member => member.Card is CitizenCard or HiddenCard))
      return AdeptsWithCitizen;
    return 0;
  }
}
