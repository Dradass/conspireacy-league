using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Decks;
using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Passives;

namespace ConspiracyLeague.Core.Game;

/// <summary>
/// Сессия игры — всё состояние партии: игроки и их балансы, очерёдность ходов, поле с личными штабами
/// и выложенные теории (какие карточки в каждой теории, кто их владельцы, сколько адептов).
/// </summary>
/// <remarks>
/// Партия идёт по фазам (<see cref="GamePhase"/>): подготовка (<see cref="Start"/>), затем раунды.
/// Раунд: игроки ходят, пока все не спасуют (<see cref="Pass"/>). За ход можно сделать <see cref="ActionsPerTurn"/>
/// действия — купить карточку, выложить её на поле, сыграть карточку; после последнего ход сам переходит
/// к следующему игроку. Игрок, которому нечем ходить (<see cref="HasMoves"/>), пасует автоматически. Затем закрываются несостоятельные штабы
/// (<see cref="CloseInsolventTheories"/>), адепты приносят деньги (<see cref="PayNextTheoryIncome"/>),
/// сыгранные карточки переворачиваются лицом вверх (<see cref="RefreshPlayedCards"/>), обновляется рынок (<see cref="RefreshMarket"/>), и начинается следующий раунд (<see cref="StartNextRound"/>).
/// Если при обновлении рынка в колоде не осталось карточек, идёт итоговый подсчёт: владельцы действующих теорий
/// получают деньги за лидеров (<see cref="PayNextLeadersBonus"/>), адепты ещё раз приносят деньги
/// (<see cref="PayNextTheoryIncome"/>), и партия завершена (<see cref="Ranking"/>).
/// Шаги конца раунда вызываются по одному, чтобы между ними можно было показать, что произошло.
/// </remarks>
public sealed class GameSession
{
  /// <summary>
  /// Сколько $ у каждого игрока в начале партии.
  /// </summary>
  public const int StartingMoney = 10;

  /// <summary>
  /// Сколько действий можно сделать за ход: купить карточку, выложить её на поле, сыграть карточку.
  /// </summary>
  public const int ActionsPerTurn = 2;

  private readonly Queue<TheoryColumn> _unpaidTheories = new();
  private readonly Random _random;

  /// <summary>
  /// Игроки и очерёдность их ходов.
  /// </summary>
  public TurnOrder Turns { get; }

  /// <summary>
  /// Игровое поле.
  /// </summary>
  public PlayingField Field { get; }

  /// <summary>
  /// Рынок: общая колода и открытые карточки.
  /// </summary>
  public Market Market { get; }

  /// <summary>
  /// Игроки в порядке хода.
  /// </summary>
  public IReadOnlyList<Player> Players => Turns.Players;

  /// <summary>
  /// Игрок, который сейчас ходит.
  /// </summary>
  public Player CurrentPlayer => Turns.CurrentPlayer;

  /// <summary>
  /// Выложенные теории.
  /// </summary>
  public IReadOnlyList<TheoryColumn> Theories => Field.TheoryColumns;

  /// <summary>
  /// Текущая фаза партии.
  /// </summary>
  public GamePhase Phase { get; private set; } = GamePhase.Setup;

  /// <summary>
  /// Номер раунда, с единицы; 0 — партия ещё не началась.
  /// </summary>
  public int Round { get; private set; }

  /// <summary>
  /// Сколько действий осталось у игрока, который сейчас ходит.
  /// </summary>
  public int ActionsLeft { get; private set; }

  /// <summary>
  /// Проверка успеха карточки, которую сейчас играют (<see cref="CheckSuccess"/>), или <c>null</c>.
  /// Пока она не завершена (<see cref="UseCard"/>), других действий сделать нельзя.
  /// </summary>
  public SuccessCheck? PendingCheck { get; private set; }

  /// <summary>
  /// Началась или завершилась проверка успеха; аргумент — <see cref="PendingCheck"/>.
  /// </summary>
  public event Action<SuccessCheck?>? PendingCheckChanged;

  /// <summary>
  /// Выбор цели для действия карточки, только что выложенной в штаб теории (<see cref="PlaceFromHand"/>),
  /// или <c>null</c>. Пока он не завершён (<see cref="ResolvePlacement"/>), других действий сделать нельзя.
  /// </summary>
  public PlacementChoice? PendingPlacement { get; private set; }

