using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Game.Cards;
using Godot;

namespace ConspiracyLeague.Game.Table;

/// <summary>
/// Использование карточек из личного штаба (своей рабочей зоны) щелчком мыши.
/// </summary>
/// <remarks>
/// Если у действия карточки один вариант, он выполняется сразу, если несколько — сначала появляется меню выбора.
/// Если действию нужна теория (например, «Добавить адепта в теорию»), игрок выбирает её щелчком по стопке теории;
/// Esc или правая кнопка мыши отменяют выбор. Затем проводится проверка успеха (<see cref="SuccessCheck"/>):
/// слева от карточки наполняется шкала, на карточке появляется галка или крест, и через секунду карточка
/// переворачивается рубашкой вверх. Действие выполняется, только если проверка успешна.
/// Что можно использовать, решает <see cref="PlayingField"/>.
/// </remarks>
public partial class CardUseController : Node
{
  private const double ResultPause = 1.0;

  private Board _board = null!;
  private GameSession _session = null!;
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
  /// Действие карточки, для которого игрок выбирает теорию.
  /// </summary>
  /// <param name="View">Карточка.</param>
  /// <param name="Cell">Ячейка карточки.</param>
  /// <param name="Option">Выбранный вариант действия.</param>
  private sealed record Targeting(CardView View, Vector2I Cell, Effect Option);

  /// <summary>
  /// Настроить контроллер.
  /// </summary>
  /// <param name="board">Стол.</param>
  /// <param name="session">Сессия игры: карточки использует игрок, который сейчас ходит.</param>
  public void Initialize(Board board, GameSession session)
  {
    _board = board;
    _session = session;
    _session.Turns.TurnChanged += OnTurnChanged;
  }

  public override void _Ready()
  {
    _menu = new PopupMenu { Name = "ActionMenu" };
    _menu.IdPressed += OnMenuIdPressed;
    // Откладываем: меню закрывается и после выбора пункта, выбор должен успеть обработаться.
    _menu.PopupHide += () => Callable.From(() => CloseSelection()).CallDeferred();
    AddChild(_menu);
  }

  public override void _ExitTree() => _session.Turns.TurnChanged -= OnTurnChanged;

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
  /// Вариант действия выбран: выполнить его сразу или сначала выбрать теорию.
  /// </summary>
  private void Choose(CardView view, Vector2I cell, Effect option)
  {
    if (!option.RequiresTheory)
    {
      view.Lifted = false;
      Use(view, cell, option, _session.CreateContext());
      return;
    }

    var theories = _board.Field.TheoryColumns.Where(theory => !theory.IsClosed).ToList();
    if (theories.Count == 0)
    {
      view.Lifted = false;
      UseDenied?.Invoke($"На поле нет ни одной действующей теории — действие «{option.Description}» некуда применить.");
      return;
    }

    _targeting = new Targeting(view, cell, option);
    view.Lifted = true;
    _board.ShowTargets(theories.SelectMany(theory => theory.Cells));
    Hint?.Invoke("Выберите теорию щелчком по её карточкам. Esc — отмена.");
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
    var theory = _board.CellAt(_board.GetGlobalMousePosition()) is { } cell ? _board.Field.ColumnAt(cell.ToCell()) : null;
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

    var targeting = EndTargeting();
    Use(targeting.View, targeting.Cell, targeting.Option, _session.CreateContext().WithTargetTheory(theory));
  }

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
      ? $"«{view.Card.Name}»: выпало {check.Roll}, нужно {check.Chance} — успех! {option.Description}."
      : $"«{view.Card.Name}»: выпало {check.Roll}, нужно {check.Chance} — провал, действие не выполняется.");
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
  }
}
