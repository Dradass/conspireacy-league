using ConspiracyLeague.Core.Bots;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Views;
using ConspiracyLeague.Game.Cards;
using ConspiracyLeague.Game.Net;
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
  /// За каких игроков играет бот (<see cref="Bot"/>), по порядку; задаётся в стартовом меню. Если не задано — все люди.
  /// </summary>
  public IReadOnlyList<bool>? BotSeats { get; init; }

  /// <summary>
  /// Уровни ботов по порядку игроков; для людей не важны. Если не задано — лёгкие.
  /// </summary>
  public IReadOnlyList<BotLevel>? BotLevels { get; init; }

  /// <summary>
  /// Режим разработчика: под рынком панель со всеми персонажами, которых можно поставить в личный штаб.
  /// В игре через сервер недоступен.
  /// </summary>
  public bool DevMode { get; init; }

  /// <summary>
  /// Сетевая партия: её ведёт сервер, интерфейс — его клиент. <c>null</c> — игра за одним компьютером.
  /// </summary>
  public NetworkGame? Net { get; init; }

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
  /// Показывать анимацию получения денег в конце раунда.
  /// </summary>
  public bool IncomeAnimation { get; init; }

  private string NameOf(int seat) => PlayerNames is { } names ? names[seat] : $"Игрок {seat + 1}";

  private bool IsBotSeat(int seat) => BotSeats is { } bots && seat < bots.Count && bots[seat];

  private BotLevel LevelOf(int seat) => BotLevels is { } levels && seat < levels.Count ? levels[seat] : BotLevel.Easy;

  private Core.Logging.GameActionLog? _actionLog;

  public override void _ExitTree() => _actionLog?.Dispose();

  public override void _Ready()
  {
	// Оформление интерфейса в стиле рубашки карточек (обычно уже применено стартовым меню).
	UiStyle.Apply();
	if (Net is { } net)
	{
	  ReadyNetworked(net);
	  return;
	}

	var session = new GameSession(Enum.GetValues<PlayerColor>()
	  .Take(PlayerCount)
	  .Select((color, index) => new Player(NameOf(index), color, GameSession.StartingMoney)
	  {
		IsBot = IsBotSeat(index),
		BotLevel = LevelOf(index),
	  }), mode: Mode);
	session.Field.FailureConsequences = FailureConsequences;

	var table = BuildTable(session, net: null);
	table.Hud.History.AttachLocal();
	session.Turns.TurnChanged += player => table.Camera.FocusOn(CameraTarget(table.Board, player));

	// Боты ходят за своих игроков теми же путями, что и люди: с полётом карточек и бросками кубика.
	var botController = new BotController { Name = "BotController", Slowdown = BotSlowdown, FollowBots = FollowBots };
	AddChild(botController);
	botController.Initialize(session, table.Board, table.Drag, table.Use, table.Camera);
	botController.Hint += table.Hud.ShowMessage;

	var roundController = new RoundController { Name = "RoundController", IncomeAnimation = IncomeAnimation };
	AddChild(roundController);
	roundController.Initialize(table.Board, session, table.Camera);
	roundController.Hint += table.Hud.ShowMessage;
	roundController.IncomeSummaryRequested += table.IncomeSummary.Open;

	var debugDumper = new DebugStateDumper { Name = "DebugStateDumper", Session = session, Board = table.Board };
	AddChild(debugDumper);
	debugDumper.Saved += table.Hud.ShowMessage;

	// Журнал действий: игроки сидят за одним компьютером, поэтому скрытое (карты взакрытую) в него не пишется.
	_actionLog = GameLogs.Open("game");
	_actionLog?.Attach(session);

	// Подготовка: стол и рука уже следят за партией — стартовые карточки рынка выезжают из колоды
	// на глазах у игроков, стартовые карточки появляются в руке.
	session.Start();
  }

  /// <summary>
  /// Сетевая партия. Её ведёт сервер: он проводит раунды, ходит за ботов и следит за временем хода, поэтому
  /// контроллеров ботов и конца раунда здесь нет. Инструментов разработчика тоже нет: они меняли бы партию в обход
  /// сервера (палитра персонажей) или показывали бы то, что этому игроку видеть не положено (снимок партии).
  /// </summary>
  private void ReadyNetworked(NetworkGame net)
  {
	AddChild(net);
	var table = BuildTable(net.Session, net);
	net.Message += table.Hud.ShowMessage;
	// Итоги конца раунда: конец раунда проводит сервер, сводку собираем из его событий.
	var roundSummary = new Core.Client.RoundSummaryCollector(net.Session);
	roundSummary.Ready += table.IncomeSummary.Open;
	// Бросок кубика показываем до того, как сервер перевернёт карточку и выполнит действие: обновление ждёт анимацию.
	net.Client.Arriving += update => ShowDice(update, table, net);
	net.Updated += update =>
	{
	  table.Hud.History.AddEvents(update);
	  ShowEvents(update, table);
	  roundSummary.Add(update);
	};
	AddChild(new NetworkStatus { Name = "NetworkStatus", Net = net });
  }

  private sealed record Table(
	Board Board,
	TableCamera Camera,
	Hud Hud,
	CardDragController Drag,
	CardUseController Use,
	IncomeSummaryPanel IncomeSummary);

  /// <summary>
  /// Стол, камера, интерфейс и контроллеры действий игрока — общие для игры за одним компьютером и через сервер.
  /// </summary>
  private Table BuildTable(GameSession session, NetworkGame? net)
  {
	var devMode = DevMode && net is null;
	var board = new Board { Name = "Board", Session = session, Radial = RadialField, LocalPlayer = net?.LocalPlayer };
	AddChild(board);
	if (devMode)
	  board.AddChild(new DevCardPalette { Name = "DevCardPalette", Board = board });

	var camera = new TableCamera
	{
	  Name = "Camera",
	  Bounds = new Rect2(Vector2.Zero, board.Size + (devMode ? new Vector2(0, DevCardPalette.Gap + DevCardPalette.Height) : Vector2.Zero)),
	  Position = CameraTarget(board, net?.LocalPlayer ?? session.CurrentPlayer),
	  Zoom = Vector2.One * StartZoom,
	};
	AddChild(camera);

	// Общее состояние окон интерфейса: окон руки, штаба и рынка может быть сколько угодно, контроллеры работают с хабами.
	var hand = new HandHub(session);
	var personalHq = new PersonalHqHub();
	personalHq.FocusRequested += player => camera.FocusOn(CameraTarget(board, player));
	var market = new MarketHub();
	market.FocusRequested += () => camera.FocusOn(board.ZoneCenter(session.Market.Zone));
	var theories = new TheoriesHub();
	theories.FocusRequested += camera.FocusOn;
	var history = new GameHistory(session);
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
	  History = history,
	  Net = net,
	};
	AddChild(hud);

	// Слой выше интерфейса, но в координатах стола: карточка летит поверх руки и масштабируется вместе со столом.
	var dragLayer = new CanvasLayer { Name = "DragLayer", Layer = 2, FollowViewportEnabled = true };
	AddChild(dragLayer);

	var dragController = new CardDragController { Name = "CardDragController", Net = net };
	AddChild(dragController);
	dragController.Initialize(board, session, dragLayer, hand, personalHq, market);
	dragController.PlacementDenied += hud.ShowMessage;

	var useController = new CardUseController { Name = "CardUseController", Net = net };
	AddChild(useController);
	useController.Initialize(board, session, hand);
	useController.UseDenied += hud.ShowMessage;
	useController.Hint += hud.ShowMessage;
	useController.Reminder += hud.ShowReminder;
	// Пока выбирают действие карточки или его цель, карточки не перетаскивают.
	dragController.IsChoosing = () => useController.IsChoosing;

	var infoLayer = new CanvasLayer { Name = "CardInfoLayer", Layer = 3 };
	AddChild(infoLayer);
	var infoPanel = new CardInfoPanel { Name = "CardInfo" };
	infoLayer.AddChild(infoPanel);
	var infoController = new CardInfoController { Name = "CardInfoController" };
	AddChild(infoController);
	infoController.Initialize(board, session, infoPanel, hand, market, theories);

	var incomeSummary = new IncomeSummaryPanel { Name = "IncomeSummary" };
	infoLayer.AddChild(incomeSummary);
	return new Table(board, camera, hud, dragController, useController, incomeSummary);
  }

  /// <summary>
  /// Игра через сервер: показать, что произошло, по событиям обновления — броски кубика на карточках и подсказки.
  /// Сам стол к этому моменту уже приведён к новой партии.
  /// </summary>
  private void ShowEvents(ServerUpdate update, Table table)
  {
	var session = table.Board.Session;
	string Name(int seat) => seat >= 0 && seat < session.Players.Count ? session.Players[seat].Name : "?";
	foreach (var tableEvent in update.Events)
	{
	  switch (tableEvent)
	  {
		case StateReset:
		  table.Hud.ShowMessage("Сервер отменил ход из-за внутренней ошибки — партия возвращена к состоянию до него.");
		  break;
		case CardPlayed played:
		  table.Hud.ShowMessage($"{Name(played.Seat)}: {played.Action}.");
		  break;
		case PlayerPassed passed:
		  table.Hud.ShowMessage($"{Name(passed.Seat)} пасует.");
		  break;
		case PlayerAutoPassed passed:
		  table.Hud.ShowMessage($"{Name(passed.Seat)} пасует автоматически: ходить нечем.");
		  break;
		case TheoryIncomePaid income:
		  table.Hud.ShowMessage($"Адепты теории «{income.Theory}» приносят {income.Total}$.");
		  break;
		case LastRoundDeclared last:
		  table.Hud.ShowMessage($"Теория «{last.Theory}» дошла до финиша! Этот раунд — последний.");
		  break;
		case FirstPlayerChanged first:
		  table.Hud.ShowMessage($"Раунд {session.Round}. Жетон первого игрока у игрока {Name(first.Seat)}.");
		  break;
	  }
	}
  }

  /// <summary>
  /// Пауза после результата броска, прежде чем действие выполнится (как в локальной игре).
  /// </summary>
  private const double DiceResultPause = 1.0;

  /// <summary>
  /// Броски кубика из обновления, которое ещё не применено: кубик катится на карточке, результат — в подсказке,
  /// и только потом партия меняется (карточка переворачивается, действие выполняется).
  /// </summary>
  private async void ShowDice(ServerUpdate update, Table table, NetworkGame net)
  {
	var rolls = update.Events.OfType<DiceRolled>().ToList();
	if (rolls.Count == 0)
	  return;

	net.Client.Pause();
	try
	{
	  foreach (var dice in rolls)
	  {
		var text = $"«{table.Board.Field.CardAt(dice.Cell)?.Name ?? "Карточка"}» (вероятность успеха {dice.Chance}%): на кубике {dice.Die} — "
		  + (dice.Succeeded ? "успех!" : "провал, действие не выполняется.");
		if (table.Board.ViewAt(dice.Cell.ToVector2I()) is not { } view)
		{
		  table.Hud.ShowMessage(text);
		  continue;
		}

		view.Lifted = true;
		var check = Cards.SuccessCheckView.Create(view, new SuccessCheck(dice.Cell, dice.Chance, dice.Die));
		await check.Play();
		if (!IsInstanceValid(this) || !IsInsideTree())
		  return;
		table.Hud.ShowMessage(text);
		await ToSignal(GetTree().CreateTimer(DiceResultPause), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || !IsInsideTree())
		  return;
		if (IsInstanceValid(check))
		  check.QueueFree();
		if (IsInstanceValid(view))
		  view.Lifted = false;
	  }
	}
	finally
	{
	  // Что бы ни случилось с показом, партия не должна застрять на прежнем виде.
	  net.Client.Resume();
	}
  }

  /// <summary>
  /// Куда смотрит камера, когда ходит игрок: чуть ниже центра его зоны, чтобы её не закрывала рука.
  /// </summary>
  private static Vector2 CameraTarget(Board board, Player player) =>
	board.ZoneCenter(board.Field.PlayerZones[player]) + new Vector2(0, StartCameraShift);
}
