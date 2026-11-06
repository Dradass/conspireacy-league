using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Cards.Professionals;
using ConspiracyLeague.Core.Decks;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Theories;
using ConspiracyLeague.Game.Cards;
using Godot;

namespace ConspiracyLeague.Game.Table;

/// <summary>
/// Режим разработчика: панель под рынком со всеми видами персонажей и их количеством в игре.
/// Нажатие на вид открывает меню теорий, в которых он есть; затем выбранный персонаж ставится в пустое место
/// любого личного штаба.
/// </summary>
public partial class DevCardPalette : Control
{
  /// <summary>
  /// Отступ панели от нижнего края стола.
  /// </summary>
  public const float Gap = 24f;

  /// <summary>
  /// Высота панели.
  /// </summary>
  public const float Height = 300f;

  private const int Columns = 6;
  private const int TitleFontSize = 26;
  private const int ButtonFontSize = 22;
  private const float ButtonHeight = 50f;
  private static readonly Color TitleColor = new("e0b84f");

  private readonly PopupMenu _theoryMenu = new() { Name = "TheoryMenu" };
  private IReadOnlyList<Theory?> _menuTheories = [];
  private PersonKind _menuKind;
  private Profession? _menuProfession;
  private PersonCard? _pending;
  private Label _hint = null!;
  private Button _handButton = null!;

  /// <summary>
  /// Стол, на котором стоит панель.
  /// </summary>
  public Board Board { get; init; } = null!;

  public override void _Ready()
  {
    var zone = Board.Market.Zone;
    Position = new Vector2(zone.Left * Board.CellSize.X, Board.Size.Y + Gap);
    Size = new Vector2(zone.Width * Board.CellSize.X, Height);
    MouseFilter = MouseFilterEnum.Ignore;

    var column = new VBoxContainer();
    column.SetAnchorsPreset(LayoutPreset.FullRect);
    column.AddThemeConstantOverride("separation", 10);
    AddChild(column);

    var title = new Label { Text = "Режим разработчика: выберите персонажа, затем теорию и место в личном штабе" };
    title.AddThemeFontSizeOverride("font_size", TitleFontSize);
    title.AddThemeColorOverride("font_color", TitleColor);
    column.AddChild(title);

    var grid = new GridContainer { Columns = Columns };
    column.AddChild(grid);
    foreach (var (kind, profession) in Items())
      grid.AddChild(CreateButton(kind, profession));

    _hint = new Label { Visible = false };
    _hint.AddThemeFontSizeOverride("font_size", TitleFontSize);
    column.AddChild(_hint);

    _handButton = new Button { Text = "В руку", Visible = false, FocusMode = FocusModeEnum.None };
    _handButton.Pressed += GiveToHand;
    column.AddChild(_handButton);

    _theoryMenu.IdPressed += OnTheoryChosen;
    AddChild(_theoryMenu);
  }

  public override void _UnhandledInput(InputEvent @event)
  {
    if (_pending is null)
      return;

    if (@event.IsActionPressed("ui_cancel"))
    {
      Finish();
      GetViewport().SetInputAsHandled();
      return;
    }

    if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }
      && Board.CellAt(Board.GetGlobalMousePosition()) is { } cell && Board.IsAvailable(cell))
    {
      var player = Board.Session.CurrentPlayer;
      var check = Board.Field.PlaceForDeveloper(cell.ToCell(), _pending, player);
      // Карточка в общей зоне могла сдвинуться вместе со столбцом теории — берём её актуальную ячейку.
      var view = CardView.Create(_pending);
      if (Board.Field.CellOf(_pending) is { } placed)
      {
        view.IsHeadquarters = check.CreatesHeadquarters;
        if (Board.Field.CommonZone.Contains(placed))
          view.CardOwner = player;
        Board.Place(view, placed.ToVector2I());
      }
      Finish();
      GetViewport().SetInputAsHandled();
    }
  }

  private void GiveToHand()
  {
    if (_pending is null)
      return;

    Board.Session.AddToHandForDeveloper(Board.Session.CurrentPlayer, _pending);
    Finish();
  }

  /// <summary>
  /// Все виды персонажей: бездомный, гражданин и каждая профессия.
  /// </summary>
  private static List<(PersonKind Kind, Profession? Profession)> Items()
  {
    var items = new List<(PersonKind Kind, Profession? Profession)>
    {
      (PersonKind.Homeless, null),
      (PersonKind.Citizen, null),
    };
    items.AddRange(Enum.GetValues<Profession>().Select(profession => (PersonKind.Professional, (Profession?)profession)));
    return items;
  }

  private Button CreateButton(PersonKind kind, Profession? profession)
  {
    var name = GameCardSet.Create(kind, null, profession).Name;
    var button = new Button
    {
      Text = $"{name}: {GameCardSet.CountOf(kind, profession)}",
      CustomMinimumSize = new Vector2(0, ButtonHeight),
      SizeFlagsHorizontal = SizeFlags.ExpandFill,
      FocusMode = FocusModeEnum.None,
    };
    button.AddThemeFontSizeOverride("font_size", ButtonFontSize);
    button.Pressed += () => OpenTheoryMenu(kind, profession);
    return button;
  }

  private void OpenTheoryMenu(PersonKind kind, Profession? profession)
  {
    _menuKind = kind;
    _menuProfession = profession;
    _menuTheories = GameCardSet.TheoriesOf(kind, profession);

    _theoryMenu.Clear();
    for (var index = 0; index < _menuTheories.Count; index++)
      _theoryMenu.AddItem(_menuTheories[index]?.Name ?? "Без теории", index);
    _theoryMenu.Popup(new Rect2I((Vector2I)GetViewport().GetMousePosition(), Vector2I.Zero));
  }

  private void OnTheoryChosen(long id)
  {
    var card = GameCardSet.Create(_menuKind, _menuTheories[(int)id], _menuProfession);
    _pending = card;
    // Личный штаб любого игрока — без проверок; общая зона — только там, где карточку можно выложить по правилам.
    var cells = Board.Field.EmptyPersonalCells()
      .Select(cell => (cell, PlacementCheck.Allowed))
      .Concat(Board.Field.CommonZone.Cells
        .Select(cell => (cell, check: Board.Field.Check(card, cell, Board.Session.CurrentPlayer)))
        .Where(item => item.check.IsAllowed)
        .Select(item => (item.cell, item.check)));
    Board.ShowAvailable(cells);
    _hint.Text = $"Выберите место для «{card.Name}»: личный штаб или общая зона. Или «В руку». Esc — отмена.";
    _hint.Visible = true;
    _handButton.Visible = true;
  }

  private void Finish()
  {
    _pending = null;
    Board.HideAvailable();
    _hint.Visible = false;
    _handButton.Visible = false;
  }
}
