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
  private const float StartZoom = 0.6f;
  private const float StartCameraShift = 80f;

  /// <summary>
  /// Количество игроков в партии; задаётся в стартовом меню (<see cref="Ui.StartMenu"/>).
  /// </summary>
  public int PlayerCount { get; init; } = 4;

  /// <summary>
  /// Имена игроков по порядку; введены в стартовом меню. Если не заданы — «Игрок N».
  /// </summary>
  public IReadOnlyList<string>? PlayerNames { get; init; }

  /// <summary>
  /// За каких игроков играет бот (<see cref="Core.Bots.Bot"/>), по порядку; задаётся в стартовом меню. Если не задано — все люди.
  /// </summary>
  public IReadOnlyList<bool>? BotSeats { get; init; }

  /// <summary>
  /// Режим разработчика: под рынком панель со всеми персонажами, которых можно поставить в личный штаб.
  /// </summary>
  public bool DevMode { get; init; }

  /// <summary>
  /// Режим партии: заработок денег или развитие теорий (победные очки); выбирается в стартовом меню.
  /// </summary>
  public GameMode Mode { get; init; } = GameMode.TheoryDevelopment;

  /// <summary>
  /// Последствия провала проверки на низких гранях (бездомный уходит, гражданин понижается); выбирается в стартовом меню.
  /// </summary>
  public bool FailureConsequences { get; init; } = true;

  /// <summary>
  /// Радиальное поле: стол в форме кругов — в центре пусто, дальше кольца теорий, рынка и личных штабов;
  /// кольца крутятся мышью. Выбирается в стартовом меню, по умолчанию выключено.
  /// </summary>
  public bool RadialField { get; init; }

  /// <summary>
  /// Во сколько раз медленнее исходной скорости ходят боты.
  /// </summary>
  public double BotSlowdown { get; init; } = 2;

  /// <summary>
  /// Камера следует за действиями ботов.
  /// </summary>
  public bool FollowBots { get; init; }

  /// <summary>
  /// Показывать итоги конца раунда столбчатой диаграммой, а не текстом.
  /// </summary>
  public bool RoundSummaryChart { get; init; }

  public override void _Ready()
  {
	// Оформление интерфейса в стиле рубашки карточек (обычно уже применено стартовым меню).
	UiStyle.Apply();
	var session = new GameSession(Enum.GetValues<PlayerColor>()
	  .Take(PlayerCount)
	  .Select((color, index) => new Player(PlayerNames is { } names ? names[index] : $"Игрок {index + 1}", color, GameSession.StartingMoney)
	  {
		IsBot = BotSeats is { } bots && index < bots.Count && bots[index],
	  }), mode: Mode);
	session.Field.FailureConsequences = FailureConsequences;

	var board = new Board { Name = "Board", Session = session, Radial = RadialField };
	AddChild(board);
	if (DevMode)
	  board.AddChild(new DevCardPalette { Name = "DevCardPalette", Board = board });

	var camera = new TableCamera
	{
	  Name = "Camera",
	  Bounds = new Rect2(Vector2.Zero, board.Size + (DevMode ? new Vector2(0, DevCardPalette.Gap + DevCardPalette.Height) : Vector2.Zero)),
	  Position = CameraTarget(board, session.CurrentPlayer),
	  Zoom = Vector2.One * StartZoom,
	};
	AddChild(camera);
	session.Turns.TurnChanged += player => camera.FocusOn(CameraTarget(board, player));

	// Общее состояние окон интерфейса: окон руки, штаба и рынка может быть сколько угодно, контроллеры работают с хабами.
	var hand = new HandHub(session);
	var personalHq = new PersonalHqHub();
	personalHq.FocusRequested += player => camera.FocusOn(CameraTarget(board, player));
	var market = new MarketHub();
	market.FocusRequested += () => camera.FocusOn(board.ZoneCenter(session.Market.Zone));
	var theories = new TheoriesHub();
	theories.FocusRequested += camera.FocusOn;
	var hud = new Hud
	{
	  Name = "Hud",
	  Layer = 1,
	  Session = session,
	  Board = board,
	  Hand = hand,
	  PersonalHq = personalHq,
	  Market = market,
	  Theories = theories,
	};
	AddChild(hud);

	// Слой выше интерфейса, но в координатах стола: карточка летит поверх руки и масштабируется вместе со столом.
	var dragLayer = new CanvasLayer { Name = "DragLayer", Layer = 2, FollowViewportEnabled = true };
	AddChild(dragLayer);

	var dragController = new CardDragController { Name = "CardDragController" };
	AddChild(dragController);
	dragController.Initialize(board, session, dragLayer, hand, personalHq, market);
	dragController.PlacementDenied += hud.ShowMessage;

	var useController = new CardUseController { Name = "CardUseController" };
	AddChild(useController);
	useController.Initialize(board, session, hand);
	useController.UseDenied += hud.ShowMessage;
	useController.Hint += hud.ShowMessage;
	useController.Reminder += hud.ShowReminder;
	// Пока выбирают действие карточки или его цель, карточки не перетаскивают.
	dragController.IsChoosing = () => useController.IsChoosing;

	// Боты ходят за своих игроков теми же путями, что и люди: с полётом карточек и бросками кубика.
	var botController = new BotController { Name = "BotController", Slowdown = BotSlowdown, FollowBots = FollowBots };
	AddChild(botController);
	botController.Initialize(session, board, dragController, useController, camera);
	botController.Hint += hud.ShowMessage;

	var roundController = new RoundController { Name = "RoundController" };
	AddChild(roundController);
	roundController.Initialize(board, session, camera);
	roundController.Hint += hud.ShowMessage;

	var infoLayer = new CanvasLayer { Name = "CardInfoLayer", Layer = 3 };
	AddChild(infoLayer);
	var infoPanel = new CardInfoPanel { Name = "CardInfo" };
	infoLayer.AddChild(infoPanel);
	var infoController = new CardInfoController { Name = "CardInfoController" };
	AddChild(infoController);
	infoController.Initialize(board, session, infoPanel, hand, market, theories);

	var incomeSummary = new IncomeSummaryPanel { Name = "IncomeSummary", Chart = RoundSummaryChart };
	infoLayer.AddChild(incomeSummary);
	roundController.IncomeSummaryRequested += incomeSummary.Open;

	var debugDumper = new DebugStateDumper { Name = "DebugStateDumper", Session = session, Board = board };
	AddChild(debugDumper);
	debugDumper.Saved += hud.ShowMessage;

	// Подготовка: стол и рука уже следят за партией — стартовые карточки рынка выезжают из колоды
	// на глазах у игроков, стартовые карточки появляются в руке.
	session.Start();
  }

  /// <summary>
  /// Куда смотрит камера, когда ходит игрок: чуть ниже центра его зоны, чтобы её не закрывала рука.
  /// </summary>
  private static Vector2 CameraTarget(Board board, Player player) =>
	board.ZoneCenter(board.Field.PlayerZones[player]) + new Vector2(0, StartCameraShift);
}
