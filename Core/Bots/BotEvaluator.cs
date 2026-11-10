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
internal sealed class BotEvaluator
{
  private readonly GameSession _session;
  private readonly Player _player;

  public BotEvaluator(GameSession session, Player player)
  {
    _session = session;
    _player = player;
    RoundsLeft = EstimateRoundsLeft();
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

    // Копим на сильного персонажа: пока он не по карману, каждый $ стоит дороже — дешёвые покупки
    // становятся невыгодными, а «получить $» — выгоднее.
    (SavingsGoal, var savingsWeight) = FindSavingsGoal();
    MoneyWeight = Math.Max(MoneyWeight, savingsWeight);
  }

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
    return (goal, best * 0.6);
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
  public double RivalWeight => 0.6 / Math.Max(1, _session.Players.Count - 1);

  /// <summary>
  /// Соперники бота.
  /// </summary>
  public IEnumerable<Player> Rivals => _session.Players.Where(player => player != _player);

  /// <summary>
  /// Слишком много карточек в руке — новые покупать незачем.
  /// </summary>
  public double HandCrowding => Math.Max(0, _player.Hand.Cards.Count - 3) * 0.4;

  private int EstimateRoundsLeft()
  {
    if (_session.Mode == GameMode.Race && _session.IsLastRound)
      return 1;
    var perRound = Math.Max(1.0, _session.MarketCardsPerRow * 1.5);
    return 1 + (int)(_session.Market.DeckCount / perRound);
  }

  // ---------- Выкладывание ----------

  /// <summary>
  /// Выгода от карточки, выложенной в общую зону: в штаб существующей теории или новой теорией.
  /// </summary>
  public double TheoryPlacementValue(PersonCard card, Cell cell, PlacementCheck check)
  {
    if (check.Column is { } column)
      return JoinValue(column, card.Influence, card is HomelessCard) + PlacedEffectValue(card, column);
    if (card.Theory is null)
      return 0;

    // Новая теория: игрок — основатель и владелец.
    var value = card.Influence * InfluenceWeight + NewTheoryWeight + LeaderWeight;
    return value + PlacedEffectValue(card, null);
  }

  /// <summary>
  /// Выгода от того, что в стопку теории придёт ещё один лидер бота с таким влиянием.
  /// </summary>
  public double JoinValue(TheoryColumn column, int influence, bool homeless)
  {
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

    var value = (ownedAfter ? totalAfter : 0) * InfluenceWeight - (ownedBefore ? column.Influence : 0) * InfluenceWeight;
    value += TheoryCardsDelta(cardBefore, cardAfter);
    if (!ownedBefore && ownedAfter)
      value += RivalWeight * (column.Influence * InfluenceWeight + (cardExists ? TheoryCardWorth : 0));
    if (!ownedAfter)
      value -= RivalWeight * (influence * InfluenceWeight + (cardGranted ? TheoryCardWorth : 0) + RaceProgressValue(column, leadersAfter));
    else
      value += LeaderWeight + RaceProgressValue(column, leadersAfter);

    // Доля адептов: у бота больше лидеров в теории — больше доход и очки от адептов.
    var shareBefore = ShareOf(column, _player);
    var shareAfter = (LeadersOf(column, _player) + 1.0) / leadersAfter;
    value += (shareAfter - shareBefore) * column.Adepts * AdeptUnit(column) * (1 + RivalWeight);

    // Ещё не владеет — но приблизился: потом можно будет перехватить.
    if (!ownedAfter && myInfluence + 2 >= bestRival)
      value += 0.3;

    if (homeless && WouldBeInsolvent(column))
    {
      value += ownedAfter
        ? -(column.Adepts * AdeptUnit(column) + 2)
        : RivalWeight * (column.Adepts * AdeptUnit(column) * 0.5 + 1);
    }

    return value;
  }

  /// <summary>
  /// Гонка: чем ближе своя теория к финишу, тем ценнее каждый новый лидер; дойти до финиша — бонус и очки адептов.
  /// </summary>
  private double RaceProgressValue(TheoryColumn column, int leadersAfter)
  {
    if (_session.Mode != GameMode.Race)
      return 0;
    var progress = Math.Min(1.0, (double)leadersAfter / RaceScoring.FinishLeaders(column, _session.Players.Count));
    var value = 3 * progress * progress;
    if (WillFinish(column, leadersAfter))
      value += RaceScoring.FinishBonus + column.Adepts * ShareOf(column, _player);
    return value;
  }

  private bool WillFinish(TheoryColumn column, int leaders) =>
    !RaceScoring.IsFinished(column, _session.Players.Count) && leaders >= RaceScoring.FinishLeaders(column, _session.Players.Count);

  /// <summary>
  /// Станет ли штаб несостоятельным, если в него придёт ещё один бездомный (приблизительно).
  /// </summary>
  private static bool WouldBeInsolvent(TheoryColumn column)
  {
    if (column.Members.Count + 1 <= TheoryColumn.SolvencyCheckLeadersThreshold)
      return false;
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
    var changed = count + (after ? 1 : 0) - (before ? 1 : 0);
    return VictoryPoints.TheoryPointsFor(changed) - VictoryPoints.TheoryPointsFor(count);
  }

