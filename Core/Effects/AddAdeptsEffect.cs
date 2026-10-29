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
  /// В штаб теории.
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

  public override void Execute(GameContext context)
  {
    if (Destination == AdeptDestination.Theory)
    {
      var theory = context.TargetTheory ?? throw new InvalidOperationException("Не выбрана теория, в которую добавить адептов.");
      context.Field.AddAdepts(theory, Count);
      return;
    }

    // TODO: привести Count адептов в штаб теории текущего игрока.
    throw new NotImplementedException();
  }

  private string Adepts => Count == 1 ? "адепта" : $"{Count} адептов";
}
