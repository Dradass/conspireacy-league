using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Game.Cards;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Окно личного штаба: уменьшенная копия зоны игрока на столе — того, кто сейчас ходит (окно «Личный штаб»),
/// или выбранного другого игрока (окно «Штаб другого игрока», <see cref="FixedPlayer"/>).
/// Сохраняет расположение карточек (соседство важно для пассивных эффектов) и показывает, какие из них уже сыграны:
/// после быстрого действия карточка повёрнута, после обычного — лежит рубашкой вверх.
/// В свой штаб можно перетащить карточку из руки — она ляжет в соответствующую ячейку штаба на столе.
/// Двойной щелчок переводит камеру на штаб на столе, где с карточками можно взаимодействовать.
/// </summary>
public partial class PersonalHqView : PanelContainer
{
  private const float CardScale = 0.385f;
  private const float Gap = 4f;
  private const int TitleFontSize = 14;

  private static readonly Vector2 CellSize = CardPainter.CardSize * CardScale;
  private static readonly Color EmptyCellColor = UiStyle.Glow with { A = 0.18f };
  private static readonly Color AvailableColor = UiStyle.Glow with { A = 0.6f };
  private static readonly Color HoverAllowedColor = new(0.55f, 0.95f, 0.6f, 0.45f);
  private static readonly Color HoverDeniedColor = new(0.95f, 0.35f, 0.35f, 0.45f);

  private Label _title = null!;
  private Control _grid = null!;
  private Player? _player;
  private int _signature;

  /// <summary>
  /// Сессия игры.
  /// </summary>
  public GameSession Session { get; init; } = null!;

  /// <summary>
  /// Общее состояние окон штаба: подсветка ячеек при перетаскивании и переход к штабу на столе.
  /// </summary>
  public PersonalHqHub Hub { get; init; } = null!;

  /// <summary>
  /// Чей штаб показывать; <c>null</c> — штаб игрока, который сейчас ходит (или выбирает действие основной теории).
  /// </summary>
  public Player? FixedPlayer { get; init; }

  /// <summary>
  /// Для кого подсказки теорий над карточками (<see cref="TheoryMarkers"/>).
  /// </summary>
  public Func<Player> Viewer { get; init; } = null!;

  /// <summary>
  /// Можно ли положить сюда карточку из руки: только в свой штаб, а не в окно чужого штаба.
  /// </summary>
  public bool AcceptsDrops => FixedPlayer is null;

  /// <summary>
  /// Игрок, чей штаб показан.
  /// </summary>
  public Player? Player => _player;

  private PlayingField Field => Session.Field;

  /// <summary>
  /// Ячейка штаба на поле под точкой экрана или <c>null</c>, если точка не над ячейкой штаба.
  /// </summary>
  public Cell? CellAtScreenPoint(Vector2 point)
  {
    if (_player is null || !Field.PlayerZones.TryGetValue(_player, out var zone))
      return null;

    // Окно можно растянуть, и содержимое масштабируется — переводим точку в координаты сетки с учётом масштаба.
    var local = _grid.GetGlobalTransform().AffineInverse() * point;
    var pitch = CellSize + new Vector2(Gap, Gap);
    if (local.X < 0 || local.Y < 0)
      return null;
    var column = (int)(local.X / pitch.X);
    var row = (int)(local.Y / pitch.Y);
    if (column >= zone.Width || row >= zone.Height)
      return null;
    return new Cell(zone.Left + column, zone.Top + row);
  }

  /// <summary>
  /// Находится ли точка экрана над окном штаба. Скрытое окно (раскладка другого игрока) не считается.
  /// </summary>
  public bool ContainsScreenPoint(Vector2 point) =>
    IsVisibleInTree() && (HudWindow.Of(this) ?? (Control)this).GetGlobalRect().HasPoint(point);

