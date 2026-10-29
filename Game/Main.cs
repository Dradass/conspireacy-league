using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Game.Table;
using ConspiracyLeague.Game.Ui;
using Godot;

namespace ConspiracyLeague.Game;

/// <summary>
/// Главная сцена: стол с зонами игроков и рынком, камера, балансы и руки игроков, перетаскивание карточек, ходы, пасы и конец раунда.
/// </summary>
public partial class Main : Node2D
{
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

	var hand = new HandView { Name = "Hand", Session = session };
	var hud = new Hud { Name = "Hud", Layer = 1, Session = session, Hand = hand };
	AddChild(hud);

	// Слой выше интерфейса, но в координатах стола: карточка летит поверх руки и масштабируется вместе со столом.
	var dragLayer = new CanvasLayer { Name = "DragLayer", Layer = 2, FollowViewportEnabled = true };
	AddChild(dragLayer);

	var dragController = new CardDragController { Name = "CardDragController" };
	AddChild(dragController);
	dragController.Initialize(board, session, dragLayer, hand);
	dragController.PlacementDenied += hud.ShowMessage;

	var useController = new CardUseController { Name = "CardUseController" };
	AddChild(useController);
	useController.Initialize(board, session);
	useController.UseDenied += hud.ShowMessage;
	useController.Hint += hud.ShowMessage;

	var roundController = new RoundController { Name = "RoundController" };
	AddChild(roundController);
	roundController.Initialize(board, session, camera);
	roundController.Hint += hud.ShowMessage;

	// Подготовка: стол и рука уже следят за партией — стартовые карточки рынка выезжают из колоды
	// на глазах у игроков, стартовые карточки появляются в руке.
	session.Start();
  }

  /// <summary>
  /// Куда смотрит камера, когда ходит игрок: чуть ниже центра его зоны, чтобы её не закрывала рука.
  /// </summary>
  private static Vector2 CameraTarget(GameSession session, Player player) =>
	Board.ZoneCenter(session.Field.PlayerZones[player]) + new Vector2(0, StartCameraShift);
}
