using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Куда приходят адепты.
/// </summary>
public enum AdeptDestination
{
  /// <summary>
  /// В теорию.
  /// </summary>
  Theory,

  /// <summary>
  /// В штаб теории, куда выложена карточка.
  /// </summary>
  TheoryHeadquarters
}

/// <summary>
/// Добавить адептов в теорию или штаб теории.
/// </summary>
public sealed class AddAdeptsEffect : Effect
{
  /// <summary>
  /// Количество адептов.
  /// </summary>
  public int Count { get; }

  /// <summary>
  /// Куда приходят адепты.
  /// </summary>
  public AdeptDestination Destination { get; }

  public AddAdeptsEffect(int count, AdeptDestination destination)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
    Count = count;
    Destination = destination;
  }

  public override string Description => Destination switch
  {
    AdeptDestination.Theory => $"Добавить {Adepts} в теорию",
    AdeptDestination.TheoryHeadquarters => $"Приводит {Adepts} в штаб теории",
    _ => throw new ArgumentOutOfRangeException(nameof(Destination))
  };

  public override bool RequiresTheory => Destination == AdeptDestination.Theory;

  public override bool CanTargetAdeptCard(Cell cell, PlayingField field) => Destination == AdeptDestination.Theory;

  public override void Execute(GameContext context)
  {
    if (Destination == AdeptDestination.Theory && context.TargetAdeptCard is { } card)
    {
      context.Field.AddAdepts(card, Count, context.CurrentPlayer);
      return;
    }

    // Для «в теорию» теорию выбирает игрок, для «в штаб теории» — это штаб, куда выложена карточка.
    var theory = context.TargetTheory ?? throw new InvalidOperationException(Destination == AdeptDestination.Theory
      ? "Не выбрана теория, в которую добавить адептов."
      : "Не указан штаб теории, в который выложена карточка.");
    // В закрытый штаб карточки класть можно, но адепты в него не приходят: действие при выкладывании просто ничего
    // не делает, а не обрывает выкладывание на полпути (карточка уже легла на поле).
    if (Destination == AdeptDestination.TheoryHeadquarters && theory.IsClosed)
      return;
    context.Field.AddAdepts(theory, Count);
  }

  private string Adepts => Count switch
  {
    1 => "адепта",
    _ when Count % 10 is >= 2 and <= 4 && Count % 100 is < 12 or > 14 => $"{Count} адепта",
    _ => $"{Count} адептов",
  };
}
