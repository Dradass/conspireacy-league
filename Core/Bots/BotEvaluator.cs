using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Cards.Professionals;
using ConspiracyLeague.Core.Cards.Theories;
using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Passives;

namespace ConspiracyLeague.Core.Bots;

/// <summary>
/// Эвристическая оценка действий для бота (<see cref="Bot"/>) — в условных победных очках.
/// </summary>
/// <remarks>
/// Веса зависят от режима партии: в режиме развития теорий важнее всего владеть теориями с их карточками и влиянием,
/// в режиме денег — доход от адептов и деньги, в гонке — довести свои теории до финиша.
/// Выгода соперников считается со знаком минус, но с меньшим весом (<see cref="RivalWeight"/>): боту важно обогнать
/// их всех, а не навредить одному.
/// </remarks>
internal sealed partial class BotEvaluator
{
  private readonly GameSession _session;
  private readonly Player _player;
  private readonly BotProfile _profile;
  private readonly Dictionary<Player, double> _rivalWeights;

  public BotEvaluator(GameSession session, Player player, BotProfile profile)
  {
    _session = session;
    _player = player;
    _profile = profile.ForMode(session.Mode);
    RoundsLeft = EstimateRoundsLeft();
    _rivalWeights = RivalWeights();
    var early = RoundsLeft > 2;
    switch (session.Mode)
    {
      case GameMode.Money:
        MoneyWeight = 1.0;
        InfluenceWeight = 0.4;
        LeaderWeight = LeadersBonus.PerLeader * 0.5;
        NewTheoryWeight = 2;
        break;
      case GameMode.Race:
        MoneyWeight = early ? 0.15 : 0.05;
        InfluenceWeight = 0.3;
        LeaderWeight = 1.5;
        NewTheoryWeight = 2;
        break;
      default:
        // 5$ — одно очко, но в начале партии деньги ещё и покупают карточки.
        MoneyWeight = early ? 0.45 : 1.0 / VictoryPoints.MoneyPerPoint;
        InfluenceWeight = VictoryPoints.LeaderPointsPerInfluence;
        LeaderWeight = 0;
        NewTheoryWeight = 3;
        break;
    }

    MoneyWeight *= _profile.MoneyFactor;
    InfluenceWeight *= _profile.InfluenceFactor;
    LeaderWeight *= _profile.LeaderFactor;
    NewTheoryWeight *= _profile.NewTheoryFactor;

    // В последнем раунде развития теорий деньги уже ничего не купят: они стоят ровно столько очков, сколько дают.
    // Колода рынка пуста — после этого раунда начнётся итоговый подсчёт.
    if (session.Mode == GameMode.TheoryDevelopment && session.Market.DeckCount == 0)
      MoneyWeight = 1.0 / VictoryPoints.MoneyPerPoint;
    // В последнем раунде гонки деньги решают только ничью.
    if (session.Mode == GameMode.Race && RoundsLeft <= 1)
      MoneyWeight = 0.01;

    // Копим на сильного персонажа: пока он не по карману, каждый $ стоит дороже — дешёвые покупки
    // становятся невыгодными, а «получить $» — выгоднее.
    (SavingsGoal, var savingsWeight) = FindSavingsGoal();
    MoneyWeight = Math.Max(MoneyWeight, savingsWeight);
    // В гонке деньги нужны только на карточки, которые станут лидерами теорий: дороже они не стоят, сколько бы
    // ни приносил персонаж, на которого бот копит.
    if (session.Mode == GameMode.Race)
      MoneyWeight = Math.Min(MoneyWeight, RaceMoneyCap);
  }

  /// <summary>
  /// Вес каждого соперника: в сумме <see cref="BotProfile.RivalShare"/>. Если <see cref="BotProfile.ThreatFocus"/>
  /// больше нуля, тот, кто впереди, весит больше: боту важнее не дать выиграть лидеру.
  /// </summary>
  private Dictionary<Player, double> RivalWeights()
  {
    var rivals = Rivals.ToList();
    if (rivals.Count == 0)
      return new Dictionary<Player, double>();
    if (_profile.ThreatFocus <= 0)
      return rivals.ToDictionary(rival => rival, _ => RivalWeight);

    var standings = _session.Players.ToDictionary(player => player, Standing);
    var scale = Math.Max(5, standings.Values.Select(Math.Abs).Max() * 0.25);
    var top = standings.Values.Max();
    var raw = rivals.ToDictionary(rival => rival, rival => Math.Exp(_profile.ThreatFocus * (standings[rival] - top) / scale));
    var sum = raw.Values.Sum();
    return raw.ToDictionary(entry => entry.Key, entry => _profile.RivalShare * entry.Value / sum);
  }

  /// <summary>
  /// Насколько игрок близок к победе сейчас — в единицах счёта режима.
  /// </summary>
  private double Standing(Player player) => _session.Mode switch
  {
    GameMode.Money => player.Money + VictoryPoints.AdeptPoints(Field).GetValueOrDefault(player) * (RoundsLeft + 1),
    GameMode.Race => CurrentRaceOutlook[player] + player.Money * 0.01,
    _ => _session.ScoreOf(player).Total,
  };

  /// <summary>
  /// С каким весом учитывается выгода соперника (<c>null</c> — неизвестно чья: средний вес).
  /// </summary>
  public double RivalWeightOf(Player? rival) =>
    rival is not null && _rivalWeights.TryGetValue(rival, out var weight) ? weight : RivalWeight;

  /// <summary>
  /// На сколько $ больше текущих денег может стоить персонаж, на которого бот копит.
  /// </summary>
  private const int MaxSavingsGap = 8;

  /// <summary>
  /// Персонаж на рынке, на которого бот копит деньги, или <c>null</c>.
  /// </summary>
  public PersonCard? SavingsGoal { get; }

  /// <summary>
  /// Самая выгодная на каждый $ карточка рынка, которую бот пока не может купить, но скоро накопит.
  /// Вес $ — доля этой выгоды: меньше, чем выгода самой карточки, чтобы, накопив, бот её купил.
  /// </summary>
  private (PersonCard? Goal, double Weight) FindSavingsGoal()
  {
    if (RoundsLeft < 2)
      return (null, 0);

    PersonCard? goal = null;
    var best = 0.0;
    foreach (var card in _session.Market.OpenCards.Values)
    {
      if (card.Cost <= _player.Money || card.Cost - _player.Money > MaxSavingsGap)
        continue;
      // Пока копим, карточку могут купить или она уйдёт с рынка.
      var perDollar = PlacementValue(card) * 0.7 / card.Cost;
      if (perDollar > best)
        (goal, best) = (card, perDollar);
    }
    return (goal, best * _profile.SavingsShare);
  }

  /// <summary>
  /// Выгода от карточки, если выложить её в лучшее место (без платы за покупку).
  /// </summary>
  public double PlacementValue(PersonCard card)
  {
    var best = 0.0;
    foreach (var (cell, check) in Field.AvailableCells(card, _player))
    {
      var value = Field.CommonZone.Contains(cell)
        ? TheoryPlacementValue(card, cell, check) - check.Cost * MoneyWeight
        : PersonalPlacementValue(card, cell);
      best = Math.Max(best, value);
    }
    return best;
  }

  public PlayingField Field => _session.Field;

  /// <summary>
  /// Настройки оценки для режима этой партии (<see cref="BotProfile.ForMode"/>).
  /// </summary>
  public BotProfile Profile => _profile;

  /// <summary>
  /// Сколько примерно раундов осталось, включая текущий.
  /// </summary>
  public int RoundsLeft { get; }

  /// <summary>
  /// Сколько стоит 1$.
  /// </summary>
  public double MoneyWeight { get; private set; }

  /// <summary>
  /// Сколько стоит единица влияния в теории, которой бот владеет.
  /// </summary>
  private double InfluenceWeight { get; }

  /// <summary>
  /// Сколько стоит лидер в своей теории (гонка — довести до финиша, деньги — награда за лидеров).
  /// </summary>
  private double LeaderWeight { get; }

  /// <summary>
  /// Выгода от основания новой теории сверх влияния: её можно развивать и получить её карточку.
  /// </summary>
  private double NewTheoryWeight { get; }

  /// <summary>
  /// С каким весом учитывается выгода каждого соперника.
  /// </summary>
  public double RivalWeight => _profile.RivalShare / Math.Max(1, _session.Players.Count - 1);

