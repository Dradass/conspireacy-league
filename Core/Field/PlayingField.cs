using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Cards.Professionals;
using ConspiracyLeague.Core.Cards.Theories;
using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Passives;
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
/// <item>новую дочернюю теорию — в третий ряд общей зоны (<see cref="SideTheoryLevel"/>), вплотную слева или справа от третьей карточки родительской теории
/// или уже выложенной дочерней теории того же родителя;</item>
/// <item>карточку уже выложенной теории — сверху её стопки;</item>
/// <item>карточку без теории — сверху стопки любой теории, заплатив суммарное влияние персон этой стопки;</item>
/// <item>закрытую карточку (<see cref="HiddenCard"/>) — как карточку без теории: она считается гражданином без теории.
/// Раскрытая закрытая карточка (<see cref="Reveal"/>) видна всем, но по-прежнему считается гражданином без теории.</item>
/// </list>
/// В закрытые штабы теорий можно добавлять новых персонажей (адептов туда не приходит).
/// Полицейский (<see cref="PolicemanPassive"/>) защищает соседние карточки своего личного штаба
/// от действий других игроков (<see cref="IsProtected"/>).
/// На некоторые карточки в личном штабе (<see cref="PersonCard.AdeptsBonus"/>, например Мастер теорий)
/// можно приводить адептов вместо теории — тогда приходит ещё и бонус.
/// Карточку из своей зоны (личного штаба) можно один раз использовать: выполнить её действие,
/// после чего она лежит рубашкой вверх до конца раунда (<see cref="RefreshUsedCards"/>).
/// После быстрого действия (<see cref="Effect.IsQuick"/>) карточка не переворачивается, а поворачивается
/// на 90 градусов (<see cref="IsRotated"/>): ей можно сыграть ещё одно любое действие, и тогда она переворачивается.
/// Персона, выложенная в штаб теории, выполняет своё действие при выкладывании в штаб
/// (<see cref="PersonCard.OnPlacedInHeadquarters"/>) для этой теории.
/// </remarks>
public sealed class PlayingField
{
  private readonly Dictionary<Cell, Card> _cards = new();
  private readonly HashSet<Cell> _usedCells = [];
  private readonly HashSet<Cell> _rotatedCells = [];
  private readonly Dictionary<Cell, int> _trainedBonuses = new();
  private readonly Dictionary<Cell, int> _cardAdepts = new();
  private readonly Dictionary<Player, Zone> _playerZones = new();
  private readonly List<TheoryColumn> _theoryColumns = [];
  private readonly List<OwnedTheoryCard> _theoryCards = [];
  private readonly Dictionary<Cell, Player> _chips = new();
  private readonly Dictionary<Cell, Player> _wheels = new();

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
  /// На карточку в личном штабе пришли адепты; аргументы — ячейка карточки и сколько адептов пришло (с бонусом).
  /// </summary>
  public event Action<Cell, int>? CardAdeptsAdded;

  /// <summary>
  /// Штаб теории закрыт как несостоятельный.
  /// </summary>
  public event Action<TheoryColumn>? TheoryClosed;

  /// <summary>
  /// Штаб теории снова состоятелен и открыт обратно.
  /// </summary>
  public event Action<TheoryColumn>? TheoryReopened;

  /// <summary>
  /// Сыгранные карточки перевернулись лицевой стороной вверх; аргумент — их ячейки.
  /// </summary>
  public event Action<IReadOnlyList<Cell>>? UsedCardsRefreshed;

  /// <summary>
  /// Закрытая карточка раскрыта — теперь её видят все; аргумент — её ячейка.
  /// </summary>
  public event Action<Cell>? HiddenCardRevealed;


  /// <summary>
  /// Закрытая карточка выложена в стопку теории; аргументы — её ячейка и владелец.
  /// </summary>
  public event Action<Cell, Player>? HiddenCardPlaced;

  /// <summary>
  /// Карточка теории принесла деньги её владельцу; аргументы — карточка и сумма в $.
  /// </summary>
  public event Action<OwnedTheoryCard, int>? TheoryEarned;

  /// <summary>
  /// В теорию поставлен мипл-лидер; аргументы — теория и чей мипл.
  /// </summary>
  public event Action<TheoryColumn, Player>? LeaderMeepleAdded;

  /// <summary>
  /// Миплов-лидеров сняли со всех теорий; аргумент — теории, где они стояли.
  /// </summary>
  public event Action<IReadOnlyList<TheoryColumn>>? LeaderMeeplesRemoved;

  /// <summary>
  /// Карточка личного штаба принесла деньги; аргументы — ячейка карточки, сколько принесло само действие,
  /// сколько добавили учёные (<see cref="ScientistPassive"/>) и сколько добавили карточки теорий.
  /// </summary>
  public event Action<Cell, int, int, int>? CardEarned;

  /// <summary>
  /// Вероятность успеха персонажа навсегда повышена (<see cref="Train"/>); аргумент — его ячейка.
  /// </summary>
  public event Action<Cell>? CardTrained;

  /// <summary>
  /// Бездомный в личном штабе провалил проверку на низкой грани (<see cref="HomelessDestroyDie"/>) и ушёл из игры;
  /// аргумент — его ячейка (карточки в ней уже нет).
  /// </summary>
  public event Action<Cell>? PersonalCardDestroyed;

  /// <summary>
  /// Гражданин в личном штабе провалил проверку на грани 1 (<see cref="CitizenDemoteDie"/>) и стал бездомным;
  /// аргумент — его ячейка (карточка в ней уже бездомный).
  /// </summary>
  public event Action<Cell>? PersonalCardDemoted;

  /// <summary>
  /// Применяются ли последствия провала на низких гранях: бездомный уходит из игры, гражданин становится бездомным.
  /// </summary>
  public bool FailureConsequences { get; set; } = true;

  /// <summary>
  /// Наибольшая грань, на которой проваленный бездомный уходит из игры.
  /// </summary>
  public const int HomelessDestroyDie = 2;

  /// <summary>
  /// Грань, на которой проваленный гражданин превращается в бездомного.
  /// </summary>
  public const int CitizenDemoteDie = 1;

  /// <summary>
  /// Адептов перевели из одной теории в другую; аргументы — откуда, куда и сколько.
  /// </summary>
  public event Action<TheoryColumn, TheoryColumn, int>? AdeptsMoved;

  /// <summary>
  /// Карточку забрали из личного штаба в руку; аргумент — её ячейка.
  /// </summary>
  public event Action<Cell>? CardTakenToHand;

  /// <summary>
  /// Карточку убрали из стопки теории (из игры или в руку владельца); карточки над ней опустились.
  /// Аргументы — ячейка убранной карточки и переезды остальных снизу вверх.
  /// </summary>
  public event Action<Cell, IReadOnlyList<(Cell From, Cell To)>>? TheoryCardRemoved;

  /// <summary>
  /// Карточка в ячейке заменена другой (бездомного обучили, лидера закрыли).
  /// </summary>
  public event Action<Cell>? CardReplaced;

  /// <summary>
  /// Карточку убрали из личного штаба из игры; аргумент — её ячейка.
  /// </summary>
  public event Action<Cell>? CardRemoved;

  /// <summary>
  /// Две карточки теории поменялись местами.
  /// </summary>
  public event Action<Cell, Cell>? CardsSwapped;

  /// <summary>
  /// Теорию сдвинули по горизонтали в соседний столбец вместе с её карточками; аргументы — теория и переезды карточек.
  /// </summary>
  public event Action<TheoryColumn, IReadOnlyList<(Cell From, Cell To)>>? TheoryColumnMoved;

  /// <summary>
  /// Карточки теорий, которые получили владельцы теорий (<see cref="GrantTheoryCards"/>).
  /// </summary>
  public IReadOnlyList<OwnedTheoryCard> TheoryCards => _theoryCards;

  /// <summary>
  /// Владелец теории получил её карточку; аргумент — карточка.
  /// </summary>
  public event Action<OwnedTheoryCard>? TheoryCardGranted;

  /// <summary>
  /// Карточка теории перешла новому владельцу теории вместе с состоянием; аргумент — карточка
  /// (уже у нового владельца и на его ячейке).
  /// </summary>
  public event Action<OwnedTheoryCard>? TheoryCardTransferred;

  /// <summary>
  /// На персонажа положили фишку; аргумент — его ячейка.
  /// </summary>
  public event Action<Cell>? CharacterChipped;

  /// <summary>
  /// На персонажа в личном штабе поставили значок руля.
  /// </summary>
  public event Action<Cell>? WheelMarked;

  /// <summary>
  /// Карточку теории применили; аргумент — карточка.
  /// </summary>
  public event Action<OwnedTheoryCard>? TheoryCardUsed;

  /// <summary>
  /// Применённые карточки теорий перевернулись лицевой стороной вверх; аргумент — они.
  /// </summary>
  public event Action<IReadOnlyList<OwnedTheoryCard>>? TheoryCardsRefreshed;

  /// <summary>
  /// Адепты ушли из теории; аргументы — теория и сколько адептов.
  /// </summary>
  public event Action<TheoryColumn, int>? AdeptsRemoved;

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
  /// Повёрнута ли карточка в ячейке на 90 градусов: она сыграла быстрое действие (<see cref="Effect.IsQuick"/>)
  /// и может сыграть ещё одно.
  /// </summary>
  public bool IsRotated(Cell cell) => _rotatedCells.Contains(cell);

  /// <summary>
  /// На сколько процентов снижена вероятность успеха персонажа в ячейке: каждый бездомный рядом с ним
  /// (по горизонтали, вертикали или диагонали) в том же личном штабе снижает её на <see cref="HomelessCard.NeighborSuccessPenalty"/>.
  /// Вне личных штабов вероятность успеха не снижается.
  /// </summary>
  public int SuccessPenaltyAt(Cell cell)
  {
    if (CardAt(cell) is not PersonCard || _playerZones.Values.FirstOrDefault(zone => zone.Contains(cell)) is not { } zone)
      return 0;

    return cell.Surrounding.Count(neighbor => zone.Contains(neighbor) && CardAt(neighbor) is HomelessCard)
      * HomelessCard.NeighborSuccessPenalty;
  }

  /// <summary>
  /// Итоговая вероятность успеха персонажа в ячейке с учётом повышения и снижения (<see cref="SuccessModifierAt"/>),
  /// от 0 до 100.
  /// У карточки, которая не персонаж, — 0.
  /// </summary>
  public int SuccessChanceAt(Cell cell) =>
    CardAt(cell) is PersonCard person
      ? IsFoiled(cell)
        ? SuccessCheck.MaxChance
        : Math.Clamp(person.SuccessChancePercent + SuccessModifierAt(cell), 0, SuccessCheck.MaxChance)
      : 0;

  /// <summary>
  /// На сколько процентов повышена вероятность успеха персонажа в ячейке: каждый учёный рядом с ним
  /// (по горизонтали, вертикали или диагонали) в том же личном штабе повышает её на
  /// <see cref="ScientistPassive.NeighborSuccessBonus"/>, плюс навсегда полученное от тренера (<see cref="Train"/>).
  /// Вне личных штабов вероятность успеха не повышается.
  /// </summary>
  public int SuccessBonusAt(Cell cell)
  {
    if (CardAt(cell) is not PersonCard || _playerZones.Values.FirstOrDefault(zone => zone.Contains(cell)) is not { } zone)
      return 0;

    return cell.Surrounding
      .Where(neighbor => zone.Contains(neighbor) && CardAt(neighbor)?.Passive is ScientistPassive && !IsUsed(neighbor))
      .Sum(scientist => ScientistSuccessBonusOf(scientist))
      + _trainedBonuses.GetValueOrDefault(cell);
  }

  /// <summary>
  /// Проверить, может ли игрок навсегда повысить вероятность успеха персонажа в личном штабе (своём или чужом).
  /// Чужого персонажа под защитой полицейского выбрать нельзя; персонажа со 100% — незачем.
  /// </summary>
  public PlacementCheck CheckTrain(Cell cell, Player player)
  {
    if (CardAt(cell) is not PersonCard || !_playerZones.Values.Any(zone => zone.Contains(cell)))
      return PlacementCheck.Deny("Здесь нет персонажа из личного штаба.");
    if (IsProtected(cell, player))
      return PlacementCheck.Deny("Этого персонажа защищает полицейский.");
    if (SuccessChanceAt(cell) >= SuccessCheck.MaxChance)
      return PlacementCheck.Deny("У этого персонажа уже 100% успеха.");

    return PlacementCheck.Allowed;
  }

  /// <summary>
  /// Пустые ячейки во всех личных штабах: отладочный режим ставит туда персонажей.
  /// </summary>
  public IEnumerable<Cell> EmptyPersonalCells() =>
    _playerZones.Values.SelectMany(zone => zone.Cells).Where(cell => !_cards.ContainsKey(cell));

  /// <summary>
  /// Положить персонажа в режиме разработчика: в пустую ячейку личного штаба — без проверок правил;
  /// в общую зону — по обычным правилам, но без оплаты.
  /// </summary>
  /// <exception cref="InvalidOperationException">Ячейка занята или сюда карточку нельзя положить.</exception>
  public PlacementCheck PlaceForDeveloper(Cell cell, PersonCard card, Player player)
  {
    if (EmptyPersonalCells().Contains(cell))
    {
      _cards[cell] = card;
      return PlacementCheck.Allowed;
    }

    var check = Place(card, cell, player);
    player.Refund(check.Cost);
    return check;
  }

  /// <summary>
  /// Персонажи в личных штабах всех игроков с условиями, на которых игрок может повысить их вероятность успеха.
  /// </summary>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> TrainTargets(Player player) =>
    _playerZones.Values
      .SelectMany(zone => zone.Cells)
      .Where(cell => CardAt(cell) is PersonCard)
      .Select(cell => (cell, CheckTrain(cell, player)));

