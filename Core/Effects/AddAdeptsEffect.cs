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
    AdeptDestination.Theory => $"Добавить {Count} адепта в теорию",
    AdeptDestination.TheoryHeadquarters => $"Приводит {Count} адепта в штаб теории",
    _ => throw new ArgumentOutOfRangeException(nameof(Destination))
  };

  public override void Execute(GameContext context)
  {
    // TODO: добавить Count адептов в Destination текущего игрока.
    throw new NotImplementedException();
  }
}
