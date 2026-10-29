using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
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
/// Если действию нужна карточка в руке (например, «Выложить карту взакрытую»), игрок сначала выбирает её щелчком
/// по руке, а затем стопку теории, сверху которой её положить.
/// Esc или правая кнопка мыши отменяют выбор. Затем проводится проверка успеха (<see cref="SuccessCheck"/>):
/// на карточку бросается двадцатигранный кубик, на нём появляется галка или крест, и через секунду карточка
/// переворачивается рубашкой вверх. Действие выполняется, только если проверка успешна.
/// Что можно использовать, решает <see cref="PlayingField"/>.
/// </remarks>
public partial class CardUseController : Node
{
  private const double ResultPause = 1.0;

  private Board _board = null!;
  private GameSession _session = null!;
  private HandView _hand = null!;
  private PopupMenu _menu = null!;
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
  private sealed record Targeting(CardView View, Vector2I Cell, Effect Option, Card? HandCard = null)
  {
    /// <summary>
    /// Игрок ещё выбирает карточку в руке.
    /// </summary>
    public bool PicksHandCard => Option.RequiresHandCard && HandCard is null;
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
    _hand.CardPicked += OnHandCardPicked;
  }

  public override void _Ready()
  {
    _menu = new PopupMenu { Name = "ActionMenu" };
    _menu.IdPressed += OnMenuIdPressed;
    // Откладываем: меню закрывается и после выбора пункта, выбор должен успеть обработаться.
    _menu.PopupHide += () => Callable.From(() => CloseSelection()).CallDeferred();
    AddChild(_menu);
  }

  public override void _ExitTree()
  {
    _session.Turns.TurnChanged -= OnTurnChanged;
    _hand.CardPicked -= OnHandCardPicked;
  }

  public override void _UnhandledInput(InputEvent @event)
  {
    if (_targeting is not null)
    {
      HandleTargeting(@event);
      return;
    }

    if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click || _menu.Visible)
      return;
    if (_session.Phase != GamePhase.PlayerTurns || _session.PendingCheck is not null)
      return;
    if (_board.CellAt(_board.GetGlobalMousePosition()) is not { } cell || !_board.Field.CanUse(cell.ToCell(), _session.CurrentPlayer))
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
  private void Choose(CardView view, Vector2I cell, Effect option)
  {
    if (!option.RequiresTheory && !option.RequiresHandCard && !option.RequiresRivalCard)
    {
      view.Lifted = false;
      Use(view, cell, option, _session.CreateContext());
      return;
    }

    if (DenialOf(option) is { } reason)
    {
      view.Lifted = false;
      UseDenied?.Invoke(reason);
      return;
    }

    _targeting = new Targeting(view, cell, option);
    view.Lifted = true;
    if (_targeting.PicksHandCard)
    {
      _hand.Picking = true;
      Hint?.Invoke("Выберите карточку в руке. Esc — отмена.");
    }
    else if (option.RequiresRivalCard)
    {
      // Подсвечиваем карточки, которые можно перевернуть, — с ценой, как при выкладывании с руки.
      _board.ShowAvailable(RivalChecks().Where(target => target.Check.IsAllowed));
      Hint?.Invoke("Выберите карточку в личном штабе другого игрока. Esc — отмена.");
    }
    else
      ShowTheoryTargets(_targeting);
  }

  /// <summary>
  /// Почему действие сейчас некуда применить, или <c>null</c>, если есть куда.
  /// </summary>
  private string? DenialOf(Effect option)
  {
    if (option.RequiresRivalCard)
    {
      var rivalChecks = RivalChecks().Select(target => target.Check).ToList();
      if (rivalChecks.Count == 0)
        return $"У других игроков нет несыгранных карточек — действие «{option.Description}» некуда применить.";
      return rivalChecks.Any(check => check.IsAllowed) ? null : rivalChecks[0].Reason;
    }

    if (!option.RequiresHandCard)
    {
      return OpenTheories.Any()
        ? null
        : $"На поле нет ни одной действующей теории — действие «{option.Description}» некуда применить.";
    }

    var cards = _session.CurrentPlayer.Hand.Cards;
    if (cards.Count == 0)
      return $"В руке нет карточек — действие «{option.Description}» не к чему применить.";

    // Закрытая карточка — гражданин без теории: куда её можно положить, не зависит от того, какая это карточка.
    var checks = HiddenChecks(cards[0]).Select(target => target.Check).ToList();
    if (checks.Count == 0)
      return $"На поле нет ни одного открытого штаба теории — действие «{option.Description}» некуда применить.";
    return checks.Any(check => check.IsAllowed) ? null : checks[0].Reason;
  }

  private IEnumerable<TheoryColumn> OpenTheories => _board.Field.TheoryColumns.Where(theory => !theory.IsClosed);

  /// <summary>
  /// Условия, на которых карточку из руки можно выложить взакрытую сверху каждой открытой теории.
  /// </summary>
  private IEnumerable<(TheoryColumn Column, PlacementCheck Check)> HiddenChecks(Card card) =>
    OpenTheories.Select(theory => (theory, _board.Field.CheckHidden(card, theory, _session.CurrentPlayer)));