  /// <summary>
  /// Примерная ценность карточки теории для соперника.
  /// </summary>
  private double TheoryCardWorth => _session.Mode == GameMode.TheoryDevelopment ? 6 : 3;

  private int OwnedTheoryCount() =>
    Field.TheoryColumns.Count(column => column.Owner == _player
      && Field.TheoryCards.Any(owned => owned.Owner == _player && owned.Card.Theory == column.Theory));

  /// <summary>
  /// Выгода от действия при выкладывании в штаб теории.
  /// </summary>
  private double PlacedEffectValue(PersonCard card, TheoryColumn? column)
  {
    var effect = card.OnPlacedInHeadquarters;
    if (effect == Effect.None)
      return 0;
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
    var uses = Math.Min(RoundsLeft, 5) * 0.8;
    var zone = Field.PlayerZones[_player];
    var neighbours = cell.Surrounding.Where(zone.Contains).Select(Field.CardAt).OfType<Card>().ToList();

    // Каждый бездомный рядом снижает успех нового персонажа на 5%.
    var penalty = HomelessCard.NeighborSuccessPenalty / 100.0;
    var chance = Math.Max(0, card.SuccessChancePercent / 100.0 - penalty * neighbours.Count(neighbour => neighbour is HomelessCard));
    var value = chance * UseValue(card) * uses;

    // Сам учёный повышает успех соседей и добавляет $ к их доходу.
    if (card.Passive is ScientistPassive)
      value += 0.4 * neighbours.Count * uses * 0.5;
    else
      value += 0.1 * neighbours.Count(neighbour => neighbour.Passive is ScientistPassive);

    // Бездомный снижает успех всех соседей, в том числе других бездомных, — не ставим их кучей. Полную потерю
    // соседей не вычитаем: свободных ячеек мало, соседи всё равно появятся, а с полной потерей бот перестаёт
    // выкладывать бездомных к себе и проигрывает в режиме денег. Небольшой штраф лишь выбирает ячейку получше.
    if (card is HomelessCard)
      value -= HomelessNeighbourPenalty * neighbours.Count;
    return value;
  }

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
    TrainEffect => 0.5,
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
        if (context.TargetAdeptCard is not null)
          return adepts.Count * 0.6 * AdeptUnit(null);
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
        return Hostile(cell, worthFactor: 1) + (CostAt(cell) + InfluenceAt(cell)) * MoneyWeight;
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
        return IsMine(cell) ? (Field.CardAt(cell!.Value) is ProfessionalCard { Name: "Политик" } ? -1 : 1.0) : -0.5;
      case TrainEffect:
        return Friendly(cell, 0.4);
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
        or GainMoneyPerCommunistLeaderEffect or GainMoneyPerOwnScientistEffect or GainMoneyPerAllAdeptsEffect:
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

    var futureUses = Math.Min(RoundsLeft - 1, 5) * 0.8;
    if (futureUses <= 0)
      return 0;

    var chance = Field.SuccessChanceAt(cell) / 100.0;
    var failFaces = (int)Math.Round(SuccessCheck.DieSides * (1 - chance));
    switch (card)
    {
      case HomelessCard:
      {
        var destroy = Math.Min(PlayingField.HomelessDestroyDie, failFaces) / (double)SuccessCheck.DieSides;
        return -destroy * chance * UseValue(card) * futureUses;
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
    var share = column is null ? 1 : ShareOf(column, _player);
    return count * AdeptUnit(column) * (share - (1 - share) * RivalWeight);
  }

  /// <summary>
  /// Сколько стоит один адепт целиком (до деления между лидерами).
  /// </summary>
  private double AdeptUnit(TheoryColumn? column) => _session.Mode switch
  {
    // Каждый раунд адепт приносит $ лидерам, в конце партии — ещё раз.
    GameMode.Money => (RoundsLeft + 1) * MoneyWeight,
    // Очки получают только теории на финише.
    GameMode.Race => (column is null ? 0.2 : Math.Min(1.0, (double)column.Leaders / RaceScoring.FinishLeaders(column, _session.Players.Count))) + RoundsLeft * MoneyWeight,
    // Адепты — очки в конце партии и деньги каждый раунд.
    _ => 1 + RoundsLeft * MoneyWeight,
  };

  private static int LeadersOf(TheoryColumn column, Player player) =>
    column.MembersOf(player).Count() + column.LeaderMeeples.Count(meeple => meeple == player);

  private static double ShareOf(TheoryColumn column, Player player) =>
    column.Leaders == 0 ? 0 : (double)LeadersOf(column, player) / column.Leaders;

  private double MeepleValue(TheoryColumn? column)
  {
    if (column is null)
      return 0;
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
    return IsRival(cell) ? RivalWeight * WorthAt(cell) * worthFactor + 0.2 : 0;
  }

  /// <summary>
  /// Полезное действие: выгодно для своего персонажа, невыгодно для чужого.
  /// </summary>
  private double Friendly(Cell? cell, double value) => IsMine(cell) ? value : -value * RivalWeight;

  private double HandCardWorth(Card card) => card is PersonCard person ? 0.3 + person.Cost * MoneyWeight * 0.5 : 0.3;

  private int OpenHomelessOf(Player player) =>
    Field.PlayerZones[player].Cells.Count(cell => Field.CardAt(cell) is HomelessCard && !Field.IsUsed(cell));
}