  /// <summary>
  /// Соперники бота.
  /// </summary>
  public IEnumerable<Player> Rivals => _session.Players.Where(player => player != _player);

  /// <summary>
  /// Слишком много карточек в руке — новые покупать незачем.
  /// </summary>
  public double HandCrowding => Math.Max(0, _player.Hand.Cards.Count - _profile.HandLimit) * 0.4;

  private int EstimateRoundsLeft()
  {
    if (_session.Mode == GameMode.Race && _session.IsLastRound)
      return 1;
    // Партия кончается на обновлении рынка с пустой колодой. Пока в колоде есть хоть одна карточка, после этого
    // раунда будет ещё как минимум один: остаток колоды выложится при обновлении, а раунд с пустой колодой — последний.
    var perRound = Math.Max(1.0, _session.MarketCardsPerRow * 1.5);
    return 1 + (int)Math.Ceiling(_session.Market.DeckCount / perRound);
  }

  // ---------- Покупка ----------

  /// <summary>
  /// Выгода от того, что соперник не получит карточку с рынка (<see cref="BotProfile.MarketDenial"/>): карточку теории,
  /// которая уже лежит на поле, соперник может положить туда лидером без доплаты. Засчитывается самая большая выгода
  /// среди соперников, которые ещё ходят и могут её купить, с весом соперника.
  /// </summary>
  public double DenialValue(PersonCard card)
  {
    if (_profile.MarketDenial <= 0 || card.Theory is not { } theory || Field.TheoryColumns.All(column => column.Theory != theory))
      return 0;
    var worst = 0.0;
    foreach (var rival in Rivals)
    {
      if (_session.Turns.HasPassed(rival) || !rival.CanAfford(card.Cost))
        continue;
      BotEvaluator? evaluator = null;
      foreach (var (cell, check) in Field.AvailableCells(card, rival))
      {
        if (check.Column is not { } column || column.Theory != theory || !rival.CanAfford(card.Cost + check.Cost))
          continue;
        evaluator ??= new BotEvaluator(_session, rival, _profile);
        var gain = evaluator.TheoryPlacementValue(card, cell, check) - (check.Cost + card.Cost) * evaluator.MoneyWeight;
        worst = Math.Max(worst, RivalWeightOf(rival) * gain);
      }
    }
    return _profile.MarketDenial * worst;
  }

  // ---------- Резерв на руке ----------

  /// <summary>
  /// Карточка, которую можно держать про запас: персонаж без теории входит в любую теорию.
  /// </summary>
  public static bool IsReserveCard(Card card) => card is PersonCard { Theory: null };

  /// <summary>
  /// Сколько карточек про запас у бота на руке.
  /// </summary>
  public int ReserveCount => _player.Hand.Cards.Count(IsReserveCard);

  /// <summary>
  /// Сколько стоит держать карточку на руке про запас (<see cref="BotProfile.HandReserve"/>): доля лучшей выгоды от её
  /// входа в штаб теории, не меньше <see cref="BotProfile.ReserveFloor"/>. В последнем раунде запас не нужен: карточки
  /// на руке очков не дают.
  /// </summary>
  public double ReserveValue(Card card)
  {
    if (_profile.HandReserve <= 0 || RoundsLeft <= 1 || !IsReserveCard(card) || card is not PersonCard person)
      return 0;
    var best = _profile.ReserveFloor;
    foreach (var (cell, check) in Field.AvailableCells(person, _player))
    {
      if (check.Column is { IsClosed: false })
        best = Math.Max(best, TheoryPlacementValue(person, cell, check) - check.Cost * MoneyWeight);
    }
    return _profile.HandReserve * best;
  }

  /// <summary>
  /// Потеря от того, что бот выложит последнюю карточку про запас с выгодой <paramref name="value"/>: вход в штаб
  /// теории — это то, ради чего её держали, поэтому теряется только недобранная часть.
  /// </summary>
  public double ReserveLoss(Card card, double value, bool intoTheory)
  {
    if (!IsReserveCard(card) || ReserveCount > 1 || !_player.Hand.Cards.Contains(card))
      return 0;
    var reserve = ReserveValue(card);
    return intoTheory ? Math.Max(0, reserve - value) : reserve;
  }

  // ---------- Выкладывание ----------

  /// <summary>
  /// Выгода от карточки, выложенной в общую зону: в штаб существующей теории или новой теорией.
  /// </summary>
  public double TheoryPlacementValue(PersonCard card, Cell cell, PlacementCheck check)
  {
    if (check.Column is { } column)
      return JoinValue(column, card.Influence, card is HomelessCard, card) + PlacedEffectValue(card, column);
    if (card.Theory is null)
      return 0;
    if (_session.Mode == GameMode.Race)
      return RaceNewTheoryValue(card.Influence) + NewTheoryWeight * 0.25 + PlacedEffectValue(card, null);

    // Новая теория: игрок — основатель и владелец. Теорию, которую соперник отберёт одной картой, бот засчитывает
    // лишь частично, а при перехвате соперник получит и её карточку.
    var keep = KeepChance(null, card.Influence);
    var value = keep * (card.Influence * InfluenceWeight * ActiveChance(1, card.Influence) + NewTheoryWeight) + LeaderWeight
      - (1 - keep) * RivalWeight * TheoryCardWorth;
    return value + PlacedEffectValue(card, null);
  }

  /// <summary>
  /// Выгода от того, что в стопку теории придёт ещё один лидер бота с таким влиянием.
  /// </summary>
  /// <param name="placed">Карточка, которую бот кладёт (для плана перехвата: её не считать второй картой).</param>
  public double JoinValue(TheoryColumn column, int influence, bool homeless, PersonCard? placed = null)
  {
    if (_session.Mode == GameMode.Race)
      return RaceJoinValue(column, influence, homeless);

    var myInfluence = column.InfluenceOf(_player) + influence;
    var bestRival = Rivals.Select(column.InfluenceOf).DefaultIfEmpty(0).Max();
    var ownedBefore = column.Owner == _player;
    var ownedAfter = myInfluence > bestRival || myInfluence == bestRival && ownedBefore;
    var totalAfter = column.Influence + influence;
    var leadersAfter = column.Leaders + 1;

    var cardExists = Field.TheoryCardOf(column.Theory) is not null;
    var cardGranted = !cardExists && !column.IsClosed
      && leadersAfter >= TheoryColumn.TheoryCardLeadersThreshold
      && totalAfter >= TheoryColumn.ActiveInfluence
      && TheoryCardCatalog.Create(column.Theory) is not null;
    var cardBefore = ownedBefore && cardExists;
    var cardAfter = ownedAfter && (cardExists || cardGranted);

    // Владение, которое соперник может перехватить одной картой, стоит меньше: укрепить шаткое лидерство выгодно.
    var keepBefore = ownedBefore ? KeepChance(column, column.InfluenceOf(_player)) : 0;
    var keepAfter = ownedAfter ? KeepChance(column, myInfluence) : 0;
    var cardWorth = cardBefore ? -TheoryCardsDelta(true, false) : TheoryCardsDelta(false, true);
    // Влияние даёт очки, только если теория будет действующей; новый лидер приближает её к этому.
    var influenceBefore = column.Influence * InfluenceWeight * ActiveChance(column);
    var influenceAfter = totalAfter * InfluenceWeight * ActiveChance(leadersAfter, totalAfter, column.IsClosed);
    var value = keepAfter * (influenceAfter + (cardAfter ? cardWorth : 0))
      - keepBefore * (influenceBefore + (cardBefore ? cardWorth : 0));
    if (!ownedBefore && ownedAfter && column.Owner is { } loser)
      value += keepAfter * RivalWeightOf(loser)
        * (influenceBefore + (cardExists ? RivalCardWorth(loser, losing: true) : 0));
    if (!ownedAfter)
      value += TakeoverPlanValue(column, influence, myInfluence, bestRival, placed);
    if (!ownedAfter)
    {
      var gainer = column.Owner ?? BestRivalIn(column);
      value -= _profile.JoinGiftFactor * RivalWeightOf(gainer) * (influenceAfter - influenceBefore
        + (cardGranted ? RivalCardWorth(gainer, losing: false) : 0));
    }
    else
      value += LeaderWeight;

    // Доля адептов: у бота больше лидеров в теории — больше доход и очки от адептов.
    var shareBefore = ShareOf(column, _player);
    var shareAfter = (LeadersOf(column, _player) + 1.0) / leadersAfter;
    value += (shareAfter - shareBefore) * AdeptsWorth(column) * (1 + RivalWeight);

    // Ещё не владеет — но приблизился: потом можно будет перехватить.
    if (!ownedAfter && myInfluence + 2 >= bestRival)
      value += 0.3;

    if (_profile.SolvencyPlay > 0)
      return value + SolvencyValue(column, influence, SlackChange(column, homeless, placed), ownedAfter);

    var insolvent = homeless && WouldBeInsolvent(column);
    if (insolvent)
    {
      // Закрытый штаб не даёт ни очков за влияние, ни очков за адептов: своему — потеря всего, что в нём есть.
      if (_profile.InsolvencyCare > 0 && (ownedAfter || shareBefore > 0))
        value -= _profile.InsolvencyCare * ((ownedBefore ? influenceBefore : 0) + shareBefore * AdeptsWorth(column)) + 2;
      else
        value += ownedAfter
          ? -(column.Adepts * AdeptUnit(column) + 2)
          : RivalWeight * (column.Adepts * AdeptUnit(column) * 0.5 + 1);
    }

    return value;
  }

