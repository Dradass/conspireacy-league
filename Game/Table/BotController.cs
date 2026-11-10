using ConspiracyLeague.Core.Bots;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using Godot;

namespace ConspiracyLeague.Game.Table;

/// <summary>
/// Ходы ботов: когда ходит игрок-бот (<see cref="Player.IsBot"/>), контроллер с паузами спрашивает у бота
/// (<see cref="Bot"/>) следующий ход и выполняет его теми же путями, что и действия человека, — карточка летит
/// на стол, на кубике видна проверка успеха. Ещё бот сам выбирает цель для действия выложенной им карточки
/// и цели действия своей основной теории, даже если сейчас ходит человек. Скорость ботов задаёт <see cref="Slowdown"/>;
/// если включено <see cref="FollowBots"/>, камера сначала переезжает туда, где бот будет действовать, и только потом
/// он действует.
/// </summary>
public partial class BotController : Node
{
  /// <summary>
  /// Пауза между действиями бота при исходной скорости, чтобы люди успевали следить за ходом.
  /// </summary>
  private const double BaseStepDelay = 0.9;

  /// <summary>
  /// Пауза перед выбором цели (действие при выкладывании, действие основной теории) при исходной скорости.
  /// </summary>
  private const double BaseChoiceDelay = 0.6;

  /// <summary>
  /// Сколько камера едет к месту действия бота, прежде чем он действует, при исходной скорости.
  /// </summary>
  private const double BaseFocusLead = 0.5;

  /// <summary>
  /// Как часто контроллер проверяет, не пора ли боту действовать.
  /// </summary>
  private const double PollInterval = 0.1;

  /// <summary>
  /// Больше стольких неудачных ходов за ход — бот пасует, чтобы партия не застряла.
  /// </summary>
  private const int MaxFailuresPerTurn = 8;

  private readonly Dictionary<Player, Bot> _bots = new();
  private readonly HashSet<string> _failed = new();
  private GameSession _session = null!;
  private Board _board = null!;
  private CardDragController _drag = null!;
  private CardUseController _use = null!;
  private TableCamera _camera = null!;
  private double _cooldown;
  private double _choiceWait;

  /// <summary>
  /// Действие бота, которое выполнится, когда камера доедет до места действия.
  /// </summary>
  private Action? _planned;

  /// <summary>
  /// Цель сыгранной карточки: камера переезжает к ней после проверки успеха.
  /// </summary>
  private Cell? _targetAfterCheck;

  /// <summary>
  /// Во сколько раз медленнее исходной скорости ходят боты.
  /// </summary>
  public double Slowdown { get; set; } = 1;

  /// <summary>
  /// Камера следует за действиями ботов.
  /// </summary>
  public bool FollowBots { get; set; }

  private double StepDelay => BaseStepDelay * Slowdown;

  private double ChoiceDelay => BaseChoiceDelay * Slowdown;

  private double FocusLead => BaseFocusLead * Slowdown;

  /// <summary>
  /// Что сделал бот — подсказка на экране.
  /// </summary>
  public event Action<string>? Hint;

  /// <summary>
  /// Настроить контроллер: по боту на каждого игрока-бота.
  /// </summary>
  public void Initialize(GameSession session, Board board, CardDragController drag, CardUseController use, TableCamera camera)
  {
    _session = session;
    _board = board;
    _drag = drag;
    _use = use;
    _camera = camera;
    _cooldown = StepDelay;
    foreach (var player in session.Players.Where(player => player.IsBot))
      _bots[player] = new Bot(player);
    _session.Turns.TurnChanged += OnTurnChanged;
  }

  public override void _ExitTree()
  {
    if (_session is not null)
      _session.Turns.TurnChanged -= OnTurnChanged;
  }

  private void OnTurnChanged(Player player)
  {
    _failed.Clear();
    _targetAfterCheck = null;
    _cooldown = StepDelay;
  }

  public override void _Process(double delta)
  {
    if (_bots.Count == 0)
      return;
    _cooldown -= delta;
    if (_cooldown > 0)
      return;

    _cooldown = PollInterval;
    try
    {
      Step();
    }
    catch (Exception error)
    {
      // Бот не должен ронять партию: показываем ошибку и пробуем дальше.
      GD.PrintErr($"Ошибка бота: {error}");
      _planned = null;
      _cooldown = StepDelay;
    }
  }

