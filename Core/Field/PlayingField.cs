using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Cards.Professionals;
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
/// <item>новую дочернюю теорию — во второй ряд общей зоны, вплотную слева или справа от карточки родительской теории
/// или уже выложенной дочерней теории того же родителя;</item>
/// <item>карточку уже выложенной теории — сверху её стопки;</item>
/// <item>карточку без теории — сверху стопки любой теории, заплатив суммарное влияние персон этой стопки;</item>
/// <item>закрытую карточку (<see cref="HiddenCard"/>) — как карточку без теории: она считается гражданином без теории.
/// Раскрытая закрытая карточка (<see cref="Reveal"/>) видна всем, но по-прежнему считается гражданином без теории.</item>
/// </list>
/// В закрытые штабы теорий карточки класть нельзя.
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
  /// Сыгранные карточки перевернулись лицевой стороной вверх; аргумент — их ячейки.
  /// </summary>
  public event Action<IReadOnlyList<Cell>>? UsedCardsRefreshed;

  /// <summary>
  /// Закрытая карточка раскрыта — теперь её видят все; аргумент — её ячейка.
  /// </summary>
  public event Action<Cell>? HiddenCardRevealed;

  /// <summary>
  /// Игрок посмотрел в закрытую карточку; аргументы — её ячейка и игрок.
  /// </summary>
  public event Action<Cell, Player>? HiddenCardPeeked;

  /// <summary>
  /// В теорию поставлен мипл-лидер; аргументы — теория и чей мипл.
  /// </summary>
  public event Action<TheoryColumn, Player>? LeaderMeepleAdded;

  /// <summary>
  /// Миплов-лидеров сняли со всех теорий; аргумент — теории, где они стояли.
  /// </summary>
  public event Action<IReadOnlyList<TheoryColumn>>? LeaderMeeplesRemoved;

  /// <summary>
  /// Карточка личного штаба принесла деньги; аргументы — ячейка карточки, сколько принесло само действие
  /// и сколько добавили учёные (<see cref="ScientistPassive"/>).
  /// </summary>
  public event Action<Cell, int, int>? CardEarned;

  /// <summary>
  /// Вероятность успеха персонажа навсегда повышена (<see cref="Train"/>); аргумент — его ячейка.
  /// </summary>
  public event Action<Cell>? CardTrained;

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
  /// Две карточки теории поменялись местами.
  /// </summary>
  public event Action<Cell, Cell>? CardsSwapped;

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
      ? Math.Clamp(person.SuccessChancePercent + SuccessModifierAt(cell), 0, SuccessCheck.MaxChance)
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

    return cell.Surrounding.Count(neighbor => zone.Contains(neighbor) && CardAt(neighbor)?.Passive is ScientistPassive)
      * ScientistPassive.NeighborSuccessBonus
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
  public int SuccessModifierAt(Cell cell) => SuccessBonusAt(cell) - SuccessPenaltyAt(cell);

  /// <summary>
  /// Сколько $ добавляют учёные (<see cref="ScientistPassive"/>) в личном штабе игрока к деньгам, которые приносит
  /// его карточка личного штаба.
  /// </summary>
  public int MoneyBonusOf(Player player) =>
    _playerZones.TryGetValue(player, out var zone)
      ? zone.Cells.Count(cell => CardAt(cell)?.Passive is ScientistPassive) * ScientistPassive.MoneyBonus
      : 0;

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

    context = context.WithSourceCell(cell);
    var card = _cards[cell];
    if (!card.OnPlayFromHand.Options.Contains(option))
      throw new ArgumentException($"У карточки «{card}» нет действия «{option}».", nameof(option));
    RequireTargets(option, context);

    ExecuteWithMoneyBonus(cell, option, context);
    Play(cell, option.IsQuick);
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
    if (option.RequiresTargetCell && (option.BorrowsTargetCard ? context.BorrowedCell : context.TargetCell) is null)
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

    var bonus = MoneyBonusOf(player);
    player.Earn(bonus);
    // Деньги принесла карточка, действие которой выполнено: при использовании другой карточки — она.
    var source = option.BorrowsTargetCard && context.BorrowedCell is { } borrowed ? borrowed : cell;
    CardEarned?.Invoke(source, earned, bonus);
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
    var owner = _playerZones.Keys.FirstOrDefault(zonePlayer => _playerZones[zonePlayer].Contains(cell));
    if (owner is null || owner == player || CardAt(cell) is null)
      return false;

    var zone = _playerZones[owner];
    return cell.Surrounding.Any(neighbor =>
      zone.Contains(neighbor) && CardAt(neighbor)?.Passive is PolicemanPassive && !IsUsed(neighbor));
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

    var homeless = zone.Cells.Where(cell => CardAt(cell) is HomelessCard).ToList();
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
    if (owner == player)
      return PlacementCheck.Deny("Это ваша карточка — скопировать можно только карточку другого игрока.");
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
      .Where(zone => zone.Key != player)
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

    ExecuteBorrowed(cell, context);
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
  private Effect ExecuteBorrowed(Cell cell, GameContext context)
  {
    var card = _cards[cell];
    var option = context.BorrowedOption ?? throw new ArgumentException("Не выбрано действие карточки.", nameof(context));
    if (!card.OnPlayFromHand.Options.Contains(option))
      throw new ArgumentException($"У карточки «{card}» нет действия «{option}».", nameof(context));
    if (!option.CanBeBorrowed)
      throw new ArgumentException($"Действие «{option}» так выполнить нельзя.", nameof(context));
    RequireTargets(option, context);

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
  /// <exception cref="InvalidOperationException">Карточку нельзя использовать.</exception>
  public void Waste(Cell cell, Player player, bool quick = false)
  {
    if (!CanUse(cell, player))
      throw new InvalidOperationException("Эту карточку нельзя использовать.");

    Play(cell, quick);
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
  /// <exception cref="InvalidOperationException">Штаб теории закрыт.</exception>
  public void AddLeaderMeeple(TheoryColumn column, Player player)
  {
    if (!_theoryColumns.Contains(column))
      throw new ArgumentException($"Теория «{column}» не выложена на это поле.", nameof(column));
    if (column.IsClosed)
      throw new InvalidOperationException($"Штаб теории «{column}» закрыт.");

    column.AddLeaderMeeple(player);
    LeaderMeepleAdded?.Invoke(column, player);
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
      LeaderMeeplesRemoved?.Invoke(columns);
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

    from.RemoveAdepts(count);
    to.AddAdepts(count);
    AdeptsMoved?.Invoke(from, to, count);
  }

  /// <summary>
  /// Проверить, может ли игрок забрать в руку своего персонажа из личного штаба.
  /// </summary>
  /// <param name="cell">Ячейка персонажа.</param>
  /// <param name="player">Игрок.</param>
  /// <param name="acting">Карточки, которые выполняют действие: их забрать нельзя.</param>
  public PlacementCheck CheckTake(Cell cell, Player player, IReadOnlyCollection<Cell> acting)
  {
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
    // Персона пришла в штаб теории — выполняется её действие при выкладывании в штаб. Действие с выбором
    // карточки на поле выполняет сессия, когда игрок её выберет (GameSession.ResolvePlacement).
    if (joined is not null && !person.OnPlacedInHeadquarters.RequiresTargetCell)
      person.PlaceInHeadquarters(new GameContext(player, this).WithTargetTheory(joined).WithSourceCell(cell));
    return check;
  }

  /// <summary>
  /// Проверить, может ли игрок выложить карточку взакрытую (<see cref="HiddenCard"/>) сверху стопки теории.
  /// Закрытая карточка — гражданин без теории: за неё платят суммарное влияние стопки.
  /// </summary>
  public PlacementCheck CheckHidden(Card card, TheoryColumn column, Player player) =>
    Check(new HiddenCard(card), column.Top.Above, player);

  /// <summary>
  /// Выложить карточку взакрытую (<see cref="HiddenCard"/>) сверху стопки теории, заплатив за это.
  /// </summary>
  /// <returns>Условия, на которых карточка положена.</returns>
  /// <exception cref="InvalidOperationException">Карточку сюда класть нельзя.</exception>
  public PlacementCheck PlaceHidden(Card card, TheoryColumn column, Player player) =>
    Place(new HiddenCard(card), column.Top.Above, player);

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
    HiddenCardPeeked?.Invoke(cell, player);
  }

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

    return PlacementCheck.Allowed;
  }

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

    ((HiddenCard)_cards[cell]).Reveal();
    HiddenCardRevealed?.Invoke(cell);
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
  /// заплатив в банк её влияние.
  /// </summary>
  /// <exception cref="InvalidOperationException">Карточку перевернуть нельзя.</exception>
  public void FlipRival(Cell cell, Player player)
  {
    var check = CheckFlipRival(cell, player);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    player.Spend(check.Cost);
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
      CardEarned?.Invoke(source, amount, 0);
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
  }

  private void ReplaceInTheory(TheoryColumn column, Cell cell, PersonCard card)
  {
    column.Replace(cell, card);
    _cards[cell] = card;
    CardReplaced?.Invoke(cell);
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

  private PlacementCheck CheckJoining(Cell cell, Player player, PersonCard person)
  {
    if (_theoryColumns.Count == 0)
      return PlacementCheck.Deny("На поле ещё нет ни одной теории — карточку без теории не к чему присоединить.");

    var column = _theoryColumns.FirstOrDefault(theoryColumn => theoryColumn.Top.Above == cell);
    if (column is null)
      return PlacementCheck.Deny("Карточку без теории можно положить только сверху стопки одной из теорий.");
    if (column.IsClosed)
      return PlacementCheck.Deny($"Штаб теории «{column.Theory}» закрыт — в него больше нельзя класть карточки.");

    var cost = Math.Max(0, column.Influence - person.JoiningDiscount);
    return player.CanAfford(cost)
      ? PlacementCheck.Allow(cost, column)
      : PlacementCheck.Deny($"Недостаточно денег: чтобы присоединиться к теории «{column.Theory}», нужно {cost}$, а у вас {player.Money}$.");
  }
}