  /// <summary>
  /// Навсегда повысить вероятность успеха персонажа в личном штабе.
  /// </summary>
  /// <param name="cell">Ячейка персонажа.</param>
  /// <param name="player">Кто повышает.</param>
  /// <param name="bonus">На сколько процентов.</param>
  /// <returns>Персонаж чужой.</returns>
  /// <exception cref="InvalidOperationException">Этого персонажа выбрать нельзя.</exception>
  public bool Train(Cell cell, Player player, int bonus)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bonus);
    var check = CheckTrain(cell, player);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    _trainedBonuses[cell] = _trainedBonuses.GetValueOrDefault(cell) + bonus;
    CardTrained?.Invoke(cell);
    return !_playerZones[player].Contains(cell);
  }

  /// <summary>
  /// Итоговое изменение вероятности успеха персонажа в ячейке: повышение (<see cref="SuccessBonusAt"/>)
  /// минус снижение (<see cref="SuccessPenaltyAt"/>).
  /// </summary>
  public int SuccessModifierAt(Cell cell) => SuccessBonusAt(cell) + CoachBonusAt(cell) - SuccessPenaltyAt(cell);

  /// <summary>
  /// Вероятность успеха без бонуса тренера (<see cref="CoachPassive"/>): по ней тренер выбирает, кого поддержать,
  /// поэтому расчёт не зависит от самого бонуса тренера.
  /// </summary>
  private int BaseSuccessChanceAt(Cell cell) =>
    CardAt(cell) is PersonCard person
      ? IsFoiled(cell)
        ? SuccessCheck.MaxChance
        : Math.Clamp(person.SuccessChancePercent + SuccessBonusAt(cell) - SuccessPenaltyAt(cell), 0, SuccessCheck.MaxChance)
      : 0;

  /// <summary>
  /// На сколько процентов пассивный тренер (<see cref="CoachPassive"/>) повышает вероятность успеха персонажа в ячейке.
  /// Каждый тренер в личном штабе поддерживает одного соседа (см. <see cref="CoachTargets"/>).
  /// </summary>
  public int CoachBonusAt(Cell cell)
  {
    if (CardAt(cell) is not PersonCard || _playerZones.FirstOrDefault(pair => pair.Value.Contains(cell)) is not { Value: { } zone } pair)
      return 0;

    return CoachTargets(pair.Key, zone).Count(target => target == cell) * CoachPassive.SuccessBonus;
  }

  /// <summary>
  /// Персонажи, которых поддерживают тренеры личного штаба: каждый тренер — один сосед с самой низкой вероятностью
  /// успеха (из равных — случайный, но постоянный для ячейки).
  /// </summary>
  private IEnumerable<Cell> CoachTargets(Player owner, Zone zone) =>
    zone.Cells
      .Where(cell => CardAt(cell)?.Passive is CoachPassive)
      .Select(coach => CoachTargetOf(coach, zone))
      .Where(target => target is not null)
      .Select(target => target!.Value);

  private Cell? CoachTargetOf(Cell coach, Zone zone)
  {
    var candidates = coach.Surrounding
      .Where(neighbour => zone.Contains(neighbour) && CardAt(neighbour) is PersonCard && BaseSuccessChanceAt(neighbour) < SuccessCheck.MaxChance)
      .ToList();
    if (candidates.Count == 0)
      return null;

    var lowest = candidates.Min(BaseSuccessChanceAt);
    return candidates.Where(cell => BaseSuccessChanceAt(cell) == lowest).MinBy(TieBreakOf);
  }

  /// <summary>
  /// Случайный, но постоянный для ячейки приоритет при равенстве шансов у соседей тренера.
  /// </summary>
  private int TieBreakOf(Cell cell)
  {
    if (!_tieBreaks.TryGetValue(cell, out var value))
    {
      value = _tieBreakRandom.Next();
      _tieBreaks[cell] = value;
    }

    return value;
  }

  private readonly Dictionary<Cell, int> _tieBreaks = new();
  private readonly Random _tieBreakRandom = new();

  /// <summary>
  /// Сколько $ добавляют учёные (<see cref="ScientistPassive"/>) к деньгам, которые приносит карточка личного штаба:
  /// каждый учёный рядом с ней (по горизонтали, вертикали или диагонали) в том же личном штабе.
  /// </summary>
  public int MoneyBonusAt(Cell cell) => ScientistBaseMoneyAt(cell) + ScientistBoostMoneyAt(cell);

  /// <summary>
  /// Базовый бонус учёных рядом с карточкой (<see cref="ScientistPassive.MoneyBonus"/>, $).
  /// </summary>
  private int ScientistBaseMoneyAt(Cell cell) =>
    ScientistNeighboursAt(cell).Count() * ScientistPassive.MoneyBonus;

  /// <summary>
  /// Бонус «Технологий от инопланетян» (<see cref="ScientistBoostPassive.MoneyBonus"/>, $) от учёных рядом с карточкой.
  /// </summary>
  private int ScientistBoostMoneyAt(Cell cell) =>
    ScientistNeighboursAt(cell)
      .Count(scientist => HoldsPassive<ScientistBoostPassive>(OwnerOfCell(scientist))) * ScientistBoostPassive.MoneyBonus;

  /// <summary>
  /// Учёные рядом с ячейкой в том же личном штабе.
  /// </summary>
  private IEnumerable<Cell> ScientistNeighboursAt(Cell cell) =>
    _playerZones.Values.FirstOrDefault(zone => zone.Contains(cell)) is { } zone
      ? cell.Surrounding.Where(neighbor => zone.Contains(neighbor) && CardAt(neighbor)?.Passive is ScientistPassive && !IsUsed(neighbor))
      : [];

  /// <summary>
  /// Сколько % к успеху соседу даёт ученый в ячейке: базовый бонус учёного и, если владелец держит
  /// «Технологии от инопланетян», он заменяет обычный бонус учёного на <see cref="ScientistBoostPassive.SuccessBonus"/>
  /// (не складывается с ним).
  /// </summary>
  private int ScientistSuccessBonusOf(Cell scientist) =>
    HoldsPassive<ScientistBoostPassive>(OwnerOfCell(scientist))
      ? ScientistBoostPassive.SuccessBonus
      : ScientistPassive.NeighborSuccessBonus;

  /// <summary>
  /// Сколько учёных на поле у игрока: в его личном штабе и в теориях, где он выложил учёного.
  /// </summary>
  public int ScientistsOf(Player player) =>
    (_playerZones.TryGetValue(player, out var zone)
      ? zone.Cells.Count(cell => CardAt(cell)?.Passive is ScientistPassive)
      : 0)
    + _theoryColumns.SelectMany(column => column.MembersOf(player)).Count(member => member.Card.Passive is ScientistPassive);

  /// <summary>
  /// Владелец рабочей зоны, в которой лежит ячейка, или <c>null</c>.
  /// </summary>
  private Player? OwnerOfCell(Cell cell) => _playerZones.Keys.FirstOrDefault(zonePlayer => _playerZones[zonePlayer].Contains(cell));

  /// <summary>
  /// Владеет ли игрок карточкой теории с такой пассивной способностью.
  /// </summary>
  private bool HoldsPassive<T>(Player? player) where T : PassiveAbility =>
    player is not null && _theoryCards.Any(owned => owned.Owner == player && owned.Card.Passive is T);

  /// <summary>
  /// Персонаж защищён «Рептилоидами»: его владелец держит эту карточку теории. Такого персонажа нельзя смотреть,
  /// раскрывать и подменять.
  /// </summary>
  public bool IsImmune(Cell cell)
  {
    var owner = ColumnAt(cell)?.OwnerAt(cell);
    return owner is not null && HoldsPassive<ReptiliansPassive>(owner);
  }

  /// <summary>
  /// Бездомные в личных штабах с условиями, на которых их можно превратить в граждан.
  /// </summary>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> HomelessTargets() =>
    _playerZones
      .SelectMany(zone => zone.Value.Cells)
      .Where(cell => CardAt(cell) is HomelessCard)
      .Select(cell => (cell, CheckTransformHomeless(cell)));

  /// <summary>
  /// Проверить, можно ли превратить бездомного в личном штабе в гражданина. Бездомного под шапочкой из фольги
  /// превратить нельзя: шапочка защищает от любых эффектов.
  /// </summary>
  public PlacementCheck CheckTransformHomeless(Cell cell)
  {
    if (CardAt(cell) is not HomelessCard || OwnerOfCell(cell) is null)
      return PlacementCheck.Deny("Здесь нет бездомного в личном штабе.");
    return IsFoiled(cell)
      ? PlacementCheck.Deny("Этого бездомного защищает шапочка из фольги.")
      : PlacementCheck.Allowed;
  }

  /// <summary>
  /// Проверить, можно ли превратить бездомного из руки игрока в гражданина.
  /// </summary>
  public PlacementCheck CheckTransformHandHomeless(Card card, Player player) =>
    card is HomelessCard && player.Hand.Contains(card)
      ? PlacementCheck.Allowed
      : PlacementCheck.Deny("В руке нет бездомного.");

  /// <summary>
  /// Превратить бездомного из руки игрока в гражданина той же теории; он остаётся на своём месте в руке.
  /// </summary>
  /// <exception cref="InvalidOperationException">Превратить нельзя.</exception>
  public void TransformHandHomeless(Card card, Player player)
  {
    var check = CheckTransformHandHomeless(card, player);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    player.Hand.Replace(card, new CitizenCard(((HomelessCard)card).Theory));
  }

  /// <summary>
  /// Превратить бездомного в личном штабе в гражданина той же теории (владелец карточки остаётся прежним).
  /// </summary>
  /// <exception cref="InvalidOperationException">Превратить нельзя.</exception>
  public void TransformHomeless(Cell cell)
  {
    var check = CheckTransformHomeless(cell);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    var homeless = (HomelessCard)_cards[cell];
    _cards[cell] = new CitizenCard(homeless.Theory);
    CardReplaced?.Invoke(cell);
  }

  /// <summary>
  /// Свои персонажи в личном штабе (кроме <paramref name="source"/>), которым можно убрать или добавить теорию.
  /// </summary>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> ToggleTheoryTargets(Player player, Cell? source) =>
    _playerZones.TryGetValue(player, out var zone)
      ? zone.Cells
        .Where(cell => cell != source && CardAt(cell) is PersonCard)
        .Select(cell => (cell, CheckToggleTheory(cell, player, source)))
      : [];

  /// <summary>
  /// Проверить, можно ли убрать или добавить теорию персонажу в своём личном штабе. Персонажа под шапочкой из фольги
  /// трогать нельзя.
  /// </summary>
  public PlacementCheck CheckToggleTheory(Cell cell, Player player, Cell? source)
  {
    if (CardAt(cell) is not PersonCard person || !_playerZones.TryGetValue(player, out var zone) || !zone.Contains(cell))
      return PlacementCheck.Deny("Выберите персонажа в своём личном штабе.");
    if (cell == source)
      return PlacementCheck.Deny("Выберите другого персонажа.");
    if (WithTheory(person, null) is null)
      return PlacementCheck.Deny($"У персонажа «{person.Name}» теорию поменять нельзя.");
    return IsFoiled(cell)
      ? PlacementCheck.Deny("Этого персонажа защищает шапочка из фольги.")
      : PlacementCheck.Allowed;
  }

  /// <summary>
  /// Проверить, можно ли убрать или добавить теорию персонажу в руке игрока.
  /// </summary>
  public PlacementCheck CheckToggleHandTheory(Card card, Player player) =>
    card is PersonCard person && player.Hand.Contains(card) && WithTheory(person, null) is not null
      ? PlacementCheck.Allowed
      : PlacementCheck.Deny("Выберите персонажа в руке.");

  /// <summary>
  /// Персонажу в своём личном штабе: убрать теорию, если она есть, иначе — добавить случайную.
  /// Карточка остаётся на месте и сохраняет состояние ячейки (сыграна, повёрнута).
  /// </summary>
  /// <returns>Новая теория персонажа или <c>null</c>, если теорию убрали.</returns>
  /// <exception cref="InvalidOperationException">Нельзя.</exception>
  public Theory? ToggleTheory(Cell cell, Player player, Cell? source, Random random)
  {
    var check = CheckToggleTheory(cell, player, source);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    var person = (PersonCard)_cards[cell];
    var theory = ToggledTheory(person, random);
    _cards[cell] = WithTheory(person, theory)!;
    CardReplaced?.Invoke(cell);
    return theory;
  }

  /// <summary>
  /// Персонажу в руке: убрать теорию, если она есть, иначе — добавить случайную. Карточка остаётся на своём месте в руке.
  /// </summary>
  /// <returns>Новая теория персонажа или <c>null</c>, если теорию убрали.</returns>
  /// <exception cref="InvalidOperationException">Нельзя.</exception>
  public Theory? ToggleHandTheory(Card card, Player player, Random random)
  {
    var check = CheckToggleHandTheory(card, player);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    var person = (PersonCard)card;
    var theory = ToggledTheory(person, random);
    player.Hand.Replace(card, WithTheory(person, theory)!);
    return theory;
  }

  private static Theory? ToggledTheory(PersonCard person, Random random) =>
    person.Theory is null ? TheoryCatalog.All[random.Next(TheoryCatalog.All.Count)] : null;

  /// <summary>
  /// Та же карточка персонажа, но с другой теорией; <c>null</c> — для такой карточки теорию поменять нельзя.
  /// </summary>
  private static PersonCard? WithTheory(PersonCard person, Theory? theory) => person switch
  {
    HomelessCard => new HomelessCard(theory),
    CitizenCard => new CitizenCard(theory),
    GenericProfessionalCard professional => new GenericProfessionalCard(professional.Profession, theory),
    _ => null,
  };

  /// <summary>
  /// Бездомные и граждане в теориях, которых можно заменить карточкой с руки.
  /// </summary>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> ReplaceTargets(PersonCard? card, Player player) =>
    _theoryColumns
      .SelectMany(column => column.Cells)
      .Where(cell => CardAt(cell) is HomelessCard or CitizenCard)
      .Select(cell => (cell, CheckReplace(cell, card, player)));

  /// <summary>
  /// Проверить, можно ли заменить бездомного или гражданина в теории карточкой с руки: карточка той же теории,
  /// что и заменяемая (или обе без теории); штаб открыт, и его не защищает полицейский.
  /// </summary>
  public PlacementCheck CheckReplace(Cell cell, PersonCard? card, Player player)
  {
    if (card is null)
      return PlacementCheck.Deny("Сначала выберите карточку в руке.");
    if (!player.Hand.Contains(card))
      return PlacementCheck.Deny("Этой карточки нет в руке.");

    var column = ColumnAt(cell);
    if (column is null || CardAt(cell) is not (HomelessCard or CitizenCard))
      return PlacementCheck.Deny("Заменить можно только бездомного или гражданина в теории.");
    if (column.IsClosed)
      return PlacementCheck.Deny($"Штаб теории «{column}» закрыт — заменять в нём нельзя.");
    if (column.IsProtected())
      return PlacementCheck.Deny("Эту теорию защищает полицейский.");
    if (IsImmune(cell))
      return PlacementCheck.Deny("Персонажей этого игрока защищают рептилоиды.");
    if (card.Theory != CardAt(cell)!.Theory)
      return PlacementCheck.Deny("Карточка с руки должна быть той же теории, что и заменяемая, либо обе без теории.");

    return PlacementCheck.Allowed;
  }

  /// <summary>
  /// Заменить бездомного или гражданина в теории карточкой с руки. Новая карточка становится собственностью игрока,
  /// заменённая уходит из игры.
  /// </summary>
  /// <exception cref="InvalidOperationException">Заменить нельзя.</exception>
  public void Replace(Cell cell, PersonCard card, Player player)
  {
    var check = CheckReplace(cell, card, player);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    player.Hand.Remove(card);
    ReplaceInTheory(ColumnAt(cell)!, cell, card, player);
  }

  /// <summary>
  /// Персонажи в личных штабах других игроков, которых можно забрать в руку.
  /// </summary>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> RivalCharacterTargets(Player player) =>
    _playerZones
      .Where(zone => zone.Key != player)
      .SelectMany(zone => zone.Value.Cells)
      .Where(cell => CardAt(cell) is PersonCard)
      .Select(cell => (cell, CheckTakeRival(cell, player)));

  /// <summary>
  /// Сколько $ сверх стоимости персонажа платят его владельцу, когда чужого персонажа забирают (<see cref="CheckTakeRival"/>).
  /// </summary>
  public const int RivalExtraPayment = 1;

  /// <summary>
  /// Проверить, может ли игрок забрать чужого персонажа из личного штаба, заплатив владельцу его стоимость и 1$.
  /// В Check.Cost — сколько заплатить.
  /// </summary>
  public PlacementCheck CheckTakeRival(Cell cell, Player player)
  {
    var owner = OwnerOfCell(cell);
    if (CardAt(cell) is not PersonCard person || owner is null)
      return PlacementCheck.Deny("Здесь нет персонажа из личного штаба.");
    if (owner == player)
      return PlacementCheck.Deny("Это ваш персонаж — забрать можно только чужого.");
    if (IsProtected(cell, player))
      return PlacementCheck.Deny("Этого персонажа защищает полицейский.");

    var price = person.Cost + RivalExtraPayment;
    return player.CanAfford(price)
      ? PlacementCheck.Allow(price)
      : PlacementCheck.Deny($"Недостаточно денег: нужно {price}$, а у вас {player.Money}$.");
  }

  /// <summary>
  /// Забрать чужого персонажа из личного штаба в руку, заплатив владельцу его стоимость и влияние.
  /// </summary>
  /// <exception cref="InvalidOperationException">Забрать нельзя.</exception>
  public void TakeRival(Cell cell, Player player)
  {
    var check = CheckTakeRival(cell, player);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    var owner = OwnerOfCell(cell)!;
    var card = DetachFromZone(cell);
    player.Spend(check.Cost);
    owner.Earn(check.Cost);
    player.Hand.Add(card);
    CardTakenToHand?.Invoke(cell);
  }

  /// <summary>
  /// Персонажи в личных штабах всех игроков, которых можно убрать из игры.
  /// </summary>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> RemovableCharacterTargets(Player player) =>
    _playerZones
      .SelectMany(zone => zone.Value.Cells)
      .Where(cell => CardAt(cell) is PersonCard)
      .Select(cell => (cell, CheckRemoveCharacter(cell, player)));

  /// <summary>
  /// Проверить, может ли игрок убрать персонажа из личного штаба: своего — всегда, чужого — если его не защищает полицейский.
  /// </summary>
  public PlacementCheck CheckRemoveCharacter(Cell cell, Player player)
  {
    if (CardAt(cell) is not PersonCard || OwnerOfCell(cell) is null)
      return PlacementCheck.Deny("Здесь нет персонажа из личного штаба.");
    if (IsProtected(cell, player))
      return PlacementCheck.Deny("Этого персонажа защищает полицейский.");

    return PlacementCheck.Allowed;
  }

  /// <summary>
  /// Убрать персонажа из личного штаба из игры; игрок получает его стоимость и влияние.
  /// </summary>
  /// <exception cref="InvalidOperationException">Убрать нельзя.</exception>
  public void RemoveCharacter(Cell cell, Player player)
  {
    var check = CheckRemoveCharacter(cell, player);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    var card = DetachFromZone(cell);
    player.Earn(card.Cost + card.Influence);
    CardRemoved?.Invoke(cell);
  }

  /// <summary>
  /// Персонаж прикрыт шапочкой из фольги: карточка лежит на нём до конца раунда.
  /// Такой персонаж имеет 100% успеха и защищён от любых эффектов.
  /// </summary>
  public bool IsFoiled(Cell cell) =>
    CardAt(cell) is PersonCard
    && _theoryCards.Any(owned => owned.Card.Passive is TinFoilPassive && owned.AttachedTo == cell);

  /// <summary>
  /// Освободить ячейку от шапочки из фольги, фишки и руля: карточка остаётся у владельца, но больше не лежит на персонаже.
  /// </summary>
  private void ReleaseFoils(Cell cell)
  {
    foreach (var owned in _theoryCards.Where(owned => owned.AttachedTo == cell))
      owned.AttachedTo = null;
    _chips.Remove(cell);
    _wheels.Remove(cell);
  }

  /// <summary>
  /// Значки руля: персонаж в личном штабе и игрок, который поставил руль (он же владелец персонажа).
  /// </summary>
  public IReadOnlyDictionary<Cell, Player> Wheels => _wheels;

  /// <summary>
  /// Проверить, можно ли поставить значок руля на персонажа в личном штабе игрока.
  /// </summary>
  public PlacementCheck CheckWheel(Cell cell, Player player, IReadOnlyCollection<Cell> acting)
  {
    if (_wheels.ContainsKey(cell))
      return PlacementCheck.Deny("На этом персонаже уже есть руль.");
    return CheckTake(cell, player, acting);
  }

  /// <summary>
  /// Персонажи личного штаба игрока, на которых можно поставить руль.
  /// </summary>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> WheelTargets(Player player, IReadOnlyCollection<Cell> acting) =>
    _playerZones.TryGetValue(player, out var zone)
      ? zone.Cells.Where(cell => CardAt(cell) is PersonCard).Select(cell => (cell, CheckWheel(cell, player, acting)))
      : [];

  /// <summary>
  /// Поставить значок руля на персонажа личного штаба.
  /// </summary>
  /// <exception cref="InvalidOperationException">На этого персонажа руль поставить нельзя.</exception>
  public void MarkForReturn(Cell cell, Player player, IReadOnlyCollection<Cell> acting)
  {
    var check = CheckWheel(cell, player, acting);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    _wheels[cell] = player;
    WheelMarked?.Invoke(cell);
  }

  /// <summary>
  /// После паса игрока персонажи с его значком руля возвращаются к нему на руку.
  /// Персонаж, которого нельзя забрать (например, под шапочкой из фольги), остаётся на месте с рулём.
  /// </summary>
  public void ReturnWheeledCards(Player player)
  {
    var cells = _wheels.Where(pair => pair.Value == player).Select(pair => pair.Key).ToList();
    foreach (var cell in cells)
    {
      if (CheckTake(cell, player, Array.Empty<Cell>()).IsAllowed)
        TakeToHand(cell, player, Array.Empty<Cell>());
    }
  }

  /// <summary>
  /// Персонажи в личных штабах, на которых можно положить шапочку из фольги.
  /// </summary>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> FoilTargets() =>
    _playerZones
      .SelectMany(zone => zone.Value.Cells)
      .Where(cell => CardAt(cell) is PersonCard)
      .Select(cell => (cell, CheckFoil(cell)));

  /// <summary>
  /// Проверить, можно ли положить шапочку из фольги на персонажа: он в личном штабе и на нём её ещё нет.
  /// </summary>
  public PlacementCheck CheckFoil(Cell cell)
  {
    if (CardAt(cell) is not PersonCard || OwnerOfCell(cell) is null)
      return PlacementCheck.Deny("Здесь нет персонажа из личного штаба.");
    if (_theoryCards.Any(owned => owned.Card.Passive is TinFoilPassive && owned.AttachedTo == cell))
      return PlacementCheck.Deny("На этом персонаже уже лежит шапочка из фольги.");

    return PlacementCheck.Allowed;
  }

  /// <summary>
  /// Бездомные, которых можно убрать к игроку на руку: только в личных штабах (любого игрока).
  /// </summary>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> HomelessToHandTargets(Player player) =>
    _cards.Keys
      .Where(cell => CardAt(cell) is HomelessCard && OwnerOfCell(cell) is not null)
      .Select(cell => (cell, CheckTakeHomeless(cell, player)));

  /// <summary>
  /// Проверить, можно ли убрать бездомного к игроку на руку: только из личного штаба и только если его не защищает
  /// полицейский или шапочка из фольги. Из штабов теорий бездомных не забирают.
  /// </summary>
  public PlacementCheck CheckTakeHomeless(Cell cell, Player player)
  {
    if (CardAt(cell) is not HomelessCard)
      return PlacementCheck.Deny("Здесь нет бездомного.");
    if (ColumnAt(cell) is not null)
      return PlacementCheck.Deny("Из штаба теории бездомного забрать нельзя — только из личного штаба.");
    if (OwnerOfCell(cell) is null)
      return PlacementCheck.Deny("Здесь нет бездомного в личном штабе.");

    return IsProtected(cell, player)
      ? PlacementCheck.Deny("Этого бездомного защищает полицейский или шапочка из фольги.")
      : PlacementCheck.Allowed;
  }

  /// <summary>
  /// Убрать бездомного из личного штаба к игроку на руку.
  /// </summary>
  /// <exception cref="InvalidOperationException">Убрать нельзя.</exception>
  public void TakeHomeless(Cell cell, Player player)
  {
    var check = CheckTakeHomeless(cell, player);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    var card = DetachFromZone(cell);
    player.Hand.Add(card);
    CardTakenToHand?.Invoke(cell);
  }

  /// <summary>
  /// Профессионалы в личных штабах, которых можно заменить политиком из колоды.
  /// </summary>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> ProfessionalTargets(Player player) =>
    _playerZones
      .SelectMany(zone => zone.Value.Cells)
      .Where(cell => CardAt(cell) is ProfessionalCard)
      .Select(cell => (cell, CheckReplaceWithPolitician(cell, player)));

  /// <summary>
  /// Проверить, можно ли заменить профессионала в личном штабе политиком.
  /// </summary>
  public PlacementCheck CheckReplaceWithPolitician(Cell cell, Player player)
  {
    if (CardAt(cell) is not ProfessionalCard || OwnerOfCell(cell) is null)
      return PlacementCheck.Deny("Здесь нет профессионала из личного штаба.");
    if (IsProtected(cell, player))
      return PlacementCheck.Deny("Этого профессионала защищает полицейский или шапочка из фольги.");

    return PlacementCheck.Allowed;
  }

  /// <summary>
  /// Заменить профессионала в личном штабе первым попавшимся политиком (не из колоды). Профессионал уходит из игры.
  /// </summary>
  /// <exception cref="InvalidOperationException">Заменить нельзя.</exception>
  public void ReplaceWithPolitician(Cell cell, Player player)
  {
    var check = CheckReplaceWithPolitician(cell, player);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    var politician = new GenericProfessionalCard(Profession.Politician);
    ReleaseFoils(cell);
    _cards[cell] = politician;
    CardReplaced?.Invoke(cell);
  }

  private static bool IsPolitician(PersonCard card) => card is GenericProfessionalCard { Profession: Profession.Politician };

  /// <summary>
  /// Может ли игрок видеть любые закрытые карточки в любой момент (<see cref="BirdsDronesPassive"/>).
  /// </summary>
  public bool CanSeeAllHidden(Player player) => HoldsPassive<BirdsDronesPassive>(player);

  /// <summary>
  /// Персонажи (кроме бездомных и закрытых карточек) в личных штабах и теориях, которых можно понизить до бездомного.
  /// </summary>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> DemoteTargets(Player player) =>
    _cards.Keys
      .Where(cell => CardAt(cell) is PersonCard and not HomelessCard and not HiddenCard)
      .Select(cell => (cell, CheckDemote(cell, player)));

  /// <summary>
  /// Проверить, можно ли понизить персонажа до бездомного: закрытая теория и защищённые персонажи не трогаем,
  /// а за чужого персонажа нужно заплатить его владельцу его влияние (<see cref="DemoteFee"/>).
  /// </summary>
  /// <param name="cell">Ячейка персонажа.</param>
  /// <param name="player">Игрок, который понижает.</param>
  /// <param name="includePolicemanToll">Учесть плату владельцу полицейского, которая берётся до понижения.</param>
  public PlacementCheck CheckDemote(Cell cell, Player player, bool includePolicemanToll = true)
  {
    var check = CheckDemoteTarget(cell, player);
    if (!check.IsAllowed || DemoteFee(cell, player) is not { } fee)
      return check;

    var toll = includePolicemanToll && PolicemanTollFor(cell, player) is { } policeman ? policeman.Amount : 0;
    // Плату показываем в стоимости проверки — по ней цель оценивает и бот.
    return player.CanAfford(fee.Amount + toll)
      ? PlacementCheck.Allow(fee.Amount, check.Column)
      : PlacementCheck.Deny($"Чтобы понизить персонажа, нужно заплатить {fee.Owner} {fee.Amount}$ (его влияние), а у вас {player.Money - toll}$.");
  }

  /// <summary>
  /// Плата за понижение персонажа: игрок платит владельцу пониженного персонажа его влияние.
  /// Своего персонажа понижать бесплатно.
  /// </summary>
  /// <returns>Кому и сколько платить или <c>null</c>, если платить не нужно.</returns>
  public (Player Owner, int Amount)? DemoteFee(Cell cell, Player player)
  {
    if (CardAt(cell) is not PersonCard person)
      return null;

    var owner = ColumnAt(cell)?.OwnerAt(cell) ?? OwnerOfCell(cell);
    return owner is null || owner == player || person.Influence <= 0 ? null : (owner, person.Influence);
  }

  /// <summary>
  /// Можно ли понизить персонажа без учёта платы за понижение.
  /// </summary>
  private PlacementCheck CheckDemoteTarget(Cell cell, Player player)
  {
    if (CardAt(cell) is not PersonCard || CardAt(cell) is HomelessCard or HiddenCard)
      return PlacementCheck.Deny("Здесь нет персонажа, которого можно понизить.");

    if (ColumnAt(cell) is { } column)
    {
      if (column.IsClosed)
        return PlacementCheck.Deny($"Штаб теории «{column}» закрыт — персонажей в нём не понизить.");
      if (column.IsProtected())
        return PlacementCheck.Deny("Эту теорию защищает полицейский.");
      if (IsImmune(cell))
        return PlacementCheck.Deny("Персонажей этого игрока защищают рептилоиды.");
      return PlacementCheck.Allowed;
    }

    if (OwnerOfCell(cell) is null)
      return PlacementCheck.Deny("Здесь нет персонажа из личного штаба.");
    return IsProtected(cell, player)
      ? PlacementCheck.Deny("Этого персонажа защищает полицейский или шапочка из фольги.")
      : PlacementCheck.Allowed;
  }

  /// <summary>
  /// Персонажи в личных штабах, которым можно снизить успех.
  /// </summary>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> WeakenTargets(Player player) =>
    _playerZones
      .SelectMany(zone => zone.Value.Cells)
      .Where(cell => CardAt(cell) is PersonCard)
      .Select(cell => (cell, CheckWeaken(cell, player)));

  /// <summary>
  /// Проверить, можно ли снизить успех персонажа в личном штабе: его не защищает полицейский или шапочка из фольги.
  /// </summary>
  public PlacementCheck CheckWeaken(Cell cell, Player player)
  {
    if (CardAt(cell) is not PersonCard || OwnerOfCell(cell) is null)
      return PlacementCheck.Deny("Здесь нет персонажа из личного штаба.");
    if (IsProtected(cell, player))
      return PlacementCheck.Deny("Этого персонажа защищает полицейский или шапочка из фольги.");

    return PlacementCheck.Allowed;
  }

  /// <summary>
  /// Снизить успех персонажа в личном штабе навсегда (как обратное обучению).
  /// </summary>
  /// <exception cref="InvalidOperationException">Снизить нельзя.</exception>
  public void Weaken(Cell cell, Player player, int reduction)
  {
    var check = CheckWeaken(cell, player);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    _trainedBonuses[cell] = _trainedBonuses.GetValueOrDefault(cell) - reduction;
    CardTrained?.Invoke(cell);
  }

  /// <summary>
  /// Понизить персонажа до бездомного той же теории (владелец остаётся прежним).
  /// </summary>
  /// <exception cref="InvalidOperationException">Понизить нельзя.</exception>
  public void Demote(Cell cell, Player player)
  {
    // Плата владельцу полицейского к этому моменту уже взята.
    var check = CheckDemote(cell, player, includePolicemanToll: false);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    if (DemoteFee(cell, player) is { } fee)
    {
      player.Spend(fee.Amount);
      using (fee.Owner.EarningFrom("Понижение персонажа"))
        EarnAt(cell, fee.Owner, fee.Amount);
    }

    var card = (PersonCard)_cards[cell];
    var homeless = new HomelessCard(card.Theory);
    if (ColumnAt(cell) is { } column)
    {
      ReplaceInTheory(column, cell, homeless);
      return;
    }

    ReleaseFoils(cell);
    _cards[cell] = homeless;
    CardReplaced?.Invoke(cell);
  }

  /// <summary>
  /// Пассивная способность «Эболы»: когда другой игрок добавляет лидера в теорию с персонажем владельца, владелец
  /// получает <see cref="EbolaPassive.HolderBonus"/>$, а добавивший платит ему <see cref="EbolaPassive.LeaderPayment"/>$.
  /// </summary>
  private void PayEbolaPassive(TheoryColumn column, Player newLeader)
  {
    foreach (var owned in _theoryCards.Where(owned => owned.Card.Passive is EbolaPassive).ToList())
    {
      var holder = owned.Owner;
      if (holder == newLeader || !column.Members.Any(member => member.Owner == holder))
        continue;

      EarnForTheory(owned, holder, EbolaPassive.HolderBonus);
      if (newLeader.CanAfford(EbolaPassive.LeaderPayment))
      {
        newLeader.Spend(EbolaPassive.LeaderPayment);
        EarnForTheory(owned, holder, EbolaPassive.LeaderPayment);
      }
    }
  }

  /// <summary>
  /// Сколько $ добавляет карточка теории «Сатанисты похищают детей» бездомному, который приносит деньги.
  /// </summary>
  private int HomelessMoneyBonusAt(Cell cell) =>
    CardAt(cell) is HomelessCard && _theoryCards.Any(owned => owned.Card.Passive is SatanistsPassive)
      ? SatanistsPassive.HomelessBonus
      : 0;

  /// <summary>
  /// Сколько $ добавляет карточка теории «Двойники президентов» политику, который приносит деньги.
  /// </summary>
  private int PoliticianMoneyBonusAt(Cell cell) =>
    CardAt(cell) is PersonCard card && IsPolitician(card) && OwnerOfCell(cell) is { } owner && HoldsPassive<PresidentsPassive>(owner)
      ? PresidentsPassive.PoliticianBonus
      : 0;

  /// <summary>
  /// Убрать карточку из личного штаба: она пропадает вместе с адептами, сыгранностью и бонусами на ней.
  /// </summary>
  private PersonCard DetachFromZone(Cell cell)
  {
    var card = (PersonCard)_cards[cell];
    ReleaseFoils(cell);
    _cards.Remove(cell);
    _usedCells.Remove(cell);
    _rotatedCells.Remove(cell);
    _cardAdepts.Remove(cell);
    _trainedBonuses.Remove(cell);
    return card;
  }

  /// <summary>
  /// Может ли игрок использовать карточку в ячейке: только ещё не использованную карточку из своей зоны.
  /// </summary>
  public bool CanUse(Cell cell, Player player) =>
    _cards.ContainsKey(cell)
    && !IsUsed(cell)
    && ((_playerZones.TryGetValue(player, out var zone) && zone.Contains(cell)) || IsChippedBy(cell, player));

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

    context = context.WithSourceCell(cell);
    var card = _cards[cell];
    if (!card.OnPlayFromHand.Options.Contains(option))
      throw new ArgumentException($"У карточки «{card}» нет действия «{option}».", nameof(option));

    // Проверка успеха уже пройдена: даже если действие не удалось выполнить (цели не подошли, эффект отказал на полпути),
    // карточка считается сыгранной — иначе её можно было бы сыграть ещё раз с новым броском.
    try
    {
      RequireTargets(option, context);
      PayPolicemanTolls(option, context);
      ExecuteWithMoneyBonus(cell, option, context);
      // Чипированного персонажа использовали как своего: по 1$ за каждого чипированного персонажа в игре.
      if (IsChippedBy(cell, context.CurrentPlayer))
        context.CurrentPlayer.Earn(_chips.Count);
    }
    finally
    {
      if (_cards.ContainsKey(cell))
        Play(cell, option.IsQuick);
    }
  }

  /// <summary>
  /// Проверить, что в контексте выбраны все цели, которые нужны варианту действия.
  /// </summary>
  private void RequireTargets(Effect option, GameContext context)
  {
    if (option.RequiresTheory)
      RequireTheoryTarget(option, context);
    if (option.RequiresHandCard && (context.HandCard is not { } handCard || !context.CurrentPlayer.Hand.Contains(handCard)))
      throw new ArgumentException($"Для действия «{option}» нужно выбрать карточку в руке.", nameof(context));
    if (option.AcceptsHandCardTarget && context.HandCard is { } targetInHand)
    {
      if (!context.CurrentPlayer.Hand.Contains(targetInHand) || !option.CheckHandCard(targetInHand, context).IsAllowed)
        throw new ArgumentException($"Для действия «{option}» нельзя выбрать эту карточку в руке.", nameof(context));
    }
    else if (option.RequiresTargetCell && (option.BorrowsTargetCard ? context.BorrowedCell : context.TargetCell) is null)
      throw new ArgumentException($"Для действия «{option}» нужно выбрать карточку на поле.", nameof(context));
    if (option.RequiresTargetPlayer && (context.TargetPlayer is not { } target || target == context.CurrentPlayer))
      throw new ArgumentException($"Для действия «{option}» нужно выбрать другого игрока.", nameof(context));
    if (option.RequiresSourceTheory)
    {
      if (context.SourceTheory is not { } source || !option.CanTargetSourceTheory(source, context.CurrentPlayer))
        throw new ArgumentException($"Для действия «{option}» нужно выбрать подходящую теорию-источник.", nameof(context));
      if (source == context.TargetTheory)
        throw new ArgumentException($"Для действия «{option}» теории-источник и цель должны быть разными.", nameof(context));
    }
  }

  /// <summary>
  /// Выполнить действие карточки личного штаба. Если оно принесло игроку деньги, учёные добавляют свой бонус.
  /// </summary>
  private void ExecuteWithMoneyBonus(Cell cell, Effect option, GameContext context)
  {
    var player = context.CurrentPlayer;
    var earned = 0;
    void OnEarned(Player earner, int amount) => earned += amount;

    player.Earned += OnEarned;
    try
    {
      option.Execute(context);
    }
    finally
    {
      player.Earned -= OnEarned;
    }

    if (earned == 0)
      return;

    // Деньги принесла карточка, действие которой выполнено: при использовании другой карточки — она.
    var source = option.BorrowsTargetCard && context.BorrowedCell is { } borrowed ? borrowed : cell;
    // Бонус дают учёные рядом с карточкой, принёсшей деньги, если она в личном штабе игрока; иначе (чужую карточку
    // использовали или скопировали) — учёные рядом с карточкой, которая действовала.
    var bonusCell = _playerZones[player].Contains(source) ? source : cell;
    // Бонус учёных — их базовые $; всё, что дают теории (в том числе «Технологии от инопланетян»), — бонус теорий.
    var scientistBonus = ScientistBaseMoneyAt(bonusCell);
    var theoryBonus = ScientistBoostMoneyAt(bonusCell) + ProfessionMoneyBonusAt(source) + HomelessMoneyBonusAt(source) + PoliticianMoneyBonusAt(source);
    player.Earn(scientistBonus + theoryBonus);
    // Деньги всплывают над карточкой, которая выполнила действие (своей у текущего игрока), даже если действие
    // было за чужую карточку.
    CardEarned?.Invoke(cell, earned, scientistBonus, theoryBonus);
  }

  private void RequireTheoryTarget(Effect option, GameContext context)
  {
    if (context.TargetAdeptCard is { } card)
    {
      if (!option.CanTargetAdeptCard(card, this))
        throw new ArgumentException($"Для действия «{option}» нельзя выбрать эту карточку в личном штабе.", nameof(context));
      if (CheckAdeptCard(card, context.CurrentPlayer) is { IsAllowed: false } check)
        throw new ArgumentException(check.Reason, nameof(context));
      return;
    }

    if (option.TheoryCount > 1)
    {
      if (context.TargetTheories.Count != option.TheoryCount
        || !option.AllowsRepeatedTheories && context.TargetTheories.Distinct().Count() != option.TheoryCount)
        throw new ArgumentException(option.AllowsRepeatedTheories
          ? $"Для действия «{option}» нужно выбрать теорию {option.TheoryCount} раза."
          : $"Для действия «{option}» нужно выбрать {option.TheoryCount} разные теории.", nameof(context));
      if (context.TargetTheories.Any(theory => !option.CanTargetTheory(theory, context.CurrentPlayer)))
        throw new ArgumentException($"Для действия «{option}» нельзя выбрать одну из теорий.", nameof(context));
      return;
    }

    if (context.TargetTheory is not { } theory)
      throw new ArgumentException($"Для действия «{option}» нужно выбрать теорию.", nameof(context));
    if (!option.CanTargetTheory(theory, context.CurrentPlayer))
      throw new ArgumentException($"Для действия «{option}» нельзя выбрать теорию «{theory}».", nameof(context));
  }

  /// <summary>
  /// Защищена ли карточка в личном штабе от действий игрока: рядом с ней (по горизонтали, вертикали или диагонали)
  /// в том же личном штабе лежит несыгранный полицейский (<see cref="PolicemanPassive"/>), а игрок — не владелец штаба.
  /// Сам полицейский соседей защищает, но сам не защищён.
  /// </summary>
  /// <param name="cell">Ячейка карточки.</param>
  /// <param name="player">Игрок, который действует; <c>null</c> — защищена ли карточка от других игроков вообще.</param>
  public bool IsProtected(Cell cell, Player? player = null)
  {
    // Шапочка из фольги защищает персонажа от любых эффектов, в том числе своего владельца.
    if (IsFoiled(cell))
      return true;

    var owner = _playerZones.Keys.FirstOrDefault(zonePlayer => _playerZones[zonePlayer].Contains(cell));
    if (owner is null || owner == player || CardAt(cell) is null)
      return false;

    var zone = _playerZones[owner];
    return cell.Surrounding.Any(neighbor =>
      zone.Contains(neighbor) && CardAt(neighbor)?.Passive is PolicemanPassive && !IsUsed(neighbor));
  }

  /// <summary>
  /// Сколько $ платит владельцу полицейского игрок, который применяет к нему эффект (<see cref="PolicemanTollFor"/>).
  /// </summary>
  public const int PolicemanToll = 3;

  /// <summary>
  /// Плата за эффект, применённый к карточке: если в ячейке открытый полицейский (<see cref="PolicemanPassive"/>)
  /// другого игрока — в личном штабе или в штабе теории, — действующий игрок платит его владельцу <see cref="PolicemanToll"/>$.
  /// </summary>
  /// <param name="cell">Ячейка карточки.</param>
  /// <param name="player">Игрок, который применяет эффект.</param>
  /// <returns>Владелец полицейского и сумма или <c>null</c>, если платить не нужно.</returns>
  public (Player Owner, int Amount)? PolicemanTollFor(Cell cell, Player player)
  {
    if (CardAt(cell) is not PersonCard { Passive: PolicemanPassive })
      return null;

    var owner = ColumnAt(cell)?.OwnerAt(cell)
      ?? _playerZones.Keys.FirstOrDefault(zonePlayer => _playerZones[zonePlayer].Contains(cell));
    return owner is null || owner == player ? null : (owner, PolicemanToll);
  }

  /// <summary>
  /// Добавить к условиям выбора карточки для действия плату владельцу полицейского (<see cref="PolicemanTollFor"/>):
  /// она прибавляется к стоимости, а если денег не хватает — выбрать карточку нельзя.
  /// </summary>
  /// <param name="check">Условия выбора без платы.</param>
  /// <param name="cell">Ячейка карточки.</param>
  /// <param name="effect">Действие.</param>
  /// <param name="context">Контекст партии.</param>
  public PlacementCheck WithPolicemanToll(PlacementCheck check, Cell cell, Effect effect, GameContext context)
  {
    if (!check.IsAllowed || PolicemanTollFor(cell, context.CurrentPlayer) is not { } toll)
      return check;

    // Вторая карточка выбирается, когда первая уже в контексте: за неё тоже придётся заплатить.
    var pending = effect.RequiresSecondTargetCell && context.TargetCell is { } first && first != cell
      ? PolicemanTollFor(first, context.CurrentPlayer)?.Amount ?? 0
      : 0;
    var player = context.CurrentPlayer;
    var total = check.Cost + toll.Amount;
    return player.CanAfford(total + pending)
      ? PlacementCheck.Allow(total, check.Column, check.CreatesHeadquarters)
      : PlacementCheck.Deny(
        $"Это полицейский игрока {toll.Owner}: применить к нему действие стоит {toll.Amount}$ владельцу, а у вас {player.Money}$.");
  }

  /// <summary>
  /// Заплатить владельцам чужих полицейских, к которым применяется действие (<see cref="PolicemanTollFor"/>):
  /// к выбранной карточке на поле, второй карточке или карточке, которую используют как свою.
  /// Вызывается после проверки целей, перед выполнением действия.
  /// </summary>
  /// <param name="effect">Действие.</param>
  /// <param name="context">Контекст партии с выбранными целями.</param>
  /// <exception cref="InvalidOperationException">Не хватает денег.</exception>
  public void PayPolicemanTolls(Effect effect, GameContext context)
  {
    if (!effect.RequiresTargetCell)
      return;

    Cell?[] targets = effect.BorrowsTargetCard
      ? [context.BorrowedCell]
      : [context.TargetCell, effect.RequiresSecondTargetCell ? context.SecondTargetCell : null];
    var player = context.CurrentPlayer;
    var tolls = targets.OfType<Cell>().Distinct()
      .Select(cell => (Cell: cell, Toll: PolicemanTollFor(cell, player)))
      .Where(target => target.Toll is not null)
      .Select(target => (target.Cell, target.Toll!.Value.Owner, target.Toll.Value.Amount))
      .ToList();
    var total = tolls.Sum(toll => toll.Amount);
    if (!player.CanAfford(total))
      throw new InvalidOperationException($"Недостаточно денег, чтобы заплатить владельцу полицейского: нужно {total}$, а у вас {player.Money}$.");

    foreach (var (cell, owner, amount) in tolls)
    {
      player.Spend(amount);
      using (owner.EarningFrom("Полицейский"))
        EarnAt(cell, owner, amount);
    }
  }

  /// <summary>
  /// Инспекция личного штаба другого игрока: инспектор получает <paramref name="moneyPerHomeless"/>$ за каждого
  /// бездомного в штабе, затем один незащищённый несыгранный бездомный с самой низкой вероятностью успеха
  /// (из равных — случайный) переворачивается рубашкой вверх, как сыгранный.
  /// </summary>
  /// <param name="target">Чей личный штаб проверяют.</param>
  /// <param name="inspector">Кто проводит инспекцию.</param>
  /// <param name="moneyPerHomeless">Сколько $ за каждого бездомного.</param>
  /// <param name="random">Источник случайности.</param>
  /// <returns>Ячейка перевёрнутого бездомного или <c>null</c>, если перевернуть некого.</returns>
  public Cell? Inspect(Player target, Player inspector, int moneyPerHomeless, Random random)
  {
    if (target == inspector || !_playerZones.TryGetValue(target, out var zone))
      throw new ArgumentException("Инспектировать можно только личный штаб другого игрока.", nameof(target));

    // Деньги платят только за открытых бездомных: лежащих рубашкой вверх не считаем.
    var homeless = zone.Cells.Where(cell => CardAt(cell) is HomelessCard && !IsUsed(cell)).ToList();
    inspector.Earn(homeless.Count * moneyPerHomeless);

    var candidates = homeless.Where(cell => !IsUsed(cell) && !IsProtected(cell, inspector)).ToList();
    if (candidates.Count == 0)
      return null;

    var lowest = candidates.Min(SuccessChanceAt);
    var weakest = candidates.Where(cell => SuccessChanceAt(cell) == lowest).ToList();
    var flipped = weakest[random.Next(weakest.Count)];
    _rotatedCells.Remove(flipped);
    _usedCells.Add(flipped);
    return flipped;
  }

  /// <summary>
  /// Проверить, может ли игрок использовать чужого непрофессионала (не <see cref="ProfessionalCard"/>)
  /// в личном штабе другого игрока: только ещё не сыгранную карточку.
  /// </summary>
  public PlacementCheck CheckBorrow(Cell cell, Player player)
  {
    var owner = _playerZones.Keys.FirstOrDefault(zonePlayer => _playerZones[zonePlayer].Contains(cell));
    if (CardAt(cell) is not PersonCard person || owner is null)
      return PlacementCheck.Deny("Здесь нет карточки из личного штаба.");
    if (owner == player)
      return PlacementCheck.Deny("Это ваша карточка — использовать можно только персонажа другого игрока.");
    if (person is ProfessionalCard)
      return PlacementCheck.Deny("Профессионала другого игрока использовать нельзя — только непрофессионала.");
    if (IsUsed(cell))
      return PlacementCheck.Deny("Эта карточка уже сыграна.");
    if (IsProtected(cell, player))
      return PlacementCheck.Deny("Эту карточку защищает полицейский.");

    return PlacementCheck.Allowed;
  }

  /// <summary>
  /// Несыгранные непрофессионалы в личных штабах других игроков, которых игрок может использовать.
  /// </summary>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> BorrowTargets(Player player) =>
    _playerZones
      .Where(zone => zone.Key != player)
      .SelectMany(zone => zone.Value.Cells)
      .Where(cell => CardAt(cell) is PersonCard and not ProfessionalCard && !IsUsed(cell))
      .Select(cell => (cell, CheckBorrow(cell, player)));

  /// <summary>
  /// Использовать чужого непрофессионала: выполнить для игрока выбранный вариант действия карточки
  /// (<see cref="GameContext.BorrowedOption"/>), после чего карточка другого игрока переворачивается, как сыгранная.
  /// </summary>
  /// <param name="cell">Ячейка чужой карточки.</param>
  /// <param name="context">Контекст партии с выбранным вариантом действия и его целями.</param>
  /// <exception cref="InvalidOperationException">Карточку использовать нельзя.</exception>
  public void UseRival(Cell cell, GameContext context)
  {
    var check = CheckBorrow(cell, context.CurrentPlayer);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    ExecuteBorrowed(cell, context);
    _rotatedCells.Remove(cell);
    _usedCells.Add(cell);
  }

  /// <summary>
  /// Проверить, может ли игрок скопировать действие карточки в личном штабе другого игрока (<see cref="CopyRivalCardEffect"/>):
  /// только открытой (несыгранной) незащищённой карточки с вариантом действия, который можно так выполнить.
  /// </summary>
  public PlacementCheck CheckCopy(Cell cell, Player player)
  {
    var owner = _playerZones.Keys.FirstOrDefault(zonePlayer => _playerZones[zonePlayer].Contains(cell));
    if (CardAt(cell) is not PersonCard person || owner is null)
      return PlacementCheck.Deny("Здесь нет карточки из личного штаба.");
    if (IsUsed(cell))
      return PlacementCheck.Deny("Эта карточка уже сыграна — скопировать можно только открытую.");
    if (IsProtected(cell, player))
      return PlacementCheck.Deny("Эту карточку защищает полицейский.");
    if (!person.OnPlayFromHand.Options.Any(option => option.CanBeBorrowed))
      return PlacementCheck.Deny($"Действие «{person.Name}» скопировать нельзя.");

    return PlacementCheck.Allowed;
  }

  /// <summary>
  /// Открытые карточки в личных штабах других игроков с условиями, на которых их действие можно скопировать.
  /// </summary>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> CopyTargets(Player player) =>
    _playerZones
      .SelectMany(zone => zone.Value.Cells)
      .Where(cell => CardAt(cell) is PersonCard && !IsUsed(cell))
      .Select(cell => (cell, CheckCopy(cell, player)));

  /// <summary>
  /// Скопировать действие карточки другого игрока: выполнить для игрока выбранный вариант её действия
  /// (<see cref="GameContext.BorrowedOption"/>). Чужая карточка не переворачивается.
  /// </summary>
  /// <exception cref="InvalidOperationException">Действие карточки скопировать нельзя.</exception>
  public void CopyRival(Cell cell, GameContext context)
  {
    var check = CheckCopy(cell, context.CurrentPlayer);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    // Вариант, который копируем: выбранный через чужое действие (CopiedOption) или прямо (BorrowedOption).
    var option = context.CopiedOption ?? context.BorrowedOption;
    // Копирование не копирует копирование: иначе копия вызывала бы саму себя бесконечно.
    if (option is CopyRivalCardEffect)
      throw new InvalidOperationException("Копирование скопировать нельзя.");
    ExecuteBorrowed(cell, context, option);
  }

  /// <summary>
  /// Проверить, может ли игрок сыграть своего персонажа из личного штаба через другую карточку
  /// (<see cref="PlayOwnCardEffect"/>): ещё не сыгранного и с вариантом действия, который можно так выполнить.
  /// </summary>
  public PlacementCheck CheckOwnPlay(Cell cell, Player player)
  {
    if (!CanUse(cell, player))
      return PlacementCheck.Deny("Здесь нет вашего несыгранного персонажа из личного штаба.");
    if (!_cards[cell].OnPlayFromHand.Options.Any(option => option.CanBeBorrowed))
      return PlacementCheck.Deny($"Действие «{_cards[cell].Name}» так сыграть нельзя.");

    return PlacementCheck.Allowed;
  }

  /// <summary>
  /// Свои персонажи в личном штабе, которых игрок может сыграть через другую карточку.
  /// </summary>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> OwnPlayTargets(Player player) =>
    _playerZones.TryGetValue(player, out var zone)
      ? zone.Cells
        .Select(cell => (cell, CheckOwnPlay(cell, player)))
        .Where(target => target.Item2.IsAllowed)
      : [];

  /// <summary>
  /// Сыграть своего персонажа из личного штаба без проверки успеха: выполнить выбранный вариант его действия
  /// (<see cref="GameContext.BorrowedOption"/>), после чего персонаж переворачивается (или поворачивается
  /// после быстрого действия), как сыгранный.
  /// </summary>
  /// <exception cref="InvalidOperationException">Персонажа так сыграть нельзя.</exception>
  public void PlayOwn(Cell cell, GameContext context)
  {
    var check = CheckOwnPlay(cell, context.CurrentPlayer);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    var option = ExecuteBorrowed(cell, context);
    Play(cell, option.IsQuick);
  }

  /// <summary>
  /// Выполнить выбранный вариант действия карточки в ячейке (<see cref="GameContext.BorrowedOption"/>).
  /// </summary>
  /// <returns>Выполненный вариант.</returns>
  private Effect ExecuteBorrowed(Cell cell, GameContext context, Effect? chosen = null)
  {
    var card = _cards[cell];
    var option = chosen ?? context.BorrowedOption ?? throw new ArgumentException("Не выбрано действие карточки.", nameof(context));
    if (!card.OnPlayFromHand.Options.Contains(option))
      throw new ArgumentException($"У карточки «{card}» нет действия «{option}».", nameof(context));
    if (!option.CanBeBorrowed)
      throw new ArgumentException($"Действие «{option}» так выполнить нельзя.", nameof(context));
    RequireTargets(option, context);
    PayPolicemanTolls(option, context);

    option.Execute(context);
    return option;
  }

  /// <summary>
  /// Карточка из личного штаба игрока сыграна неудачно: действие не выполняется, а карточка переворачивается
  /// рубашкой вверх — или, если это было быстрое действие, поворачивается, как после успеха.
  /// </summary>
  /// <param name="cell">Ячейка карточки.</param>
  /// <param name="player">Игрок, который играл карточку.</param>
  /// <param name="quick">Это было быстрое действие (<see cref="Effect.IsQuick"/>).</param>
  /// <param name="die">Выпавшая грань кубика: на низкой грани проваленная карточка может уйти из игры или понизиться.</param>
  /// <exception cref="InvalidOperationException">Карточку нельзя использовать.</exception>
  public void Waste(Cell cell, Player player, bool quick, int die)
  {
    if (!CanUse(cell, player))
      throw new InvalidOperationException("Эту карточку нельзя использовать.");

    // Пассивная способность «Ленина — гриба»: неудачная проверка своего персонажа приносит деньги.
    var mushrooms = _theoryCards.Count(owned => owned.Owner == player && owned.Card.Passive is LeninMushroomPassive);
    if (mushrooms > 0)
      player.Earn(mushrooms * LeninMushroomPassive.MoneyPerFailure);

    if (FailureConsequences)
    {
      switch (_cards[cell])
      {
        case HomelessCard when die <= HomelessDestroyDie:
          DestroyFailedHomeless(cell);
          return;
        case CitizenCard when die == CitizenDemoteDie:
          DemoteFailedCitizen(cell);
          break;
      }
    }

    Play(cell, quick);
  }

  /// <summary>
  /// Проваленный бездомный уходит из игры вместе с тем, что было связано с его местом.
  /// </summary>
  private void DestroyFailedHomeless(Cell cell)
  {
    DetachFromZone(cell);
    _chips.Remove(cell);
    _wheels.Remove(cell);
    PersonalCardDestroyed?.Invoke(cell);
  }

  /// <summary>
  /// Проваленный гражданин становится бездомным той же теории (владелец остаётся прежним).
  /// </summary>
  private void DemoteFailedCitizen(Cell cell)
  {
    var citizen = (CitizenCard)_cards[cell];
    ReleaseFoils(cell);
    _cards[cell] = new HomelessCard(citizen.Theory);
    PersonalCardDemoted?.Invoke(cell);
  }

  /// <summary>
  /// Карточка сыграла действие: после первого быстрого действия она поворачивается, иначе переворачивается.
  /// </summary>
  private void Play(Cell cell, bool quick)
  {
    if (quick && _rotatedCells.Add(cell))
      return;

    _rotatedCells.Remove(cell);
    _usedCells.Add(cell);
  }

  /// <summary>
  /// Поставить мипл-лидер игрока в теорию (<see cref="TheoryColumn.LeaderMeeples"/>).
  /// </summary>
  /// <exception cref="InvalidOperationException">Штаб теории закрыт или защищён полицейским.</exception>
  public void AddLeaderMeeple(TheoryColumn column, Player player)
  {
    if (!_theoryColumns.Contains(column))
      throw new ArgumentException($"Теория «{column}» не выложена на это поле.", nameof(column));
    if (column.IsClosed)
      throw new InvalidOperationException($"Штаб теории «{column}» закрыт.");
    if (column.IsProtected())
      throw new InvalidOperationException($"Теорию «{column}» защищает полицейский.");

    // Пассивная способность «Коммунистов на Луне» действует только на её владельца: он ставит два мипла.
    var meeples = HoldsPassive<CommunistsOnMoonPassive>(player)
      ? CommunistsOnMoonPassive.MeeplesPerLeader
      : 1;
    for (var i = 0; i < meeples; i++)
    {
      column.AddLeaderMeeple(player);
      LeaderMeepleAdded?.Invoke(column, player);
    }
    UpdateTheoryCards();
    PayEbolaPassive(column, player);
  }

  /// <summary>
  /// Снять миплов-лидеров со всех теорий.
  /// </summary>
  public void RemoveLeaderMeeples()
  {
    var columns = _theoryColumns.Where(column => column.LeaderMeeples.Count > 0).ToList();
    foreach (var column in columns)
      column.ClearLeaderMeeples();
    if (columns.Count > 0)
    {
      LeaderMeeplesRemoved?.Invoke(columns);
      // Снятые миплы могли быть единственным влиянием владельца теории: пересчитываем владельцев и карточки теорий.
      UpdateTheoryCards();
    }
  }

  /// <summary>
  /// Добавить адептов в выложенную теорию. Каждый открытый Мастер теорий в стопке (<see cref="TheoryMasterPassive"/>)
  /// приводит ещё <see cref="TheoryMasterPassive.AdeptsBonus"/>.
  /// </summary>
  /// <param name="column">Теория.</param>
  /// <param name="count">Количество адептов.</param>
  /// <returns>Сколько адептов пришло с бонусом мастеров.</returns>
  public int AddAdepts(TheoryColumn column, int count)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
    if (!_theoryColumns.Contains(column))
      throw new ArgumentException($"Теория «{column}» не выложена на это поле.", nameof(column));
    if (column.IsClosed)
      throw new InvalidOperationException($"Штаб теории «{column}» закрыт — адепты в него не приходят.");

    var total = count + column.Cards.Count(card => card.Passive is TheoryMasterPassive) * TheoryMasterPassive.AdeptsBonus;
    column.AddAdepts(total);
    AdeptsAdded?.Invoke(column, total);
    return total;
  }

  /// <summary>
  /// Перевести адептов из одной действующей теории в другую.
  /// </summary>
  /// <exception cref="InvalidOperationException">Перевести нельзя: штаб закрыт или адептов не хватает.</exception>
  public void MoveAdepts(TheoryColumn from, TheoryColumn to, int count)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
    if (!_theoryColumns.Contains(from) || !_theoryColumns.Contains(to))
      throw new ArgumentException("Теория не выложена на это поле.");
    if (from == to)
      throw new ArgumentException("Теории должны быть разными.");
    if (from.IsClosed || to.IsClosed)
      throw new InvalidOperationException("Штаб теории закрыт — адептов переводить нельзя.");
    if (from.Adepts < count)
      throw new InvalidOperationException($"В теории «{from}» нет {count} адептов.");
    if (from.AdeptsShielded)
      throw new InvalidOperationException($"На адептов в теории «{from}» не действуют эффекты.");

    from.RemoveAdepts(count);
    to.AddAdepts(count);
    AdeptsMoved?.Invoke(from, to, count);
  }

  /// <summary>
  /// Убрать адептов из теории: они уходят из игры.
  /// </summary>
  /// <exception cref="InvalidOperationException">Адептов не хватает.</exception>
  public void RemoveAdepts(TheoryColumn column, int count)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
    if (!_theoryColumns.Contains(column))
      throw new ArgumentException($"Теория «{column}» не выложена на это поле.", nameof(column));
    if (column.Adepts < count)
      throw new InvalidOperationException($"В теории «{column}» нет {count} адептов.");
    if (column.AdeptsShielded)
      throw new InvalidOperationException($"На адептов в теории «{column}» не действуют эффекты.");

    column.RemoveAdepts(count);
    AdeptsRemoved?.Invoke(column, count);
  }

  /// <summary>
  /// Проверить, может ли игрок забрать в руку своего персонажа из личного штаба.
  /// </summary>
  /// <param name="cell">Ячейка персонажа.</param>
  /// <param name="player">Игрок.</param>
  /// <param name="acting">Карточки, которые выполняют действие: их забрать нельзя.</param>
  public PlacementCheck CheckTake(Cell cell, Player player, IReadOnlyCollection<Cell> acting)
  {
    if (IsFoiled(cell))
      return PlacementCheck.Deny("Этого персонажа защищает шапочка из фольги.");
    if (CardAt(cell) is not PersonCard || !_playerZones.TryGetValue(player, out var zone) || !zone.Contains(cell))
      return PlacementCheck.Deny("Забрать можно только своего персонажа из личного штаба.");
    if (acting.Contains(cell))
      return PlacementCheck.Deny("Эта карточка сама выполняет действие — её забрать нельзя.");

    return PlacementCheck.Allowed;
  }

  /// <summary>
  /// Свои персонажи в личном штабе, которых игрок может забрать в руку.
  /// </summary>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> TakeTargets(Player player, IReadOnlyCollection<Cell> acting) =>
    _playerZones.TryGetValue(player, out var zone)
      ? zone.Cells.Where(cell => CardAt(cell) is PersonCard).Select(cell => (cell, CheckTake(cell, player, acting)))
      : [];

  /// <summary>
  /// Забрать своего персонажа из личного штаба в руку. Всё, что было связано с его местом, пропадает:
  /// сыгранность, адепты на карточке, навсегда полученная прибавка к успеху.
  /// </summary>
  /// <exception cref="InvalidOperationException">Этого персонажа забрать нельзя.</exception>
  public void TakeToHand(Cell cell, Player player, IReadOnlyCollection<Cell> acting)
  {
    var check = CheckTake(cell, player, acting);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    var card = _cards[cell];
    ReleaseFoils(cell);
    _cards.Remove(cell);
    _usedCells.Remove(cell);
    _rotatedCells.Remove(cell);
    _cardAdepts.Remove(cell);
    _trainedBonuses.Remove(cell);
    player.Hand.Add(card);
    CardTakenToHand?.Invoke(cell);
  }

  /// <summary>
  /// Сколько адептов на карточке в личном штабе.
  /// </summary>
  public int AdeptsAt(Cell cell) => _cardAdepts.GetValueOrDefault(cell);

  /// <summary>
  /// Проверить, можно ли игроку привести адептов на карточку: только на свою карточку в личном штабе,
  /// у которой есть <see cref="PersonCard.AdeptsBonus"/>.
  /// </summary>
  public PlacementCheck CheckAdeptCard(Cell cell, Player player)
  {
    if (CardAt(cell) is not PersonCard { AdeptsBonus: not null })
      return PlacementCheck.Deny("На эту карточку нельзя приводить адептов.");
    if (!_playerZones.TryGetValue(player, out var zone) || !zone.Contains(cell))
      return PlacementCheck.Deny("Приводить адептов можно только на карточку в своём личном штабе.");

    return PlacementCheck.Allowed;
  }

  /// <summary>
  /// Карточки в личном штабе игрока, на которые можно приводить адептов.
  /// </summary>
  public IEnumerable<Cell> AdeptCardsOf(Player player) =>
    _playerZones.TryGetValue(player, out var zone)
      ? zone.Cells.Where(cell => CheckAdeptCard(cell, player).IsAllowed)
      : [];

  /// <summary>
  /// Привести адептов на карточку в личном штабе: приходит ещё <see cref="PersonCard.AdeptsBonus"/> адептов.
  /// </summary>
  /// <param name="cell">Ячейка карточки.</param>
  /// <param name="count">Сколько адептов приводят.</param>
  /// <param name="player">Игрок, который приводит адептов.</param>
  /// <exception cref="InvalidOperationException">На карточку нельзя приводить адептов.</exception>
  public void AddAdepts(Cell cell, int count, Player player)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
    var check = CheckAdeptCard(cell, player);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    var total = count + ((PersonCard)_cards[cell]).AdeptsBonus!.Value;
    _cardAdepts[cell] = AdeptsAt(cell) + total;
    CardAdeptsAdded?.Invoke(cell, total);
  }

  /// <summary>
  /// После паса игрока его Мастера теорий в личном штабе приводят адептов на свою карточку: по одному за каждого
  /// соседнего профессионала и ещё одного за самого Мастера.
  /// </summary>
  public void AddMasterAdepts(Player player)
  {
    if (!_playerZones.TryGetValue(player, out var zone))
      return;

    var masters = zone.Cells
      .Where(cell => CardAt(cell) is GenericProfessionalCard { Profession: Profession.TheoryMaster })
      .ToList();
    foreach (var cell in masters)
    {
      var professionals = cell.Surrounding.Count(neighbor => zone.Contains(neighbor) && CardAt(neighbor) is ProfessionalCard);
      AddAdepts(cell, professionals + 1, player);
    }
  }

  /// <summary>
  /// Сыгранные карточки личных штабов переворачиваются лицевой стороной вверх, а повёрнутые возвращаются
  /// в обычное положение — их снова можно сыграть.
  /// </summary>
  public void RefreshUsedCards()
  {
    var cells = _usedCells.Concat(_rotatedCells).ToList();
    _usedCells.Clear();
    _rotatedCells.Clear();
    if (cells.Count > 0)
      UsedCardsRefreshed?.Invoke(cells);

    // Шапочка из фольги действует только до конца раунда: в конце раунда её снимают с персонажа.
    // Карточка остаётся у владельца и может быть выложена снова.
    foreach (var foil in _theoryCards.Where(card => card.Card.Passive is TinFoilPassive && card.AttachedTo is not null))
      foil.AttachedTo = null;

    var theoryCards = _theoryCards.Where(card => card.IsUsed).ToList();
    foreach (var card in theoryCards)
      card.IsUsed = false;
    if (theoryCards.Count > 0)
      TheoryCardsRefreshed?.Invoke(theoryCards);
  }

  /// <summary>
  /// Ряд карточек теорий игрока: одна строка сразу под его рабочей зоной (см. <see cref="FieldLayout"/>).
  /// </summary>
  public Zone TheoryCardZoneOf(Player player)
  {
    var zone = _playerZones[player];
    return new Zone(zone.Left, zone.BottomRow + 1, zone.Width, 1);
  }

  /// <summary>
  /// Где могут лежать карточки теорий игрока, если ряд под его штабом (<see cref="TheoryCardZoneOf"/>) заполнен:
  /// тот же ряд дальше вправо — до штаба соседа — и ещё один ряд ниже (пустой ряд перед рынком).
  /// </summary>
  public Zone TheoryCardAreaOf(Player player)
  {
    var zone = _playerZones[player];
    var width = Math.Min(zone.Width + FieldLayout.PlayerZoneGap, Columns - zone.Left);
    return new Zone(zone.Left, zone.BottomRow + 1, width, 2);
  }

  /// <summary>
  /// Закрытые штабы теорий (<see cref="TheoryColumn.IsHeadquarters"/>), где игрок — владелец.
  /// </summary>
  public IEnumerable<TheoryColumn> ClosedHeadquartersOf(Player player) =>
    _theoryColumns.Where(column => column.IsClosed && column.IsHeadquarters && column.Owner == player);

  /// <summary>
  /// Карточка теории, которую владелец получил за теорию.
  /// </summary>
  public OwnedTheoryCard? TheoryCardOf(Theory theory) => _theoryCards.FirstOrDefault(card => card.Card.Theory == theory);

  /// <summary>
  /// Проверить, может ли игрок применить карточку теории: только свою и ещё не применённую.
  /// </summary>
  public bool CanUseTheoryCard(OwnedTheoryCard card, Player player) =>
    card.Owner == player && !card.IsUsed && card.Card.OnPlayFromHand != Effect.None && !IsTheoryClosed(card.Card.Theory);

  /// <summary>
  /// Закрыт ли штаб теории: тогда её карточка эффекта недоступна, пока штаб не откроется снова.
  /// </summary>
  public bool IsTheoryClosed(Theory theory) => ColumnOf(theory) is { IsClosed: true };

  /// <summary>
  /// Применить карточку теории: выполнить её действие, после чего карточка переворачивается рубашкой вверх.
  /// Если карточка относится к дочерней теории, действие карточки основной теории срабатывает снова — для её владельца,
  /// в любом случае: даже если карточка основной теории уже применена или её владелец сам применил эту карточку.
  /// </summary>
  /// <param name="card">Карточка теории.</param>
  /// <param name="context">Контекст партии с выбранными целями.</param>
  /// <exception cref="InvalidOperationException">Карточку нельзя применить.</exception>
  /// <returns>Действие основной теории, которое нужно разрешить владельцу основной теории (<see cref="ResolveTheoryTrigger"/>),
  /// или <c>null</c>, если всё выполнено.</returns>
  public TheoryTrigger? UseTheoryCard(OwnedTheoryCard card, GameContext context)
  {
    if (!CanUseTheoryCard(card, context.CurrentPlayer))
      throw new InvalidOperationException("Эту карточку теории нельзя применить.");

    var effect = card.Card.OnPlayFromHand;
    // Действию, которому не нужна теория, даём теорию самой карточки (например, «5G» раздаёт адептов соседям).
    if (!effect.RequiresTheory && ColumnOf(card.Card.Theory) is { } home)
      context = context.WithTargetTheory(home);
    RequireTargets(effect, context);
    if (card.Card.Passive is TinFoilPassive && context.TargetCell is { } foilCell && CheckFoil(foilCell) is { IsAllowed: false } foilCheck)
      throw new InvalidOperationException(foilCheck.Reason);
    PayPolicemanTolls(effect, context);

    // Все проверки пройдены. Если действие откажет на полпути, карточка всё равно считается применённой:
    // часть действия уже могла выполниться, и применить её второй раз нельзя.
    var executed = false;
    try
    {
      effect.Execute(context);
      executed = true;
    }
    finally
    {
      // Шапочка остаётся лежать на персонаже до конца раунда, а сама карточка переворачивается, как любая другая.
      if (executed && card.Card.Passive is TinFoilPassive)
        card.AttachedTo = context.TargetCell;
      card.IsUsed = true;
      TheoryCardUsed?.Invoke(card);
    }

    // Основная теория срабатывает для своего владельца; если её действию нужен выбор, владелец выберет позже.
    TheoryTrigger? pending = null;
    // Закрытый штаб основной теории не срабатывает: её карточка эффекта недоступна.
    if (card.Card.Theory.Parent is { } parent && TheoryCardOf(parent) is { } main && !IsTheoryClosed(parent))
    {
      var trigger = new TheoryTrigger(main, new GameContext(main.Owner, this, context.Random, context.Market));
      if (!NeedsChoice(main.Card.OnPlayFromHand))
      {
        // Дочерняя карточка уже применена: если действие основной теории сейчас выполнить нельзя, оно просто
        // не срабатывает, а не отменяет уже сделанное.
        try
        {
          main.Card.OnPlayFromHand.Execute(trigger.Context);
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        {
        }
      }
      else if (HasChoices(trigger))
        pending = trigger;
    }

    return pending;
  }

  /// <summary>
  /// Разрешить действие основной теории, которое ждало выбора (<see cref="UseTheoryCard"/>).
  /// Все нужные выборы должны быть сделаны в <see cref="TheoryTrigger"/>.
  /// </summary>
  public void ResolveTheoryTrigger(TheoryTrigger trigger)
  {
    if (!trigger.IsComplete)
      throw new InvalidOperationException("Выбраны не все цели.");

    RequireTargets(trigger.Effect, trigger.Context);
    PayPolicemanTolls(trigger.Effect, trigger.Context);
    trigger.Effect.Execute(trigger.Context);
  }

  private static bool NeedsChoice(Effect effect) => effect.RequiresHandCard || effect.RequiresTheory || effect.RequiresTargetCell;

  /// <summary>
  /// Есть ли у владельца что выбрать для действия основной теории.
  /// </summary>
  private bool HasChoices(TheoryTrigger trigger)
  {
    var effect = trigger.Effect;
    var owner = trigger.Owner;
    return (!effect.RequiresHandCard || owner.Hand.Cards.Count > 0)
      && (!effect.RequiresTheory || _theoryColumns.Any(column => effect.CanTargetTheory(column, owner)))
      && (!effect.RequiresTargetCell
        || effect.TargetCells(trigger.Context).Any(target => target.Check.IsAllowed)
        || effect.AcceptsHandCardTarget && owner.Hand.Cards.Any(card => effect.CheckHandCard(card, trigger.Context).IsAllowed));
  }

  /// <summary>
  /// Обновить карточки теорий после изменения штабов: сначала пересчитать влияние, которое зависит от карточек теорий
  /// («Масоны» — влияние закрытых персонажей, «Плоская Земля» — влияние основателя), затем карточки переходят к новым
  /// владельцам теорий и выдаются новые.
  /// </summary>
  /// <remarks>
  /// Выдача или переход карточки снова меняет влияние (например, игрок получил «Масонов»), а от влияния зависят
  /// владельцы и выдача — поэтому пересчитываем, пока карточки не перестанут меняться.
  /// </remarks>
  private void UpdateTheoryCards()
  {
    const int maxPasses = 5;
    for (var pass = 0; pass < maxPasses; pass++)
    {
      SyncHiddenInfluence();
      SyncTheoryRules();
      var before = _theoryCards.Select(owned => (owned, owned.Owner)).ToList();
      MoveTheoryCardsToOwners();
      GrantTheoryCards();
      if (_theoryCards.Count == before.Count && before.All(item => item.owned.Owner == item.Owner))
        break;
    }

    SyncHiddenInfluence();
    SyncTheoryRules();
  }

  /// <summary>
  /// Пассивные способности, которые меняют штабы теорий: влияние основателя «Плоской Земли» и сложность закрытия
  /// штабов владельца «Антипрививочников».
  /// </summary>
  private void SyncTheoryRules()
  {
    var antiVaccinationOwner = TheoryCardOf(TheoryCatalog.AntiVaccination)?.Owner;
    var antarcticaOwners = _theoryCards.Where(owned => owned.Card.Passive is AntarcticaPassive).Select(owned => owned.Owner).ToList();
    var illuminatiOwners = _theoryCards.Where(owned => owned.Card.Passive is IlluminatiPassive).Select(owned => owned.Owner).ToList();
    foreach (var column in _theoryColumns)
    {
      column.IlluminatiHiddenOwners = illuminatiOwners;
      column.FounderInfluenceBonus = TheoryCardOf(column.Theory)?.Card.Passive is FlatEarthPassive ? FlatEarthPassive.FounderBonus : 0;
      column.SolvencyMargin = antiVaccinationOwner is not null && column.IsHeadquarters && column.Owner == antiVaccinationOwner
        ? AntiVaccinationPassive.SolvencyBonus
        : 0;
      column.DoubleHomelessOwners = antarcticaOwners;
      column.AdeptsShielded = TheoryCardOf(column.Theory)?.Card.Passive is TinFoilPassive;
    }
  }

  /// <summary>
  /// Штаб нельзя закрыть как несостоятельный, пока у теории лежит карточка с пассивной способностью «Плоской Земли».
  /// </summary>
  private bool IsUncloseable(TheoryColumn column) => TheoryCardOf(column.Theory)?.Card.Passive is FlatEarthPassive;

  /// <summary>
  /// Владелец карточки «Чипирование» — тот, кто положил фишку — может использовать чипированного персонажа как своего.
  /// </summary>
  public bool IsChippedBy(Cell cell, Player player) =>
    _chips.TryGetValue(cell, out var chipper) && chipper == player && HoldsPassive<ChipPassive>(player);

  /// <summary>
  /// Ячейки персонажей с фишками и тот, кто их положил.
  /// </summary>
  public IReadOnlyDictionary<Cell, Player> Chips => _chips;

  /// <summary>
  /// Персонажи в личных штабах, на которых можно положить фишку.
  /// </summary>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> ChipTargets(Player player) =>
    _playerZones
      .SelectMany(zone => zone.Value.Cells)
      .Where(cell => CardAt(cell) is PersonCard)
      .Select(cell => (cell, CheckChip(cell, player)));

  /// <summary>
  /// Проверить, можно ли положить фишку на персонажа: он в личном штабе, без фишки, и его не защищает полицейский.
  /// В Check.Cost — сколько заплатить владельцу персонажа.
  /// </summary>
  public PlacementCheck CheckChip(Cell cell, Player player)
  {
    if (CardAt(cell) is not PersonCard person || OwnerOfCell(cell) is null)
      return PlacementCheck.Deny("Здесь нет персонажа из личного штаба.");
    if (_chips.ContainsKey(cell))
      return PlacementCheck.Deny("На этом персонаже уже есть фишка.");
    if (IsProtected(cell, player))
      return PlacementCheck.Deny("Этого персонажа защищает полицейский или шапочка из фольги.");

    return player.CanAfford(person.Cost)
      ? PlacementCheck.Allow(person.Cost)
      : PlacementCheck.Deny($"Недостаточно денег: нужно {person.Cost}$, а у вас {player.Money}$.");
  }

  /// <summary>
  /// Положить фишку на персонажа в личном штабе и заплатить его владельцу стоимость персонажа.
  /// </summary>
  /// <exception cref="InvalidOperationException">Фишку положить нельзя.</exception>
  public void Chip(Cell cell, Player player)
  {
    var check = CheckChip(cell, player);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    var owner = OwnerOfCell(cell)!;
    player.Spend(check.Cost);
    owner.Earn(check.Cost);
    _chips[cell] = player;
    CharacterChipped?.Invoke(cell);
  }

  /// <summary>
  /// Адепты переходят в эту теорию: первая теория слева и первая справа по полю, где есть адепты (и их можно увести),
  /// отдают по одному адепту. Теории без адептов пропускаются.
  /// </summary>
  /// <exception cref="InvalidOperationException">Штаб закрыт.</exception>
  public void MoveAdeptsFromNeighbours(TheoryColumn home)
  {
    if (!_theoryColumns.Contains(home) || home.IsClosed)
      throw new InvalidOperationException("Адептов в закрытый штаб не переводят.");

    var open = _theoryColumns
      .Where(column => !column.IsClosed && column != home && column.Adepts > 0 && !column.AdeptsShielded)
      .ToList();
    var left = open.Where(column => column.TheoryCell.Column < home.TheoryCell.Column)
      .OrderByDescending(column => column.TheoryCell.Column).FirstOrDefault();
    var right = open.Where(column => column.TheoryCell.Column > home.TheoryCell.Column)
      .OrderBy(column => column.TheoryCell.Column).FirstOrDefault();
    foreach (var neighbour in new[] { left, right })
    {
      if (neighbour is not null)
        MoveAdepts(neighbour, home, 1);
    }
  }

  /// <summary>
  /// Влияние закрытых персонажей зависит от пассивных способностей владельцев: у владельца «Масонов» оно больше.
  /// </summary>
  private void SyncHiddenInfluence()
  {
    foreach (var member in _theoryColumns.SelectMany(column => column.Members))
    {
      if (member.Card is HiddenCard hidden)
        hidden.SetInfluence(HoldsPassive<MasonsPassive>(member.Owner) ? MasonsPassive.HiddenInfluence : hidden.BaseInfluence);
    }
  }

  /// <summary>
  /// Пассивная способность «Иллюминатов»: когда в теорию приходит чужой лидер, владелец закрытого персонажа
  /// в этой теории получает деньги из банка.
  /// </summary>
  /// <summary>
  /// Владелец «Иллюминатов», если теория закрыта и он в ней лидер: адепты закрытой теории приносят деньги только ему
  /// (пассивная способность <see cref="IlluminatiPassive"/>). Для открытых теорий и других игроков — <c>null</c>.
  /// </summary>
  public Player? IlluminatiBeneficiary(TheoryColumn column)
  {
    if (!column.IsClosed)
      return null;

    var owner = _theoryCards.FirstOrDefault(owned => owned.Card.Passive is IlluminatiPassive)?.Owner;
    return owner is not null && column.IsLeader(owner)
      ? owner
      : null;
  }

  /// <summary>
  /// Деньги пришли от карточки теории: игрок их получает, а над карточкой всплывает сумма (<see cref="TheoryEarned"/>).
  /// </summary>
  /// <param name="owned">Карточка теории, которая принесла деньги.</param>
  /// <param name="player">Получатель.</param>
  /// <param name="amount">Сумма в $.</param>
  public void EarnForTheory(OwnedTheoryCard owned, Player player, int amount)
  {
    if (amount <= 0)
      return;

    using (player.EarningFrom($"Теория «{owned.Card.Name}»"))
      player.Earn(amount);
    ReportTheoryEarned(owned, amount);
  }

  /// <summary>
  /// Сообщить, что карточка теории принесла деньги, уже полученные владельцем (<see cref="TheoryEarned"/>).
  /// </summary>
  public void ReportTheoryEarned(OwnedTheoryCard owned, int amount)
  {
    if (amount > 0)
      TheoryEarned?.Invoke(owned, amount);
  }

  /// <summary>
  /// Карточка теории всегда у владельца теории: если он сменился, карточка переходит новому владельцу
  /// в текущем состоянии (применена она или нет) и ложится в его ряд.
  /// </summary>
  private void MoveTheoryCardsToOwners()
  {
    foreach (var owned in _theoryCards)
    {
      var owner = ColumnOf(owned.Card.Theory)!.Owner;
      if (owner == owned.Owner)
        continue;

      owned.Owner = owner;
      owned.Cell = FirstFreeTheoryCell(owner);
      TheoryCardTransferred?.Invoke(owned);
    }
  }

  /// <summary>
  /// Выдать карточки теорий: владелец действующей теории (не закрытой, с влиянием не меньше
  /// <see cref="TheoryColumn.ActiveInfluence"/>) и хотя бы с <see cref="TheoryColumn.TheoryCardLeadersThreshold"/>
  /// лидерами получает её карточку. Карточка выдаётся один раз, дальше следует за владельцем теории.
  /// </summary>
  private void GrantTheoryCards()
  {
    foreach (var column in _theoryColumns)
    {
      if (column.IsClosed
        || column.Leaders < TheoryColumn.TheoryCardLeadersThreshold
        || column.Influence < TheoryColumn.ActiveInfluence
        || TheoryCardOf(column.Theory) is not null
        || TheoryCardCatalog.Create(column.Theory) is not { } card)
      {
        continue;
      }

      var owned = new OwnedTheoryCard(card, column.Owner, FirstFreeTheoryCell(column.Owner));
      _theoryCards.Add(owned);
      TheoryCardGranted?.Invoke(owned);
    }
  }

  /// <summary>
  /// Первая свободная ячейка для карточки теории игрока: сначала ряд под штабом слева направо, затем этот ряд дальше
  /// вправо и ряд ниже (<see cref="TheoryCardAreaOf"/>).
  /// </summary>
  /// <exception cref="InvalidOperationException">Места нет (у игрока больше карточек теорий, чем ячеек).</exception>
  private Cell FirstFreeTheoryCell(Player player)
  {
    var area = TheoryCardAreaOf(player);
    var cells = Enumerable.Range(area.Top, area.Height)
      .SelectMany(row => Enumerable.Range(area.Left, area.Width).Select(column => new Cell(column, row)));
    foreach (var cell in cells)
    {
      if (!_theoryCards.Any(owned => owned.Cell == cell))
        return cell;
    }

    throw new InvalidOperationException($"У игрока {player} нет места для ещё одной карточки теории.");
  }

  /// <summary>
  /// Сколько $ добавляет карточка теории «Золотой миллиард» профессионалу в личном штабе: если его владелец
  /// держит такую карточку, то профессионал приносит на <see cref="GoldenBillionPassive.ProfessionalBonus"/>$ больше.
  /// </summary>
  private int ProfessionMoneyBonusAt(Cell cell)
  {
    if (CardAt(cell) is not ProfessionalCard || _playerZones.Keys.FirstOrDefault(zonePlayer => _playerZones[zonePlayer].Contains(cell)) is not { } owner)
      return 0;

    return _theoryCards.Any(card => card.Owner == owner && card.Card.Passive is GoldenBillionPassive)
      ? GoldenBillionPassive.ProfessionalBonus
      : 0;
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
    var closed = _theoryColumns.Where(column => !column.IsClosed && column.IsInsolvent && !IsUncloseable(column)).ToList();
    foreach (var column in closed)
    {
      column.Close();
      TheoryClosed?.Invoke(column);
    }

    // Закрытие меняет, какие теории действующие: пересчитываем карточки теорий.
    if (closed.Count > 0)
      UpdateTheoryCards();
    return closed;
  }

  /// <summary>
  /// Открыть обратно закрытые штабы, которые перестали быть несостоятельными (состав изменился).
  /// </summary>
  /// <returns>Штабы, открытые сейчас.</returns>
  public IReadOnlyList<TheoryColumn> ReopenSolventTheories()
  {
    var reopened = _theoryColumns.Where(column => column.IsClosed && !column.IsInsolvent).ToList();
    foreach (var column in reopened)
    {
      column.Reopen();
      TheoryReopened?.Invoke(column);
    }

    // Открывшийся штаб снова может получить карточку теории, если он действующий и лидеров хватает.
    if (reopened.Count > 0)
      UpdateTheoryCards();
    return reopened;
  }

  /// <summary>
  /// Ячейка, где лежит карточка, или <c>null</c>, если её нет на поле.
  /// </summary>
  public Cell? CellOf(Card card) => _cards.FirstOrDefault(pair => pair.Value == card) is { Value: not null } pair ? pair.Key : null;

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

    return person.Theory is { } theory ? CheckTheoryCard(theory, cell) : CheckJoining(cell, player, person);
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
    TheoryColumn? created = null;
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
        created = joined;
        _theoryColumns.Add(joined);
        ResolveColumnConflict(joined);
      }
    }

    player.Spend(check.Cost);
    if (joined is not null)
    {
      GrowCommonZone(created);
      UpdateTheoryCards();
      if (check.Column is { } joinedColumn)
      {
        PayEbolaPassive(joinedColumn, player);
      }
    }
    // Персона пришла в штаб теории — выполняется её действие при выкладывании в штаб. Действие с выбором
    // карточки на поле выполняет сессия, когда игрок её выберет (GameSession.ResolvePlacement).
    // Карточка могла сдвинуться вместе со столбцом (см. ResolveColumnConflict), поэтому берём её верхнюю ячейку.
    if (joined is not null && !person.OnPlacedInHeadquarters.RequiresTargetCell)
      person.PlaceInHeadquarters(new GameContext(player, this).WithTargetTheory(joined).WithSourceCell(joined.Top));
    return check;
  }

  /// <summary>
  /// Проверить, может ли игрок выложить карточку взакрытую (<see cref="HiddenCard"/>) сверху стопки теории.
  /// Закрытая карточка — гражданин без теории: за неё платят суммарное влияние стопки.
  /// </summary>
  /// <param name="discount">Скидка на выкладывание, $.</param>
  public PlacementCheck CheckHidden(Card card, TheoryColumn column, Player player, int discount = 0) =>
    Check(new HiddenCard(card, discount), column.Top.Above, player);

  /// <summary>
  /// Выложить карточку взакрытую (<see cref="HiddenCard"/>) сверху стопки теории, заплатив за это.
  /// </summary>
  /// <returns>Условия, на которых карточка положена.</returns>
  /// <exception cref="InvalidOperationException">Карточку сюда класть нельзя.</exception>
  /// <param name="discount">Скидка на выкладывание, $.</param>
  public PlacementCheck PlaceHidden(Card card, TheoryColumn column, Player player, int discount = 0)
  {
    var cell = column.Top.Above;
    var check = Place(new HiddenCard(card, discount), cell, player);
    HiddenCardPlaced?.Invoke(cell, player);
    return check;
  }

  /// <summary>
  /// Закрытые персонажи (<see cref="HiddenCard"/>) игрока на поле — во всех стопках теорий, включая закрытые штабы.
  /// Раскрытые не считаются.
  /// </summary>
  public IEnumerable<TheoryMember> HiddenCardsOf(Player player) =>
    _theoryColumns.SelectMany(column => column.MembersOf(player))
      .Where(member => member.Card is HiddenCard { IsRevealed: false });

  /// <summary>
  /// Проверить, может ли игрок посмотреть в закрытую карточку (<see cref="HiddenCard"/>) другого игрока.
  /// </summary>
  public PlacementCheck CheckPeek(Cell cell, Player player)
  {
    if (CardAt(cell) is not HiddenCard hidden)
      return PlacementCheck.Deny("Здесь нет закрытой карточки.");
    if (hidden.IsRevealed)
      return PlacementCheck.Deny("Эта карточка уже раскрыта — её и так видно.");
    if (ColumnAt(cell)?.IsProtected() == true)
      return PlacementCheck.Deny("Эту теорию защищает полицейский.");
    if (ColumnAt(cell)?.OwnerAt(cell) == player)
      return PlacementCheck.Deny("Это ваша закрытая карточка — вы и так её знаете.");
    if (IsImmune(cell))
      return PlacementCheck.Deny("Персонажей этого игрока защищают рептилоиды.");
    if (hidden.IsKnownBy(player))
      return PlacementCheck.Deny("Вы уже смотрели эту карточку.");

    return PlacementCheck.Allowed;
  }

  /// <summary>
  /// Закрытые карточки других игроков, в которые игрок может посмотреть.
  /// </summary>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> PeekTargets(Player player) =>
    _theoryColumns
      .SelectMany(column => column.Members)
      .Where(member => member.Card is HiddenCard { IsRevealed: false } && member.Owner != player)
      .Select(member => (member.Cell, CheckPeek(member.Cell, player)));

  /// <summary>
  /// Посмотреть в закрытую карточку другого игрока: теперь игрок знает её, как владелец, остальные — нет.
  /// </summary>
  /// <exception cref="InvalidOperationException">В карточку посмотреть нельзя.</exception>
  public void Peek(Cell cell, Player player)
  {
    var check = CheckPeek(cell, player);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    ((HiddenCard)_cards[cell]).ShowTo(player);
  }

  /// <summary>
  /// Сколько $ получает владелец раскрытой карточки, совпавшей с теорией штаба, за каждую единицу суммарного влияния штаба.
  /// </summary>
  public const int RevealBonusPerInfluence = 2;

  /// <summary>
  /// Проверить, может ли игрок раскрыть закрытую карточку (<see cref="HiddenCard"/>) другого игрока.
  /// </summary>
  public PlacementCheck CheckReveal(Cell cell, Player player)
  {
    if (CardAt(cell) is not HiddenCard hidden)
      return PlacementCheck.Deny("Здесь нет закрытой карточки.");
    if (hidden.IsRevealed)
      return PlacementCheck.Deny("Эта карточка уже раскрыта.");
    if (ColumnAt(cell)?.IsProtected() == true)
      return PlacementCheck.Deny("Эту теорию защищает полицейский.");
    if (ColumnAt(cell)?.OwnerAt(cell) == player)
      return PlacementCheck.Deny("Это ваша закрытая карточка — раскрыть можно только карточку другого игрока.");
    if (IsImmune(cell))
      return PlacementCheck.Deny("Персонажей этого игрока защищают рептилоиды.");

    return player.CanAfford(RevealCost)
      ? PlacementCheck.Allow(RevealCost)
      : PlacementCheck.Deny($"Недостаточно денег: раскрытие стоит {RevealCost}$, а у вас {player.Money}$.");
  }

  /// <summary>
  /// Сколько $ стоит раскрыть закрытую карточку другого игрока (действие журналиста).
  /// </summary>
  public const int RevealCost = 1;

  /// <summary>
  /// Закрытые карточки других игроков, которые игрок может раскрыть.
  /// </summary>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> RevealTargets(Player player) =>
    _theoryColumns
      .SelectMany(column => column.Members)
      .Where(member => member.Card is HiddenCard { IsRevealed: false } && member.Owner != player)
      .Select(member => (member.Cell, CheckReveal(member.Cell, player)));

  /// <summary>
  /// Раскрыть закрытую карточку другого игрока: теперь её видят все.
  /// </summary>
  /// <exception cref="InvalidOperationException">Карточку раскрыть нельзя.</exception>
  public void Reveal(Cell cell, Player player)
  {
    var check = CheckReveal(cell, player);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    // Деньги за раскрытие уходят владельцу раскрытой карточки; над ней всплывает сумма.
    var owner = ColumnAt(cell)?.OwnerAt(cell) ?? throw new InvalidOperationException("У закрытой карточки нет владельца.");
    player.Spend(check.Cost);
    using (owner.EarningFrom("Раскрытие карточки"))
      EarnAt(cell, owner, check.Cost);
    ((HiddenCard)_cards[cell]).Reveal();
    HiddenCardRevealed?.Invoke(cell);
    ResolveRevealed(cell);
  }

  /// <summary>
  /// Раскрыть все закрытые карточки на поле (в конце игры перед итоговым подсчётом).
  /// </summary>
  public void RevealAllHiddenCards()
  {
    for (var cell = FirstUnrevealedHidden(); cell is { } target; cell = FirstUnrevealedHidden())
    {
      ((HiddenCard)_cards[target]).Reveal();
      HiddenCardRevealed?.Invoke(target);
      ResolveRevealed(target);
    }
  }

  /// <summary>
  /// Первая закрытая, но ещё не раскрытая карточка в теориях, или <c>null</c>.
  /// </summary>
  private Cell? FirstUnrevealedHidden() =>
    _theoryColumns
      .SelectMany(column => column.Members)
      .Where(member => member.Card is HiddenCard { IsRevealed: false })
      .Select(member => (Cell?)member.Cell)
      .FirstOrDefault();

  /// <summary>
  /// Что происходит с раскрытой карточкой в штабе теории:
  /// без теории — ничего; с теорией штаба — владелец получает <see cref="RevealBonusPerInfluence"/>$ за каждую единицу
  /// суммарного влияния штаба; с другой теорией — карточка уходит из игры.
  /// </summary>
  private void ResolveRevealed(Cell cell)
  {
    var column = ColumnAt(cell) ?? throw new InvalidOperationException("Раскрытая карточка не в теории.");
    var face = ((HiddenCard)_cards[cell]).Face;
    if (face is not PersonCard { Theory: { } theory })
      return;

    if (theory == column.Theory)
      EarnAt(cell, column.OwnerAt(cell) ?? throw new InvalidOperationException("У карточки нет владельца."), RevealBonusPerInfluence * column.Influence);
    else
      RemoveFromTheory(column, cell);
  }

  /// <summary>
  /// Проверить, может ли игрок перевернуть карточку другого игрока в его личном штабе, как будто она уже сыграна.
  /// Платят в банк влияние переворачиваемого персонажа (<see cref="PlacementCheck.Cost"/>).
  /// </summary>
  public PlacementCheck CheckFlipRival(Cell cell, Player player)
  {
    var owner = _playerZones.Keys.FirstOrDefault(zonePlayer => _playerZones[zonePlayer].Contains(cell));
    if (CardAt(cell) is not PersonCard person || owner is null)
      return PlacementCheck.Deny("Здесь нет карточки из личного штаба.");
    if (owner == player)
      return PlacementCheck.Deny("Это ваша карточка — перевернуть можно только карточку другого игрока.");
    if (IsUsed(cell))
      return PlacementCheck.Deny("Эта карточка уже перевёрнута.");
    if (IsProtected(cell, player))
      return PlacementCheck.Deny("Эту карточку защищает полицейский.");

    var cost = person.Influence;
    return player.CanAfford(cost)
      ? PlacementCheck.Allow(cost)
      : PlacementCheck.Deny($"Недостаточно денег: чтобы перевернуть «{person}», нужно {cost}$, а у вас {player.Money}$.");
  }

  /// <summary>
  /// Несыгранные карточки в личных штабах других игроков с условиями, на которых их можно перевернуть.
  /// </summary>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> FlipRivalTargets(Player player) =>
    _playerZones
      .Where(zone => zone.Key != player)
      .SelectMany(zone => zone.Value.Cells)
      .Where(cell => CardAt(cell) is not null && !IsUsed(cell))
      .Select(cell => (cell, CheckFlipRival(cell, player)));

  /// <summary>
  /// Перевернуть карточку другого игрока в его личном штабе рубашкой вверх, как будто она уже сыграна,
  /// заплатив её влияние владельцу карточки.
  /// </summary>
  /// <exception cref="InvalidOperationException">Карточку перевернуть нельзя.</exception>
  public void FlipRival(Cell cell, Player player)
  {
    var check = CheckFlipRival(cell, player);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    var owner = OwnerOfCell(cell)!;
    player.Spend(check.Cost);
    owner.Earn(check.Cost);
    _rotatedCells.Remove(cell);
    _usedCells.Add(cell);
  }

  /// <summary>
  /// Игрок получил деньги за действие карточки: над ней всплывает сумма (<see cref="CardEarned"/>).
  /// </summary>
  /// <param name="cell">Карточка, которая принесла деньги, или <c>null</c>.</param>
  /// <param name="player">Игрок.</param>
  /// <param name="amount">Сумма в $.</param>
  public void EarnAt(Cell? cell, Player player, int amount)
  {
    if (amount <= 0)
      return;

    player.Earn(amount);
    if (cell is { } source)
      CardEarned?.Invoke(source, amount, 0, 0);
  }

  /// <summary>
  /// Посмотреть все закрытые карточки других игроков в теории (кроме уже известных), если теория не защищена.
  /// </summary>
  /// <param name="column">Теория.</param>
  /// <param name="player">Кто смотрит.</param>
  /// <param name="source">Карточка, которая действует.</param>
  public void PeekTheory(TheoryColumn column, Player player, Cell? source)
  {
    if (column.IsProtected(source))
      return;

    var cells = column.Members
      .Where(member => member.Card is HiddenCard && CheckPeek(member.Cell, player).IsAllowed)
      .Select(member => member.Cell)
      .ToList();
    foreach (var cell in cells)
      Peek(cell, player);
  }

  /// <summary>
  /// Забрать <paramref name="amount"/>$ за каждую карточку других игроков в теории. Если владельцу платить нечем,
  /// карточка возвращается ему в руку (закрытая — своей лицевой стороной), а он получает <paramref name="compensation"/>$.
  /// В защищённой полицейским теории ничего не происходит.
  /// </summary>
  public void RobLeaders(TheoryColumn column, Player player, Cell? source, int amount, int compensation)
  {
    if (column.IsProtected(source))
      return;

    var robbed = 0;
    foreach (var member in column.Members.Where(member => member.Owner != player).ToList())
    {
      if (member.Owner.CanAfford(amount))
      {
        member.Owner.Spend(amount);
        robbed += amount;
        continue;
      }

      var cell = column.Members.First(current => current.Card == member.Card).Cell;
      RemoveFromTheory(column, cell);
      member.Owner.Hand.Add(member.Card is HiddenCard hidden ? hidden.Face : member.Card);
      member.Owner.Earn(compensation);
    }

    EarnAt(source, player, robbed);
  }

  /// <summary>
  /// Проверить, может ли игрок убрать из игры бездомного из теории.
  /// </summary>
  public PlacementCheck CheckRemoveHomeless(Cell cell, TheoryColumn column, Cell? source)
  {
    if (ColumnAt(cell) != column || CardAt(cell) is not HomelessCard)
      return PlacementCheck.Deny("Убрать можно только бездомного из этой теории.");
    if (column.IsProtected(source))
      return PlacementCheck.Deny("Эту теорию защищает полицейский.");

    return PlacementCheck.Allowed;
  }

  /// <summary>
  /// Бездомные в теории с условиями, на которых их можно убрать из игры.
  /// </summary>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> RemoveHomelessTargets(TheoryColumn column, Cell? source) =>
    column.Members
      .Where(member => member.Card is HomelessCard)
      .Select(member => (member.Cell, CheckRemoveHomeless(member.Cell, column, source)))
      .ToList();

  /// <summary>
  /// Убрать бездомного из теории из игры: карточки над ним опускаются.
  /// </summary>
  /// <exception cref="InvalidOperationException">Убрать нельзя.</exception>
  public void RemoveHomeless(Cell cell, TheoryColumn column, Cell? source)
  {
    var check = CheckRemoveHomeless(cell, column, source);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    RemoveFromTheory(column, cell);
  }

  /// <summary>
  /// Проверить, может ли игрок закрыть лидера в теории: открытую карточку любого игрока, кроме действующей.
  /// </summary>
  public PlacementCheck CheckCloseLeader(Cell cell, TheoryColumn column, Cell? source)
  {
    if (ColumnAt(cell) != column)
      return PlacementCheck.Deny("Закрыть можно только лидера этой теории.");
    if (cell == source)
      return PlacementCheck.Deny("Эта карточка сама выполняет действие — её закрыть нельзя.");
    if (CardAt(cell) is HiddenCard)
      return PlacementCheck.Deny("Эта карточка уже закрыта.");
    if (column.IsProtected(source))
      return PlacementCheck.Deny("Эту теорию защищает полицейский.");

    return PlacementCheck.Allowed;
  }

  /// <summary>
  /// Открытые лидеры теории с условиями, на которых их можно закрыть.
  /// </summary>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> CloseLeaderTargets(TheoryColumn column, Cell? source) =>
    column.Members
      .Where(member => member.Card is not HiddenCard && member.Cell != source)
      .Select(member => (member.Cell, CheckCloseLeader(member.Cell, column, source)))
      .ToList();

  /// <summary>
  /// Закрыть лидера теории: карточка переворачивается рубашкой вверх и становится закрытой картой (гражданином
  /// без теории) своего владельца.
  /// </summary>
  /// <exception cref="InvalidOperationException">Закрыть нельзя.</exception>
  public void CloseLeader(Cell cell, TheoryColumn column, Cell? source)
  {
    var check = CheckCloseLeader(cell, column, source);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    ReplaceInTheory(column, cell, new HiddenCard(_cards[cell]));
  }

  /// <summary>
  /// Проверить, можно ли обучить бездомного в теории: в колоде рынка должен найтись гражданин
  /// с той же теорией, что и у бездомного, или без теории.
  /// </summary>
  public PlacementCheck CheckTrainHomeless(Cell cell, TheoryColumn column, Market market)
  {
    if (ColumnAt(cell) != column || CardAt(cell) is not HomelessCard homeless)
      return PlacementCheck.Deny("Обучить можно только бездомного из этой теории.");
    if (IsImmune(cell))
      return PlacementCheck.Deny("Персонажей этого игрока защищают рептилоиды.");
    if (!market.DeckContains(card => IsTraineeFor(homeless, card)))
      return PlacementCheck.Deny("В колоде рынка нет подходящего гражданина.");

    return PlacementCheck.Allowed;
  }

  /// <summary>
  /// Бездомные в теории с условиями, на которых их можно обучить.
  /// </summary>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> TrainHomelessTargets(TheoryColumn column, Market market) =>
    column.Members
      .Where(member => member.Card is HomelessCard)
      .Select(member => (member.Cell, CheckTrainHomeless(member.Cell, column, market)))
      .ToList();

  /// <summary>
  /// Обучить бездомного в теории: его заменяет первый подходящий гражданин из колоды рынка
  /// (с той же теорией или без теории), а бездомный уходит из игры.
  /// </summary>
  /// <exception cref="InvalidOperationException">Обучить нельзя.</exception>
  public void TrainHomeless(Cell cell, TheoryColumn column, Market market)
  {
    var check = CheckTrainHomeless(cell, column, market);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    var homeless = (PersonCard)_cards[cell];
    var trainee = market.DrawFirst(card => IsTraineeFor(homeless, card))!;
    ReplaceInTheory(column, cell, trainee);
  }

  /// <summary>
  /// Подходит ли карточка, чтобы заменить обученного персонажа: следующая ступень (бездомный → гражданин →
  /// профессионал) с той же теорией или без теории.
  /// </summary>
  private static bool IsTraineeFor(PersonCard person, PersonCard card)
  {
    var nextRank = person switch
    {
      HomelessCard => card is CitizenCard,
      CitizenCard => card is ProfessionalCard,
      _ => false,
    };
    return nextRank && (card.Theory is null || card.Theory == person.Theory);
  }

  /// <summary>
  /// Проверить, можно ли выбрать карточку теории для обмена местами.
  /// </summary>
  /// <param name="cell">Ячейка карточки.</param>
  /// <param name="column">Теория.</param>
  /// <param name="first">Уже выбранная первая карточка или <c>null</c>.</param>
  public PlacementCheck CheckSwap(Cell cell, TheoryColumn column, Cell? first)
  {
    if (ColumnAt(cell) != column)
      return PlacementCheck.Deny("Поменять местами можно только карточки этой теории.");
    if (cell == first)
      return PlacementCheck.Deny("Выберите другую карточку.");
    if (IsImmune(cell))
      return PlacementCheck.Deny("Персонажей этого игрока защищают рептилоиды.");

    return PlacementCheck.Allowed;
  }

  /// <summary>
  /// Карточки теории, которые можно выбрать для обмена местами.
  /// </summary>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> SwapTargets(TheoryColumn column, Cell? first) =>
    column.Members.Count < 2
      ? []
      : column.Cells.Select(cell => (cell, CheckSwap(cell, column, first))).ToList();

  /// <summary>
  /// Поменять местами две карточки теории вместе с их владельцами.
  /// </summary>
  /// <exception cref="InvalidOperationException">Менять нельзя.</exception>
  public void Swap(Cell first, Cell second, TheoryColumn column)
  {
    var check = CheckSwap(first, column, null) is { IsAllowed: false } denied ? denied : CheckSwap(second, column, first);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    column.Swap(first, second);
    (_cards[first], _cards[second]) = (_cards[second], _cards[first]);
    CardsSwapped?.Invoke(first, second);
    UpdateTheoryCards();
  }

  private void ReplaceInTheory(TheoryColumn column, Cell cell, PersonCard card, Player? owner = null)
  {
    column.Replace(cell, card, owner);
    _cards[cell] = card;
    CardReplaced?.Invoke(cell);
    UpdateTheoryCards();
  }

  private void RemoveFromTheory(TheoryColumn column, Cell cell)
  {
    var moves = column.Remove(cell);
    _cards.Remove(cell);
    foreach (var (from, to) in moves)
    {
      _cards[to] = _cards[from];
      _cards.Remove(from);
    }

    TheoryCardRemoved?.Invoke(cell, moves);
    UpdateTheoryCards();
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
  /// Ячейки, которые подсвечивают, пока игрок решает, брать ли карточку с рынка. Теория выложена — вся её стопка.
  /// Подтеория выложена — её стопка и основная теория. Подтеории нет — только первая карточка основной теории.
  /// </summary>
  public IEnumerable<Cell> TheoryCellsOf(Card card)
  {
    if (card is not PersonCard { Theory: { } theory })
      return [];

    if (theory.Parent is not { } parent)
      return ColumnOf(theory)?.Cells ?? [];

    if (ColumnOf(theory) is { } child)
      return child.Cells.Concat(ColumnOf(parent)?.Cells ?? []);

    return ColumnOf(parent) is { } main ? [main.TheoryCell] : [];
  }

  /// <summary>
  /// Вырастить общую зону под выложенные карточки. Нижняя строка зоны не меняется.
  /// </summary>
  /// <param name="created">Новая теория, если карточка открыла её; зона растёт в сторону, где она стоит у края.</param>
  /// <remarks>
  /// Над самой высокой карточкой остаётся <see cref="FieldLayout.CommonZoneFreeRows"/> свободных ряда.
  /// Если новая теория легла в крайний столбец зоны, зона расширяется на <see cref="FieldLayout.CommonZoneSideGrowth"/>
  /// столбца с этой стороны, пока не упрётся в край поля.
  /// </remarks>
  private void GrowCommonZone(TheoryColumn? created)
  {
    var zone = CommonZone;
    var cells = _cards.Keys.Where(zone.Contains).ToList();

    var levels = cells.Select(cell => zone.BottomRow - cell.Row + 1).DefaultIfEmpty(0).Max();
    var height = Math.Min(Math.Max(zone.Height, levels + FieldLayout.CommonZoneFreeRows), zone.BottomRow + 1);

    var left = zone.Left;
    var right = zone.Left + zone.Width;
    if (created is not null)
    {
      var column = created.TheoryCell.Column;
      if (column == zone.Left)
        left = Math.Max(0, left - FieldLayout.CommonZoneSideGrowth);
      if (column == right - 1)
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

  /// <summary>
  /// Дочерние (побочные) теории выкладываются рядом с этой по счёту карточкой стопки родительской теории.
  /// </summary>
  public const int SideTheoryLevel = 3;

  /// <summary>
  /// Ряд общей зоны, где лежат дочерние теории: на уровне <see cref="SideTheoryLevel"/>-й карточки родительской теории.
  /// </summary>
  private int SideTheoryRow => CommonZone.BottomRow - (SideTheoryLevel - 1);

  private PlacementCheck CheckTheoryCard(Theory theory, Cell cell)
  {
    if (ColumnOf(theory) is { } column)
    {
      return cell == column.Top.Above
        ? PlacementCheck.Allow(column: column)
        : PlacementCheck.Deny($"Теория «{theory}» уже выложена: карточку можно положить только сверху её стопки.");
    }

    if (theory.Parent is not { } parentTheory)
    {
      if (cell.Row != CommonZone.BottomRow)
        return PlacementCheck.Deny($"Новую теорию «{theory}» можно выложить только в нижний ряд общей зоны.");
      // Если над ячейкой уже лежит дочерняя теория, базовую придётся сдвинуть — нужно место, куда её сдвинуть.
      if (TheoryAtColumn(cell.Column) is { } child && FreeColumn(cell.Column, ConflictDirection(child.Theory, child.TheoryCell)) is null)
        return PlacementCheck.Deny($"Столбец занят теорией «{child.Theory}», а сдвинуть базовую теорию «{theory}» некуда.");
      return PlacementCheck.Allow(createsHeadquarters: true);
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
    if (cell.Row != SideTheoryRow || !isNextToFamily)
    {
      return PlacementCheck.Deny(
        $"Дочернюю теорию «{theory}» можно выложить только в {SideTheoryLevel}-й ряд общей зоны, вплотную слева или справа "
        + $"от {SideTheoryLevel}-й карточки родительской теории «{parentTheory}» или уже выложенной дочерней теории «{parentTheory}».");
    }
    // Если в этом столбце стоит базовая теория, она сдвинется — нужно место, куда её сдвинуть.
    if (TheoryAtColumn(cell.Column) is { } baseTheory && FreeColumn(cell.Column, ConflictDirection(theory, cell)) is null)
      return PlacementCheck.Deny($"Столбец занят теорией «{baseTheory.Theory}», а сдвинуть её некуда.");
    return PlacementCheck.Allow();
  }

  /// <summary>
  /// Стопка теории, лежащая в столбце (в одном столбце может быть только одна теория).
  /// </summary>
  private TheoryColumn? TheoryAtColumn(int column) => _theoryColumns.FirstOrDefault(theory => theory.TheoryCell.Column == column);

  /// <summary>
  /// Куда сдвигать базовую теорию из-за дочерней теории <paramref name="childTheory"/>, которая лежит над ней
  /// (ячейка <paramref name="childCell"/>): вправо, если слева вплотную стоит её семейство (родительская теория или
  /// другая дочерняя того же родителя), и влево — иначе. Чужие теории слева не в счёт: иначе базовая теория уедет
  /// вправо вместе с родительской, и дочерняя оторвётся от семейства.
  /// </summary>
  private int ConflictDirection(Theory childTheory, Cell childCell)
  {
    var parentTheory = childTheory.Parent;
    var familyOnLeft = _theoryColumns.Any(column =>
      column.Theory != childTheory
      && (column.Theory == parentTheory || column.Theory.Parent == parentTheory)
      && column.Cells.Contains(childCell.Left));
    return familyOnLeft ? 1 : -1;
  }

  /// <summary>
  /// Ближайший свободный столбец общей зоны от столбца <paramref name="column"/> в сторону <paramref name="direction"/>.
  /// </summary>
  /// <returns>Номер столбца или <c>null</c>, если свободного нет.</returns>
  private int? FreeColumn(int column, int direction)
  {
    for (var target = column + direction; target >= CommonZone.Left && target < CommonZone.Left + CommonZone.Width; target += direction)
    {
      if (TheoryAtColumn(target) is null)
        return target;
    }

    return null;
  }

  /// <summary>
  /// Новая теория легла на столбец, где уже лежит другая теория: в одном столбце может быть только одна.
  /// Базовая теория уходит в соседний свободный столбец — вправо, если родительская теория дочерней стоит слева,
  /// и влево, если справа. Занятые столбцы на её пути сдвигаются в ту же сторону, пока не найдётся свободный.
  /// </summary>
  private void ResolveColumnConflict(TheoryColumn placed)
  {
    var index = placed.TheoryCell.Column;
    if (_theoryColumns.FirstOrDefault(theory => theory != placed && theory.TheoryCell.Column == index) is not { } other)
      return;

    var (moved, child) = placed.IsHeadquarters ? (placed, other) : (other, placed);
    var direction = ConflictDirection(child.Theory, child.TheoryCell);
    var free = FreeColumn(index, direction)
      ?? throw new InvalidOperationException($"Некуда сдвинуть базовую теорию «{moved.Theory}».");
    // Сначала сдвигаем занятые столбцы, начиная с самого дальнего, затем базовую теорию.
    for (var column = free - direction; column != index; column -= direction)
      MoveColumn(TheoryAtColumn(column)!, direction);
    MoveColumn(moved, direction);
  }

  /// <summary>
  /// Сдвинуть теорию на <paramref name="dx"/> столбцов вместе с её карточками и всем, что лежит на её ячейках.
  /// </summary>
  private void MoveColumn(TheoryColumn column, int dx)
  {
    var moves = column.Cells.Select(cell => (From: cell, To: cell with { Column = cell.Column + dx })).ToList();
    MoveCells(_cards, moves);
    MoveCells(_usedCells, moves);
    MoveCells(_rotatedCells, moves);
    MoveCells(_trainedBonuses, moves);
    MoveCells(_cardAdepts, moves);
    MoveCells(_chips, moves);
    column.Shift(dx);
    TheoryColumnMoved?.Invoke(column, moves);
  }

  /// <summary>
  /// Проверить, можно ли вставить дочернюю теорию в перегородку между столбцами <paramref name="boundary"/> - 1 и
  /// <paramref name="boundary"/>: в ряду дочерних теорий, вплотную к стопке родительской (или её дочерней) теории,
  /// справа или слева от перегородки (см. <see cref="WallInsertion"/>).
  /// </summary>
  public PlacementCheck CheckInWall(Card card, int boundary, Player player) => WallInsertionAt(card, boundary).Check;

  /// <summary>
  /// Куда вставляется дочерняя теория в перегородке и можно ли это.
  /// </summary>
  /// <param name="ToLeft">Новая теория встаёт в столбец слева от перегородки; иначе — в столбец справа.</param>
  /// <param name="Check">Условия вставки.</param>
  private readonly record struct WallInsertion(bool ToLeft, PlacementCheck Check);

  /// <summary>
  /// Вставка дочерней теории в перегородку. Если родительская (или её дочерняя) теория стоит справа от перегородки,
  /// новая встаёт слева, и занятые столбцы слева сдвигаются на 1 влево. Иначе, если она стоит слева, новая встаёт справа,
  /// и занятые столбцы справа сдвигаются на 1 вправо.
  /// </summary>
  private WallInsertion WallInsertionAt(Card card, int boundary)
  {
    var deny = (string reason) => new WallInsertion(false, PlacementCheck.Deny(reason));
    if (card is not PersonCard { Theory: { Parent: { } parentTheory } theory })
      return deny("В перегородку можно вставить только дочернюю теорию.");
    if (ColumnOf(theory) is not null)
      return deny($"Теория «{theory}» уже выложена.");
    if (ColumnOf(parentTheory) is not { } parent || parent.IsClosed)
      return deny($"Родительская теория «{parentTheory}» не выложена или её штаб закрыт.");

    var row = SideTheoryRow;
    var leftCell = new Cell(boundary - 1, row);
    var rightCell = new Cell(boundary, row);
    var family = _theoryColumns.Where(column => column == parent || column.Theory.Parent == parentTheory).ToList();
    bool InFamily(Cell cell) => family.Any(column => column.Cells.Contains(cell));

    var canLeft = _cards.ContainsKey(leftCell) && InFamily(rightCell)
      && _theoryColumns.Where(column => column.TheoryCell.Column < boundary).Select(column => column.TheoryCell.Column).DefaultIfEmpty(boundary).Min() > 0;
    if (canLeft)
      return new WallInsertion(true, PlacementCheck.Allow());

    var canRight = _cards.ContainsKey(rightCell) && InFamily(leftCell)
      && _theoryColumns.Where(column => column.TheoryCell.Column >= boundary).Select(column => column.TheoryCell.Column).DefaultIfEmpty(boundary - 1).Max() < Columns - 1;
    if (canRight)
      return new WallInsertion(false, PlacementCheck.Allow());

    if (!_cards.ContainsKey(leftCell) && !_cards.ContainsKey(rightCell))
      return deny("Здесь свободно — карточку можно положить обычно.");
    return deny($"Перегородка должна быть вплотную к карточке родительской теории «{parentTheory}» и в поле должно хватить места для сдвига.");
  }

  /// <summary>
  /// Вставить дочернюю теорию в перегородку (см. <see cref="CheckInWall"/>): теории слева сдвигаются на 1 столбец влево,
  /// и карточка ложится в освободившийся столбец.
  /// </summary>
  /// <returns>Условия, на которых карточка положена.</returns>
  /// <exception cref="InvalidOperationException">Карточку сюда класть нельзя.</exception>
  public PlacementCheck PlaceInWall(Card card, int boundary, Player player)
  {
    var insertion = WallInsertionAt(card, boundary);
    if (!insertion.Check.IsAllowed)
      throw new InvalidOperationException(insertion.Check.Reason);

    var row = SideTheoryRow;
    if (insertion.ToLeft)
    {
      // Сдвигаем левые теории от ближней к дальней: их новые столбцы всегда свободны.
      var left = _theoryColumns.Where(column => column.TheoryCell.Column < boundary).OrderBy(column => column.TheoryCell.Column).ToList();
      foreach (var column in left)
        MoveColumn(column, -1);

      // Сдвинутые теории не должны выпасть за общую зону: зона тянется влево.
      var leftmost = left.Select(column => column.TheoryCell.Column).DefaultIfEmpty(CommonZone.Left).Min();
      if (leftmost < CommonZone.Left)
      {
        CommonZone = CommonZone with { Left = leftmost, Width = CommonZone.Width + CommonZone.Left - leftmost };
        CommonZoneChanged?.Invoke(CommonZone);
      }

      return Place(card, new Cell(boundary - 1, row), player);
    }

    // Правые теории сдвигаем от дальней к ближней, чтобы не наступить на соседний столбец.
    var right = _theoryColumns.Where(column => column.TheoryCell.Column >= boundary).OrderByDescending(column => column.TheoryCell.Column).ToList();
    foreach (var column in right)
      MoveColumn(column, 1);

    var rightmost = right.Select(column => column.TheoryCell.Column).DefaultIfEmpty(CommonZone.Left + CommonZone.Width - 1).Max();
    var zoneRight = CommonZone.Left + CommonZone.Width - 1;
    if (rightmost > zoneRight)
    {
      CommonZone = CommonZone with { Width = rightmost - CommonZone.Left + 1 };
      CommonZoneChanged?.Invoke(CommonZone);
    }

    return Place(card, new Cell(boundary, row), player);
  }

  /// <summary>
  /// Граница для вставки дочерней теории, если её кладут поверх соседней карточки в ряду дочерних теорий
  /// (справа или слева от семейства теорий): сама карточка и все теории на её стороне сдвигаются на 1 столбец
  /// (см. <see cref="PlaceInWall"/>).
  /// </summary>
  /// <returns>Граница (номер столбца справа от неё) или <c>null</c>, если над этой ячейкой так класть нельзя.</returns>
  public int? NeighbourBoundary(Card card, Cell cell)
  {
    if (card is not PersonCard { Theory: { Parent: { } parentTheory } }
      || !_cards.ContainsKey(cell)
      || cell.Row != SideTheoryRow
      || ColumnOf(parentTheory) is not { } parent)
    {
      return null;
    }

    var family = FamilyColumns(parent, parentTheory);
    if (FamilyContains(family, cell))
      return null;
    if (FamilyContains(family, cell.Right))
      return cell.Column + 1;
    if (FamilyContains(family, cell.Left))
      return cell.Column;
    return null;
  }

  /// <summary>
  /// Стрелки над местами, куда можно положить дочернюю теорию этого семейства: слева от крайней левой и справа
  /// от крайней правой теории семейства в ряду дочерних теорий (<see cref="ArrowTargets"/>).
  /// </summary>
  /// <returns>Ячейка и сторона: <c>true</c> — слева от семейства.</returns>
  public IEnumerable<(Cell Cell, bool ToLeft)> ArrowTargets(Card card, Player player)
  {
    if (card is not PersonCard { Theory: { Parent: { } parentTheory } }
      || ColumnOf(parentTheory) is not { } parent)
    {
      return [];
    }

    var row = SideTheoryRow;
    var anchors = FamilyColumns(parent, parentTheory).SelectMany(column => column.Cells).Where(cell => cell.Row == row).ToList();
    if (anchors.Count == 0)
      return [];

    var left = new Cell(anchors.Min(cell => cell.Column) - 1, row);
    var right = new Cell(anchors.Max(cell => cell.Column) + 1, row);
    return new[] { (left, true), (right, false) }
      .Where(target => ArrowTargetAllowed(card, target.Item1, player))
      .Select(target => (target.Item1, target.Item2));
  }

  private bool ArrowTargetAllowed(Card card, Cell cell, Player player)
  {
    if (_cards.ContainsKey(cell))
      return NeighbourBoundary(card, cell) is { } boundary && CheckInWall(card, boundary, player).IsAllowed;
    return Check(card, cell, player).IsAllowed;
  }

  /// <summary>
  /// Проверка для карточки, которую тащат над ячейкой: если это соседняя карточка семейства, то условия вставки, иначе обычные.
  /// </summary>
  public PlacementCheck CheckDrop(Card card, Cell cell, Player player) =>
    NeighbourBoundary(card, cell) is { } boundary ? CheckInWall(card, boundary, player) : Check(card, cell, player);

  private List<TheoryColumn> FamilyColumns(TheoryColumn parent, Theory parentTheory) =>
    _theoryColumns.Where(column => column == parent || column.Theory.Parent == parentTheory).ToList();

  private static bool FamilyContains(IEnumerable<TheoryColumn> family, Cell cell) => family.Any(column => column.Cells.Contains(cell));

  private static void MoveCells<T>(IDictionary<Cell, T> values, IReadOnlyList<(Cell From, Cell To)> moves)
  {
    var moved = moves.Where(move => values.ContainsKey(move.From)).Select(move => (move.To, values[move.From])).ToList();
    foreach (var move in moves)
      values.Remove(move.From);
    foreach (var (to, value) in moved)
      values[to] = value;
  }

  private static void MoveCells(ISet<Cell> cells, IReadOnlyList<(Cell From, Cell To)> moves)
  {
    var moved = moves.Where(move => cells.Contains(move.From)).Select(move => move.To).ToList();
    foreach (var move in moves)
      cells.Remove(move.From);
    foreach (var to in moved)
      cells.Add(to);
  }

  /// <summary>
  /// Сколько $ стоит одна единица суммарного убеждения теории, чтобы присоединить к ней карточку без теории.
  /// </summary>
  public const int JoiningPricePerInfluence = 2;

  private PlacementCheck CheckJoining(Cell cell, Player player, PersonCard person)
  {
    if (_theoryColumns.Count == 0)
      return PlacementCheck.Deny("На поле ещё нет ни одной теории — карточку без теории не к чему присоединить.");

    var column = _theoryColumns.FirstOrDefault(theoryColumn => theoryColumn.Top.Above == cell);
    if (column is null)
      return PlacementCheck.Deny("Карточку без теории можно положить только сверху стопки одной из теорий.");

    var cost = Math.Max(0, JoiningPricePerInfluence * column.Influence - person.JoiningDiscount);
    return player.CanAfford(cost)
      ? PlacementCheck.Allow(cost, column)
      : PlacementCheck.Deny($"Недостаточно денег: чтобы присоединиться к теории «{column.Theory}», нужно {cost}$, а у вас {player.Money}$.");
  }
}
