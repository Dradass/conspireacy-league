namespace ConspiracyLeague.Core.Theories;

/// <summary>
/// Теория заговора, к которой относятся карточки.
/// Может быть родительской или дочерней (у дочерней указана родительская).
/// </summary>
/// <remarks>
/// Все теории игры перечислены в <see cref="TheoryCatalog"/>.
/// </remarks>
public sealed class Theory
{
  /// <summary>
  /// Название теории.
  /// </summary>
  public string Name { get; }

  /// <summary>
  /// Родительская теория. Отсутствует у родительских теорий.
  /// </summary>
  public Theory? Parent { get; }

  /// <summary>
  /// Является ли теория дочерней.
  /// </summary>
  public bool IsChild => Parent is not null;

  /// <param name="name">Название.</param>
  /// <param name="parent">Родительская теория; не указывается для родительской теории.</param>
  public Theory(string name, Theory? parent = null)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    Name = name;
    Parent = parent;
  }

  /// <summary>
  /// Относится ли теория к указанной (совпадает с ней или является её дочерней).
  /// </summary>
  /// <param name="other">Теория для сравнения.</param>
  public bool BelongsTo(Theory other)
  {
    for (var theory = this; theory is not null; theory = theory.Parent)
    {
      if (ReferenceEquals(theory, other))
        return true;
    }

    return false;
  }

  public override string ToString() => Name;
}
