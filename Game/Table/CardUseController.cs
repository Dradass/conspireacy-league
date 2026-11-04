using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Theories;
using ConspiracyLeague.Game.Cards;
using ConspiracyLeague.Game.Ui;
using Godot;

namespace ConspiracyLeague.Game.Table;

/// <summary>
/// Использование карточек из личного штаба (своей рабочей зоны) щелчком мыши.
/// </summary>
/// <remarks>
/// Если у действия карточки один вариант, он выполняется сразу, если несколько — сначала появляется меню выбора.
/// Если действию нужна теория (например, «Добавить адепта в теорию»), игрок выбирает её щелчком по стопке теории;
/// некоторые действия можно вместо теории применить к своей карточке в личном штабе, на которую приводят адептов
/// (например, к Мастеру теорий).
/// Если действию нужна карточка в руке (например, «Выложить карту взакрытую»), игрок сначала выбирает её щелчком
/// по руке, а затем стопку теории, сверху которой её положить.
/// Если действию нужна карточка на поле (например, «Перевернуть карточку другого игрока»), подходящие карточки
/// подсвечиваются, и игрок выбирает одну из них.
/// Esc или щелчок левой кнопкой мимо карточек отменяют выбор;
/// перемещение камеры правой кнопкой его не сбрасывает. Затем проводится проверка успеха (<see cref="SuccessCheck"/>):
/// на карточку бросается двадцатигранный кубик, на нём появляется галка или крест, и через секунду карточка
/// переворачивается рубашкой вверх. Действие выполняется, только если проверка успешна.
/// Если действие использует другую карточку (например, «Использовать чужого непрофессионала» у Бандита или
/// «Сыграть своего персонажа со 100% успехом» у Учёного), игрок выбирает подсвеченную карточку, затем вариант
/// её действия (если их несколько) и, если нужно, теорию.
/// Что можно использовать, решает <see cref="PlayingField"/>.
/// </remarks>
public partial class CardUseController : Node
{
  private const double ResultPause = 1.0;

  private Board _board = null!;
  private GameSession _session = null!;
  private HandView _hand = null!;
  private PopupMenu _menu = null!;
  private PopupMenu _borrowMenu = null!;
  private PopupMenu _playerMenu = null!;
  private Targeting? _choosingPlayer;
  private Selection? _selection;
  private Targeting? _targeting;

  /// <summary>
  /// Карточку не удалось использовать; аргумент — причина.
  /// </summary>
  public event Action<string>? UseDenied;

  /// <summary>
  /// Подсказка игроку, что делать дальше.
  /// </summary>
  public event Action<string>? Hint;

  /// <summary>
  /// Карточка, для которой открыто меню выбора действия.
  /// </summary>
  /// <param name="View">Карточка.</param>
  /// <param name="Cell">Ячейка карточки.</param>
  private sealed record Selection(CardView View, Vector2I Cell);

  /// <summary>
  /// Действие карточки, для которого игрок выбирает карточку в руке и теорию.
  /// </summary>
  /// <param name="View">Карточка.</param>
  /// <param name="Cell">Ячейка карточки.</param>
  /// <param name="Option">Выбранный вариант действия.</param>
  /// <param name="HandCard">Выбранная карточка в руке или <c>null</c>.</param>
  /// <param name="BorrowedCell">Чужая карточка, которую использует действие (<see cref="Effect.BorrowsTargetCard"/>).</param>
  /// <param name="BorrowedOption">Выбранный вариант действия чужой карточки.</param>
  /// <param name="SourceTheory">Выбранная теория-источник (<see cref="Effect.RequiresSourceTheory"/>).</param>
  /// <param name="TargetCell">Выбранная карточка на поле (<see cref="Effect.RequiresTargetCell"/>).</param>
  /// <param name="SecondTargetCell">Вторая выбранная карточка (<see cref="Effect.RequiresSecondTargetCell"/>).</param>
  /// <param name="Placement">Цель выбирают для действия карточки, выложенной в штаб теории, а не сыгранной.</param>
  /// <param name="OwnedTheory">Применяется карточка теории, а не персонаж из личного штаба.</param>
  private sealed record Targeting(
    CardView View,
    Vector2I Cell,
    Effect Option,
    Card? HandCard = null,
    Cell? BorrowedCell = null,
    Effect? BorrowedOption = null,
    TheoryColumn? SourceTheory = null,
    Cell? TargetCell = null,
    Cell? SecondTargetCell = null,
    PlacementChoice? Placement = null,
    OwnedTheoryCard? OwnedTheory = null,
    IReadOnlyList<TheoryColumn>? Theories = null,
    Cell? CopiedCell = null,
    Effect? CopiedOption = null)
  {
    /// <summary>
    /// Игрок ещё выбирает теорию-источник.
    /// </summary>
    public bool PicksSourceTheory => Active.RequiresSourceTheory && SourceTheory is null;

    /// <summary>
    /// Игрок ещё выбирает карточку в руке.
    /// </summary>
    public bool PicksHandCard => Active.RequiresHandCard && HandCard is null;

    /// <summary>
    /// Игрок ещё выбирает карточку, действие которой выполнит своя (<see cref="Effect.BorrowsTargetCard"/>).
    /// </summary>
    public bool PicksBorrowedCell => Option.BorrowsTargetCard && BorrowedCell is null;

    /// <summary>
    /// Игрок ещё выбирает карточку на поле: ту, чьё действие выполнить, или цель действия.
    /// </summary>
    public bool PicksTargetCell =>
      PicksBorrowedCell
      || PicksCopiedCell
      || Active.RequiresTargetCell && !Active.BorrowsTargetCard && TargetCell is null && !HandTargetPicked
      || PicksSecondTargetCell;

    /// <summary>
    /// Вместо карточки на поле выбрана карточка в руке (<see cref="Effect.AcceptsHandCardTarget"/>).
    /// </summary>
    public bool HandTargetPicked => Active.AcceptsHandCardTarget && HandCard is not null;

    /// <summary>
    /// Копирование выбрано через чужое действие: теперь выбирают карточку, действие которой скопируют.
    /// </summary>
    public bool PicksCopiedCell => BorrowedOption is CopyRivalCardEffect && CopiedCell is null;

    /// <summary>
    /// Игрок выбирает вторую карточку на поле.
    /// </summary>
    public bool PicksSecondTargetCell => Active.RequiresSecondTargetCell && TargetCell is not null && SecondTargetCell is null;

    /// <summary>
    /// Действие, для которого сейчас выбирают карточку на поле.
    /// </summary>
    public Effect CellPicker => PicksCopiedCell ? BorrowedOption! : PicksBorrowedCell ? Option : Active;

    /// <summary>
    /// Действие, для которого сейчас выбирают цели: скопированный вариант, если он выбран, иначе вариант чужой карточки,
    /// иначе своё.
    /// </summary>
    public Effect Active => CopiedOption ?? BorrowedOption ?? Option;
  }

