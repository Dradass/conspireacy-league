using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Game.Table;
using ConspiracyLeague.Game.Ui;
using Godot;

namespace ConspiracyLeague.Game;

/// <summary>
/// Главная сцена: стол, камера, колоды в левом нижнем углу и перетаскивание карточек.
/// </summary>
public partial class Main : Node2D
{
  private const int CardsPerDeck = 10;
  private const float DecksMargin = 24f;
  private const float StartZoom = 0.75f;

  public override void _Ready()
  {
    var board = new Board { Name = "Board" };
    AddChild(board);

    AddChild(new TableCamera
    {
      Name = "Camera",
      Bounds = new Rect2(Vector2.Zero, Board.Size),
      Position = Board.Size / 2,
      Zoom = Vector2.One * StartZoom,
    });

    var decks = new[]
    {
      DeckView.Create("Бездомные", CreateCards<HomelessCard>()),
      DeckView.Create("Граждане", CreateCards<CitizenCard>()),
    };
    AddChild(CreateHud(decks));

    // Слой выше интерфейса, но в координатах стола: карточка летит поверх колод и масштабируется вместе со столом.
    var dragLayer = new CanvasLayer { Name = "DragLayer", Layer = 2, FollowViewportEnabled = true };
    AddChild(dragLayer);

    var dragController = new CardDragController { Name = "CardDragController" };
    AddChild(dragController);
    dragController.Initialize(board, dragLayer, decks);
  }

  private static CanvasLayer CreateHud(IEnumerable<DeckView> decks)
  {
    var hud = new CanvasLayer { Name = "Hud", Layer = 1 };

    var root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Ignore };
    root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    hud.AddChild(root);

    var row = new HBoxContainer
    {
      Name = "Decks",
      MouseFilter = Control.MouseFilterEnum.Ignore,
      AnchorTop = 1,
      AnchorBottom = 1,
      OffsetLeft = DecksMargin,
      OffsetTop = -DecksMargin,
      OffsetBottom = -DecksMargin,
      GrowVertical = Control.GrowDirection.Begin,
    };
    row.AddThemeConstantOverride("separation", (int)DecksMargin);
    foreach (var deck in decks)
      row.AddChild(deck);

    root.AddChild(row);
    return hud;
  }

  private static List<Card> CreateCards<T>()
    where T : Card, new()
    => Enumerable.Range(0, CardsPerDeck).Select(_ => (Card)new T()).ToList();
}
