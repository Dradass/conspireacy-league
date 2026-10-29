using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Field;

/// <summary>
/// Игровое поле: рабочие зоны игроков и общая зона, где растут теории.
/// </summary>
/// <remarks>
/// Правила выкладывания персон:
/// <list type="bullet">
/// <item>в свою зону: первую карточку — куда угодно, остальные — вплотную к уже лежащим;</item>
/// <item>новую родительскую теорию — в нижний ряд общей зоны, появляется штаб теории;</item>
/// <item>новую дочернюю теорию — во второй ряд общей зоны, вплотную слева или справа от карточки родительской теории
/// или уже выложенной дочерней теории того же родителя;</item>
/// <item>карточку уже выложенной теории — сверху её стопки;</item>
/// <item>карточку без теории — сверху стопки любой теории, заплатив суммарное влияние персон этой стопки.</item>
/// </list>
/// В закрытые штабы теорий карточки класть нельзя.
/// Карточку из своей зоны (личного штаба) можно один раз использовать: выполнить её действие,
/// после чего она лежит рубашкой вверх до конца раунда (<see cref="RefreshUsedCards"/>).
/// Персона, выложенная в штаб теории, выполняет своё действие при выкладывании в штаб
/// (<see cref="PersonCard.OnPlacedInHeadquarters"/>) для этой теории.
/// </remarks>
public sealed class PlayingField
{
  private readonly Dictionary<Cell, Card> _cards = new();
  private readonly HashSet<Cell> _usedCells = [];
  private readonly Dictionary<Player, Zone> _playerZones = new();
  private readonly List<TheoryColumn> _theoryColumns = [];

  /// <summary>
  /// Количество столбцов.
  /// </summary>
  public int Columns { get; }

  /// <summary>
  /// Количество строк.
  /// </summary>
  public int Rows { get; }

  /// <summary>
  /// Общая зона всех игроков.
  /// </summary>
  public Zone CommonZone { get; private set; }

  /// <summary>
  /// Общая зона выросла; аргумент — новая зона.
  /// </summary>
  public event Action<Zone>? CommonZoneChanged;

  /// <summary>
  /// Рабочие зоны игроков.
  /// </summary>
  public IReadOnlyDictionary<Player, Zone> PlayerZones => _playerZones;

  /// <summary>
  /// Выложенные теории.
  /// </summary>
  public IReadOnlyList<TheoryColumn> TheoryColumns => _theoryColumns;

  /// <summary>
  /// В теорию пришли адепты; аргументы — теория и сколько адептов пришло.
  /// </summary>
  public event Action<TheoryColumn, int>? AdeptsAdded;

  /// <summary>
  /// Штаб теории закрыт как несостоятельный.
  /// </summary>
  public event Action<TheoryColumn>? TheoryClosed;

  /// <summary>
  /// Сыгранные карточки перевернулись лицевой стороной вверх; аргумент — их ячейки.
  /// </summary>
  public event Action<IReadOnlyList<Cell>>? UsedCardsRefreshed;