  /// <summary>
  /// Начался или завершился выбор цели для действия при выкладывании; аргумент — <see cref="PendingPlacement"/>.
  /// </summary>
  public event Action<PlacementChoice?>? PendingPlacementChanged;

  /// <summary>
  /// Изменилось количество оставшихся действий; аргумент — сколько осталось.
  /// </summary>
  public event Action<int>? ActionsLeftChanged;

  /// <summary>
  /// Игрок спасовал автоматически — ходить ему нечем; аргумент — этот игрок.
  /// </summary>
  public event Action<Player>? AutoPassed;

  /// <summary>
  /// Фаза партии сменилась; аргумент — новая фаза.
  /// </summary>
  public event Action<GamePhase>? PhaseChanged;

  /// <summary>
  /// Подготовить партию: поле стандартного размера, личные штабы игроков стоят в ряд внизу поля,
  /// перемешанная общая колода (<see cref="MarketDeck"/>) лежит на рынке.
  /// </summary>
  /// <param name="players">Игроки в порядке хода.</param>
  /// <param name="random">Источник случайности; по умолчанию — общий.</param>
  public GameSession(IEnumerable<Player> players, Random? random = null)
  {
    Turns = new TurnOrder(players);
    Field = new PlayingField(FieldLayout.Columns, FieldLayout.Rows, FieldLayout.CommonZone(Players.Count));
    for (var i = 0; i < Players.Count; i++)
      Field.AddPlayer(Players[i], FieldLayout.PlayerZone(i, Players.Count));
    _random = random ?? Random.Shared;
    Market = new Market(FieldLayout.MarketZone, MarketDeck.Create(Players.Count, _random));
    // Подписываемся первыми: к моменту, когда о новом ходе узнают остальные, действия уже восстановлены.
    Turns.TurnChanged += _ => SetActionsLeft(ActionsPerTurn);
  }

  /// <summary>
  /// Сколько карточек открыто в каждой строке рынка: по две на игрока, всего на рынке 4N.
  /// </summary>
  public int MarketCardsPerRow => Players.Count * 2;

  /// <summary>
  /// Подготовка и начало первого раунда: каждый игрок получает стартовую руку (<see cref="StartingHand"/>) —
  /// новых бездомных и граждан со случайными теориями, на рынке открывается 4N карточек: в первую и третью строки по <see cref="MarketCardsPerRow"/>.
  /// </summary>
  public void Start()
  {
    RequirePhase(GamePhase.Setup);
    foreach (var player in Players)
    {
      foreach (var card in StartingHand.Create(_random))
        player.Hand.Add(card);
    }

    Market.OpenStartingCards(MarketCardsPerRow);
    StartNextRound();
  }

  /// <summary>
  /// Игрок, который сейчас ходит, покупает карточку с рынка в руку. Это действие.
  /// </summary>
  /// <param name="cell">Ячейка рынка.</param>
  /// <returns>Купленная карточка.</returns>
  public PersonCard BuyFromMarket(Cell cell)
  {
    RequireAction();
    var card = Market.Buy(cell, CurrentPlayer);
    CompleteAction();
    return card;
  }

  /// <summary>
  /// Игрок, который сейчас ходит, выкладывает карточку с руки на поле — в личный штаб или в общую зону. Это действие.
  /// Если у карточки в штабе теории есть действие с выбором цели и выбрать есть что, действие завершается
  /// только после выбора (<see cref="PendingPlacement"/>, <see cref="ResolvePlacement"/>).
  /// </summary>
  /// <returns>Условия, на которых карточка положена.</returns>
  /// <exception cref="InvalidOperationException">Карточки нет в руке или её сюда класть нельзя.</exception>
  public PlacementCheck PlaceFromHand(Card card, Cell cell) =>
    PlaceCardFromHand(card, () => Field.Place(card, cell, CurrentPlayer));