  /// <summary>
  /// Условия, на которых можно перевернуть каждую несыгранную карточку в личных штабах других игроков.
  /// </summary>
  private IEnumerable<(Cell Cell, PlacementCheck Check)> RivalChecks()
  {
    var player = _session.CurrentPlayer;
    return _board.Field.PlayerZones
      .Where(zone => zone.Key != player)
      .SelectMany(zone => zone.Value.Cells)
      .Where(cell => _board.Field.CardAt(cell) is not null && !_board.Field.IsUsed(cell))
      .Select(cell => (cell, _board.Field.CheckFlipRival(cell, player)));
  }

  /// <summary>
  /// Подсветить теории, которые можно выбрать для действия.
  /// </summary>
  private void ShowTheoryTargets(Targeting targeting)
  {
    if (targeting.HandCard is not { } card)
    {
      _board.ShowTargets(OpenTheories.SelectMany(theory => theory.Cells));
      Hint?.Invoke("Выберите теорию щелчком по её карточкам. Esc — отмена.");
      return;
    }

    // Подсвечиваем места, куда ляжет карточка, — платные выделены, как при выкладывании с руки.
    _board.ShowAvailable(HiddenChecks(card)
      .Where(target => target.Check.IsAllowed)
      .Select(target => (target.Column.Top.Above, target.Check)));
    Hint?.Invoke("Выберите теорию: карточка ляжет сверху её стопки рубашкой вверх. "
      + "Можно выбрать в руке другую карточку. Esc — отмена.");
  }

  /// <summary>
  /// Игрок выбрал карточку в руке: теперь он выбирает теорию.
  /// </summary>
  private void OnHandCardPicked(Card card)
  {
    if (_targeting is null)
      return;

    _targeting = _targeting with { HandCard = card };
    _hand.SelectedCard = card;
    ShowTheoryTargets(_targeting);
  }

  private void HandleTargeting(InputEvent @event)
  {
    var cancel = @event.IsActionPressed("ui_cancel")
      || @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right };
    if (cancel)
    {
      GetViewport().SetInputAsHandled();
      EndTargeting();
      return;
    }

    if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
      return;

    GetViewport().SetInputAsHandled();
    if (_targeting!.PicksHandCard)
    {
      UseDenied?.Invoke("Выберите карточку в руке или нажмите Esc.");
      return;
    }

    if (_targeting.Option.RequiresRivalCard)
    {
      ChooseRivalCard();
      return;
    }

    var theory = _board.CellAt(_board.GetGlobalMousePosition()) is { } cell ? TheoryAt(cell.ToCell()) : null;
    if (theory is null)
    {
      UseDenied?.Invoke("Здесь нет теории. Выберите подсвеченную стопку теории или нажмите Esc.");
      return;
    }
    if (theory.IsClosed)
    {
      UseDenied?.Invoke($"Штаб теории «{theory}» закрыт. Выберите подсвеченную стопку теории или нажмите Esc.");
      return;
    }

    if (_targeting.HandCard is { } card && _board.Field.CheckHidden(card, theory, _session.CurrentPlayer) is { IsAllowed: false } check)
    {
      UseDenied?.Invoke($"{check.Reason} Выберите подсвеченную теорию или нажмите Esc.");
      return;
    }

    var targeting = EndTargeting();
    var context = _session.CreateContext().WithTargetTheory(theory);
    if (targeting.HandCard is not null)
      context = context.WithHandCard(targeting.HandCard);
    Use(targeting.View, targeting.Cell, targeting.Option, context);
  }

  /// <summary>
  /// Игрок щёлкнул по столу, выбирая карточку другого игрока, которую перевернуть.
  /// </summary>
  private void ChooseRivalCard()
  {
    if (_board.CellAt(_board.GetGlobalMousePosition()) is not { } target)
    {
      UseDenied?.Invoke("Выберите подсвеченную карточку другого игрока или нажмите Esc.");
      return;
    }

    var check = _board.Field.CheckFlipRival(target.ToCell(), _session.CurrentPlayer);
    if (!check.IsAllowed)
    {
      UseDenied?.Invoke($"{check.Reason} Выберите подсвеченную карточку или нажмите Esc.");
      return;
    }

    var targeting = EndTargeting();
    Use(targeting.View, targeting.Cell, targeting.Option, _session.CreateContext().WithTargetCell(target.ToCell()));
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
    Hint?.Invoke(check.Succeeded
      ? $"«{view.Card.Name}» (вероятность успеха {check.Chance}%): на кубике {check.Die} — успех! {option.Description}."
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

    view.FlipFaceDown();
    if (check.Succeeded && context.HandCard is not null)
      ShowHiddenCard(view, context);
    // Перевёрнутая карточка другого игрока лежит рубашкой вверх, как сыгранная.
    if (check.Succeeded && context.TargetCell is { } flipped)
      _board.ViewAt(flipped.ToVector2I())?.FlipFaceDown();
  }

  /// <summary>
  /// Карточка выложена взакрытую: она выезжает из сыгранной карточки на верх стопки теории
  /// и отмечается миплом владельца.
  /// </summary>
  private void ShowHiddenCard(CardView source, GameContext context)
  {
    var top = context.TargetTheory!.Top;
    var hidden = CardView.Create(_board.Field.CardAt(top)!);
    hidden.Position = source.Position;
    _board.Place(hidden, top.ToVector2I());
    hidden.CardOwner = context.CurrentPlayer;
  }
}
