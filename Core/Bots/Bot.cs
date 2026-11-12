using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Cards.Theories;
using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Bots;

/// <summary>
/// Бот, который играет за игрока и пытается выиграть.
/// </summary>
/// <remarks>
/// Бот жадный: на каждом шаге он перебирает все доступные действия — купить карточку, выложить карточку с руки,
/// сыграть персонажа из личного штаба, применить карточку теории — вместе с целями, оценивает каждое эвристикой
/// (<see cref="BotEvaluator"/>) и выбирает самое выгодное. Если выгодных действий нет, бот пасует.
/// Что можно сделать и какие цели доступны, бот спрашивает у тех же правил, что и интерфейс игрока
/// (<see cref="PlayingField"/>, <see cref="Effect.TargetCells"/>), поэтому сам правил не дублирует.
/// Уровень бота (<see cref="BotLevel"/>) задаёт веса оценки — <see cref="BotProfile"/>.
/// </remarks>
public sealed class Bot
{
  /// <summary>
  /// Больше стольких вариантов целей для одного действия не перебираем.
  /// </summary>
  private const int MaxCandidates = 300;

  /// <summary>
  /// Игрок, за которого играет бот.
  /// </summary>
  public Player Player { get; }

  /// <summary>
  /// Настройки оценки ходов — уровень бота.
  /// </summary>
  public BotProfile Profile { get; }

  /// <param name="player">Игрок, за которого играет бот; уровень — <see cref="Player.BotLevel"/>.</param>
  public Bot(Player player) : this(player, BotProfile.For(player.BotLevel))
  {
  }

  /// <param name="player">Игрок, за которого играет бот.</param>
  /// <param name="profile">Настройки оценки ходов.</param>
  public Bot(Player player, BotProfile profile)
  {
    Player = player;
    Profile = profile;
  }

  private BotEvaluator Evaluator(GameSession session) => new(session, Player, Profile);

  /// <summary>
  /// Выбрать следующий ход. Вызывается, когда ходит игрок бота и ничего не ждёт выбора.
  /// </summary>
  /// <param name="session">Партия.</param>
  /// <param name="excluded">Ключи ходов (<see cref="BotMove.Key"/>), которые пробовать не нужно: они уже не удались.</param>
  public BotMove ChooseMove(GameSession session, IReadOnlySet<string>? excluded = null)
  {
    var evaluator = Evaluator(session);
    var moves = new List<BotMove>();
    moves.AddRange(UseCardMoves(session, evaluator));
    moves.AddRange(TheoryCardMoves(session, evaluator));
    moves.AddRange(PlaceMoves(session, evaluator));
    moves.AddRange(HiddenPlaceMoves(session, evaluator));
    moves.AddRange(BuyMoves(session, evaluator));

    var best = moves
      .Where(move => excluded is null || !excluded.Contains(move.Key))
      .Where(move => move.Value >= (move is UseCardMove ? Profile.MinUseValue : Profile.MinValue))
      .MaxBy(move => move.Value);
    return best ?? new PassMove();
  }

  /// <summary>
  /// Выбрать цель для действия только что выложенной карточки (<see cref="GameSession.PendingPlacement"/>).
  /// </summary>
  public PlacementResponse ChoosePlacement(GameSession session, PlacementChoice choice)
  {
    var evaluator = Evaluator(session);
    var effect = choice.Effect;
    var context = session.PlacementContext(choice);
    var best = CellCandidates(effect, context, evaluator)
      .Select(candidate => (Context: candidate, Value: evaluator.ValueOf(effect, candidate) - evaluator.CostOf(candidate, effect)))
      .Take(MaxCandidates)
      .DefaultIfEmpty()
      .MaxBy(candidate => candidate.Value);

    if (best.Context is null || best.Value <= 0 && effect.TargetIsOptional)
      return new PlacementResponse(null, null);
    return new PlacementResponse(best.Context.TargetCell, best.Context.SecondTargetCell);
  }