  /// <summary>
  /// Выгода от плана перехвата на два хода (<see cref="BotProfile.TakeoverPlan"/>): после этой карты бот ещё не владелец,
  /// но самой дешёвой подходящей второй картой (с руки или с рынка на оставшиеся деньги) он обгонит соперника.
  /// Тогда засчитывается доля выгоды перехвата: своё влияние и карточка теории, потеря владельца и возврат подарка
  /// за первую карту, за вычетом цены второй карты.
  /// </summary>
  private double TakeoverPlanValue(TheoryColumn column, int influence, int myInfluence, int bestRival, PersonCard? placed)
  {
    if (_profile.TakeoverPlan <= 0 || column.IsClosed || _session.Mode != GameMode.TheoryDevelopment)
      return 0;

    var need = bestRival - myInfluence + 1;
    var inHand = placed is not null && _player.Hand.Cards.Contains(placed);
    var firstCost = (placed is null || inHand ? 0 : placed.Cost)
      + (placed?.Theory == column.Theory ? 0 : Math.Max(0, PlayingField.JoiningPricePerInfluence * column.Influence - (placed?.JoiningDiscount ?? 0)));
    var moneyLeft = _player.Money - firstCost;
    var totalAfter = column.Influence + influence;

    // Самая дешёвая вторая карта, которая даёт нужное влияние.
    var candidates = _player.Hand.Cards.OfType<PersonCard>().Where(card => card != placed).Select(card => (Card: card, Price: 0))
      .Concat(_session.Market.OpenCards.Values.Where(card => card != placed).Select(card => (Card: card, Price: card.Cost)));
    (PersonCard Card, int Cost)? second = null;
    foreach (var (card, price) in candidates)
    {
      if (card.Influence < need || card.Theory is { } theory && theory != column.Theory)
        continue;
      var join = card.Theory == column.Theory ? 0
        : Math.Max(0, PlayingField.JoiningPricePerInfluence * totalAfter - card.JoiningDiscount);
      var cost = price + join;
      if (cost <= moneyLeft && (second is null || cost < second.Value.Cost))
        second = (card, cost);
    }
    if (second is not { } plan)
      return 0;

    var total = totalAfter + plan.Card.Influence;
    var cardExists = Field.TheoryCardOf(column.Theory) is not null;
    var cardComes = cardExists || column.Leaders + 2 >= TheoryColumn.TheoryCardLeadersThreshold && total >= TheoryColumn.ActiveInfluence;
    var owner = column.Owner ?? BestRivalIn(column);
    var chanceBefore = ActiveChance(column);
    var chanceAfter = ActiveChance(column.Leaders + 2, total, column.IsClosed);
    var payoff = KeepChance(column, myInfluence + plan.Card.Influence) * (total * InfluenceWeight * chanceAfter + (cardComes ? TheoryCardsDelta(false, true) : 0))
      + RivalWeightOf(owner) * (column.Influence * InfluenceWeight * chanceBefore + (cardExists ? RivalCardWorth(owner, losing: true) : 0))
      + _profile.JoinGiftFactor * RivalWeightOf(owner) * influence * InfluenceWeight * chanceAfter
      - plan.Cost * MoneyWeight;
    return _profile.TakeoverPlan * Math.Max(0, payoff);
  }

  // ---------- Состоятельность штабов ----------

  /// <summary>
  /// Насколько новый лидер меняет запас состоятельности штаба (<see cref="TheoryColumn.SolvencySlack"/>): бездомный
  /// уменьшает его (у владельца «Коммунистов в Антарктиде» — на 2), остальные увеличивают, учёный — ещё на
  /// <see cref="ScientistPassive.SolvencyBonus"/>.
  /// </summary>
  private int SlackChange(TheoryColumn column, bool homeless, PersonCard? placed)
  {
    if (homeless)
      return -(column.DoubleHomelessOwners.Contains(_player) ? 2 : 1);
    return 1 + (placed?.Passive is ScientistPassive ? ScientistPassive.SolvencyBonus : 0);
  }

  /// <summary>
  /// Будет ли штаб закрыт после проверки штабов в конце раунда, если его запас состоятельности изменится на
  /// <paramref name="slackChange"/>, а лидеров-карточек станет <paramref name="members"/>.
  /// </summary>
  private bool ClosedAtRoundEnd(TheoryColumn column, int slackChange, int members)
  {
    var insolvent = members > TheoryColumn.SolvencyCheckLeadersThreshold && column.SolvencySlack + slackChange < 0;
    // Закрытый штаб открывается, только когда перестаёт быть несостоятельным; открытый закрывается, если несостоятелен,
    // — кроме «Плоской Земли», которую не закрыть.
    return column.IsClosed ? insolvent : insolvent && Field.TheoryCardOf(column.Theory)?.Card.Passive is not FlatEarthPassive;
  }

  /// <summary>
  /// Выгода от того, что новый лидер откроет закрытый штаб или закроет открытый (<see cref="BotProfile.SolvencyPlay"/>).
  /// Открыть свой закрытый штаб — вернуть себе очки за влияние всех его лидеров, карточку теории и долю адептов.
  /// Закрыть бездомными чужой сильный штаб — отнять всё это у его владельца и лидеров (с весом соперников, больше всего —
  /// у того, кто впереди), потеряв свою долю. Если одной карты мало, засчитывается доля выгоды — тем меньшая, чем больше
  /// карт ещё нужно.
  /// </summary>
  /// <remarks>
  /// Остальная оценка входа в теорию (<see cref="JoinValue"/>) считает закрытый штаб мёртвым, а открытый — живым до и после
  /// хода, поэтому здесь считается только разница от смены состояния.
  /// </remarks>
  private double SolvencyValue(TheoryColumn column, int influence, int slackChange, bool ownedAfter)
  {
    var members = column.Members.Count + 1;
    var deadBefore = ClosedAtRoundEnd(column, 0, column.Members.Count);
    var deadAfter = ClosedAtRoundEnd(column, slackChange, members);
    // Ход до конца раунда ещё могут исправить соперники, которые не спасовали.
    var rivalsActive = Rivals.Any(rival => !_session.Turns.HasPassed(rival) && column.IsLeader(rival));
    var certainty = rivalsActive ? 1 - SolvencyCounterRisk : 1;

    if (deadBefore && !deadAfter)
      return _profile.SolvencyPlay * certainty * StakesValue(column, influence, ownedAfter);
    if (!deadBefore && deadAfter)
      return Sabotage(-_profile.SolvencyPlay * certainty * StakesValue(column, influence, ownedAfter));

    // Одной карты мало: сколько ещё таких же карт нужно, чтобы открыть или закрыть штаб.
    var slackAfter = column.SolvencySlack + slackChange;
    if (deadBefore && slackChange > 0)
    {
      var gap = (int)Math.Ceiling(-slackAfter / (double)slackChange);
      return SolvencyPlanShare(gap, needHomeless: false) * StakesValue(column, influence, ownedAfter);
    }
    if (!column.IsClosed && !deadBefore && slackChange < 0)
    {
      var gap = Math.Max((int)Math.Ceiling((slackAfter + 1) / (double)-slackChange),
        TheoryColumn.SolvencyCheckLeadersThreshold + 1 - members);
      return Sabotage(-SolvencyPlanShare(gap, needHomeless: true) * StakesValue(column, influence, ownedAfter));
    }
    return 0;
  }