  /// <summary>
  /// Игрок, который сейчас ходит, вставляет дочернюю теорию с руки в перегородку между столбцами
  /// (см. <see cref="PlayingField.CheckInWall"/>). Это действие.
  /// </summary>
  /// <returns>Условия, на которых карточка положена.</returns>
  /// <exception cref="InvalidOperationException">Карточки нет в руке или её сюда класть нельзя.</exception>
  public PlacementCheck PlaceInWallFromHand(Card card, int boundary) =>
    PlaceCardFromHand(card, () => Field.PlaceInWall(card, boundary, CurrentPlayer));

  private PlacementCheck PlaceCardFromHand(Card card, Func<PlacementCheck> place)
  {
    RequireAction();
    var hand = CurrentPlayer.Hand;
    if (!hand.Contains(card))
      throw new InvalidOperationException($"Карточки «{card}» нет в руке игрока {CurrentPlayer}.");

    var check = place();
    // Выложенная теория могла сдвинуться в соседний столбец вместе с карточкой — берём её актуальную ячейку.
    var placedCell = Field.CellOf(card) ?? throw new InvalidOperationException("Карточка не легла на поле.");
    hand.Remove(card);
    if (Field.ColumnAt(placedCell) is { } theory && card is PersonCard { OnPlacedInHeadquarters: { RequiresTargetCell: true } effect })
    {
      var choice = new PlacementChoice(placedCell, effect, theory);
      if (effect.TargetCells(PlacementContext(choice)).Any(target => target.Check.IsAllowed))
      {
        PendingPlacement = choice;
        PendingPlacementChanged?.Invoke(choice);
        return check;
      }

      // Выбрать нечего — действие выполняется без цели (например, получить деньги).
      effect.Execute(PlacementContext(choice));
    }

    CompleteAction();
    return check;
  }

  /// <summary>
  /// Контекст для действия карточки, выложенной в штаб теории: штаб и сама карточка.
  /// </summary>
  public GameContext PlacementContext(PlacementChoice choice) =>
    CreateContext().WithTargetTheory(choice.Theory).WithSourceCell(choice.Cell);

  /// <summary>
  /// Завершить выбор цели для действия карточки, выложенной в штаб теории (<see cref="PendingPlacement"/>):
  /// выполнить действие с выбранными карточками или без них (игрок отказался) и завершить действие хода.
  /// </summary>
  /// <param name="target">Выбранная карточка или <c>null</c>, если игрок отказался.</param>
  /// <param name="second">Вторая карточка (<see cref="Effect.RequiresSecondTargetCell"/>).</param>
  /// <exception cref="InvalidOperationException">Выбора нет или карточки выбрать нельзя.</exception>
  public void ResolvePlacement(Cell? target = null, Cell? second = null)
  {
    var choice = PendingPlacement ?? throw new InvalidOperationException("Сейчас не выбирают цель для действия выложенной карточки.");
    var effect = choice.Effect;
    var context = PlacementContext(choice);
    if (target is { } first)
    {
      context = context.WithTargetCell(RequireTarget(effect, first, context));
      if (effect.RequiresSecondTargetCell)
      {
        if (second is not { } other)
          throw new ArgumentException($"Для действия «{effect}» нужно выбрать две карточки.", nameof(second));
        context = context.WithSecondTargetCell(RequireTarget(effect, other, context));
      }
    }
    else if (!effect.TargetIsOptional)
      throw new ArgumentException($"Для действия «{effect}» нужно выбрать карточку.", nameof(target));

    PendingPlacement = null;
    PendingPlacementChanged?.Invoke(null);
    effect.Execute(context);
    CompleteAction();
  }

  private static Cell RequireTarget(Effect effect, Cell cell, GameContext context)
  {
    var check = effect.CheckTargetCell(cell, context);
    return check.IsAllowed ? cell : throw new InvalidOperationException(check.Reason);
  }

  /// <summary>
  /// Игрок, который сейчас ходит, начинает играть карточку из своего личного штаба: проводится проверка успеха
  /// (<see cref="SuccessCheck"/>). Розыгрыш завершается вызовом <see cref="UseCard"/>.
  /// </summary>
  /// <param name="cell">Ячейка карточки.</param>
  /// <returns>Проверка успеха.</returns>
  /// <exception cref="InvalidOperationException">Карточку нельзя использовать.</exception>
  public SuccessCheck CheckSuccess(Cell cell)
  {
    RequireAction();
    if (!Field.CanUse(cell, CurrentPlayer))
      throw new InvalidOperationException("Эту карточку нельзя использовать.");

    var check = SuccessCheck.Perform(cell, Field.SuccessChanceAt(cell), _random);
    SetPendingCheck(check);
    return check;
  }