  /// <summary>
  /// Выбрать цели для действия основной теории, которое ждёт выбора владельца — игрока бота.
  /// </summary>
  public TriggerResponse ChooseTrigger(GameSession session, TheoryTrigger trigger)
  {
    var evaluator = Evaluator(session);
    var effect = trigger.Effect;
    var best = Candidates(effect, trigger.Context, evaluator)
      .Take(MaxCandidates)
      .Select(candidate => (Context: candidate, Value: evaluator.ValueOf(effect, candidate)))
      .DefaultIfEmpty()
      .MaxBy(candidate => candidate.Value);

    if (best.Context is null || best.Value < -0.5)
      return new TriggerResponse(Skip: true);
    var context = best.Context;
    return new TriggerResponse(
      Skip: false,
      HandCard: effect.RequiresHandCard || effect.AcceptsHandCardTarget ? context.HandCard : null,
      Theory: effect.RequiresTheory ? context.TargetTheory : null,
      Cell: effect.RequiresTargetCell ? context.TargetCell : null);
  }

  /// <summary>
  /// Выполнить ход без анимаций — прямо через сессию (для игры ботов между собой и проверок).
  /// После выкладывания с выбором цели и после действия основной теории бот сразу же отвечает сам
  /// (<see cref="RespondToPending"/>).
  /// </summary>
  public void Execute(GameSession session, BotMove move)
  {
    switch (move)
    {
      case BuyMove buy:
        session.BuyFromMarket(buy.Cell);
        break;
      case PlaceMove place:
        session.PlaceFromHand(place.Card, place.Cell);
        break;
      case PlaceHiddenMove hidden:
        session.PlaceHiddenFromHand(hidden.Card, hidden.Column);
        break;
      case UseCardMove use:
        session.CheckSuccess(use.Cell);
        session.UseCard(use.Option, use.Context);
        break;
      case UseTheoryCardMove theory:
        session.UseTheoryCard(theory.Card, theory.Context);
        break;
      case PassMove:
        session.Pass();
        break;
    }
  }

  /// <summary>
  /// Ответить на выбор, который ждёт этого бота: цель действия выложенной карточки или действие основной теории.
  /// </summary>
  /// <returns><c>true</c>, если бот что-то выбрал.</returns>
  public bool RespondToPending(GameSession session)
  {
    if (session.PendingTheoryTrigger is { } trigger && trigger.Owner == Player)
    {
      ApplyTrigger(session, trigger, ChooseTrigger(session, trigger));
      return true;
    }

    if (session.PendingPlacement is { } choice && session.CurrentPlayer == Player)
    {
      ApplyPlacement(session, choice, ChoosePlacement(session, choice));
      return true;
    }

    return false;
  }

  /// <summary>
  /// Выбрать цель действия выложенной карточки (<see cref="ChoosePlacement"/>). Если цель не подошла — без цели,
  /// а если без цели нельзя, первую подходящую.
  /// </summary>
  public static void ApplyPlacement(GameSession session, PlacementChoice choice, PlacementResponse response)
  {
    try
    {
      session.ResolvePlacement(response.Target, response.Second);
    }
    catch (Exception error) when (error is ArgumentException or InvalidOperationException)
    {
      if (session.PendingPlacement is null)
        return;
      if (choice.Effect.TargetIsOptional)
        session.ResolvePlacement();
      else
        session.ResolvePlacement(FirstAllowed(choice.Effect, session.PlacementContext(choice)));
    }
  }

  /// <summary>
  /// Сделать выборы для действия основной теории по одному, как это делает игрок. Если выбор не принят — отказаться.
  /// </summary>
  public static void ApplyTrigger(GameSession session, TheoryTrigger trigger, TriggerResponse response)
  {
    try
    {
      if (response.Skip)
      {
        session.SkipTheoryTrigger();
        return;
      }

      if (response.HandCard is { } card && session.PendingTheoryTrigger == trigger && trigger.HandCard is null)
        session.SelectTriggerHandCard(card);
      if (response.Theory is { } theory && session.PendingTheoryTrigger == trigger && trigger.Theory is null)
        session.SelectTriggerTheory(theory);
      if (response.Cell is { } cell && session.PendingTheoryTrigger == trigger && trigger.Cell is null)
        session.SelectTriggerCell(cell);
    }
    catch (Exception error) when (error is ArgumentException or InvalidOperationException)
    {
      // Выбор не принят — дальше ничего не выбрать.
    }

    if (session.PendingTheoryTrigger == trigger)
      session.SkipTheoryTrigger();
  }