  /// <summary>
  /// Выгода от закрытия штаба засчитывается с долей <see cref="BotProfile.SabotageFactor"/>; потеря (закрыть штаб,
  /// где у бота больше всех ставок) — полностью.
  /// </summary>
  private double Sabotage(double value) => value > 0 ? value * _profile.SabotageFactor : value;

  /// <summary>
  /// Доля выгоды плана, для которого нужно ещё <paramref name="gap"/> таких же карт: если их нет на руке и на рынке
  /// или до конца партии их не успеть выложить — ничего.
  /// </summary>
  private double SolvencyPlanShare(int gap, bool needHomeless)
  {
    if (gap <= 0 || gap > RoundsLeft * GameSession.ActionsPerTurn)
      return 0;
    var available = _player.Hand.Cards.Concat(_session.Market.OpenCards.Values)
      .Count(card => card is PersonCard && card is HomelessCard == needHomeless);
    return available < gap ? 0 : _profile.SolvencyPlay * SolvencyPlanFactor / (gap + 1);
  }

  /// <summary>
  /// Какую долю выгоды открытия или закрытия штаба бот засчитывает себе, пока для этого не хватает карт.
  /// </summary>
  private const double SolvencyPlanFactor = 0.5;

  /// <summary>
  /// С какой вероятностью соперник, который ещё ходит, успеет исправить состояние штаба до конца раунда.
  /// </summary>
  private const double SolvencyCounterRisk = 0.3;

  /// <summary>
  /// Сколько стоит для бота живой штаб (после того как в него придёт лидер бота с влиянием <paramref name="influence"/>):
  /// владельцу — очки за влияние всех лидеров и карточка теории, каждому лидеру — доля адептов (очки и доход).
  /// Своё — с плюсом, соперников — с минусом и их весом.
  /// </summary>
  private double StakesValue(TheoryColumn column, int influence, bool ownedAfter)
  {
    var leaders = column.Leaders + 1;
    var total = column.Influence + influence;
    var chance = ActiveChance(leaders, total);
    var owner = ownedAfter ? _player : column.Owner;
    var cardComes = Field.TheoryCardOf(column.Theory) is not null
      || leaders >= TheoryColumn.TheoryCardLeadersThreshold && total >= TheoryColumn.ActiveInfluence
      && TheoryCardCatalog.Create(column.Theory) is not null;
    var ownerPoints = total * InfluenceWeight * chance;
    var adeptUnit = chance + RoundsLeft * MoneyWeight;

    double StakeOf(Player player)
    {
      var stake = (LeadersOf(column, player) + (player == _player ? 1 : 0)) / (double)leaders * column.Adepts * adeptUnit;
      if (player == owner)
        stake += ownerPoints + (cardComes ? player == _player ? TheoryCardsDelta(false, true) : RivalCardWorth(player, losing: true) : 0);
      return stake;
    }

    return StakeOf(_player) - Rivals.Sum(rival => RivalWeightOf(rival) * StakeOf(rival));
  }

  private Player? BestRivalIn(TheoryColumn column) =>
    Rivals.Where(rival => column.InfluenceOf(rival) > 0).MaxBy(column.InfluenceOf);

  /// <summary>
  /// Станет ли штаб несостоятельным, если в него придёт ещё один бездомный (приблизительно).
  /// </summary>
  private bool WouldBeInsolvent(TheoryColumn column)
  {
    if (column.Members.Count + 1 <= TheoryColumn.SolvencyCheckLeadersThreshold)
      return false;
    if (_profile.InsolvencyCare > 0)
      // Точный запас состоятельности штаба (с учёными, «Антипрививочниками» и «Коммунистами в Антарктиде»).
      return column.SolvencySlack - (column.DoubleHomelessOwners.Contains(_player) ? 2 : 1) < 0;
    var homeless = column.Members.Count(member => member.Card is HomelessCard) + 1;
    var others = column.Members.Count(member => member.Card is not HomelessCard);
    var scientists = column.Members.Count(member => member.Card.Passive is ScientistPassive);
    return homeless > others + scientists * ScientistPassive.SolvencyBonus;
  }

  /// <summary>
  /// Насколько изменятся очки за карточки теорий, если у бота появится или пропадёт карточка.
  /// </summary>
  private double TheoryCardsDelta(bool before, bool after)
  {
    if (before == after)
      return 0;
    if (_session.Mode != GameMode.TheoryDevelopment)
      return (after ? 1 : -1) * TheoryCardWorth;

    var count = OwnedTheoryCount();
    // Карточка недействующей теории в счёт не входит: её потеря не уменьшит число теорий ниже нуля.
    var changed = Math.Max(0, count + (after ? 1 : 0) - (before ? 1 : 0));
    var delta = VictoryPoints.TheoryPointsFor(changed) - VictoryPoints.TheoryPointsFor(count);
    // Стремимся к цели по числу теорий: до неё каждая теория стоит не меньше средней цены теории при цели.
    var target = _profile.TheoryCountTarget;
    if (target > 0 && Math.Max(count, changed) <= target)
    {
      var average = (double)VictoryPoints.TheoryPointsFor(target) / target;
      return Math.Sign(delta) * Math.Max(Math.Abs(delta), average);
    }
    return delta;
  }

  /// <summary>
  /// Примерная ценность карточки теории для соперника.
  /// </summary>
  private double TheoryCardWorth => _session.Mode == GameMode.TheoryDevelopment ? 6 : 3;

  private int OwnedTheoryCount() => OwnedTheoryCountOf(_player);

  private int OwnedTheoryCountOf(Player player) =>
    Field.TheoryColumns.Count(column => column.Owner == player && column.IsActive
      && Field.TheoryCards.Any(owned => owned.Owner == player && owned.Card.Theory == column.Theory));

  /// <summary>
  /// Сколько очков соперник потеряет (<paramref name="losing"/>) или получит от карточки теории. В развитии теорий
  /// очки за теории растут всё быстрее, поэтому теория у того, у кого их много, стоит дороже
  /// (<see cref="BotProfile.TheoryTableDenial"/>).
  /// </summary>
  private double RivalCardWorth(Player? rival, bool losing)
  {
    if (rival is null || _session.Mode != GameMode.TheoryDevelopment || _profile.TheoryTableDenial <= 0)
      return TheoryCardWorth;
    var count = OwnedTheoryCountOf(rival);
    var table = losing
      ? VictoryPoints.TheoryPointsFor(count) - VictoryPoints.TheoryPointsFor(Math.Max(0, count - 1))
      : VictoryPoints.TheoryPointsFor(count + 1) - VictoryPoints.TheoryPointsFor(count);
    return (1 - _profile.TheoryTableDenial) * TheoryCardWorth + _profile.TheoryTableDenial * table;
  }

  /// <summary>
  /// Насколько бот уверен, что удержит теорию с влиянием <paramref name="myInfluence"/>: если ещё не спасовавший
  /// соперник может обогнать его одной картой, засчитывается только часть (<see cref="BotProfile.TakeoverRisk"/>) —
  /// тем меньшая, чем сильнее он обгонит.
  /// </summary>
  /// <param name="column">Теория; <c>null</c> — новая теория, которую бот основывает.</param>
  private double KeepChance(TheoryColumn? column, int myInfluence)
  {
    // Только в развитии теорий: там владение теорией даёт очки; в остальных режимах учёт перехвата не помог.
    if (_profile.TakeoverRisk <= 0 || column is { IsClosed: true } || _session.Mode != GameMode.TheoryDevelopment)
      return 1;
    // На сколько соперник обгонит бота, положив свою лучшую карту: чем больше, тем легче ему перехватить теорию.
    var overtake = Rivals.Where(rival => !_session.Turns.HasPassed(rival))
      .Select(rival => (column?.InfluenceOf(rival) ?? 0) + TakeoverReach(rival) - myInfluence)
      .DefaultIfEmpty(0).Max();
    return 1 - _profile.TakeoverRisk * Math.Clamp(overtake / (double)TakeoverFullRiskMargin, 0, 1);
  }

  /// <summary>
  /// Сколько влияния соперник может добавить в теорию одной картой: с руки или купленной с рынка на свои деньги.
  /// </summary>
  private int TakeoverReach(Player rival)
  {
    var reach = _takeoverReach.GetValueOrDefault(rival, -1);
    if (reach >= 0)
      return reach;
    var hand = rival.Hand.Cards.OfType<PersonCard>().Select(card => card.Influence);
    var market = _session.Market.OpenCards.Values.Where(card => card.Cost <= rival.Money).Select(card => card.Influence);
    reach = hand.Concat(market).DefaultIfEmpty(0).Max();
    _takeoverReach[rival] = reach;
    return reach;
  }

