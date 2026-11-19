using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Cards.Theories;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Theories;
using ConspiracyLeague.Core.Views;

namespace ConspiracyLeague.Core.Client;

/// <summary>
/// Партия на стороне клиента: собрана по виду (<see cref="PlayerView"/>), который прислал сервер. По ней интерфейс
/// рисует стол и подсказывает ходы теми же правилами, что и в одиночной игре, но сама она ничего не решает —
/// ходы уходят на сервер командами, а следующий вид снова пересобирает её (<see cref="Apply"/>).
/// </summary>
/// <remarks>
/// <para>
/// Клиент знает только то, что ему положено: чужие руки, колода и резерв рынка здесь — заглушки нужного числа,
/// у закрытых карт, которых игрок не знает, лицевая сторона — заглушка. Поэтому подсказки по зеркалу могут
/// ошибаться там, где решает скрытое, — это не страшно: команду всё равно проверяет сервер.
/// </para>
/// <para>
/// Объекты партии (<see cref="GameSession"/>, игроки, поле, рынок) живут всё время — интерфейс подписывается
/// на них один раз. Карточки с тем же идентификатором и той же лицевой стороной тоже остаются прежними объектами,
/// поэтому выбранная в интерфейсе карточка переживает обновление. После каждого обновления
/// вызывается <see cref="GameSession.Restored"/>.
/// </para>
/// </remarks>
public sealed class ClientMirror
{
  private Dictionary<int, Card> _cards = [];

  /// <summary>
  /// Закрытые карты, чью лицевую сторону этот игрок не знает: у них лицевая сторона — заглушка.
  /// </summary>
  private HashSet<HiddenCard> _unknown = new(ReferenceEqualityComparer.Instance);

  /// <summary>
  /// Собрать партию по первому виду от сервера.
  /// </summary>
  /// <param name="view">Вид партии.</param>
  /// <param name="othersAsBots">
  /// Пометить чужие места ботами (<see cref="Player.IsBot"/>), а своё — человеком: интерфейс не действует за тех,
  /// кто помечен ботом, — так он и не будет действовать за других игроков. Настоящие боты видны в <see cref="View"/>.
  /// </param>
  /// <exception cref="InvalidDataException">Вид противоречив — по нему партию не собрать.</exception>
  public ClientMirror(PlayerView view, bool othersAsBots = false)
  {
    ArgumentNullException.ThrowIfNull(view);
    if (view.Players.Count is < 1 or > 8)
      throw new InvalidDataException($"Неверное число игроков: {view.Players.Count}.");

    Seat = view.Viewer;
    try
    {
      Session = new GameSession(
        view.Players.Select(player => new Player(player.Name, player.Color, player.Money)
        {
          IsBot = othersAsBots ? player.Seat != view.Viewer : player.IsBot,
          BotLevel = player.BotLevel,
        }),
        new Random(0),
        view.Mode);
    }
    catch (ArgumentException error)
    {
      throw new InvalidDataException($"Неверные игроки: {error.Message}", error);
    }

    View = view;
    Restore(view);
  }

  /// <summary>
  /// Партия, по которой рисует интерфейс.
  /// </summary>
  public GameSession Session { get; }

  /// <summary>
  /// Место игрока за столом или <c>null</c> — зритель.
  /// </summary>
  public int? Seat { get; }

  /// <summary>
  /// Игрок этого клиента или <c>null</c> — зритель.
  /// </summary>
  public Player? Player => Seat is { } seat ? Session.Players[seat] : null;

  /// <summary>
  /// Последний применённый вид партии.
  /// </summary>
  public PlayerView View { get; private set; }

  /// <summary>
  /// Применить новый вид от сервера: партия пересобирается и вызывается <see cref="GameSession.Restored"/>.
  /// Если вид противоречив, партия не меняется.
  /// </summary>
  /// <exception cref="InvalidDataException">Вид противоречив или не для этого клиента.</exception>
  public void Apply(PlayerView view)
  {
    ArgumentNullException.ThrowIfNull(view);
    if (view.Viewer != Seat)
      throw new InvalidDataException("Вид партии прислан для другого игрока.");
    if (view.Mode != Session.Mode)
      throw new InvalidDataException("В виде партии другой режим.");
    if (view.Players.Count != Session.Players.Count
      || view.Players.Where((player, index) => player.Seat != index || player.Name != Session.Players[index].Name
        || player.Color != Session.Players[index].Color).Any())
      throw new InvalidDataException("В виде партии другие игроки.");

    Restore(view);
    View = view;
    Session.RaiseRestored();
  }