  private static Cell? FirstAllowed(Effect effect, GameContext context) =>
    effect.TargetCells(context).Where(target => target.Check.IsAllowed).Select(target => (Cell?)target.Cell).FirstOrDefault();

  // ---------- Ходы ----------

  private IEnumerable<BotMove> UseCardMoves(GameSession session, BotEvaluator evaluator)
  {
    var field = session.Field;
    var cells = field.PlayerZones[Player].Cells.Concat(field.Chips.Where(chip => chip.Value == Player).Select(chip => chip.Key)).Distinct();
    foreach (var cell in cells)
    {
      if (!field.CanUse(cell, Player) || field.CardAt(cell) is not { } card)
        continue;

      var chance = field.SuccessChanceAt(cell) / 100.0;
      var baseContext = session.CreateContext().WithSourceCell(cell);
      foreach (var option in card.OnPlayFromHand.Options)
      {
        if (BestPlan(option, baseContext, evaluator) is not { } plan)
          continue;

        var value = chance * plan.Value + evaluator.FailurePenalty(cell);
        // Быстрое действие не тратит действие хода — его не жалко сыграть.
        if (option.IsQuick)
          value += Profile.QuickBonus;
        yield return new UseCardMove(cell, option, plan.Context, value);
      }
    }
  }

  private IEnumerable<BotMove> TheoryCardMoves(GameSession session, BotEvaluator evaluator)
  {
    foreach (var owned in session.Field.TheoryCards)
    {
      if (!session.Field.CanUseTheoryCard(owned, Player))
        continue;

      var effect = owned.Card.OnPlayFromHand;
      var context = session.CreateContext();
      if (!effect.RequiresTheory && session.Field.ColumnOf(owned.Card.Theory) is { } home)
        context = context.WithTargetTheory(home);
      if (BestPlan(effect, context, evaluator) is { } plan)
        yield return new UseTheoryCardMove(owned, plan.Context, plan.Value);
    }
  }

  private IEnumerable<BotMove> PlaceMoves(GameSession session, BotEvaluator evaluator)
  {
    foreach (var card in Player.Hand.Cards.Distinct())
    {
      if (BestPlacement(session, evaluator, card, extraCost: 0) is { } placement)
        yield return new PlaceMove(card, placement.Cell, placement.Value, placement.Column);
    }
  }

  /// <summary>
  /// Выложить карточку с руки взакрытую в теорию, где лежит открытый Спецагент бота.
  /// </summary>
  private IEnumerable<BotMove> HiddenPlaceMoves(GameSession session, BotEvaluator evaluator)
  {
    foreach (var card in Player.Hand.Cards.Distinct())
    {
      foreach (var (column, check) in session.Field.HiddenPlacementTargets(card, Player))
      {
        if (!check.IsAllowed || !Player.CanAfford(check.Cost))
          continue;
        yield return new PlaceHiddenMove(card, column, evaluator.HiddenPlacementValue(card, column, check));
      }
    }
  }

  private IEnumerable<BotMove> BuyMoves(GameSession session, BotEvaluator evaluator)
  {
    foreach (var (cell, card) in session.Market.OpenCards)
    {
      if (!session.Market.CheckBuy(cell, Player).IsAllowed)
        continue;
      if (BestPlacement(session, evaluator, card, extraCost: card.Cost) is not { } placement)
        continue;

      // Купленную карточку ещё нужно выложить — это второе действие, поэтому выгода меньше.
      var value = placement.Value * Profile.BuyFactor - card.Cost * evaluator.MoneyWeight - evaluator.HandCrowding
        + ThemedBuyBonus(card, placement.Column);
      yield return new BuyMove(cell, card, value);
    }
  }