  /// <summary>
  /// Завершить розыгрыш карточки после проверки успеха (<see cref="CheckSuccess"/>). Это действие.
  /// Если проверка успешна, выполняется выбранный вариант действия карточки (<see cref="PlayingField.Use"/>),
  /// иначе действие не выполняется (<see cref="PlayingField.Waste"/>). В обоих случаях карточка переворачивается
  /// (после быстрого действия — поворачивается). Быстрое действие (<see cref="Effect.IsQuick"/>) не тратит действие хода.
  /// </summary>
  /// <param name="option">Выбранный вариант действия карточки.</param>
  /// <param name="context">Контекст партии.</param>
  /// <returns>Проверка успеха.</returns>
  /// <exception cref="InvalidOperationException">Проверка успеха не проводилась.</exception>
  public SuccessCheck UseCard(Effect option, GameContext context)
  {
    var check = PendingCheck ?? throw new InvalidOperationException("Сначала нужно провести проверку успеха.");
    SetPendingCheck(null);
    RequireAction();
    if (check.Succeeded)
      Field.Use(check.Cell, option, context);
    else
      Field.Waste(check.Cell, CurrentPlayer, option.IsQuick);
    // Быстрое действие не тратит действие игрока, пока карточка только поворачивается. Если же она перевернулась
    // рубашкой вверх (второе быстрое действие), действие игрока тратится.
    if (!option.IsQuick || Field.IsUsed(check.Cell))
      CompleteAction();
    return check;
  }

  /// <summary>
  /// Игрок, который сейчас ходит, применяет свою карточку теории (<see cref="PlayingField.UseTheoryCard"/>).
  /// Это действие. Карточка переворачивается и восстанавливается в фазе обновления карточек.
  /// </summary>
  /// <param name="card">Карточка теории.</param>
  /// <param name="context">Контекст партии с выбранными целями (например, теорией).</param>
  public void UseTheoryCard(OwnedTheoryCard card, GameContext context)
  {
    // Перенос «Шапочки из фольги» ничего не тратит: она уже лежит на персонаже.
    var move = card.AttachedTo is not null && card.Card.Passive is TinFoilPassive;
    if (move)
    {
      RequireNoPendingCheck();
      RequirePhase(GamePhase.PlayerTurns);
    }
    else
      RequireAction();

    var trigger = Field.UseTheoryCard(card, context);
    if (trigger is not null)
      SetPendingTheoryTrigger(trigger);
    else if (!move)
      CompleteAction();
  }

  /// <summary>
  /// Действие основной теории, которое ждёт выбора владельца основной теории (после <see cref="UseTheoryCard"/>), или <c>null</c>.
  /// Пока оно не разрешено, другие действия сделать нельзя; ход не заканчивается.
  /// </summary>
  public TheoryTrigger? PendingTheoryTrigger { get; private set; }

  /// <summary>
  /// Начался или завершился выбор для действия основной теории; аргумент — <see cref="PendingTheoryTrigger"/>.
  /// </summary>
  public event Action<TheoryTrigger?>? PendingTheoryTriggerChanged;

  /// <summary>
  /// Владелец основной теории выбирает карточку в руке для её действия.
  /// </summary>
  /// <exception cref="InvalidOperationException">Выбора нет или карточку выбрать нельзя.</exception>
  public void SelectTriggerHandCard(Card card)
  {
    var trigger = RequirePendingTheoryTrigger();
    var effect = trigger.Effect;
    if (!(effect.RequiresHandCard || effect.AcceptsHandCardTarget) || !trigger.Owner.Hand.Contains(card))
      throw new InvalidOperationException("Эту карточку выбрать нельзя.");
    if (!effect.RequiresHandCard && effect.CheckHandCard(card, trigger.Context) is { IsAllowed: false } denied)
      throw new InvalidOperationException(denied.Reason);
    trigger.HandCard = card;
    ContinueTheoryTrigger(trigger);
  }