  private void Restore(PlayerView view)
  {
    // Сначала собираем всё новое и проверяем, потом разом подменяем: на противоречивом виде партия не меняется.
    var state = new Builder(this, view).Build();
    var session = Session;
    for (var seat = 0; seat < session.Players.Count; seat++)
    {
      var player = session.Players[seat];
      var info = view.Players[seat];
      player.Restore(info.Money, info.EarnedFrom);
      player.Hand.Restore(state.Hands[seat]);
    }

    session.Turns.Restore(view.CurrentSeat, view.FirstSeat, view.Players.Where(player => player.Passed).Select(player => session.Players[player.Seat]));
    session.Field.FailureConsequences = view.FailureConsequences;
    session.Field.Restore(state.CommonZone, state.Cells, state.Columns, state.TheoryCards);
    foreach (var (card, influence) in state.Influences)
      card.Influence = influence;
    session.Market.Restore(state.Market, state.Placeholders(view.Market.DeckCount), state.Placeholders(view.Market.ReserveCount), session.MarketCardsPerRow);
    session.Restore(view.Phase, view.Round, view.IsLastRound, view.ActionsLeft, state.Check, state.Placement, state.Trigger);

    session.CardIds.Clear();
    foreach (var (id, card) in state.Cards)
      session.CardIds.Assign(card, id);
    _cards = state.Cards;
    _unknown = state.Unknown;
  }

  /// <summary>
  /// Новое состояние партии, собранное по виду.
  /// </summary>
  private sealed class State
  {
    public Dictionary<int, Card> Cards { get; } = [];

    public HashSet<HiddenCard> Unknown { get; } = new(ReferenceEqualityComparer.Instance);

    public List<(PersonCard Card, int Influence)> Influences { get; } = [];

    public List<List<Card>> Hands { get; } = [];

    public Zone CommonZone { get; set; } = null!;

    public List<RestoredCell> Cells { get; } = [];

    public List<TheoryColumn> Columns { get; } = [];

    public List<OwnedTheoryCard> TheoryCards { get; } = [];

    public List<KeyValuePair<Cell, PersonCard>> Market { get; } = [];

    public SuccessCheck? Check { get; set; }

    public PlacementChoice? Placement { get; set; }

    public TheoryTrigger? Trigger { get; set; }

    /// <summary>
    /// Карточки-заглушки там, где клиент знает только их число.
    /// </summary>
    public IEnumerable<PersonCard> Placeholders(int count) => Enumerable.Range(0, count).Select(_ => new HomelessCard());
  }

  private sealed class Builder(ClientMirror mirror, PlayerView view)
  {
    /// <summary>
    /// Больше карточек в руке или в колоде не бывает — защита от вида, из-за которого клиент создал бы миллионы объектов.
    /// </summary>
    private const int MaxCount = 1000;

    private readonly State _state = new();
    private readonly GameSession _session = mirror.Session;

    private IReadOnlyList<Player> Players => _session.Players;

    public State Build()
    {
      Validate();
      Hands();
      _state.CommonZone = new Zone(view.CommonZone.Left, view.CommonZone.Top, view.CommonZone.Width, view.CommonZone.Height);
      FieldCards();
      TheoryCards();
      foreach (var open in view.Market.Open)
        _state.Market.Add(new(open.Cell, Person(Resolve(open.Card))));
      if (_state.Market.Select(pair => pair.Key).Distinct().Count() != _state.Market.Count)
        throw new InvalidDataException("На рынке две карточки в одной ячейке.");

      if (view.CurrentSeat < 0 || view.CurrentSeat >= Players.Count || view.FirstSeat < 0 || view.FirstSeat >= Players.Count)
        throw new InvalidDataException("Неверный игрок, который ходит.");
      if (view.PendingCheck is { } check)
        _state.Check = new SuccessCheck(check.Cell, check.Chance, check.Die);
      Placement();
      Trigger();
      return _state;
    }

