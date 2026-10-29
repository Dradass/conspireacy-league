using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Field;

/// <summary>
/// Стопка карточек одной теории в общей зоне. Растёт снизу вверх.
/// </summary>
public sealed class TheoryColumn
{
  private readonly List<Cell> _cells = [];
  private readonly List<PersonCard> _cards = [];

  /// <summary>
  /// Теория.
  /// </summary>
  public Theory Theory { get; }

  /// <summary>
  /// Является ли стопка штабом теории. Штаб появляется, когда выложена родительская теория.
  /// </summary>
  public bool IsHeadquarters => !Theory.IsChild;

  /// <summary>
  /// Ячейки стопки снизу вверх.
  /// </summary>
  public IReadOnlyList<Cell> Cells => _cells;

  /// <summary>
  /// Карточки стопки снизу вверх.
  /// </summary>
  public IReadOnlyList<PersonCard> Cards => _cards;

  /// <summary>
  /// Ячейка последней (верхней) карточки.
  /// </summary>
  public Cell Top => _cells[^1];

  /// <summary>
  /// Суммарное влияние персон в стопке.
  /// </summary>
  public int Influence => _cards.Sum(card => card.Influence);

  /// <summary>
  /// Количество адептов теории.
  /// </summary>
  public int Adepts { get; private set; }

  /// <summary>
  /// Ячейка карточки теории — нижней в стопке. На неё ставятся фигурки адептов.
  /// </summary>
  public Cell TheoryCell => _cells[0];

  internal TheoryColumn(Theory theory, Cell cell, PersonCard card)
  {
    Theory = theory;
    Add(cell, card);
  }

  internal void Add(Cell cell, PersonCard card)
  {
    _cells.Add(cell);
    _cards.Add(card);
  }

  internal void AddAdepts(int count) => Adepts += count;

  public override string ToString() => Theory.Name;
}
