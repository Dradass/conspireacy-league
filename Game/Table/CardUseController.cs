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
/// Использованная карточка переворачивается рубашкой вверх. Что можно использовать, решает <see cref="PlayingField"/>.
/// </remarks>
public partial class CardUseController : Node
{
  private Board _board = null!;
  private GameContext _context = null!;
  private PopupMenu _menu = null!;
  private Selection? _selection;

  /// <summary>
  /// Карточку не удалось использовать; аргумент — причина.
  /// </summary>
  public event Action<string>? UseDenied;

  /// <summary>
  /// Карточка, для которой открыто меню выбора действия.
  /// </summary>
  /// <param name="View">Карточка.</param>
  /// <param name="Cell">Ячейка карточки.</param>
  private sealed record Selection(CardView View, Vector2I Cell);

  /// <summary>
  /// Настроить контроллер.
  /// </summary>
  /// <param name="board">Стол.</param>
  /// <param name="context">Контекст партии: в нём выполняются действия карточек.</param>
  public void Initialize(Board board, GameContext context)
  {
    _board = board;
    _context = context;
  }

  public override void _Ready()
  {
    _menu = new PopupMenu { Name = "ActionMenu" };
    _menu.IdPressed += OnMenuIdPressed;
    // Откладываем: меню закрывается и после выбора пункта, выбор должен успеть обработаться.
    _menu.PopupHide += () => Callable.From(CloseSelection).CallDeferred();
    AddChild(_menu);
  }

  public override void _UnhandledInput(InputEvent @event)
  {
    if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click || _menu.Visible)
      return;
    if (_board.CellAt(_board.GetGlobalMousePosition()) is not { } cell || !_board.Field.CanUse(cell.ToCell(), _context.CurrentPlayer))
      return;
    if (_board.ViewAt(cell) is not { } view)
      return;

    GetViewport().SetInputAsHandled();
    var options = view.Card.OnPlayFromHand.Options;
    if (options.Count == 1)
    {
      Use(view, cell, options[0]);
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
    if (CloseSelection() is { } selection)
      Use(selection.View, selection.Cell, selection.View.Card.OnPlayFromHand.Options[(int)id]);
  }

  private Selection? CloseSelection()
  {
    var selection = _selection;
    _selection = null;
    if (selection is not null)
      selection.View.Lifted = false;
    return selection;
  }

  private void Use(CardView view, Vector2I cell, Effect option)
  {
    try
    {
      _board.Field.Use(cell.ToCell(), option, _context);
    }
    // TODO: убрать, когда будут реализованы все действия (сейчас — добавление адептов).
    catch (NotImplementedException)
    {
      UseDenied?.Invoke($"Действие «{option.Description}» пока не реализовано.");
      return;
    }

    view.FlipFaceDown();
  }
}