    /// <summary>
    /// Больше денег, адептов, влияния не бывает: такие числа — подделка, и на них правила переполнились бы.
    /// </summary>
    private const int MaxMoney = 10_000_000;
    private const int MaxAdepts = 100_000;
    private const int MaxInfluence = 100;
    private const int MaxRound = 10_000;
    private const int MaxLabel = 256;

    /// <summary>
    /// Проверить числа и ячейки вида до того, как что-то менять: всё, что может не пройти, должно отказать здесь,
    /// а не посреди применения (тогда партия осталась бы наполовину новой).
    /// </summary>
    private void Validate()
    {
      static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string what)
      {
        if (!condition)
          throw new InvalidDataException($"Неверный вид партии: {what}.");
      }

      var field = _session.Field;
      bool OnField(Cell cell) => cell.Column >= 0 && cell.Column < field.Columns && cell.Row >= 0 && cell.Row < field.Rows;
      bool Seat(int seat) => seat >= 0 && seat < Players.Count;
      static bool Face(CardFace? face) => face is null || face.Influence is >= 0 and <= MaxInfluence && face.Name is not null;

      Require(view.Version >= 0, "версия");
      Require(view.Round is >= 0 and <= MaxRound, "раунд");
      Require(view.ActionsLeft is >= 0 and <= GameSession.ActionsPerTurn, "действия");
      Require(Enum.IsDefined(view.Phase), "фаза");
      Require(Seat(view.CurrentSeat) && Seat(view.FirstSeat), "кто ходит");
      var zone = view.CommonZone;
      Require(zone is not null && zone.Left >= 0 && zone.Top >= 0 && zone.Width > 0 && zone.Height > 0
        && zone.Left + zone.Width <= field.Columns && zone.Top + zone.Height <= field.Rows, "общая зона");
      Require(view.Players is not null && view.Hand is not null && view.Cards is not null && view.Theories is not null
        && view.TheoryCards is not null && view.Market is not null, "нет раздела");

      foreach (var player in view.Players!)
      {
        Require(player is not null && player.Money is >= 0 and <= MaxMoney && player.HandCount is >= 0 and <= MaxCount, "игрок");
        Require(player!.EarnedFrom is null || player.EarnedFrom.Count <= MaxLabel
          && player.EarnedFrom.All(pair => pair.Key.Length <= MaxLabel && pair.Value is >= 0 and <= MaxMoney), "доходы");
      }

      Require(view.Hand!.All(card => card?.Face is not null && Face(card.Face)), "рука");
      foreach (var card in view.Cards!)
      {
        Require(card?.Card is not null && OnField(card.Cell) && Seat(card.OwnerSeat), "карточка на поле");
        Require(card!.Influence is >= 0 and <= MaxInfluence && card.SuccessChance is >= 0 and <= SuccessCheck.MaxChance
          && card.Trained is >= -SuccessCheck.MaxChance and <= SuccessCheck.MaxChance && card.Adepts is >= 0 and <= MaxAdepts
          && Face(card.Card.Face), "числа карточки");
        Require((card.ChippedBy is null || Seat(card.ChippedBy.Value)) && (card.WheelOf is null || Seat(card.WheelOf.Value)), "метки карточки");
      }

      foreach (var theory in view.Theories!)
      {
        Require(theory?.Name is not null && theory.Members is not null && theory.LeaderMeeples is not null, "теория");
        Require(theory!.Adepts is >= 0 and <= MaxAdepts && theory.LeaderMeeples.Count <= MaxCount
          && theory.LeaderMeeples.All(Seat) && theory.Members.All(OnField), "числа теории");
      }

      foreach (var owned in view.TheoryCards!)
      {
        Require(owned?.Card is not null && OnField(owned.Cell) && Seat(owned.OwnerSeat)
          && (owned.AttachedTo is not { } attached || OnField(attached)), "карточка теории");
      }

      Require(view.Market!.Open is not null && view.Market.Open.All(open => open?.Card is not null && OnField(open.Cell) && Face(open.Card.Face))
        && view.Market.DeckCount is >= 0 and <= MaxCount && view.Market.ReserveCount is >= 0 and <= MaxCount, "рынок");
      if (view.PendingCheck is { } check)
        Require(OnField(check.Cell) && check.Chance is >= 0 and <= SuccessCheck.MaxChance
          && check.Die is >= 1 and <= SuccessCheck.DieSides, "проверка успеха");
      if (view.PendingPlacement is { } placement)
        Require(OnField(placement.Cell) && placement.Theory is not null
          && (placement.AllowedTargets is null || placement.AllowedTargets.All(OnField)), "выбор цели");
      if (view.PendingTrigger is { } trigger)
        Require(Seat(trigger.OwnerSeat) && (trigger.ChosenCell is not { } chosen || OnField(chosen))
          && (trigger.AllowedCells is null || trigger.AllowedCells.All(OnField)), "действие теории");
    }