  /// <param name="columns">Количество столбцов.</param>
  /// <param name="rows">Количество строк.</param>
  /// <param name="commonZone">Общая зона.</param>
  public PlayingField(int columns, int rows, Zone commonZone)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columns);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
    Columns = columns;
    Rows = rows;
    CommonZone = commonZone;
  }

  /// <summary>
  /// Добавить игрока и его рабочую зону.
  /// </summary>
  public void AddPlayer(Player player, Zone zone) => _playerZones.Add(player, zone);

  /// <summary>
  /// Карточка в ячейке.
  /// </summary>
  public Card? CardAt(Cell cell) => _cards.GetValueOrDefault(cell);

  /// <summary>
  /// Использована ли карточка в ячейке (лежит рубашкой вверх).
  /// </summary>
  public bool IsUsed(Cell cell) => _usedCells.Contains(cell);

  /// <summary>
  /// Может ли игрок использовать карточку в ячейке: только ещё не использованную карточку из своей зоны.
  /// </summary>
  public bool CanUse(Cell cell, Player player) =>
    _cards.ContainsKey(cell)
    && !IsUsed(cell)
    && _playerZones.TryGetValue(player, out var zone)
    && zone.Contains(cell);

  /// <summary>
  /// Использовать карточку из личного штаба текущего игрока: выполнить выбранный вариант её действия
  /// и перевернуть карточку рубашкой вверх.
  /// </summary>
  /// <param name="cell">Ячейка карточки.</param>
  /// <param name="option">Вариант из <see cref="Effect.Options"/> действия карточки при розыгрыше.</param>
  /// <param name="context">Контекст партии.</param>
  /// <exception cref="InvalidOperationException">Карточку нельзя использовать.</exception>
  public void Use(Cell cell, Effect option, GameContext context)
  {
    if (!CanUse(cell, context.CurrentPlayer))
      throw new InvalidOperationException("Эту карточку нельзя использовать.");

    var card = _cards[cell];
    if (!card.OnPlayFromHand.Options.Contains(option))
      throw new ArgumentException($"У карточки «{card}» нет действия «{option}».", nameof(option));
    if (option.RequiresTheory && context.TargetTheory is null)
      throw new ArgumentException($"Для действия «{option}» нужно выбрать теорию.", nameof(context));

    option.Execute(context);
    _usedCells.Add(cell);
  }

  /// <summary>
  /// Добавить адептов в выложенную теорию.
  /// </summary>
  /// <param name="column">Теория.</param>
  /// <param name="count">Количество адептов.</param>
  public void AddAdepts(TheoryColumn column, int count)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
    if (!_theoryColumns.Contains(column))
      throw new ArgumentException($"Теория «{column}» не выложена на это поле.", nameof(column));
    if (column.IsClosed)
      throw new InvalidOperationException($"Штаб теории «{column}» закрыт — адепты в него не приходят.");

    column.AddAdepts(count);
    AdeptsAdded?.Invoke(column, count);
  }

  /// <summary>
  /// Сыгранные карточки личных штабов переворачиваются лицевой стороной вверх — их снова можно сыграть.
  /// </summary>
  public void RefreshUsedCards()
  {
    var cells = _usedCells.ToList();
    _usedCells.Clear();
    if (cells.Count > 0)
      UsedCardsRefreshed?.Invoke(cells);
  }

  /// <summary>
  /// Есть ли у игрока в личном штабе карточки, которые ещё не сыграны.
  /// </summary>
  public bool HasUnusedCards(Player player) =>
    _playerZones.TryGetValue(player, out var zone) && _cards.Keys.Any(cell => zone.Contains(cell) && !IsUsed(cell));

  /// <summary>
  /// Закрыть несостоятельные штабы теорий (<see cref="TheoryColumn.IsInsolvent"/>) — те, где больше
  /// <see cref="TheoryColumn.SolvencyCheckLeadersThreshold"/> лидеров и бездомных больше, чем остальных персонажей.
  /// </summary>
  /// <returns>Штабы, закрытые сейчас.</returns>
  public IReadOnlyList<TheoryColumn> CloseInsolventTheories()
  {
    var closed = _theoryColumns.Where(column => !column.IsClosed && column.IsInsolvent).ToList();
    foreach (var column in closed)
    {
      column.Close();
      TheoryClosed?.Invoke(column);
    }

    return closed;
  }

  /// <summary>
  /// Теория, к стопке которой относится ячейка.
  /// </summary>
  public TheoryColumn? ColumnAt(Cell cell) => _theoryColumns.FirstOrDefault(column => column.Cells.Contains(cell));

  /// <summary>
  /// Проверить, может ли игрок положить карточку в ячейку.
  /// </summary>
  public PlacementCheck Check(Card card, Cell cell, Player player)
  {
    if (card is not PersonCard person)
      return PlacementCheck.Deny("На поле можно класть только карточки персон.");
    if (!IsInside(cell))
      return PlacementCheck.Deny("Это за пределами поля.");
    if (_cards.ContainsKey(cell))
      return PlacementCheck.Deny("Ячейка уже занята.");

    if (_playerZones.TryGetValue(player, out var ownZone) && ownZone.Contains(cell))
      return CheckPlayerZone(ownZone, cell);
    if (_playerZones.Values.Any(zone => zone.Contains(cell)))
      return PlacementCheck.Deny("Это рабочая зона другого игрока.");
    if (!CommonZone.Contains(cell))
      return PlacementCheck.Deny("Карточки можно класть только в свою рабочую зону или в общую зону.");

    return person.Theory is { } theory ? CheckTheoryCard(theory, cell) : CheckJoining(cell, player);
  }

  /// <summary>
  /// Положить карточку в ячейку, заплатив за это, если нужно.
  /// </summary>
  /// <returns>Условия, на которых карточка положена.</returns>
  /// <exception cref="InvalidOperationException">Карточку сюда класть нельзя.</exception>
  public PlacementCheck Place(Card card, Cell cell, Player player)
  {
    var check = Check(card, cell, player);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    var person = (PersonCard)card;
    _cards[cell] = person;
    TheoryColumn? joined = null;
    if (CommonZone.Contains(cell))
    {
      var member = new TheoryMember(cell, person, player);
      if (check.Column is { } column)
      {
        column.Add(member);
        joined = column;
      }
      else
      {
        joined = new TheoryColumn(person.Theory!, member);
        _theoryColumns.Add(joined);
      }
    }

    player.Spend(check.Cost);
    if (joined is not null)
      GrowCommonZone();
    // Персона пришла в штаб теории — выполняется её действие при выкладывании в штаб.
    if (joined is not null)
      person.PlaceInHeadquarters(new GameContext(player, this).WithTargetTheory(joined));
    return check;
  }

  /// <summary>
  /// Все ячейки, куда игрок может положить карточку, с условиями выкладывания.
  /// </summary>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> AvailableCells(Card card, Player player)
  {
    for (var column = 0; column < Columns; column++)
    {
      for (var row = 0; row < Rows; row++)
      {
        var cell = new Cell(column, row);
        var check = Check(card, cell, player);
        if (check.IsAllowed)
          yield return (cell, check);
      }
    }
  }

  /// <summary>
  /// Стопка теории, если теория уже выложена.
  /// </summary>
  public TheoryColumn? ColumnOf(Theory theory) => _theoryColumns.FirstOrDefault(column => column.Theory == theory);

  /// <summary>
  /// Вырастить общую зону под выложенные карточки. Нижняя строка зоны не меняется.
  /// </summary>
  /// <remarks>
  /// Над самой высокой карточкой остаётся <see cref="FieldLayout.CommonZoneFreeRows"/> свободных ряда.
  /// Когда во всех столбцах зоны есть карточки, зона расширяется на <see cref="FieldLayout.CommonZoneSideGrowth"/>
  /// столбца с каждой стороны, пока не упрётся в края поля.
  /// </remarks>
  private void GrowCommonZone()
  {
    var zone = CommonZone;
    var cells = _cards.Keys.Where(zone.Contains).ToList();

    var levels = cells.Select(cell => zone.BottomRow - cell.Row + 1).DefaultIfEmpty(0).Max();
    var height = Math.Min(Math.Max(zone.Height, levels + FieldLayout.CommonZoneFreeRows), zone.BottomRow + 1);

    var left = zone.Left;
    var right = zone.Left + zone.Width;
    while ((left > 0 || right < Columns)
      && Enumerable.Range(left, right - left).All(column => cells.Any(cell => cell.Column == column)))
    {
      left = Math.Max(0, left - FieldLayout.CommonZoneSideGrowth);
      right = Math.Min(Columns, right + FieldLayout.CommonZoneSideGrowth);
    }

    var grown = new Zone(left, zone.BottomRow - height + 1, right - left, height);
    if (grown == zone)
      return;

    CommonZone = grown;
    CommonZoneChanged?.Invoke(grown);
  }

  private bool IsInside(Cell cell) => cell.Column >= 0 && cell.Column < Columns && cell.Row >= 0 && cell.Row < Rows;

  private PlacementCheck CheckPlayerZone(Zone zone, Cell cell)
  {
    var zoneIsEmpty = !_cards.Keys.Any(zone.Contains);
    if (zoneIsEmpty || cell.Neighbors.Any(neighbor => zone.Contains(neighbor) && _cards.ContainsKey(neighbor)))
      return PlacementCheck.Allow();

    return PlacementCheck.Deny("Карточку нужно положить вплотную к уже лежащим в вашей зоне — слева, справа, сверху или снизу.");
  }

  private PlacementCheck CheckTheoryCard(Theory theory, Cell cell)
  {
    if (ColumnOf(theory) is { } column)
    {
      if (column.IsClosed)
        return PlacementCheck.Deny($"Штаб теории «{theory}» закрыт — в него больше нельзя класть карточки.");
      return cell == column.Top.Above
        ? PlacementCheck.Allow(column: column)
        : PlacementCheck.Deny($"Теория «{theory}» уже выложена: карточку можно положить только сверху её стопки.");
    }

    if (theory.Parent is not { } parentTheory)
    {
      return cell.Row == CommonZone.BottomRow
        ? PlacementCheck.Allow(createsHeadquarters: true)
        : PlacementCheck.Deny($"Новую теорию «{theory}» можно выложить только в нижний ряд общей зоны.");
    }

    if (ColumnOf(parentTheory) is not { } parent)
    {
      return PlacementCheck.Deny(
        $"«{theory}» — дочерняя теория: её можно выложить, только когда на поле есть родительская теория «{parentTheory}».");
    }
    if (parent.IsClosed)
    {
      return PlacementCheck.Deny(
        $"«{theory}» — дочерняя теория, а штаб её родительской теории «{parentTheory}» закрыт — выложить её нельзя.");
    }

    // Дочерние теории выстраиваются в ряд по обе стороны от родительской: новую можно положить вплотную
    // к стопке родительской теории или к стопке любой уже выложенной дочерней теории того же родителя.
    var family = _theoryColumns.Where(column => column == parent || column.Theory.Parent == parentTheory);
    var isNextToFamily = family.Any(column => column.Cells.Contains(cell.Left) || column.Cells.Contains(cell.Right));
    return cell.Row == CommonZone.BottomRow - 1 && isNextToFamily
      ? PlacementCheck.Allow()
      : PlacementCheck.Deny(
        $"Дочернюю теорию «{theory}» можно выложить только во второй ряд общей зоны, вплотную слева или справа "
        + $"от карточки родительской теории «{parentTheory}» или уже выложенной дочерней теории «{parentTheory}».");
  }

  private PlacementCheck CheckJoining(Cell cell, Player player)
  {
    if (_theoryColumns.Count == 0)
      return PlacementCheck.Deny("На поле ещё нет ни одной теории — карточку без теории не к чему присоединить.");

    var column = _theoryColumns.FirstOrDefault(theoryColumn => theoryColumn.Top.Above == cell);
    if (column is null)
      return PlacementCheck.Deny("Карточку без теории можно положить только сверху стопки одной из теорий.");
    if (column.IsClosed)
      return PlacementCheck.Deny($"Штаб теории «{column.Theory}» закрыт — в него больше нельзя класть карточки.");

    var cost = column.Influence;
    return player.CanAfford(cost)
      ? PlacementCheck.Allow(cost, column)
      : PlacementCheck.Deny($"Недостаточно денег: чтобы присоединиться к теории «{column.Theory}», нужно {cost}$, а у вас {player.Money}$.");
  }
}