  /// <summary>
  /// Владелец основной теории выбирает теорию для её действия.
  /// </summary>
  /// <exception cref="InvalidOperationException">Выбора нет или теорию выбрать нельзя.</exception>
  public void SelectTriggerTheory(TheoryColumn theory)
  {
    var trigger = RequirePendingTheoryTrigger();
    if (!trigger.Effect.RequiresTheory || !trigger.Effect.CanTargetTheory(theory, trigger.Owner))
      throw new InvalidOperationException($"Теорию «{theory}» выбрать нельзя.");
    trigger.Theory = theory;
    ContinueTheoryTrigger(trigger);
  }

  /// <summary>
  /// Владелец основной теории выбирает карточку на поле для её действия.
  /// </summary>
  /// <exception cref="InvalidOperationException">Выбора нет или карточку выбрать нельзя.</exception>
  public void SelectTriggerCell(Cell cell)
  {
    var trigger = RequirePendingTheoryTrigger();
    if (!trigger.Effect.RequiresTargetCell)
      throw new InvalidOperationException("Здесь нужен выбор не карточки на поле.");
    var check = trigger.Effect.CheckTargetCell(cell, trigger.Context);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);
    trigger.Cell = cell;
    ContinueTheoryTrigger(trigger);
  }

  /// <summary>
  /// Владелец основной теории отказывается от её действия: оно не выполняется, действие хода тратится.
  /// </summary>
  public void SkipTheoryTrigger()
  {
    RequirePendingTheoryTrigger();
    SetPendingTheoryTrigger(null);
    CompleteAction();
  }

  private TheoryTrigger RequirePendingTheoryTrigger() =>
    PendingTheoryTrigger ?? throw new InvalidOperationException("Сейчас ничего не выбирают.");

  /// <summary>
  /// Все выборы сделаны — действие выполняется, и тратится действие хода игрока, применившего дочернюю карточку.
  /// Иначе ждём остальных выборов.
  /// </summary>
  private void ContinueTheoryTrigger(TheoryTrigger trigger)
  {
    if (!trigger.IsComplete)
    {
      PendingTheoryTriggerChanged?.Invoke(trigger);
      return;
    }

    SetPendingTheoryTrigger(null);
    try
    {
      Field.ResolveTheoryTrigger(trigger);
    }
    catch (Exception)
    {
      SetPendingTheoryTrigger(trigger);
      throw;
    }
    CompleteAction();
  }

  private void SetPendingTheoryTrigger(TheoryTrigger? trigger)
  {
    PendingTheoryTrigger = trigger;
    PendingTheoryTriggerChanged?.Invoke(trigger);
  }

  /// <summary>
  /// Передать ход следующему игроку, который ещё не спасовал.
  /// </summary>
  public void EndTurn()
  {
    RequireNoPendingCheck();
    RequirePhase(GamePhase.PlayerTurns);
    Turns.EndTurn();
    AutoPass();
  }

  /// <summary>
  /// Игрок, который сейчас ходит, пасует и до конца раунда больше не ходит. Когда спасуют все,
  /// начинается проверка штабов теорий.
  /// </summary>
  public void Pass()
  {
    RequireNoPendingCheck();
    RequirePhase(GamePhase.PlayerTurns);
    PassCurrent();
    AutoPass();
  }

  /// <summary>
  /// Может ли игрок ещё что-то сделать: у него есть карточки в руке, несыгранные карточки в личном штабе
  /// или на рынке есть карточки.
  /// </summary>
  public bool HasMoves(Player player) =>
    player.Hand.Cards.Count > 0 || Field.HasUnusedCards(player) || Market.OpenCards.Count > 0;

  /// <summary>
  /// Проверка штабов теорий: закрыть несостоятельные (<see cref="TheoryColumn.IsInsolvent"/>) — те, где больше
  /// <see cref="TheoryColumn.SolvencyCheckLeadersThreshold"/> лидеров и бездомных больше, чем остальных персонажей.
  /// После этого адепты действующих теорий приносят деньги.
  /// </summary>
  /// <returns>Штабы, закрытые сейчас.</returns>
  public IReadOnlyList<TheoryColumn> CloseInsolventTheories()
  {
    RequirePhase(GamePhase.HeadquartersCheck);
    var closed = Field.CloseInsolventTheories();

    // Пассивные способности карточек теорий срабатывают в конце раунда, уже после закрытия штабов.
    foreach (var owned in Field.TheoryCards.ToList())
    {
      var earned = 0;
      void OnEarned(Player _, int amount) => earned += amount;

      owned.Owner.Earned += OnEarned;
      try
      {
        owned.Card.HandleEvent(new RoundEnded(), new GameContext(owned.Owner, Field, _random, Market));
      }
      finally
      {
        owned.Owner.Earned -= OnEarned;
      }
      Field.ReportTheoryEarned(owned, earned);
    }

    EnqueueActiveTheories(column => column.Adepts > 0);
    SetPhase(GamePhase.AdeptIncome);
    return closed;
  }

  /// <summary>
  /// Адепты следующей действующей теории приносят деньги её лидерам (<see cref="TheoryIncome"/>).
  /// Когда все теории получили доход, в обычном раунде миплов-лидеров (<see cref="TheoryColumn.LeaderMeeples"/>) снимают
  /// и начинается обновление карточек; в итоговом подсчёте миплы остаются, и партия завершается.
  /// </summary>
  /// <returns>Доход теории или <c>null</c>, если все теории уже получили доход.</returns>
  public TheoryIncome? PayNextTheoryIncome()
  {
    if (Phase is not (GamePhase.AdeptIncome or GamePhase.FinalAdeptIncome))
      throw new InvalidOperationException($"Доход от адептов нельзя получить в фазе {Phase}.");
    if (!_unpaidTheories.TryDequeue(out var column))
    {
      // Миплы-лидеры снимаются после дохода от адептов в обычном раунде; в итоговом подсчёте остаются.
      if (Phase == GamePhase.AdeptIncome)
        Field.RemoveLeaderMeeples();
      SetPhase(Phase == GamePhase.AdeptIncome ? GamePhase.CardsRefresh : GamePhase.GameOver);
      return null;
    }

    var income = TheoryIncome.Of(column);
    foreach (var (player, amount) in income.ByPlayer)
      player.Earn(amount);
    return income;
  }

  /// <summary>
  /// Обновление карточек: сыгранные карточки личных штабов переворачиваются лицевой стороной вверх.
  /// После этого обновляется рынок.
  /// </summary>
  public void RefreshPlayedCards()
  {
    RequirePhase(GamePhase.CardsRefresh);
    Field.RefreshUsedCards();
    SetPhase(GamePhase.MarketRefresh);
  }

  /// <summary>
  /// Обновить рынок: нижняя строка уходит, верхняя сдвигается вниз, пустые места заполняются из общей колоды.
  /// Если в колоде не осталось карточек для выкладывания, рынок не обновляется — начинается итоговый подсчёт.
  /// </summary>
  /// <returns><c>true</c>, если рынок обновлён и будет следующий раунд; <c>false</c>, если начался итоговый подсчёт.</returns>
  public bool RefreshMarket()
  {
    RequirePhase(GamePhase.MarketRefresh);
    if (Market.DeckCount == 0)
    {
      // Перед итоговым подсчётом все закрытые карточки раскрываются: их владельцы получают бонусы или карточки уходят.
      Field.RevealAllHiddenCards();
      EnqueueActiveTheories(column => true);
      SetPhase(GamePhase.FinalLeadersBonus);
      return false;
    }

    Market.Refresh();
    return true;
  }

  /// <summary>
  /// Итоговый подсчёт: владелец следующей действующей теории получает по <see cref="LeadersBonus.PerLeader"/>$
  /// за каждого её лидера. Когда награды выданы, адепты ещё раз приносят деньги (<see cref="PayNextTheoryIncome"/>).
  /// </summary>
  /// <returns>Награда или <c>null</c>, если все владельцы её уже получили.</returns>
  public LeadersBonus? PayNextLeadersBonus()
  {
    RequirePhase(GamePhase.FinalLeadersBonus);
    if (!_unpaidTheories.TryDequeue(out var column))
    {
      EnqueueActiveTheories(column => column.Adepts > 0);
      SetPhase(GamePhase.FinalAdeptIncome);
      return null;
    }

    var bonus = LeadersBonus.Of(column);
    bonus.Owner.Earn(bonus.Amount);
    return bonus;
  }

  /// <summary>
  /// Итоги партии: игроки от самого богатого к самому бедному. При равенстве денег — в порядке хода.
  /// </summary>
  public IReadOnlyList<Player> Ranking => Players.OrderByDescending(player => player.Money).ToList();

  /// <summary>
  /// Начать следующий раунд: пасы сброшены, первым ходит игрок с жетоном первого игрока. В первом раунде
  /// он выбирается случайно, затем в начале каждого следующего раунда жетон переходит по кругу направо.
  /// </summary>
  public void StartNextRound()
  {
    if (Phase is not (GamePhase.Setup or GamePhase.MarketRefresh))
      throw new InvalidOperationException($"Новый раунд нельзя начать в фазе {Phase}.");

    var first = Round == 0 ? Players[_random.Next(Players.Count)] : Turns.NextAfter(Turns.FirstPlayer);
    Round++;
    SetPhase(GamePhase.PlayerTurns);
    Turns.StartRound(first);
    AutoPass();
  }

  /// <summary>
  /// Контекст для действий игрока, который сейчас ходит.
  /// </summary>
  public GameContext CreateContext() => new(CurrentPlayer, Field, _random, Market);

  private void RequireAction()
  {
    RequireNoPendingCheck();
    RequirePhase(GamePhase.PlayerTurns);
    if (ActionsLeft == 0)
      throw new InvalidOperationException($"У игрока {CurrentPlayer} не осталось действий в этом ходу.");
  }

  /// <summary>
  /// Действие сделано: если действий больше не осталось, ход переходит к следующему игроку.
  /// </summary>
  private void CompleteAction()
  {
    SetActionsLeft(ActionsLeft - 1);
    if (ActionsLeft == 0)
      Turns.EndTurn();
    AutoPass();
  }

  /// <summary>
  /// Пока игроку, который сейчас ходит, нечем ходить, он пасует автоматически.
  /// </summary>
  /// <remarks>
  /// Вызывается после смены хода, а не из события смены хода: иначе подписчики получили бы события
  /// о ходах вперемешку.
  /// </remarks>
  private void AutoPass()
  {
    while (Phase == GamePhase.PlayerTurns && !HasMoves(CurrentPlayer))
    {
      AutoPassed?.Invoke(CurrentPlayer);
      PassCurrent();
    }
  }

  private void PassCurrent()
  {
    Turns.Pass();
    if (Turns.AllPassed)
      SetPhase(GamePhase.HeadquartersCheck);
  }

  private void RequireNoPendingCheck()
  {
    if (PendingCheck is not null)
      throw new InvalidOperationException("Идёт проверка успеха карточки — дождитесь её окончания.");
    if (PendingPlacement is not null)
      throw new InvalidOperationException("Сначала выберите цель для действия выложенной карточки.");
    if (PendingTheoryTrigger is not null)
      throw new InvalidOperationException("Владелец основной теории ещё выбирает цель её действия.");
  }

  private void SetPendingCheck(SuccessCheck? check)
  {
    PendingCheck = check;
    PendingCheckChanged?.Invoke(check);
  }

  private void SetActionsLeft(int actionsLeft)
  {
    ActionsLeft = actionsLeft;
    ActionsLeftChanged?.Invoke(actionsLeft);
  }

  private void RequirePhase(GamePhase phase)
  {
    if (Phase != phase)
      throw new InvalidOperationException($"Это можно сделать только в фазе {phase}, а сейчас {Phase}.");
  }

  /// <summary>
  /// Поставить в очередь на выплату действующие (не закрытые) теории, подходящие под условие.
  /// </summary>
  private void EnqueueActiveTheories(Func<TheoryColumn, bool> predicate)
  {
    _unpaidTheories.Clear();
    // Слева направо: деньги за теории выплачиваются от левого края поля к правому.
    foreach (var column in Field.TheoryColumns.Where(column => !column.IsClosed && predicate(column)).OrderBy(column => column.TheoryCell.Column))
      _unpaidTheories.Enqueue(column);
  }

  private void SetPhase(GamePhase phase)
  {
    Phase = phase;
    PhaseChanged?.Invoke(phase);
  }
}