  private readonly Dictionary<Player, int> _takeoverReach = new();

  /// <summary>
  /// С какого перевеса соперника после его лучшей карты риск перехвата засчитывается полностью.
  /// </summary>
  private const int TakeoverFullRiskMargin = 3;

  /// <summary>
  /// Выгода от того, чтобы выложить карточку с руки взакрытую в теорию (по праву Спецагента): ещё один лидер
  /// с влиянием гражданина, но сама карточка из руки уходит.
  /// </summary>
  public double HiddenPlacementValue(Card card, TheoryColumn column, PlacementCheck check) =>
    JoinValue(column, new HiddenCard(card).Influence, homeless: false) - HandCardWorth(card) - check.Cost * MoneyWeight;

  /// <summary>
  /// Выгода от действия при выкладывании в штаб теории.
  /// </summary>
  private double PlacedEffectValue(PersonCard card, TheoryColumn? column)
  {
    var effect = card.OnPlacedInHeadquarters;
    if (effect == Effect.None)
      return 0;
    // Право выкладывать взакрытую: потом можно будет класть сюда лидеров с руки.
    if (effect is AllowHiddenPlacementEffect)
      return column is null ? 0.3 : 0.5 + 0.5 * InfluenceWeight;
    // Цель выберем потом; обычно найдётся что-то полезное.
    if (effect.RequiresTargetCell)
      return 0.6;

    var context = new GameContext(_player, Field, market: _session.Market);
    if (column is not null)
      context = context.WithTargetTheory(column);
    return SafeValue(effect, context);
  }

  /// <summary>
  /// Штраф бездомному за каждого соседа в личном штабе — чтобы бездомные не стояли кучей.
  /// </summary>
  private const double HomelessNeighbourPenalty = 0.1;

  /// <summary>
  /// Выгода от карточки в личном штабе: её действие можно играть каждый раунд. Каждый бездомный рядом снижает её
  /// успех; бездомного ставим подальше от остальных карточек, в том числе от других бездомных.
  /// </summary>
  public double PersonalPlacementValue(PersonCard card, Cell cell)
  {
    var uses = Math.Min(RoundsLeft, _profile.UsesCap) * _profile.UsesFactor;
    var zone = Field.PlayerZones[_player];
    var neighbourCells = cell.Surrounding.Where(zone.Contains).Where(neighbour => Field.CardAt(neighbour) is not null).ToList();
    var neighbours = neighbourCells.Select(neighbour => Field.CardAt(neighbour)!).ToList();

    // Каждый бездомный рядом снижает успех нового персонажа на 5%.
    var penalty = HomelessCard.NeighborSuccessPenalty / 100.0;
    var chance = Math.Max(0, card.SuccessChancePercent / 100.0 - penalty * neighbours.Count(neighbour => neighbour is HomelessCard));
    var value = chance * UseValue(card) * uses;

    // Учёный повышает успех соседей и добавляет $ к их доходу: и когда кладём его самого, и когда кладём рядом с ним.
    var openScientists = neighbourCells.Count(neighbour => Field.CardAt(neighbour)!.Passive is ScientistPassive && !Field.IsUsed(neighbour));
    double roughScientist, exactScientist;
    if (card.Passive is ScientistPassive)
    {
      roughScientist = 0.4 * neighbours.Count * uses * 0.5;
      exactScientist = neighbourCells.Where(neighbour => Field.CardAt(neighbour) is PersonCard)
        .Sum(neighbour => ScientistGainFor(Field.CardAt(neighbour)!, Field.SuccessChanceAt(neighbour) / 100.0)) * uses;
    }
    else
    {
      roughScientist = 0.1 * neighbours.Count(neighbour => neighbour.Passive is ScientistPassive);
      exactScientist = openScientists * ScientistGainFor(card, chance) * uses;
    }
    value += _profile.ScientistExact * exactScientist + (1 - _profile.ScientistExact) * roughScientist;

    // Бездомный снижает успех всех соседей, в том числе других бездомных, — не ставим их кучей. Полную потерю
    // соседей засчитываем лишь долей (HomelessHarm): свободных ячеек мало, соседи всё равно появятся, а с полной
    // потерей бот перестаёт выкладывать бездомных к себе и проигрывает в режиме денег.
    if (card is HomelessCard)
    {
      value -= HomelessNeighbourPenalty * neighbours.Count + InspectionRisk() * uses;
      if (_profile.HomelessHarm > 0)
        value -= _profile.HomelessHarm * uses * neighbourCells
          .Where(neighbour => Field.CardAt(neighbour) is PersonCard && Field.SuccessChanceAt(neighbour) > 0)
          .Sum(neighbour => penalty * UseValue(Field.CardAt(neighbour)!));
    }

    // Полицейский защищает соседей от чужих негативных действий: кладём его к самым ценным, а ценных — к нему.
    if (_profile.ProtectionValue > 0 && HostileThreat > 0)
    {
      var guarded = card.Passive is PolicemanPassive
        ? neighbourCells.Where(neighbour => !Field.IsProtected(neighbour)).Sum(neighbour => WorthAt(neighbour))
        : neighbourCells.Any(neighbour => Field.CardAt(neighbour)!.Passive is PolicemanPassive && !Field.IsUsed(neighbour))
          ? card.Cost * MoneyWeight + card.Influence * 0.3
          : 0;
      value += _profile.ProtectionValue * HostileThreat * guarded;
    }
    return value;
  }

  /// <summary>
  /// Выгода от тренировки персонажа: деньги тренеру и +<see cref="TrainEffect.Bonus"/>% успеха к каждому оставшемуся
  /// розыгрышу персонажа, в том числе в этом раунде, если он ещё не сыгран. Чужого — плюс деньги, но польза сопернику.
  /// Себя тренер тоже может прокачать: тогда выгода — более успешные будущие тренировки (их цена — цена действия тренера).
  /// </summary>
  private double TrainValue(Cell? cell, Cell? coach)
  {
    if (cell is not { } target || Field.CardAt(target) is not PersonCard card)
      return 0;
    var gain = Math.Min(TrainEffect.Bonus, SuccessCheck.MaxChance - Field.SuccessChanceAt(target)) / 100.0;
    // Тренер, прокачивающий себя, в этом раунде уже сыгран — бонус действует со следующего раунда.
    var playsThisRound = !Field.IsUsed(target) && target != coach;
    var uses = (playsThisRound ? 1 : 0) + Math.Max(0, Math.Min(RoundsLeft - 1, _profile.UsesCap));
    var boost = gain * UseValue(card) * uses;
    return IsMine(target)
      ? TrainEffect.OwnReward * MoneyWeight + boost
      : TrainEffect.RivalReward * MoneyWeight - RivalWeightOf(OwnerOf(target)) * boost;
  }

  /// <summary>
  /// Сколько выгоды бот теряет, если сыграет своего персонажа до тренировки: его свой несыгранный тренер мог бы
  /// сначала добавить ему <see cref="TrainEffect.Bonus"/>% успеха на этот розыгрыш.
  /// </summary>
  public double UntrainedLoss(Cell cell, double useValue)
  {
    if (useValue <= 0 || Field.SuccessChanceAt(cell) >= SuccessCheck.MaxChance || !Field.CheckTrain(cell, _player).IsAllowed)
      return 0;
    var coachReady = Field.PlayerZones[_player].Cells.Any(coach => coach != cell
      && Field.CardAt(coach) is { } card && card.OnPlayFromHand.Options.Any(IsTrain) && Field.CanUse(coach, _player));
    if (!coachReady)
      return 0;
    var gain = Math.Min(TrainEffect.Bonus, SuccessCheck.MaxChance - Field.SuccessChanceAt(cell)) / 100.0;
    return gain * useValue;
  }

  private static bool IsTrain(Effect effect) => effect switch
  {
    TrainEffect => true,
    QuickEffect quick => IsTrain(quick.Inner),
    ChooseOneEffect choose => choose.Options.Any(IsTrain),
    _ => false,
  };