  /// <summary>
  /// Настроить контроллер.
  /// </summary>
  /// <param name="board">Стол.</param>
  /// <param name="session">Сессия игры: карточки использует игрок, который сейчас ходит.</param>
  /// <param name="hand">Рука игрока, который сейчас ходит: в ней выбирают карточку для действия.</param>
  public void Initialize(Board board, GameSession session, HandView hand)
  {
    _board = board;
    _session = session;
    _hand = hand;
    _session.Turns.TurnChanged += OnTurnChanged;
    _session.PendingPlacementChanged += OnPendingPlacementChanged;
    _session.PendingTheoryTriggerChanged += OnPendingTheoryTriggerChanged;
    _hand.CardPicked += OnHandCardPicked;
  }

  public override void _Ready()
  {
    _menu = new PopupMenu { Name = "ActionMenu" };
    _menu.IdPressed += OnMenuIdPressed;
    // Откладываем: меню закрывается и после выбора пункта, выбор должен успеть обработаться.
    _menu.PopupHide += () => Callable.From(() => CloseSelection()).CallDeferred();
    AddChild(_menu);

    _borrowMenu = new PopupMenu { Name = "BorrowedActionMenu" };
    _borrowMenu.IdPressed += OnBorrowMenuIdPressed;
    // Меню закрыли, не выбрав вариант, — выбор отменяется. Откладываем, как и у основного меню.
    _borrowMenu.PopupHide += () => Callable.From(() =>
    {
      if (_targeting is { BorrowedCell: not null, BorrowedOption: null } or { CopiedCell: not null, CopiedOption: null })
        EndTargeting();
    }).CallDeferred();
    AddChild(_borrowMenu);

    _playerMenu = new PopupMenu { Name = "PlayerMenu" };
    _playerMenu.IdPressed += OnPlayerMenuIdPressed;
    // Меню закрыли, не выбрав игрока, — выбор отменяется. Откладываем, как и у основного меню.
    _playerMenu.PopupHide += () => Callable.From(() =>
    {
      if (_choosingPlayer is { } choosing)
      {
        _choosingPlayer = null;
        choosing.View.Lifted = false;
      }
    }).CallDeferred();
    AddChild(_playerMenu);
  }

  public override void _ExitTree()
  {
    _session.Turns.TurnChanged -= OnTurnChanged;
    _session.PendingPlacementChanged -= OnPendingPlacementChanged;
    _session.PendingTheoryTriggerChanged -= OnPendingTheoryTriggerChanged;
    _hand.CardPicked -= OnHandCardPicked;
  }

  public override void _UnhandledInput(InputEvent @event)
  {
    // Владелец основной теории выбирает цель своего действия: его ход и руку не трогаем.
    if (_session.PendingTheoryTrigger is { } trigger)
    {
      HandleTheoryTriggerInput(@event, trigger);
      return;
    }

    if (_targeting is not null)
    {
      HandleTargeting(@event);
      return;
    }

    if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click || _menu.Visible)
      return;
    if (_session.Phase != GamePhase.PlayerTurns || _session.PendingCheck is not null || _session.PendingPlacement is not null)
      return;
    if (_board.CellAt(_board.GetGlobalMousePosition()) is not { } cell)
      return;
    if (_board.TheoryCardAt(cell) is { } theoryCard)
    {
      GetViewport().SetInputAsHandled();
      ChooseTheoryCard(theoryCard);
      return;
    }
    if (!_board.Field.CanUse(cell.ToCell(), _session.CurrentPlayer))
      return;
    if (_board.ViewAt(cell) is not { } view)
      return;

    GetViewport().SetInputAsHandled();
    var options = view.Card.OnPlayFromHand.Options;
    if (options.Count == 1)
    {
      Choose(view, cell, options[0]);
      return;
    }