    private Player PlayerAt(int seat) =>
      seat >= 0 && seat < Players.Count ? Players[seat] : throw new InvalidDataException($"Нет игрока на месте {seat}.");

    private void Hands()
    {
      for (var seat = 0; seat < Players.Count; seat++)
      {
        var info = view.Players[seat];
        if (info.HandCount is < 0 or > MaxCount)
          throw new InvalidDataException("Неверное число карточек в руке.");

        if (seat == view.Viewer)
        {
          if (view.Hand.Count != info.HandCount)
            throw new InvalidDataException("Своя рука не сходится с числом карточек.");
          _state.Hands.Add(view.Hand.Select(Resolve).ToList());
        }
        else
          _state.Hands.Add(_state.Placeholders(info.HandCount).Cast<Card>().ToList());
      }
    }

    private void FieldCards()
    {
      var cells = new HashSet<Cell>();
      foreach (var card in view.Cards)
      {
        if (!cells.Add(card.Cell))
          throw new InvalidDataException($"В ячейке {card.Cell} две карточки.");

        var owner = PlayerAt(card.OwnerSeat);
        var resolved = Person(card.Card.Hidden ? Hidden(card.Card, owner, card.Revealed) : Resolve(card.Card));
        _state.Influences.Add((resolved, card.Influence));
        _state.Cells.Add(new RestoredCell(
          card.Cell,
          resolved,
          card.Used,
          card.Rotated,
          card.Trained,
          card.Adepts,
          card.ChippedBy is { } chip ? PlayerAt(chip) : null,
          card.WheelOf is { } wheel ? PlayerAt(wheel) : null));
      }

      var byCell = view.Cards.ToDictionary(card => card.Cell);
      var cardAt = _state.Cells.ToDictionary(cell => cell.Cell, cell => (PersonCard)cell.Card);
      var theories = new HashSet<string>();
      foreach (var theoryView in view.Theories)
      {
        if (!theories.Add(theoryView.Name) || theoryView.Members.Count == 0)
          throw new InvalidDataException($"Неверная теория «{theoryView.Name}».");

        var theory = TheoryNamed(theoryView.Name);
        TheoryColumn? column = null;
        foreach (var cell in theoryView.Members)
        {
          if (!byCell.TryGetValue(cell, out var card) || card.Theory != theoryView.Name)
            throw new InvalidDataException($"Карточки теории «{theoryView.Name}» нет в ячейке {cell}.");

          var member = new TheoryMember(cell, cardAt[cell], PlayerAt(card.OwnerSeat));
          if (column is null)
            column = new TheoryColumn(theory, member);
          else
            column.Add(member);
        }

        column!.Restore(Math.Max(0, theoryView.Adepts), theoryView.Closed, theoryView.LeaderMeeples.Select(PlayerAt));
        _state.Columns.Add(column);
      }

      if (view.Cards.Any(card => card.Theory is { } name && !theories.Contains(name)))
        throw new InvalidDataException("Карточка лежит в теории, которой нет.");
    }

    private void TheoryCards()
    {
      foreach (var owned in view.TheoryCards)
      {
        if (Resolve(owned.Card) is not TheoryCard card)
          throw new InvalidDataException("Это не карточка теории.");

        _state.TheoryCards.Add(new OwnedTheoryCard(card, PlayerAt(owned.OwnerSeat), owned.Cell)
        {
          IsUsed = owned.Used,
          AttachedTo = owned.AttachedTo,
        });
      }
    }

    private void Placement()
    {
      if (view.PendingPlacement is not { } placement)
        return;

      var cell = _state.Cells.FirstOrDefault(cell => cell.Cell == placement.Cell)?.Card as PersonCard
        ?? throw new InvalidDataException("Нет карточки, которая выбирает цель.");
      var column = _state.Columns.FirstOrDefault(column => column.Theory.Name == placement.Theory)
        ?? throw new InvalidDataException("Нет теории, где выбирают цель.");
      _state.Placement = new PlacementChoice(placement.Cell, cell.OnPlacedInHeadquarters, column);
    }