  /// <summary>
  /// Сколько за раунд теряют соседи бездомного в личном штабе бота из-за снижения успеха (с долей
  /// <see cref="BotProfile.HomelessHarm"/>).
  /// </summary>
  private double HomelessHarmAt(Cell cell)
  {
    if (_profile.HomelessHarm <= 0)
      return 0;
    var zone = Field.PlayerZones[_player];
    return _profile.HomelessHarm * cell.Surrounding
      .Where(neighbour => zone.Contains(neighbour) && Field.CardAt(neighbour) is PersonCard && Field.SuccessChanceAt(neighbour) > 0)
      .Sum(neighbour => HomelessCard.NeighborSuccessPenalty / 100.0 * UseValue(Field.CardAt(neighbour)!));
  }

  /// <summary>
  /// Выгода за раунд от учёного рядом с персонажем: +5% успеха и +1$, если его действие приносит деньги.
  /// </summary>
  private double ScientistGainFor(Card card, double chance) =>
    ScientistPassive.NeighborSuccessBonus / 100.0 * UseValue(card)
    + (card.OnPlayFromHand.Options.Any(BringsMoney) ? ScientistPassive.MoneyBonus * MoneyWeight * chance : 0);

  private static bool BringsMoney(Effect effect) => effect switch
  {
    GainMoneyEffect => true,
    QuickEffect quick => BringsMoney(quick.Inner),
    AllOfEffect all => all.Effects.Any(BringsMoney),
    _ => false,
  };

  /// <summary>
  /// Насколько вероятно, что соперник ударит по персонажу в личном штабе бота до конца партии: по 25% за каждую
  /// карту соперников (в личном штабе, на руке, карточку теории), которая понижает, переворачивает, убирает,
  /// забирает или чипирует персонажа; не больше 1.
  /// </summary>
  private double HostileThreat => _hostileThreat ??= Math.Min(1, 0.25 * Rivals.Sum(rival =>
    Field.PlayerZones[rival].Cells.Select(Field.CardAt).OfType<Card>()
      .Concat(rival.Hand.Cards)
      .Concat(Field.TheoryCards.Where(owned => owned.Owner == rival).Select(owned => owned.Card))
      .Count(card => card.OnPlayFromHand.Options.Any(IsHostileToPerson))));

  private double? _hostileThreat;

  private static bool IsHostileToPerson(Effect effect) => effect switch
  {
    DemoteToHomelessEffect or WeakenCharacterEffect or FlipRivalCardEffect
      or TakeRivalCharacterEffect or ChipPersonEffect => true,
    QuickEffect quick => IsHostileToPerson(quick.Inner),
    AllOfEffect all => all.Effects.Any(IsHostileToPerson),
    _ => false,
  };

  /// <summary>
  /// Примерная выгода от одного розыгрыша действия, пока цели не выбраны.
  /// </summary>
  private double StaticValue(Effect effect) => effect switch
  {
    QuickEffect quick => StaticValue(quick.Inner) + 0.2,
    AllOfEffect all => all.Effects.Sum(StaticValue),
    GainMoneyEffect money => money.Amount * MoneyWeight,
    AddAdeptsEffect adepts => adepts.Count * BestAdeptValue(),
    AddAdeptsToThreeTheoriesEffect => effect.TheoryCount * BestAdeptValue(),
    AddAdeptsToOwnLeaderTheoryEffect or AddAdeptsPerProfessionalEffect => 2 * BestAdeptValue(),
    // Сыграть своего персонажа без риска провала.
    PlayOwnCardEffect => 1.5,
    // Чужая карточка — обычно лучшая на столе.
    UseRivalCardEffect or CopyRivalCardEffect => 3 * MoneyWeight + 0.8,
    // Взакрытую — ещё один лидер в теории.
    PlaceHiddenCardEffect => InfluenceWeight + 0.5,
    MoveAdeptEffect => BestAdeptValue() * (1 + RivalWeight) + MoneyWeight,
    PlaceLeaderMeepleEffect => 1.0,
    GainMoneyPerAdeptEffect => 3 * MoneyWeight,
    InspectionEffect => 2 * MoneyWeight + 0.3,
    RemoveAdeptEffect => BestAdeptValue() * RivalWeight * 2,
    FlipRivalCardEffect => 0.5,
    TrainEffect => TrainEffect.OwnReward * MoneyWeight + 0.5,
    _ => 2 * MoneyWeight + 0.3,
  };

  private double BestAdeptValue() =>
    Math.Max(0.2, Field.TheoryColumns.Where(column => !column.IsClosed).Select(column => AdeptValue(column, 1)).DefaultIfEmpty(0).Max());

  // ---------- Действия ----------

  /// <summary>
  /// Выгода от действия с выбранными целями; если оценить не получилось — сильно отрицательная.
  /// </summary>
  public double ValueOf(Effect effect, GameContext context) => SafeValue(effect, context);

  private double SafeValue(Effect effect, GameContext context)
  {
    try
    {
      return Value(effect, context);
    }
    catch (Exception)
    {
      return -100;
    }
  }

  private double Value(Effect effect, GameContext context)
  {
    var cell = context.TargetCell;
    var theory = context.TargetTheory;
    switch (effect)
    {
      case QuickEffect quick:
        return Value(quick.Inner, context);
      case AllOfEffect all:
        return all.Effects.Sum(inner => Value(inner, context));
      case GainMoneyEffect money:
        return money.Amount * MoneyWeight;

      // Адепты.
      case AddAdeptsEffect adepts:
        if (context.TargetAdeptCard is { } targetCard)
          return adepts.Count * CardAdeptValue(targetCard);
        return AdeptValue(theory, adepts.Count);
      case AddAdeptsToThreeTheoriesEffect:
        return context.TargetTheories.Sum(target => AdeptValue(target, 1));
      case AddAdeptsToOwnLeaderTheoryEffect:
        return AdeptValue(theory, 2);
      case AddAdeptsPerProfessionalEffect perProfessional:
        return AdeptValue(theory, perProfessional.BaseCount + (theory?.Cards.Count(card => card is ProfessionalCard) ?? 0));
      case AddAdeptsInOwnTheoryEffect ownTheory:
        var sourceTheory = context.SourceCell is { } source ? Field.CardAt(source)?.Theory : null;
        return theory is not null && sourceTheory is not null && sourceTheory.BelongsTo(theory.Theory) ? AdeptValue(theory, ownTheory.Count) : 0;
      case RemoveAdeptEffect remove:
        return -AdeptValue(theory, remove.Count);
      case MoveAdeptEffect:
        return AdeptValue(theory, 1) - (context.SourceTheory is { } from ? AdeptValue(from, 1) : 0) + MoneyWeight;
      case MoveNeighbourAdeptsEffect:
        return AdeptValue(theory, 1.5);
      case PlaceLeaderMeepleEffect:
        return MeepleValue(theory);
      case RobLeadersEffect:
        return theory is null ? 0 : theory.Members.Count(member => member.Owner != _player) * RobLeadersEffect.Amount * MoneyWeight * (1 + RivalWeight);
      case GainMoneyPerAllAdeptsEffect:
        return (theory?.Adepts ?? 0) / 2 * MoneyWeight;
      case GainMoneyPerAdeptEffect:
        var adeptCount = context.TargetAdeptCard is { } adeptCard ? Field.AdeptsAt(adeptCard) : theory?.Adepts ?? 0;
        return adeptCount / 2 * MoneyWeight;
      case PlaceHiddenCardEffect:
        if (theory is null || context.HandCard is not { } hand)
          return -1;
        return JoinValue(theory, 2, homeless: false) - HandCardWorth(hand);

      // Карточки на поле.
      case DemoteToHomelessEffect:
        return Hostile(cell, worthFactor: 1.5);
      case WeakenCharacterEffect:
        return Hostile(cell, worthFactor: 0.3) + InfluenceAt(cell) * MoneyWeight;
      case FlipRivalCardEffect:
        return Hostile(cell, worthFactor: 0) + RivalWeight * (2 * MoneyWeight + 0.3);
      case RemoveCharacterEffect:
        // Только свой персонаж: выгодно, если выручка больше того, что он ещё принесёт.
        return (CostAt(cell) + InfluenceAt(cell)) * MoneyWeight - WorthAt(cell);
      case TakeRivalCharacterEffect:
        return Hostile(cell, worthFactor: 1) + WorthAt(cell);
      case CloseLeaderEffect:
        return Hostile(cell, worthFactor: 0) + (IsRival(cell) ? RivalWeight * InfluenceAt(cell) * InfluenceWeight * 0.5 + 0.3 : 0);
      case RemoveHomelessEffect:
        return IsMine(cell) ? -0.3 : RivalWeight * 0.8;
      case TakeHomelessToHandEffect:
        return IsMine(cell) ? 0.1 : RivalWeight * 0.6 + 0.3;
      case ChipPersonEffect:
        return IsMine(cell) ? -1 : (cell is { } chipped && Field.CardAt(chipped) is { } card
          ? card.OnPlayFromHand.Options.Select(StaticValue).DefaultIfEmpty(0).Max() * Math.Min(RoundsLeft, 3) * 0.6
          : 0);
      case ReplaceProfessionalWithPoliticianEffect:
        // Из руки: политик выгоден так же, как в штабе, но начнёт приносить деньги, только когда его выложат.
        if (context.HandCard is { } handProfessional)
          return handProfessional is ProfessionalCard { Name: "Политик" } ? -1 : 0.8;
        return IsMine(cell) ? (Field.CardAt(cell!.Value) is ProfessionalCard { Name: "Политик" } ? -1 : 1.0) : -0.5;
      case TrainEffect:
        return TrainValue(cell, context.SourceCell);
      case AttachTinFoilEffect:
        return Friendly(cell, 0.6);
      case TrainHomelessEffect:
        return Friendly(cell, 0.8);
      case TransformHomelessEffect:
        return context.HandCard is not null ? 4 * MoneyWeight + 0.3 : Friendly(cell, 4 * MoneyWeight + 0.3);
      case MarkForReturnEffect mark:
        return mark.Reward * MoneyWeight - 0.1;
      case RemoveMarketCardForInfluenceEffect:
        return cell is { } marketCell && _session.Market.OpenCards.TryGetValue(marketCell, out var marketCard) ? marketCard.Influence * MoneyWeight : 0;
      case InspectionEffect:
        return context.TargetPlayer is { } target ? OpenHomelessOf(target) * MoneyWeight + RivalWeight * 0.3 : 0;
      case AddReserveCardToMarketEffect:
        return 2 * MoneyWeight;
      case SwapCardsEffect:
        return -0.05;
      case PeekHiddenCardEffect or PeekTheoryEffect:
        return 0.02;
      case ToggleTheoryEffect:
        return 0.05;
      case RevealHiddenCardEffect:
        return 0.1;
      case GainMoneyPerOwnedTheoryEffect or GainMoneyPerClosedHeadquartersEffect or GainMoneyPerLeaderInClosedTheoriesEffect
        or GainMoneyPerCommunistLeaderEffect or GainMoneyPerOwnScientistEffect:
        return 2 * MoneyWeight;
    }

    // Неизвестное действие (чаще всего у карточек теорий) обычно полезно; к своей теории — полезнее.
    var value = 0.4;
    if (theory is not null)
      value += 0.4 * (ShareOf(theory, _player) - 0.5);
    if (cell is { } targetCell)
      value += IsMine(targetCell) ? 0.1 : 0;
    return value;
  }

