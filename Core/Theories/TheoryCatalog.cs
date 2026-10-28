namespace ConspiracyLeague.Core.Theories;

/// <summary>
/// Список всех теорий игры.
/// </summary>
/// <remarks>
/// Новая теория добавляется сюда: статическим свойством и в <see cref="All"/>.
/// Дочерняя теория объявляется после родительской.
/// </remarks>
public static class TheoryCatalog
{
  // TODO: примеры — заменить настоящим списком теорий.

  /// <summary>
  /// Рептилоиды.
  /// </summary>
  public static Theory Reptilians { get; } = new("Рептилоиды");

  /// <summary>
  /// Рептилоиды в правительстве.
  /// </summary>
  public static Theory ReptiliansInGovernment { get; } = new("Рептилоиды в правительстве", Reptilians);

  /// <summary>
  /// Плоская Земля.
  /// </summary>
  public static Theory FlatEarth { get; } = new("Плоская Земля");

  /// <summary>
  /// Все теории.
  /// </summary>
  public static IReadOnlyList<Theory> All { get; } =
  [
    Reptilians,
    ReptiliansInGovernment,
    FlatEarth,
  ];

  /// <summary>
  /// Родительские теории.
  /// </summary>
  public static IEnumerable<Theory> Parents => All.Where(theory => !theory.IsChild);

  /// <summary>
  /// Дочерние теории указанной теории.
  /// </summary>
  /// <param name="parent">Родительская теория.</param>
  public static IEnumerable<Theory> ChildrenOf(Theory parent) => All.Where(theory => theory.Parent == parent);
}
