using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Theories;
using ConspiracyLeague.Game.Table;
using ConspiracyLeague.Game.Ui;
using Godot;

namespace ConspiracyLeague.Game;

/// <summary>
/// Главная сцена: стол с зонами, камера, баланс и колоды игрока, перетаскивание карточек.
/// </summary>
public partial class Main : Node2D
{
  private const int CardsPerDeck = 10;
  private const int StartingMoney = 10;
  private const float StartZoom = 0.6f;
  private const float StartCameraShift = 80f;

  public override void _Ready()
  {
    var player = new Player("Игрок", StartingMoney);
    var field = new PlayingField(FieldLayout.Columns, FieldLayout.Rows, FieldLayout.CommonZone);
    field.AddPlayer(player, FieldLayout.PlayerZone);

    var board = new Board { Name = "Board", Field = field, LocalPlayer = player };
    AddChild(board);

    AddChild(new TableCamera
    {
      Name = "Camera",
      Bounds = new Rect2(Vector2.Zero, board.Size),
      // Чуть ниже центра зоны игрока, чтобы её не закрывали колоды.
      Position = Board.ZoneCenter(FieldLayout.PlayerZone) + new Vector2(0, StartCameraShift),
      Zoom = Vector2.One * StartZoom,
    });

    // TODO: колоды с теориями — тестовые, пока в игре нет настоящих карточек персон с теориями.
    DeckView[] decks =
    [
      DeckView.Create("Бездомные", CreateCards(() => new HomelessCard())),
      DeckView.Create("Граждане", CreateCards(() => new CitizenCard())),
      DeckView.Create(TheoryCatalog.Reptilians.Name, CreateCards(() => new CitizenCard(TheoryCatalog.Reptilians))),
      DeckView.Create(TheoryCatalog.ReptiliansInGovernment.Name, CreateCards(() => new CitizenCard(TheoryCatalog.ReptiliansInGovernment))),
      DeckView.Create(TheoryCatalog.FlatEarth.Name, CreateCards(() => new HomelessCard(TheoryCatalog.FlatEarth))),
    ];
    var hud = new Hud { Name = "Hud", Layer = 1, Player = player, Decks = decks };
    AddChild(hud);

    // Слой выше интерфейса, но в координатах стола: карточка летит поверх колод и масштабируется вместе со столом.
    var dragLayer = new CanvasLayer { Name = "DragLayer", Layer = 2, FollowViewportEnabled = true };
    AddChild(dragLayer);

    var dragController = new CardDragController { Name = "CardDragController" };
    AddChild(dragController);
    dragController.Initialize(board, player, dragLayer, decks);
    dragController.PlacementDenied += hud.ShowMessage;
  }

  private static List<Card> CreateCards(Func<Card> create) => Enumerable.Range(0, CardsPerDeck).Select(_ => create()).ToList();
}