  /// <summary>
  /// Сколько $ стоят выбранные цели (плата полицейскому, выкладывание взакрытую) — в очках.
  /// </summary>
  public double CostOf(GameContext context, Effect effect)
  {
    try
    {
      var cost = 0;
      if (effect.RequiresTargetCell && context.TargetCell is { } cell)
        cost += effect.CheckTargetCell(cell, context) is { IsAllowed: true } check ? check.Cost : 0;
      if (effect.RequiresSecondTargetCell && context.SecondTargetCell is { } second)
        cost += effect.CheckTargetCell(second, context) is { IsAllowed: true } check ? check.Cost : 0;
      if (effect is PlaceHiddenCardEffect hidden && context.HandCard is { } card && context.TargetTheory is { } theory)
        cost += Field.CheckHidden(card, theory, _player, hidden.JoiningDiscount).Cost;
      return cost * MoneyWeight * (1 + RivalWeight * 0.5);
    }
    catch (Exception)
    {
      return 0;
    }
  }

  /// <summary>
  /// Ожидаемая потеря от критического провала (уже с учётом его вероятности, отрицательная): бездомный уходит
  /// из игры на гранях 1–<see cref="PlayingField.HomelessDestroyDie"/>, гражданин на гранях
  /// 1–<see cref="PlayingField.CitizenDemoteDie"/> становится бездомным. Теряются только будущие розыгрыши —
  /// в последнем раунде потерять нечего.
  /// </summary>
  public double FailurePenalty(Cell cell)
  {
    if (!Field.FailureConsequences || Field.CardAt(cell) is not PersonCard card)
      return 0;

    // Карточку можно сыграть не больше раза за раунд: множитель UsesFactor, который делает личный штаб привлекательнее
    // при выкладывании, здесь не завышает потерю.
    var futureUses = Math.Min(RoundsLeft - 1, _profile.UsesCap) * Math.Min(1, _profile.UsesFactor);
    if (futureUses <= 0)
      return 0;

    var chance = Field.SuccessChanceAt(cell) / 100.0;
    var failFaces = (int)Math.Round(SuccessCheck.DieSides * (1 - chance));
    switch (card)
    {
      case HomelessCard:
      {
        var destroy = Math.Min(PlayingField.HomelessDestroyDie, failFaces) / (double)SuccessCheck.DieSides;
        // Без бездомного соседи перестают терять успех, а инспекции нечего с него взять: теряется только остаток.
        var worth = chance * UseValue(card) - HomelessHarmAt(cell) - InspectionRisk();
        return -destroy * Math.Max(0, worth) * futureUses;
      }
      case CitizenCard:
      {
        var demote = Math.Min(PlayingField.CitizenDemoteDie, failFaces) / (double)SuccessCheck.DieSides;
        var homeless = new HomelessCard();
        var homelessChance = Math.Max(0, chance - (card.SuccessChancePercent - homeless.SuccessChancePercent) / 100.0);
        var loss = chance * UseValue(card) - homelessChance * UseValue(homeless);
        return -demote * Math.Max(0, loss) * futureUses;
      }
      default:
        return 0;
    }
  }

  /// <summary>
  /// Примерная выгода одного розыгрыша карточки (лучшее из её действий).
  /// </summary>
  private double UseValue(Card card) => card.OnPlayFromHand.Options.Select(StaticValue).DefaultIfEmpty(0).Max();

  // ---------- Адепты ----------

  /// <summary>
  /// Выгода от адептов в теории: доля бота за вычетом доли соперников.
  /// <c>null</c> — теория ещё не выложена, бот в ней единственный лидер.
  /// </summary>
  public double AdeptValue(TheoryColumn? column, double count)
  {
    if (column is { IsClosed: true })
      return 0;
    if (column is null)
      return count * AdeptUnit(null);
    var share = ShareOf(column, _player);
    // Доля соперников — с весом каждого; без слежения за лидером все весят одинаково.
    var rivals = _profile.ThreatFocus > 0 ? Rivals.Sum(rival => ShareOf(column, rival) * RivalWeightOf(rival)) : (1 - share) * RivalWeight;
    var focus = share >= 1 ? 1 + _profile.FocusBonus : 1;
    return count * AdeptUnit(column) * _profile.AdeptFactor * focus * (share - rivals);
  }

  /// <summary>
  /// Сколько стоит адепт на карточке в личном штабе (Мастер теорий). Очков он не даёт ни в одном режиме: каждые
  /// <see cref="GainMoneyPerAdeptEffect.AdeptsPerDollar"/> адепта приносят 1$ при каждом успешном розыгрыше карточки —
  /// в этом раунде, если она ещё не сыграна, и в каждом следующем.
  /// </summary>
  private double CardAdeptValue(Cell card)
  {
    var uses = (Field.IsUsed(card) ? 0 : 1) + Math.Max(0, RoundsLeft - 1);
    var chance = Field.SuccessChanceAt(card) / 100.0;
    return uses * chance * MoneyWeight / GainMoneyPerAdeptEffect.AdeptsPerDollar;
  }

  /// <summary>
  /// Сколько стоит один адепт целиком (до деления между лидерами).
  /// </summary>
  private double AdeptUnit(TheoryColumn? column) => _session.Mode switch
  {
    // Каждый раунд адепт приносит $ лидерам, в конце партии — ещё раз.
    GameMode.Money => (RoundsLeft + 1) * MoneyWeight,
    // Очки получают только теории на финише.
    GameMode.Race => (column is null ? 0.2 : RaceFinishChance(column)) + RoundsLeft * MoneyWeight,
    // Адепты — деньги каждый раунд и очки в конце партии, если теория будет действующей.
    _ => ActiveChance(column) + RoundsLeft * MoneyWeight,
  };