  /// <summary>
  /// Сделать один шаг за бота, если сейчас его очередь.
  /// </summary>
  private void Step()
  {
    // Идёт проверка успеха с анимацией — ждём её окончания.
    if (_session.PendingCheck is not null)
      return;

    // Камера доехала — бот делает задуманное.
    if (_planned is { } planned)
    {
      _planned = null;
      Act(planned);
      return;
    }

    // Проверка успеха прошла — показываем, на что подействовала карточка.
    if (_targetAfterCheck is { } target)
    {
      _targetAfterCheck = null;
      Focus(target);
      _cooldown = FocusLead;
      return;
    }

    // Действие основной теории ждёт выбора бота — даже в чужой ход.
    if (_session.PendingTheoryTrigger is { } trigger)
    {
      if (_bots.TryGetValue(trigger.Owner, out var owner))
        Choose(() =>
        {
          var response = owner.ChooseTrigger(_session, trigger);
          var theory = trigger.Parent.Card.Theory;
          Cell? place = response.Skip ? null : response.Cell ?? response.Theory?.TheoryCell ?? trigger.Parent.Cell;
          Plan(place, () =>
          {
            Bot.ApplyTrigger(_session, trigger, response);
            Hint?.Invoke(response.Skip
              ? $"{owner.Player.Name} не играет действие теории \"{theory}\"."
              : $"{owner.Player.Name}: сработал эффект теории \"{theory}\": \"{trigger.Effect.Description}\".");
          });
        });
      return;
    }

    if (_session.Phase != GamePhase.PlayerTurns || !_bots.TryGetValue(_session.CurrentPlayer, out var bot))
      return;

    if (_session.PendingPlacement is { } choice)
    {
      Choose(() =>
      {
        var response = bot.ChoosePlacement(_session, choice);
        Plan(response.Target ?? choice.Cell, () => Bot.ApplyPlacement(_session, choice, response));
      });
      return;
    }

    if (_failed.Count >= MaxFailuresPerTurn)
    {
      Hint?.Invoke($"{bot.Player.Name} пасует.");
      _session.Pass();
      _cooldown = StepDelay;
      return;
    }

    var move = bot.ChooseMove(_session, _failed);
    Hint?.Invoke($"{bot.Player.Name} {move.Describe(_session)}.");
    Plan(PlaceOf(move), () => Run(move));
  }

  /// <summary>
  /// Выполнить ход бота; неудачный ход больше не предлагается до конца хода.
  /// </summary>
  private void Run(BotMove move)
  {
    try
    {
      if (!Execute(move))
        _failed.Add(move.Key);
      else if (FollowBots)
        _targetAfterCheck = TargetOf(move);
    }
    catch (Exception error) when (error is InvalidOperationException or ArgumentException or NotImplementedException)
    {
      GD.PrintErr($"Ход бота «{move.Describe(_session)}» не удался: {error.Message}");
      _failed.Add(move.Key);
    }
  }

  /// <summary>
  /// Выполнить ход бота так же, как его сделал бы человек.
  /// </summary>
  /// <returns><c>false</c>, если ход не начался.</returns>
  private bool Execute(BotMove move)
  {
    switch (move)
    {
      case BuyMove buy:
        _session.BuyFromMarket(buy.Cell);
        return true;
      case PlaceMove place:
        _drag.PlaceForBot(place.Card, place.Cell);
        return true;
      case UseCardMove use:
        // Розыгрыш с анимацией кубика; если проверка не началась, ход не удался.
        return _use.UseForBot(use.Cell, use.Option, use.Context) && _session.PendingCheck is not null;
      case UseTheoryCardMove theory:
        _use.UseTheoryCardForBot(theory.Card, theory.Context);
        return theory.Card.IsUsed;
      case PassMove:
        _session.Pass();
        return true;
      default:
        return false;
    }
  }

  /// <summary>
  /// Где бот действует: ячейка рынка, куда кладёт карточку, или карточка, которую играет.
  /// </summary>
  private static Cell? PlaceOf(BotMove move) => move switch
  {
    BuyMove buy => buy.Cell,
    PlaceMove place => place.Cell,
    UseCardMove use => use.Cell,
    UseTheoryCardMove theory => theory.Card.Cell,
    _ => null,
  };

  /// <summary>
  /// На что подействовала сыгранная карточка, если цель не там же, где карточка.
  /// </summary>
  private static Cell? TargetOf(BotMove move)
  {
    var (source, context) = move switch
    {
      UseCardMove use => (use.Cell, use.Context),
      UseTheoryCardMove theory => (theory.Card.Cell, theory.Context),
      _ => (default(Cell), (GameContext?)null),
    };
    var target = context?.TargetCell ?? context?.TargetTheory?.TheoryCell;
    return target == source ? null : target;
  }

  /// <summary>
  /// Сделать действие: если камера следует за ботами, сначала перевести её на место действия и подождать.
  /// </summary>
  private void Plan(Cell? place, Action action)
  {
    if (FollowBots && place is { } cell)
    {
      Focus(cell);
      _planned = action;
      _cooldown = FocusLead;
      return;
    }

    Act(action);
  }

  private void Act(Action action)
  {
    try
    {
      action();
    }
    finally
    {
      _board.ShowPlayStates();
      _cooldown = StepDelay;
    }
  }

  private void Focus(Cell cell) => _camera.FocusOn(_board.CellCenter(cell.ToVector2I()));

  /// <summary>
  /// Выбор цели — с небольшой паузой, чтобы было видно, что выбирает бот.
  /// </summary>
  private void Choose(Action choose)
  {
    // Шаг вызывается раз в PollInterval — набираем паузу.
    _choiceWait += PollInterval;
    if (_choiceWait < ChoiceDelay)
      return;

    _choiceWait = 0;
    choose();
  }
}