  /// <summary>
  /// Прибавка к покупке карточки с теорией, которую бот подложит в штаб этой же теории: чем больше там адептов,
  /// тем выгоднее (<see cref="BotProfile.ThemedBuyBonus"/> за адепта, не больше <see cref="BotProfile.ThemedBuyAdeptsCap"/> адептов).
  /// </summary>
  private double ThemedBuyBonus(Card card, TheoryColumn? target) =>
    card.Theory is { } theory && target is { IsClosed: false } column && column.Theory == theory
      ? Profile.ThemedBuyBonus * Math.Min(column.Adepts, Profile.ThemedBuyAdeptsCap)
      : 0;

  /// <summary>
  /// Лучшее место для карточки: в личном штабе или в общей зоне, с оценкой выгоды.
  /// </summary>
  /// <param name="extraCost">Сколько $ потратится до выкладывания (покупка карточки).</param>
  private (Cell Cell, double Value, TheoryColumn? Column)? BestPlacement(GameSession session, BotEvaluator evaluator, Card card, int extraCost)
  {
    if (card is not PersonCard person)
      return null;

    (Cell Cell, double Value, TheoryColumn? Column)? best = null;
    foreach (var (cell, check) in session.Field.AvailableCells(card, Player))
    {
      if (!Player.CanAfford(check.Cost + extraCost))
        continue;

      var value = session.Field.CommonZone.Contains(cell)
        ? evaluator.TheoryPlacementValue(person, cell, check)
        : evaluator.PersonalPlacementValue(person, cell);
      value -= check.Cost * evaluator.MoneyWeight;
      if (best is null || value > best.Value.Value)
        best = (cell, value, check.Column);
    }

    return best;
  }

  // ---------- Цели действий ----------

  private sealed record Plan(GameContext Context, double Value);

  /// <summary>
  /// Лучшие цели для варианта действия и его оценка, или <c>null</c>, если целей нет.
  /// </summary>
  private Plan? BestPlan(Effect option, GameContext context, BotEvaluator evaluator)
  {
    if (option.BorrowsTargetCard)
      return BestBorrowPlan(option, context, evaluator);

    Plan? best = null;
    foreach (var candidate in Candidates(option, context, evaluator).Take(MaxCandidates))
    {
      var value = evaluator.ValueOf(option, candidate) - evaluator.CostOf(candidate, option);
      if (best is null || value > best.Value)
        best = new Plan(candidate, value);
    }
    return best;
  }

  /// <summary>
  /// Действие использует другую карточку (бандит, актёр, учёный): выбираем карточку, вариант её действия и его цели.
  /// </summary>
  private Plan? BestBorrowPlan(Effect option, GameContext context, BotEvaluator evaluator)
  {
    Plan? best = null;
    foreach (var (cell, check) in option.TargetCells(context))
    {
      if (!check.IsAllowed || !Player.CanAfford(check.Cost) || context.Field.CardAt(cell) is not { } card)
        continue;

      // Своего персонажа учёный играет со 100% успехом: выгода — в том, что не нужно рисковать проверкой.
      var factor = option is PlayOwnCardEffect ? Math.Max(0.3, 1 - context.Field.SuccessChanceAt(cell) / 100.0) : 1.0;
      foreach (var inner in card.OnPlayFromHand.Options.Where(inner => inner.CanBeBorrowed && inner is not CopyRivalCardEffect))
      {
        var borrowed = context.WithBorrowedCell(cell).WithBorrowedOption(inner);
        foreach (var candidate in Candidates(inner, borrowed, evaluator).Take(MaxCandidates))
        {
          var value = evaluator.ValueOf(inner, candidate) * factor - evaluator.CostOf(candidate, inner) - check.Cost * evaluator.MoneyWeight;
          if (best is null || value > best.Value)
            best = new Plan(candidate, value);
        }
      }
    }
    return best;
  }

