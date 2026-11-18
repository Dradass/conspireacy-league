using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using Godot;

namespace ConspiracyLeague.Game.Table;

/// <summary>
/// Конец игры, когда закрытые персонажи раскрываются не все (<see cref="GamePhase.FinalReveal"/>): владелец щелчком
/// раскрывает своих закрытых персонажей, они подсвечены. Выбор заканчивается кнопкой «Больше не раскрывать закрытых
/// персонажей» (<see cref="Ui.TurnButtons"/>). За бота выбирает <see cref="BotController"/>.
/// </summary>
public partial class FinalRevealController : Node
{
  private Board _board = null!;
  private GameSession _session = null!;

  /// <summary>
  /// Сообщение игрокам о том, что происходит.
  /// </summary>
  public event Action<string>? Hint;

  /// <summary>
  /// Настроить контроллер.
  /// </summary>
  public void Initialize(Board board, GameSession session)
  {
    _board = board;
    _session = session;
    _session.PhaseChanged += OnPhaseChanged;
    _session.Turns.TurnChanged += OnTurnChanged;
    _session.Field.HiddenCardRevealed += OnHiddenCardRevealed;
  }

  public override void _ExitTree()
  {
    _session.PhaseChanged -= OnPhaseChanged;
    _session.Turns.TurnChanged -= OnTurnChanged;
    _session.Field.HiddenCardRevealed -= OnHiddenCardRevealed;
  }

  private bool IsChoosing => _session.Phase == GamePhase.FinalReveal && _session.FinalRevealPlayer is { IsBot: false };

  private void OnPhaseChanged(GamePhase phase)
  {
    if (phase == GamePhase.FinalReveal)
      AnnounceChooser();
    else
      _board.HideAvailable();
  }

  private void OnTurnChanged(Player _)
  {
    if (_session.Phase == GamePhase.FinalReveal)
      AnnounceChooser();
  }

  private void OnHiddenCardRevealed(Cell _)
  {
    if (_session.Phase == GamePhase.FinalReveal)
      ShowTargets();
  }

  private void AnnounceChooser()
  {
    if (_session.FinalRevealPlayer is not { } player)
      return;
    Hint?.Invoke(player.IsBot
      ? $"{player.Name} решает, каких закрытых персонажей раскрыть."
      : $"{player.Name}: щёлкните закрытых персонажей, которых хотите раскрыть, затем нажмите «Больше не раскрывать закрытых персонажей».");
    ShowTargets();
  }

  /// <summary>
  /// Подсветить закрытых персонажей, которых человек может раскрыть.
  /// </summary>
  private void ShowTargets()
  {
    if (IsChoosing)
      _board.ShowTargets(_session.FinalRevealTargets());
    else
      _board.HideAvailable();
  }

  public override void _UnhandledInput(InputEvent @event)
  {
    if (!IsChoosing || @event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
      return;
    if (_board.CellAt(_board.GetGlobalMousePosition())?.ToCell() is not { } cell || !_session.FinalRevealTargets().Contains(cell))
      return;

    GetViewport().SetInputAsHandled();
    _session.RevealAtEnd(cell);
  }
}
