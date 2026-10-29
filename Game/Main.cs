using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Theories;
using ConspiracyLeague.Game.Table;
using ConspiracyLeague.Game.Ui;
using Godot;

namespace ConspiracyLeague.Game;

/// <summary>
/// Главная сцена: стол с зонами игроков, камера, балансы игроков и колоды, перетаскивание карточек, переход хода.
/// </summary>
public partial class Main : Node2D
{
  private const int CardsPerDeck = 10;
  private const int StartingMoney = 10;
  private const int PlayerCount = 4;
  private const float StartZoom = 0.6f;
  private const float StartCameraShift = 80f;

  public override void _Ready()
  {
	var session = new GameSession(Enum.GetValues<PlayerColor>()
	  .Take(PlayerCount)
	  .Select((color, index) => new Player($"Игрок {index + 1}", color, StartingMoney)));

	var board = new Board { Name = "Board", Session = session };
	AddChild(board);

	var camera = new TableCamera
	{
	  Name = "Camera",
	  Bounds = new Rect2(Vector2.Zero, board.Size),
	  Position = CameraTarget(session, session.CurrentPlayer),
	  Zoom = Vector2.One * StartZoom,
	};
	AddChild(camera);
	session.Turns.TurnChanged += player => camera.FocusOn(CameraTarget(session, player));

	// TODO: колоды с теориями — тестовые, пока в игре нет настоящих карточек персон с теориями.
	DeckView[] decks =
	[
	  DeckView.Create("Бездомные", CreateCards(() => new HomelessCard())),
	  DeckView.Create("Граждане", CreateCards(() => new CitizenCard())),
	  DeckView.Create(TheoryCatalog.SecretGovernment.Name, CreateCards(() => new CitizenCard(TheoryCatalog.SecretGovernment))),
	  DeckView.Create(TheoryCatalog.Reptilians.Name, CreateCards(() => new CitizenCard(TheoryCatalog.Reptilians))),
	  DeckView.Create(TheoryCatalog.TinFoilHat.Name, CreateCards(() => new HomelessCard(TheoryCatalog.TinFoilHat))),
	];
	var hud = new Hud { Name = "Hud", Layer = 1, Session = session, Decks = decks };
	AddChild(hud);

	// Слой выше интерфейса, но в координатах стола: карточка летит поверх колод и масштабируется вместе со столом.
	var dragLayer = new CanvasLayer { Name = "DragLayer", Layer = 2, FollowViewportEnabled = true };
	AddChild(dragLayer);

	var dragController = new CardDragController { Name = "CardDragController" };
	AddChild(dragController);
	dragController.Initialize(board, session, dragLayer, decks);
	dragController.PlacementDenied += hud.ShowMessage;

	var useController = new CardUseController { Name = "CardUseController" };
	AddChild(useController);
	useController.Initialize(board, session);
	useController.UseDenied += hud.ShowMessage;
	useController.Hint += hud.ShowMessage;
  }

  /// <summary>
  /// Куда смотрит камера, когда ходит игрок: чуть ниже центра его зоны, чтобы её не закрывали колоды.
  /// </summary>
  private static Vector2 CameraTarget(GameSession session, Player player) =>
	Board.ZoneCenter(session.Field.PlayerZones[player]) + new Vector2(0, StartCameraShift);

  private static List<Card> CreateCards(Func<Card> create) => Enumerable.Range(0, CardsPerDeck).Select(_ => create()).ToList();
}