  /// <summary>
  /// Все наборы целей для действия: карточка в руке, карточки на поле, другой игрок, теории — что нужно действию.
  /// </summary>
  private IEnumerable<GameContext> Candidates(Effect effect, GameContext context, BotEvaluator evaluator)
  {
    IEnumerable<GameContext> candidates = [context];
    if (effect.RequiresHandCard)
      candidates = candidates.SelectMany(candidate => Player.Hand.Cards.Distinct().Select(candidate.WithHandCard));
    if (effect.RequiresTargetCell)
      candidates = candidates.SelectMany(candidate => CellCandidates(effect, candidate, evaluator));
    if (effect.RequiresTargetPlayer)
      candidates = candidates.SelectMany(candidate => evaluator.Rivals.Select(candidate.WithTargetPlayer));
    if (effect.RequiresTheory)
      candidates = candidates.SelectMany(candidate => TheoryCandidates(effect, candidate, evaluator));
    return candidates;
  }

  private IEnumerable<GameContext> CellCandidates(Effect effect, GameContext context, BotEvaluator evaluator)
  {
    foreach (var (cell, check) in effect.TargetCells(context).ToList())
    {
      if (!check.IsAllowed || !Player.CanAfford(check.Cost))
        continue;

      var first = context.WithTargetCell(cell);
      if (!effect.RequiresSecondTargetCell)
      {
        yield return first;
        continue;
      }

      foreach (var (second, secondCheck) in effect.TargetCells(first).ToList())
      {
        if (second != cell && secondCheck.IsAllowed && Player.CanAfford(check.Cost + secondCheck.Cost))
          yield return first.WithSecondTargetCell(second);
      }
    }

    if (effect.AcceptsHandCardTarget && context.HandCard is null)
    {
      foreach (var card in Player.Hand.Cards.Distinct())
      {
        if (effect.CheckHandCard(card, context).IsAllowed)
          yield return context.WithHandCard(card);
      }
    }
  }

  private IEnumerable<GameContext> TheoryCandidates(Effect effect, GameContext context, BotEvaluator evaluator)
  {
    var field = context.Field;
    var theories = field.TheoryColumns.Where(column => effect.CanTargetTheory(column, Player)).ToList();

    if (effect.RequiresSourceTheory)
    {
      foreach (var source in field.TheoryColumns.Where(column => effect.CanTargetSourceTheory(column, Player)))
      {
        foreach (var target in theories.Where(target => target != source))
          yield return context.WithSourceTheory(source).WithTargetTheories([target]);
      }
      yield break;
    }

    if (context.HandCard is { } card)
    {
      // Выложить карточку с руки взакрытую: подходят теории, куда её можно положить.
      var discount = effect is PlaceHiddenCardEffect hidden ? hidden.JoiningDiscount : 0;
      foreach (var column in field.TheoryColumns)
      {
        var check = field.CheckHidden(card, column, Player, discount);
        if (check.IsAllowed && Player.CanAfford(check.Cost) && effect.CanTargetTheory(column, Player))
          yield return context.WithTargetTheories([column]);
      }
      yield break;
    }

    if (effect.TheoryCount > 1)
    {
      // Несколько теорий: лучшие по выгоде адептов, с повторами, если можно.
      var ranked = theories.OrderByDescending(column => evaluator.AdeptValue(column, 1)).ToList();
      if (ranked.Count == 0)
        yield break;
      var picked = effect.AllowsRepeatedTheories
        ? Enumerable.Repeat(ranked[0], effect.TheoryCount).ToList()
        : ranked.Take(effect.TheoryCount).ToList();
      if (picked.Count == effect.TheoryCount)
        yield return context.WithTargetTheories(picked);
      yield break;
    }

    foreach (var column in theories)
      yield return context.WithTargetTheories([column]);
    foreach (var cell in field.AdeptCardsOf(Player).Where(cell => effect.CanTargetAdeptCard(cell, field)))
      yield return context.WithTargetAdeptCard(cell);
  }
}