    _selection = new Selection(view, cell);
    view.Lifted = true;
    _menu.Clear();
    _menu.AddSeparator(view.Card.Name);
    for (var i = 0; i < options.Count; i++)
      _menu.AddItem(options[i].Description, i);
    _menu.ResetSize();
    _menu.Popup(new Rect2I((Vector2I)click.Position, Vector2I.Zero));
  }

  private void OnMenuIdPressed(long id)
  {
    if (CloseSelection(keepLifted: true) is { } selection)
      Choose(selection.View, selection.Cell, selection.View.Card.OnPlayFromHand.Options[(int)id]);
  }

  private Selection? CloseSelection(bool keepLifted = false)
  {
    var selection = _selection;
    _selection = null;
    if (selection is not null && !keepLifted)
      selection.View.Lifted = false;
    return selection;
  }

  /// <summary>
  /// Вариант действия выбран: выполнить его сразу или сначала выбрать карточку в руке и теорию.
  /// </summary>
  private void Choose(CardView view, Vector2I cell, Effect option, OwnedTheoryCard? ownedTheory = null)
  {
    _targeting = new Targeting(view, cell, option, OwnedTheory: ownedTheory);
    if (DenialOf(option, ContextFor(_targeting)) is { } reason)
    {
      EndTargeting();
      UseDenied?.Invoke(reason);
      return;
    }

    view.Lifted = true;
    Advance();
  }

  /// <summary>
  /// Игрок щёлкнул свою карточку теории: применить её действие, выбрав цель, если она нужна.
  /// </summary>
  private void ChooseTheoryCard(OwnedTheoryCard owned)
  {
    if (!_board.Field.CanUseTheoryCard(owned, _session.CurrentPlayer) || _board.TheoryViewOf(owned) is not { } view)
    {
      UseDenied?.Invoke(owned.IsUsed
        ? $"Карточка теории «{owned.Card.Name}» уже применена — она восстановится в фазе обновления карточек."
        : owned.Card.OnPlayFromHand == Effect.None
          ? $"У карточки «{owned.Card.Name}» нет действия — она работает пассивно."
          : "Эта карточка теории не ваша.");
      return;
    }

    Choose(view, owned.Cell.ToVector2I(), owned.Card.OnPlayFromHand, owned);
  }

  /// <summary>
  /// Все цели выбраны: применить карточку теории или сыграть карточку из личного штаба с проверкой успеха.
  /// </summary>
  private void Run(Targeting targeting, GameContext context)
  {
    if (targeting.OwnedTheory is not { } owned)
    {
      Use(targeting.View, targeting.Cell, targeting.Option, context);
      return;
    }

    try
    {
      _session.UseTheoryCard(owned, context);
    }
    catch (Exception error) when (error is ArgumentException or InvalidOperationException)
    {
      UseDenied?.Invoke(error.Message);
      return;
    }
    // Если основная теория ждёт выбора владельца, подсказку выставит выбор.
    if (_session.PendingTheoryTrigger is null)
      Hint?.Invoke(TheoryTriggerPrefix(owned.Card.Theory) + $"Применена карточка теории «{owned.Card.Name}»: {owned.Card.OnPlayFromHand.Description}.");
  }

  /// <summary>
  /// Перейти к следующей цели, которую нужно выбрать для действия, или выполнить действие, если выбраны все.
  /// </summary>
  private void Advance()
  {
    var targeting = _targeting!;
    var active = targeting.Active;
    // Сначала карточка в руке: от неё зависят цели на поле.
    if (targeting.PicksHandCard && active.RequiresTargetCell)
    {
      _hand.Picking = true;
      Hint?.Invoke("Выберите карточку в руке. Esc или щелчок мимо карточек — отмена.");
      return;
    }
    if (targeting.PicksTargetCell)
    {
      // Подсвечиваем карточки, которые можно выбрать, — с ценой, как при выкладывании с руки.
      var picker = targeting.CellPicker;
      _board.ShowAvailable(picker.TargetCells(ContextFor(targeting)).Where(target => target.Check.IsAllowed));
      // Карточку в руке тоже можно выбрать вместо карточки на поле.
      var handToo = picker.AcceptsHandCardTarget && targeting.HandCard is null;
      _hand.Picking = handToo;
      var which = targeting.PicksSecondTargetCell ? "вторую карточку" : handToo ? "подсвеченную карточку или в руке" : "подсвеченную карточку";
      Hint?.Invoke(targeting.Placement is not null
        ? $"«{targeting.View.Card.Name}»: {Lowercase(picker.Description)}. Выберите {which}. Esc или щелчок мимо карточек — пропустить."
        : $"Выберите {which}: {Lowercase(picker.Description)}. Esc или щелчок мимо карточек — отмена.");
    }
    else if (active.RequiresTargetPlayer)
      ChoosePlayer(EndTargeting());
    else if (targeting.PicksHandCard)
    {
      _hand.Picking = true;
      Hint?.Invoke("Выберите карточку в руке. Esc или щелчок мимо карточек — отмена.");
    }
    else if (active.RequiresTheory)
      ShowTheoryTargets(targeting);
    else if (targeting.Placement is not null)
    {
      EndTargeting();
      _session.ResolvePlacement(targeting.TargetCell, targeting.SecondTargetCell);
    }
    else
    {
      EndTargeting();
      Run(targeting, ContextFor(targeting));
    }
  }

  /// <summary>
  /// Карточку выложили в штаб теории, и её действию нужна цель: начать выбор. Откладываем — карточка
  /// ещё не легла на стол.
  /// </summary>
  private void OnPendingPlacementChanged(PlacementChoice? choice)
  {
    if (choice is not null)
      Callable.From(() => StartPlacement(choice)).CallDeferred();
  }

  private void StartPlacement(PlacementChoice choice)
  {
    if (_session.PendingPlacement != choice)
      return;
    if (_board.ViewAt(choice.Cell.ToVector2I()) is not { } view)
    {
      _session.ResolvePlacement();
      return;
    }

    _targeting = new Targeting(view, choice.Cell.ToVector2I(), choice.Effect, Placement: choice);
    view.Lifted = true;
    Advance();
  }

  /// <summary>
  /// Игрок отменил выбор. Для выложенной карточки это отказ от цели: её действие выполняется без неё.
  /// </summary>
  private void CancelTargeting()
  {
    if (EndTargeting().Placement is not null)
      _session.ResolvePlacement();
  }

  /// <summary>
  /// Действию нужен другой игрок: показать меню с другими игроками.
  /// </summary>
  private void ChoosePlayer(Targeting targeting)
  {
    _choosingPlayer = targeting;
    targeting.View.Lifted = true;
    _playerMenu.Clear();
    _playerMenu.AddSeparator(targeting.Active.Description);
    foreach (var (player, index) in OtherPlayers.Select((player, index) => (player, index)))
      _playerMenu.AddItem(player.Name, index);
    _playerMenu.ResetSize();
    _playerMenu.Popup(new Rect2I((Vector2I)GetViewport().GetMousePosition(), Vector2I.Zero));
  }

  private IEnumerable<Player> OtherPlayers => _session.Players.Where(player => player != _session.CurrentPlayer);

  private void OnPlayerMenuIdPressed(long id)
  {
    if (_choosingPlayer is not { } choosing)
      return;

    _choosingPlayer = null;
    var target = OtherPlayers.ElementAt((int)id);
    Run(choosing, ContextFor(choosing).WithTargetPlayer(target));
  }

  /// <summary>
  /// Почему действие сейчас некуда применить, или <c>null</c>, если есть куда.
  /// </summary>
  /// <param name="option">Вариант действия.</param>
  /// <param name="context">Контекст с карточкой, которая выполняет действие.</param>
  private string? DenialOf(Effect option, GameContext context)
  {
    // Другой игрок есть всегда, а действию без целей и выбирать нечего.
    if (option.RequiresTargetPlayer || !option.RequiresTheory && !option.RequiresHandCard && !option.RequiresTargetCell)
      return null;

    if (option.RequiresSourceTheory)
    {
      var canMove = SourceTheoryTargets(option).Any(from => TheoryTargets(option).Any(to => to != from));
      return canMove
        ? null
        : $"Нет двух подходящих теорий — действие «{option.Description}» некуда применить.";
    }

    // Цели на поле зависят от карточки в руке, которой ещё нет.
    if (option.RequiresTargetCell && option.RequiresHandCard)
      return null;
    if (option.RequiresTargetCell)
    {
      if (option.AcceptsHandCardTarget && _session.CurrentPlayer.Hand.Cards.Any(card => option.CheckHandCard(card, context).IsAllowed))
        return null;
      var targetChecks = option.TargetCells(context).Select(target => target.Check).ToList();
      if (targetChecks.Count == 0)
        return $"На поле нет подходящих карточек — действие «{option.Description}» некуда применить.";
      return targetChecks.Any(check => check.IsAllowed) ? null : targetChecks[0].Reason;
    }

    if (!option.RequiresHandCard)
    {
      return TheoryTargets(option).Any() || AdeptCardTargets(option).Any()
        ? null
        : $"На поле нет подходящей теории — действие «{option.Description}» некуда применить.";
    }

    var cards = _session.CurrentPlayer.Hand.Cards;
    if (cards.Count == 0)
      return $"В руке нет карточек — действие «{option.Description}» не к чему применить.";

    // Закрытая карточка — гражданин без теории: куда её можно положить, не зависит от того, какая это карточка.
    var checks = HiddenChecks(cards[0], option).Select(target => target.Check).ToList();
    if (checks.Count == 0)
      return $"На поле нет ни одного открытого штаба теории — действие «{option.Description}» некуда применить.";
    return checks.Any(check => check.IsAllowed) ? null : checks[0].Reason;
  }

  /// <summary>
  /// Теории, куда можно положить персонажа взакрытую: открытые и закрытые штабы.
  /// </summary>
  private IEnumerable<TheoryColumn> PlaceableTheories => _board.Field.TheoryColumns;

  /// <summary>
  /// Теории, которые можно выбрать для действия.
  /// </summary>
  private IEnumerable<TheoryColumn> TheoryTargets(Effect option) =>
    _board.Field.TheoryColumns.Where(theory => option.CanTargetTheory(theory, _session.CurrentPlayer));

  /// <summary>
  /// Теории, которые можно выбрать источником для действия.
  /// </summary>
  private IEnumerable<TheoryColumn> SourceTheoryTargets(Effect option) =>
    _board.Field.TheoryColumns.Where(theory => option.CanTargetSourceTheory(theory, _session.CurrentPlayer));

  /// <summary>
  /// Свои карточки в личном штабе, на которые приводят адептов, если их можно выбрать для действия вместо теории.
  /// </summary>
  private IEnumerable<Cell> AdeptCardTargets(Effect option) =>
    _board.Field.AdeptCardsOf(_session.CurrentPlayer).Where(cell => option.CanTargetAdeptCard(cell, _board.Field));

  /// <summary>
  /// Условия, на которых карточку из руки можно выложить взакрытую сверху каждой открытой теории.
  /// </summary>
  private IEnumerable<(TheoryColumn Column, PlacementCheck Check)> HiddenChecks(Card card, Effect option) =>
    PlaceableTheories.Select(theory => (theory, _board.Field.CheckHidden(card, theory, _session.CurrentPlayer, DiscountOf(option))));

  /// <summary>
  /// Скидка на выкладывание взакрытую, которую даёт действие.
  /// </summary>
  private static int DiscountOf(Effect option) => option is PlaceHiddenCardEffect hidden ? hidden.JoiningDiscount : 0;

  private static string Lowercase(string text) => text.Length == 0 ? text : char.ToLower(text[0]) + text[1..];

  /// <summary>
  /// Подсветить теории, которые можно выбрать для действия.
  /// </summary>
  private void ShowTheoryTargets(Targeting targeting)
  {
    if (targeting.PicksSourceTheory)
    {
      _board.ShowTargets(SourceTheoryTargets(targeting.Active).SelectMany(theory => theory.Cells));
      Hint?.Invoke("Выберите теорию, откуда забрать адепта. Esc или щелчок мимо карточек — отмена.");
      return;
    }

    if (targeting.SourceTheory is { } source)
    {
      _board.ShowTargets(TheoryTargets(targeting.Active).Where(theory => theory != source).SelectMany(theory => theory.Cells));
      Hint?.Invoke($"Выберите теорию, куда перевести адепта из «{source}». Esc или щелчок мимо карточек — отмена.");
      return;
    }

    if (targeting.HandCard is not { } card)
    {
      var adeptCards = AdeptCardTargets(targeting.Active).ToList();
      _board.ShowTargets(TheoryTargets(targeting.Active).Where(theory => !PickedTheories.Contains(theory))
        .SelectMany(theory => theory.Cells).Concat(adeptCards));
      Hint?.Invoke(adeptCards.Count > 0
        ? "Выберите теорию щелчком по её карточкам или подсвеченную карточку в личном штабе. Esc или щелчок мимо карточек — отмена."
        : "Выберите теорию щелчком по её карточкам. Esc или щелчок мимо карточек — отмена.");
      return;
    }

    // Подсвечиваем места, куда ляжет карточка, — платные выделены, как при выкладывании с руки.
    _board.ShowAvailable(HiddenChecks(card, targeting.Active)
      .Where(target => target.Check.IsAllowed)
      .Select(target => (target.Column.Top.Above, target.Check)));
    Hint?.Invoke("Выберите теорию: карточка ляжет сверху её стопки рубашкой вверх. "
      + "Можно выбрать в руке другую карточку. Esc или щелчок мимо карточек — отмена.");
  }

  /// <summary>
  /// Игрок выбрал карточку в руке: теперь он выбирает теорию.
  /// </summary>
  private void OnHandCardPicked(Card card)
  {
    if (_session.PendingTheoryTrigger is not null)
    {
      try
      {
        _session.SelectTriggerHandCard(card);
      }
      catch (Exception error) when (error is ArgumentException or InvalidOperationException)
      {
        UseDenied?.Invoke(error.Message);
      }
      return;
    }

    if (_targeting is null)
      return;

    // Карточку в руке вместо карточки на поле проверяем сразу, чтобы игрок выбрал другую.
    var active = _targeting.Active;
    if (active.AcceptsHandCardTarget && !active.RequiresHandCard && active.CheckHandCard(card, ContextFor(_targeting)) is { IsAllowed: false } denied)
    {
      UseDenied?.Invoke(denied.Reason ?? "Эту карточку выбрать нельзя.");
      return;
    }

    _targeting = _targeting with { HandCard = card };
    _hand.SelectedCard = card;
    Advance();
  }

  /// <summary>
  /// Начался или закончился выбор для действия основной теории: выбирает её владелец, даже если ход другого игрока.
  /// </summary>
  private void OnPendingTheoryTriggerChanged(TheoryTrigger? trigger)
  {
    _board.HideAvailable();
    if (trigger is null)
    {
      _hand.Picking = false;
      _hand.SelectedCard = null;
      _hand.ShowHandOf(_session.CurrentPlayer);
      return;
    }

    _hand.ShowHandOf(trigger.Owner);
    ShowTheoryTrigger(trigger);
  }

  /// <summary>
  /// Подсветить то, что владелец основной теории ещё должен выбрать: карточку в руке, теорию или карточку на поле.
  /// </summary>
  private void ShowTheoryTrigger(TheoryTrigger trigger)
  {
    var effect = trigger.Effect;
    var owner = trigger.Owner;
    var name = $"«{trigger.Parent.Card.Name}» ({owner})";
    var prefix = $"Сработал эффект теории: \"{trigger.Parent.Card.Theory}\". ";
    // Карточку в руке можно выбрать, если действие требует её или принимает вместо карточки на поле.
    var handPick = (effect.RequiresHandCard || effect.AcceptsHandCardTarget) && trigger.HandCard is null;
    _hand.Picking = handPick;
    _hand.SelectedCard = trigger.HandCard;

    if (effect.RequiresHandCard && trigger.HandCard is null)
    {
      Hint?.Invoke(prefix + $"Основная теория {name}: выберите карточку в руке. Esc — пропустить действие.");
      return;
    }
    if (effect.RequiresTheory && trigger.Theory is null)
    {
      _board.ShowTargets(_board.Field.TheoryColumns.Where(theory => effect.CanTargetTheory(theory, owner)).SelectMany(theory => theory.Cells));
      Hint?.Invoke(prefix + $"Основная теория {name}: выберите теорию. Esc — пропустить действие.");
      return;
    }
    if (effect.RequiresTargetCell && trigger.Cell is null)
    {
      _board.ShowAvailable(effect.TargetCells(trigger.Context).Where(target => target.Check.IsAllowed));
      var which = handPick ? "подсвеченную карточку или карточку в руке" : "подсвеченную карточку";
      Hint?.Invoke(prefix + $"Основная теория {name}: выберите {which}. Esc — пропустить действие.");
    }
  }

  /// <summary>
  /// Начало подсказки, если эффект базовой теории сработал от дочерней карточки; иначе пусто.
  /// </summary>
  private string TheoryTriggerPrefix(Theory theory) =>
    theory.Parent is { } parent && _board.Field.TheoryCards.Any(owned => owned.Card.Theory == parent)
      ? $"Сработал эффект теории: \"{parent}\". "
      : string.Empty;

  /// <summary>
  /// Щелчок или Esc, пока владелец основной теории выбирает цель её действия.
  /// </summary>
  private void HandleTheoryTriggerInput(InputEvent @event, TheoryTrigger trigger)
  {
    if (@event.IsActionPressed("ui_cancel"))
    {
      GetViewport().SetInputAsHandled();
      _session.SkipTheoryTrigger();
      return;
    }
    if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
      return;

    GetViewport().SetInputAsHandled();
    var cell = _board.CellAt(_board.GetGlobalMousePosition())?.ToCell();
    try
    {
      if (trigger.Effect.RequiresTheory && trigger.Theory is null && cell is { } picked && TheoryAt(picked) is { } theory)
        _session.SelectTriggerTheory(theory);
      else if (trigger.Effect.RequiresTargetCell && trigger.Cell is null && cell is { } target)
        _session.SelectTriggerCell(target);
      else
        UseDenied?.Invoke("Выберите подсвеченную цель или нажмите Esc.");
    }
    catch (Exception error) when (error is ArgumentException or InvalidOperationException)
    {
      UseDenied?.Invoke(error.Message);
    }
  }

  private void HandleTargeting(InputEvent @event)
  {
    // Правая и средняя кнопки двигают камеру — выбор при этом не сбрасывается.
    if (@event.IsActionPressed("ui_cancel"))
    {
      GetViewport().SetInputAsHandled();
      CancelTargeting();
      return;
    }

    if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
      return;

    GetViewport().SetInputAsHandled();
    if (!IsOnCard(_board.CellAt(_board.GetGlobalMousePosition())))
    {
      CancelTargeting();
      return;
    }

    if (_targeting!.PicksHandCard)
    {
      UseDenied?.Invoke("Выберите карточку в руке или нажмите Esc.");
      return;
    }

    if (_targeting.PicksTargetCell)
    {
      ChooseTargetCell();
      return;
    }

    var clicked = _board.CellAt(_board.GetGlobalMousePosition())?.ToCell();
    if (clicked is { } adeptCard && AdeptCardTargets(_targeting.Active).Contains(adeptCard))
    {
      var chosen = EndTargeting();
      Run(chosen, ContextFor(chosen).WithTargetAdeptCard(adeptCard));
      return;
    }

    var theory = clicked is { } cell ? TheoryAt(cell) : null;
    if (theory is null)
    {
      UseDenied?.Invoke("Здесь нет теории. Выберите подсвеченную стопку теории или нажмите Esc.");
      return;
    }
    if (_targeting.PicksSourceTheory)
    {
      if (!_targeting.Active.CanTargetSourceTheory(theory, _session.CurrentPlayer))
      {
        UseDenied?.Invoke($"Из теории «{theory}» забрать нельзя. Выберите подсвеченную стопку теории или нажмите Esc.");
        return;
      }

      _targeting = _targeting with { SourceTheory = theory };
      ShowTheoryTargets(_targeting);
      return;
    }
    if (theory == _targeting.SourceTheory)
    {
      UseDenied?.Invoke("Выберите другую теорию или нажмите Esc.");
      return;
    }
    if (!_targeting.Active.CanTargetTheory(theory, _session.CurrentPlayer))
    {
      UseDenied?.Invoke($"Теорию «{theory}» нельзя выбрать для этого действия. Выберите подсвеченную стопку теории или нажмите Esc.");
      return;
    }

    if (_targeting.HandCard is { } card && _board.Field.CheckHidden(card, theory, _session.CurrentPlayer, DiscountOf(_targeting.Active)) is { IsAllowed: false } check)
    {
      UseDenied?.Invoke($"{check.Reason} Выберите подсвеченную теорию или нажмите Esc.");
      return;
    }

    // Действию нужно несколько теорий: запоминаем выбранные и просим следующую.
    if (PickedTheories.Contains(theory))
    {
      UseDenied?.Invoke($"Теория «{theory}» уже выбрана. Выберите другую или нажмите Esc.");
      return;
    }
    var picked = PickedTheories.Append(theory).ToList();
    if (picked.Count < _targeting.Active.TheoryCount)
    {
      _targeting = _targeting with { Theories = picked };
      ShowTheoryTargets(_targeting);
      Hint?.Invoke($"Выбрано теорий: {picked.Count} из {_targeting.Active.TheoryCount}. Выберите следующую.");
      return;
    }

    var targeting = EndTargeting();
    var context = ContextFor(targeting).WithTargetTheories(picked);
    if (targeting.HandCard is not null)
      context = context.WithHandCard(targeting.HandCard);
    Run(targeting, context);
  }

  /// <summary>
  /// Теории, уже выбранные для действия, которому нужно несколько теорий.
  /// </summary>
  private IReadOnlyList<TheoryColumn> PickedTheories => _targeting?.Theories ?? [];

  /// <summary>
  /// Контекст действия: карточка, которая его выполняет, и всё, что игрок уже выбрал.
  /// </summary>
  private GameContext ContextFor(Targeting targeting)
  {
    var context = targeting.Placement is { } placement
      ? _session.PlacementContext(placement)
      : _session.CreateContext().WithSourceCell(targeting.Cell.ToCell());
    if (targeting.SourceTheory is { } source)
      context = context.WithSourceTheory(source);
    if (targeting.BorrowedCell is { } borrowed)
      context = context.WithBorrowedCell(borrowed);
    if (targeting.TargetCell is { } target)
      context = context.WithTargetCell(target);
    if (targeting.SecondTargetCell is { } second)
      context = context.WithSecondTargetCell(second);
    if (targeting.BorrowedOption is { } option)
      context = context.WithBorrowedOption(option);
    if (targeting.CopiedCell is { } copied)
      context = context.WithCopiedCell(copied);
    if (targeting.CopiedOption is { } copiedOption)
      context = context.WithCopiedOption(copiedOption);
    return context;
  }

  /// <summary>
  /// Щелчок пришёлся по карточке или по подсвеченной ячейке (туда ляжет закрытая карточка).
  /// </summary>
  private bool IsOnCard(Vector2I? cell) =>
    cell is { } target && (_board.ViewAt(target) is not null || _board.MarketViewAt(target) is not null || _board.IsAvailable(target));

  /// <summary>
  /// Игрок щёлкнул по столу, выбирая карточку на поле для действия.
  /// </summary>
  private void ChooseTargetCell()
  {
    if (_board.CellAt(_board.GetGlobalMousePosition()) is not { } target)
    {
      UseDenied?.Invoke("Выберите подсвеченную карточку или нажмите Esc.");
      return;
    }

    var check = _targeting!.CellPicker.CheckTargetCell(target.ToCell(), ContextFor(_targeting));
    if (!check.IsAllowed)
    {
      UseDenied?.Invoke($"{check.Reason} Выберите подсвеченную карточку или нажмите Esc.");
      return;
    }

    if (_targeting.PicksBorrowedCell)
    {
      Borrow(target.ToCell());
      return;
    }
    if (_targeting.PicksCopiedCell)
    {
      BorrowCopied(target.ToCell());
      return;
    }

    _targeting = _targeting.TargetCell is null
      ? _targeting with { TargetCell = target.ToCell() }
      : _targeting with { SecondTargetCell = target.ToCell() };
    _board.HideAvailable();
    Advance();
  }

  /// <summary>
  /// Выбрана карточка, которую использует действие (чужая или своя): теперь выбирают вариант её действия.
  /// </summary>
  private void Borrow(Cell cell)
  {
    var card = _board.Field.CardAt(cell)!;
    // Копирование не копирует копирование: его выбирают через карточку, которую уже копируют.
    var copying = _targeting!.Option is CopyRivalCardEffect;
    var usable = copying ? CopyableOptions(card) : BorrowableOptions(card);
    if (usable.Count == 0)
    {
      UseDenied?.Invoke(copying
        ? $"Действие «{card.Name}» скопировать нельзя. Выберите другую карточку или нажмите Esc."
        : $"Действие «{card.Name}» использовать нельзя. Выберите другую карточку или нажмите Esc.");
      return;
    }

    _targeting = _targeting! with { BorrowedCell = cell };
    _board.HideAvailable();
    var options = card.OnPlayFromHand.Options;
    if (options.Count == 1)
    {
      ChooseBorrowed(options[0]);
      return;
    }

    // Все варианты видны: недоступные — серыми, с причиной.
    ShowOptionMenu(card, options, option => ContextFor(_targeting! with { BorrowedOption = option }), option => UnusableReason(option, copying));
  }

  /// <summary>
  /// Почему вариант действия чужой карточки нельзя выполнить через другого персонажа, или <c>null</c>, если можно.
  /// </summary>
  private static string? UnusableReason(Effect option, bool copying)
  {
    if (copying && option is CopyRivalCardEffect)
      return "Копирование не копирует копирование.";
    if (!option.CanBeBorrowed)
      return "Через другого персонажа нельзя сыграть действие, которое само использует чужую карточку.";
    return null;
  }

  /// <summary>
  /// Выбрана карточка, действие которой скопируют через чужое действие (<see cref="CopyRivalCardEffect"/>):
  /// теперь выбирают вариант её действия.
  /// </summary>
  private void BorrowCopied(Cell cell)
  {
    var card = _board.Field.CardAt(cell)!;
    if (CopyableOptions(card).Count == 0)
    {
      UseDenied?.Invoke($"Действие «{card.Name}» скопировать нельзя. Выберите другую карточку или нажмите Esc.");
      return;
    }

    _targeting = _targeting! with { CopiedCell = cell };
    _board.HideAvailable();
    var options = card.OnPlayFromHand.Options;
    if (options.Count == 1)
    {
      ChooseCopied(options[0]);
      return;
    }

    ShowOptionMenu(card, options, option => ContextFor(_targeting! with { CopiedOption = option }), option => UnusableReason(option, copying: true));
  }

  /// <summary>
  /// Меню вариантов действия карточки. Вариант, который сейчас некуда применить, виден, но недоступен.
  /// </summary>
  /// <param name="card">Карточка, чьи варианты показываем.</param>
  /// <param name="options">Варианты.</param>
  /// <param name="contextFor">Контекст, если выбран этот вариант.</param>
  /// <param name="unusable">Причина, почему вариант нельзя взять через эту карточку, или <c>null</c>.</param>
  private void ShowOptionMenu(Card card, IReadOnlyList<Effect> options, Func<Effect, GameContext> contextFor, Func<Effect, string?> unusable)
  {
    _borrowMenu.Clear();
    _borrowMenu.AddSeparator(card.Name);
    for (var i = 0; i < options.Count; i++)
    {
      _borrowMenu.AddItem(options[i].Description, i);
      if ((unusable(options[i]) ?? DenialOf(options[i], contextFor(options[i]))) is { } reason)
      {
        var index = _borrowMenu.GetItemIndex(i);
        _borrowMenu.SetItemDisabled(index, true);
        _borrowMenu.SetItemTooltip(index, reason);
      }
    }
    _borrowMenu.ResetSize();
    _borrowMenu.Popup(new Rect2I((Vector2I)GetViewport().GetMousePosition(), Vector2I.Zero));
  }

  /// <summary>
  /// Варианты действия выбранной карточки, которые можно выполнить через другую карточку.
  /// </summary>
  private static List<Effect> BorrowableOptions(Card card) =>
    card.OnPlayFromHand.Options.Where(option => option.CanBeBorrowed).ToList();

  /// <summary>
  /// Варианты действия карточки, которые можно скопировать: копирование не копирует копирование.
  /// </summary>
  private static List<Effect> CopyableOptions(Card card) =>
    BorrowableOptions(card).Where(option => option is not CopyRivalCardEffect).ToList();

  private void OnBorrowMenuIdPressed(long id)
  {
    if (_targeting is { CopiedCell: { } copied, CopiedOption: null })
      ChooseCopied(_board.Field.CardAt(copied)!.OnPlayFromHand.Options[(int)id]);
    else if (_targeting is { BorrowedCell: { } cell })
      ChooseBorrowed(_board.Field.CardAt(cell)!.OnPlayFromHand.Options[(int)id]);
  }

  /// <summary>
  /// Выбран вариант действия карточки, которую скопируют: выполнить сразу или сначала выбрать для него цели.
  /// </summary>
  private void ChooseCopied(Effect option)
  {
    _targeting = _targeting! with { CopiedOption = option };
    if (DenialOf(option, ContextFor(_targeting)) is { } reason)
    {
      EndTargeting();
      UseDenied?.Invoke(reason);
      return;
    }

    Advance();
  }

  /// <summary>
  /// Выбран вариант действия чужой карточки: выполнить сразу или сначала выбрать для него цели.
  /// </summary>
  private void ChooseBorrowed(Effect option)
  {
    _targeting = _targeting! with { BorrowedOption = option };
    if (DenialOf(option, ContextFor(_targeting)) is { } reason)
    {
      EndTargeting();
      UseDenied?.Invoke(reason);
      return;
    }

    Advance();
  }

  /// <summary>
  /// Теория, к стопке которой относится ячейка или над стопкой которой она лежит (туда кладут новую карточку).
  /// </summary>
  private TheoryColumn? TheoryAt(Cell cell) =>
    _board.Field.ColumnAt(cell) ?? _board.Field.TheoryColumns.FirstOrDefault(theory => theory.Top.Above == cell);

  /// <summary>
  /// Ход перешёл к другому игроку: начатый выбор действия прежнего игрока отменяется.
  /// </summary>
  private void OnTurnChanged(Player player)
  {
    _menu.Hide();
    _borrowMenu.Hide();
    _playerMenu.Hide();
    if (_targeting is not null)
      EndTargeting();
  }

  private Targeting EndTargeting()
  {
    var targeting = _targeting!;
    _targeting = null;
    targeting.View.Lifted = false;
    _board.HideAvailable();
    _hand.Picking = false;
    _hand.SelectedCard = null;
    return targeting;
  }

  /// <summary>
  /// Провести проверку успеха с анимацией, затем выполнить действие (если проверка успешна) и перевернуть карточку.
  /// </summary>
  private async void Use(CardView view, Vector2I cell, Effect option, GameContext context)
  {
    var check = _session.CheckSuccess(cell.ToCell());
    view.Lifted = true;
    var checkView = SuccessCheckView.Create(view, check);
    await checkView.Play();
    var action = context.BorrowedOption is { } borrowed ? $"{option.Description}: {Lowercase(borrowed.Description)}" : option.Description;
    Hint?.Invoke(check.Succeeded
      ? $"«{view.Card.Name}» (вероятность успеха {check.Chance}%): на кубике {check.Die} — успех! {action}."
      : $"«{view.Card.Name}» (вероятность успеха {check.Chance}%): на кубике {check.Die} — провал, действие не выполняется.");
    await ToSignal(GetTree().CreateTimer(ResultPause), SceneTreeTimer.SignalName.Timeout);

    checkView.QueueFree();
    view.Lifted = false;
    try
    {
      _session.UseCard(option, context);
    }
    // TODO: убрать, когда у всех действий будет реализовано выполнение.
    catch (NotImplementedException)
    {
      UseDenied?.Invoke($"Действие «{option.Description}» пока не реализовано.");
      return;
    }

    // После быстрого действия карточка поворачивается, иначе переворачивается; действие могло перевернуть
    // и чужие карточки (например, инспекция).
    _board.ShowPlayStates();
  }
}