  public override void _GuiInput(InputEvent @event)
  {
    if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left, DoubleClick: true } && _player is not null)
    {
      AcceptEvent();
      Hub.RaiseFocus(_player);
    }
  }

  public override void _Ready()
  {
    // Ловим щелчки (двойной — к штабу на столе), чтобы они не уходили на стол под панелью.
    MouseFilter = MouseFilterEnum.Stop;
    MouseDefaultCursorShape = CursorShape.PointingHand;
    TooltipText = AcceptsDrops
      ? "Двойной щелчок — перейти к личному штабу на столе. Сюда можно перетащить карточку из руки."
      : "Двойной щелчок — перейти к штабу этого игрока на столе.";
    // Рамку рисует окно (HudWindow), у самого штаба — только отступ.
    AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

    var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
    column.AddThemeConstantOverride("separation", 4);
    AddChild(column);

    _title = new Label { MouseFilter = MouseFilterEnum.Ignore };
    _title.AddThemeFontSizeOverride("font_size", TitleFontSize);
    column.AddChild(_title);

    _grid = new Control { Name = "Grid", MouseFilter = MouseFilterEnum.Ignore };
    _grid.Draw += DrawGrid;
    column.AddChild(_grid);

    Hub.Register(this);
    Hub.Changed += _grid.QueueRedraw;
    // Размер сетки нужен сразу: по нему окно подбирает свой начальный размер.
    UpdatePlayer();
  }

  public override void _ExitTree()
  {
    Hub.Unregister(this);
    Hub.Changed -= _grid.QueueRedraw;
  }

  /// <summary>
  /// Карточки штаба меняются от множества событий поля; дешевле сверять состояние зоны (28 ячеек) каждый кадр
  /// и перерисовывать только при изменении.
  /// </summary>
  public override void _Process(double delta)
  {
    UpdatePlayer();
    var signature = Signature();
    if (signature == _signature)
      return;
    _signature = signature;
    _grid.QueueRedraw();
  }

  private void UpdatePlayer()
  {
    var player = FixedPlayer ?? Session.PendingTheoryTrigger?.Owner ?? Session.CurrentPlayer;
    if (player == _player)
      return;

    _player = player;
    _title.Text = FixedPlayer is null ? $"Личный штаб: {player.Name}" : $"Штаб игрока {player.Name}";
    _title.AddThemeColorOverride("font_color", PlayerPalette.MainOf(player.Color));
    if (Field.PlayerZones.TryGetValue(player, out var zone))
      _grid.CustomMinimumSize = new Vector2(
        zone.Width * CellSize.X + (zone.Width - 1) * Gap,
        zone.Height * CellSize.Y + (zone.Height - 1) * Gap);
  }

  private int Signature()
  {
    var hash = new HashCode();
    hash.Add(_player);
    if (_player is null || !Field.PlayerZones.TryGetValue(_player, out var zone))
      return hash.ToHashCode();

    foreach (var cell in zone.Cells)
    {
      var card = Field.CardAt(cell);
      hash.Add(card);
      if (card is null)
        continue;
      hash.Add(Field.IsUsed(cell));
      hash.Add(Field.IsRotated(cell));
      hash.Add(card is HiddenCard { IsRevealed: true });
      foreach (var marker in TheoryMarkers.For(Session, Viewer(), card))
        hash.Add(marker);
    }

    return hash.ToHashCode();
  }

  private void DrawGrid()
  {
    if (_player is null || !Field.PlayerZones.TryGetValue(_player, out var zone))
      return;

    // Подсветка ячеек при перетаскивании относится к штабу того, кто ходит, — в чужом штабе её нет.
    var available = AcceptsDrops ? Hub.Available : new HashSet<Cell>();
    foreach (var cell in zone.Cells)
    {
      var position = new Vector2(
        (cell.Column - zone.Left) * (CellSize.X + Gap),
        (cell.Row - zone.Top) * (CellSize.Y + Gap));
      var rect = new Rect2(position, CellSize);
      if (Field.CardAt(cell) is { } card)
        DrawCard(rect, cell, card);
      else
        _grid.DrawRect(rect, available.Contains(cell) ? AvailableColor : EmptyCellColor, filled: false,
          width: available.Contains(cell) ? 2 : 1);

      if (Hub.Hover is { } hover && hover.Cell == cell)
        _grid.DrawRect(rect, hover.Allowed ? HoverAllowedColor : HoverDeniedColor);
    }
  }

  /// <summary>
  /// Нарисовать карточку в ячейке. Повёрнутая карточка (после быстрого действия) лежит боком и уменьшена,
  /// чтобы не вылезать за ячейку.
  /// </summary>
  private void DrawCard(Rect2 rect, Cell cell, Card card)
  {
    var rotated = Field.IsRotated(cell);
    var size = rotated ? CellSize * (CellSize.X / CellSize.Y) : CellSize;
    _grid.DrawSetTransform(rect.GetCenter(), rotated ? Mathf.Pi / 2 : 0, Vector2.One);
    var local = new Rect2(-size / 2, size);

    if (Field.IsUsed(cell))
      CardPainter.DrawPlainBack(_grid, local);
    else if (card is HiddenCard { IsRevealed: false } hidden)
      CardPainter.DrawHiddenBack(_grid, local, hidden);
    else
    {
      CardPainter.DrawFace(_grid, local, card is HiddenCard shown ? shown.Face : card);
      TheoryMarkers.DrawOnTopEdge(_grid, local, TheoryMarkers.For(Session, Viewer(), card));
    }

    _grid.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
  }
}