    private void Trigger()
    {
      if (view.PendingTrigger is not { } trigger)
        return;

      var parent = _state.TheoryCards.FirstOrDefault(owned => _state.Cards.GetValueOrDefault(trigger.ParentCardId) == owned.Card)
        ?? throw new InvalidDataException("Нет карточки основной теории.");
      var owner = PlayerAt(trigger.OwnerSeat);
      var context = new GameContext(owner, _session.Field, _session.CreateContext().Random, _session.Market);
      var pending = new TheoryTrigger(parent, context);
      if (trigger.HandCardChosen)
      {
        var hand = _state.Hands[trigger.OwnerSeat];
        pending.HandCard = trigger.HandCardId is { } id
          ? _state.Cards.GetValueOrDefault(id) is { } card && hand.Contains(card) ? card : throw new InvalidDataException("Выбранной карточки нет в руке.")
          // Чужой выбор: какую карточку выбрали, не видно — достаточно знать, что выбор сделан.
          : new HomelessCard();
      }

      if (trigger.ChosenTheory is { } theory)
        pending.Theory = _state.Columns.FirstOrDefault(column => column.Theory.Name == theory) ?? throw new InvalidDataException("Нет выбранной теории.");
      pending.Cell = trigger.ChosenCell;
      _state.Trigger = pending;
    }

    private static Theory TheoryNamed(string name) =>
      TheoryCatalog.All.FirstOrDefault(theory => theory.Name == name) ?? throw new InvalidDataException($"Нет теории «{name}».");

    private static PersonCard Person(Card card) =>
      card as PersonCard ?? throw new InvalidDataException($"«{card.Name}» — не персонаж.");

    /// <summary>
    /// Открытая карточка: прежний объект, если идентификатор и лицевая сторона те же, иначе новый.
    /// </summary>
    private Card Resolve(CardView view)
    {
      if (view.Hidden || view.Face is not { } face)
        throw new InvalidDataException("Здесь закрытых карт не бывает.");

      var card = mirror._cards.GetValueOrDefault(view.Id) is { } known and not HiddenCard && SameFace(known, face)
        ? known
        : CreateCard(face);
      if (card is PersonCard person)
        _state.Influences.Add((person, face.Influence));
      return Register(view.Id, card);
    }

    /// <summary>
    /// Закрытая карта на поле. Лицевую сторону знают не все: у незнающих она — заглушка.
    /// Знание и раскрытие не отменяются, поэтому, если они «пропали» (сервер откатил партию), карта создаётся заново.
    /// </summary>
    private Card Hidden(CardView view, Player owner, bool revealed)
    {
      var viewer = mirror.Player;
      HiddenCard? card = null;
      if (mirror._cards.GetValueOrDefault(view.Id) is HiddenCard known && known.IsRevealed == revealed)
      {
        var unknown = mirror._unknown.Contains(known);
        if (view.Face is { } face ? !unknown && SameFace(known.Face, face) : unknown)
          card = known;
      }

      if (card is null)
      {
        if (view.Face is { } face)
        {
          var real = CreateCard(face);
          if (real is PersonCard person)
            person.Influence = face.Influence;
          card = new HiddenCard(real);
        }
        else
          card = HiddenCard.Unknown();
        if (revealed)
          card.Reveal();
      }

      if (view.Face is null)
        _state.Unknown.Add(card);
      // Подсмотренная карта: лицевую сторону знает и этот игрок.
      else if (!revealed && viewer is not null && viewer != owner)
        card.ShowTo(viewer);
      return Register(view.Id, card);
    }

    private Card Register(int id, Card card)
    {
      if (id <= 0 || !_state.Cards.TryAdd(id, card))
        throw new InvalidDataException($"Неверный или повторный идентификатор карточки {id}.");
      return card;
    }

    private static Card CreateCard(CardFace face)
    {
      try
      {
        return CardFaces.Create(face);
      }
      catch (ArgumentException error)
      {
        throw new InvalidDataException(error.Message, error);
      }
    }

    private static bool SameFace(Card card, CardFace face) =>
      card is not HiddenCard && CardFaces.Of(card) with { Influence = 0 } == face with { Influence = 0 };
  }
}