  /// <summary>
  /// Сколько стоят все адепты теории до конца партии (до деления между лидерами): нынешние и доля
  /// (<see cref="BotProfile.AdeptGrowth"/>) тех, что игроки приведут в неё своими персонажами. Новые адепты придут
  /// не сразу: доход они принесут в среднем за половину оставшихся раундов.
  /// </summary>
  private double AdeptsWorth(TheoryColumn column)
  {
    var now = column.Adepts * AdeptUnit(column);
    // В гонке адепты дают очки только теориям на финише — учёт роста там не помог.
    if (_profile.AdeptGrowth <= 0 || column.IsClosed || _session.Mode == GameMode.Race)
      return now;
    // Текущий раунд ещё идёт — засчитываем его половину.
    var rounds = Math.Max(0, RoundsLeft - 0.5);
    var future = _profile.AdeptGrowth * rounds * AdeptGrowthOf(column);
    var futureUnit = _session.Mode == GameMode.Money
      ? (rounds / 2 + 1) * MoneyWeight
      : ActiveChance(column) + rounds / 2 * MoneyWeight;
    return now + future * futureUnit;
  }

  /// <summary>
  /// Сколько адептов за раунд приходит в теорию: каждый игрок отправляет всех адептов своих персонажей в свою главную
  /// теорию — ту, где он владелец и где больше всего адептов.
  /// </summary>
  private double AdeptGrowthOf(TheoryColumn column)
  {
    if (_adeptGrowth is null)
    {
      _adeptGrowth = new Dictionary<TheoryColumn, double>();
      foreach (var player in _session.Players)
      {
        var focus = Field.TheoryColumns.Where(theory => theory.Owner == player && !theory.IsClosed)
          .MaxBy(theory => theory.Adepts);
        if (focus is null)
          continue;
        var perRound = Field.PlayerZones[player].Cells
          .Where(cell => Field.CardAt(cell) is PersonCard)
          .Sum(cell => Field.CardAt(cell)!.OnPlayFromHand.Options.Select(AdeptsPerUse).DefaultIfEmpty(0).Max()
            * Field.SuccessChanceAt(cell) / 100.0);
        _adeptGrowth[focus] = _adeptGrowth.GetValueOrDefault(focus) + perRound;
      }
    }
    return _adeptGrowth.GetValueOrDefault(column);
  }

  private Dictionary<TheoryColumn, double>? _adeptGrowth;

  /// <summary>
  /// Сколько адептов в теорию приводит один розыгрыш действия.
  /// </summary>
  private static double AdeptsPerUse(Effect effect) => effect switch
  {
    QuickEffect quick => AdeptsPerUse(quick.Inner),
    AllOfEffect all => all.Effects.Sum(AdeptsPerUse),
    AddAdeptsEffect adepts => adepts.Count,
    AddAdeptsToOwnLeaderTheoryEffect or AddAdeptsPerProfessionalEffect => 2,
    _ => 0,
  };

  /// <summary>
  /// Шанс, что теория будет действующей к итоговому подсчёту: только действующие теории дают очки за влияние и адептов.
  /// Чем больше не хватает лидеров и влияния и чем меньше осталось раундов, тем он ниже. Вне режима развития теорий — 1.
  /// </summary>
  /// <param name="leaders">Лидеров в теории.</param>
  /// <param name="influence">Суммарное влияние теории.</param>
  /// <param name="closed">Штаб закрыт — действующей теория уже не станет.</param>
  private double ActiveChance(int leaders, int influence, bool closed = false)
  {
    if (_session.Mode != GameMode.TheoryDevelopment)
      return 1;
    if (closed)
      return 0;
    // Сколько ещё карточек нужно: не хватающих лидеров или влияния (по ~2 за карточку).
    var missingLeaders = Math.Max(0, TheoryColumn.TheoryCardLeadersThreshold - leaders);
    var missingInfluence = (int)Math.Ceiling(Math.Max(0, TheoryColumn.ActiveInfluence - influence) / 2.0);
    var gap = Math.Max(missingLeaders, missingInfluence);
    return gap == 0 ? 1 : Math.Clamp(1 - gap / (RoundsLeft + 1.0), 0, 1);
  }

  private double ActiveChance(TheoryColumn? column) =>
    column is null ? ActiveChance(1, 0) : ActiveChance(column.Leaders, column.Influence, column.IsClosed);

  private static int LeadersOf(TheoryColumn column, Player player) =>
    column.MembersOf(player).Count() + column.LeaderMeeples.Count(meeple => meeple == player);

  private static double ShareOf(TheoryColumn column, Player player) =>
    column.Leaders == 0 ? 0 : (double)LeadersOf(column, player) / column.Leaders;

  private double MeepleValue(TheoryColumn? column)
  {
    if (column is null)
      return 0;
    if (_session.Mode == GameMode.Race && RaceMeepleValue(column) is { } raceValue)
      return raceValue;
    var before = ShareOf(column, _player);
    var after = (LeadersOf(column, _player) + 1.0) / (column.Leaders + 1);
    var value = column.Adepts * (after - before) * MoneyWeight * (1 + RivalWeight) + 0.2;
    // Мипл даёт влияние: можно на раунд стать владельцем теории и сыграть её карточку.
    var myInfluence = column.InfluenceOf(_player) + TheoryColumn.LeaderMeepleInfluence;
    if (column.Owner != _player && myInfluence > Rivals.Select(column.InfluenceOf).DefaultIfEmpty(0).Max()
      && Field.TheoryCardOf(column.Theory) is { IsUsed: false })
      value += 1;
    return value;
  }

  // ---------- Карточки на поле ----------

  private Player? OwnerOf(Cell? cell)
  {
    if (cell is not { } target)
      return null;
    foreach (var (player, zone) in Field.PlayerZones)
    {
      if (zone.Contains(target))
        return player;
    }
    return Field.ColumnAt(target)?.OwnerAt(target);
  }

  private bool IsMine(Cell? cell) => OwnerOf(cell) == _player;

  private bool IsRival(Cell? cell) => OwnerOf(cell) is { } owner && owner != _player;

  private int InfluenceAt(Cell? cell) => cell is { } target && Field.CardAt(target) is PersonCard person ? person.Influence : 0;

  private int CostAt(Cell? cell) => cell is { } target && Field.CardAt(target) is PersonCard person ? person.Cost : 0;

  /// <summary>
  /// Примерная ценность персонажа на поле.
  /// </summary>
  private double WorthAt(Cell? cell) =>
    cell is { } target && Field.CardAt(target) is PersonCard person ? person.Cost * MoneyWeight + person.Influence * 0.3 : 0.3;

  /// <summary>
  /// Негативное действие: выгодно против соперника, невыгодно против себя.
  /// </summary>
  private double Hostile(Cell? cell, double worthFactor)
  {
    if (IsMine(cell))
      return -2 - WorthAt(cell);
    return IsRival(cell) ? RivalWeightOf(OwnerOf(cell)) * WorthAt(cell) * worthFactor + 0.2 : 0;
  }

  /// <summary>
  /// Полезное действие: выгодно для своего персонажа, невыгодно для чужого.
  /// </summary>
  private double Friendly(Cell? cell, double value) => IsMine(cell) ? value : -value * RivalWeightOf(OwnerOf(cell));

  private double HandCardWorth(Card card) => card is PersonCard person ? 0.3 + person.Cost * MoneyWeight * 0.5 : 0.3;

  /// <summary>
  /// Сколько за раунд теряет бот от ещё одного открытого бездомного в личном штабе: каждый полицейский соперника
  /// инспекцией берёт по <see cref="InspectionEffect.MoneyPerHomeless"/>$ за каждого бездомного.
  /// </summary>
  public double InspectionRisk() =>
    Rivals.Sum(rival => RivalWeightOf(rival) * Field.PlayerZones[rival].Cells.Count(cell =>
      Field.CardAt(cell) is { } card && card.OnPlayFromHand.Options.Any(option => option is InspectionEffect)))
    * InspectionEffect.MoneyPerHomeless * MoneyWeight;

  private int OpenHomelessOf(Player player) =>
    Field.PlayerZones[player].Cells.Count(cell => Field.CardAt(cell) is HomelessCard && !Field.IsUsed(cell));
}
