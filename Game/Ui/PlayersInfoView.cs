using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Окно «Информация об игроках»: раунд и фаза, чей ход и сколько действий осталось, игроки с балансами.
/// Кнопка «Пас» — не здесь, а под кнопкой меню (<see cref="TurnButtons"/>).
/// </summary>
public partial class PlayersInfoView : VBoxContainer
{
  private const float WaitingPlayerAlpha = 0.55f;

  private readonly Dictionary<Player, Label> _balances = new();
  private Label _round = null!;
  private Label _turn = null!;
  private Label _actions = null!;

  /// <summary>
  /// Сессия игры: игроки, чьи балансы показываются, и очерёдность ходов.
  /// </summary>
  public GameSession Session { get; init; } = null!;

  private TurnOrder Turns => Session.Turns;

  public override void _Ready()
  {
    MouseFilter = MouseFilterEnum.Ignore;
    AddThemeConstantOverride("separation", 3);

    _round = new Label();
    _round.AddThemeFontSizeOverride("font_size", 14);
    AddChild(_round);

    _turn = new Label();
    _turn.AddThemeFontSizeOverride("font_size", 17);
    AddChild(_turn);

    _actions = new Label();
    _actions.AddThemeFontSizeOverride("font_size", 14);
    AddChild(_actions);

    foreach (var player in Turns.Players)
    {
      var balance = new Label { Name = $"Balance{player.Color}" };
      balance.AddThemeFontSizeOverride("font_size", 18);
      UiStyle.MakeNumber(balance);
      balance.AddThemeColorOverride("font_color", PlayerPalette.MainOf(player.Color));
      AddChild(balance);
      _balances[player] = balance;
    }

    foreach (var player in Turns.Players)
      player.MoneyChanged += OnMoneyChanged;
    Turns.TurnChanged += OnTurnChanged;
    Turns.Passed += OnMoneyChanged;
    Turns.FirstPlayerChanged += OnFirstPlayerChanged;
    Session.PhaseChanged += OnPhaseChanged;
    Session.ActionsLeftChanged += OnActionsLeftChanged;
    Session.LastRoundDeclared += OnLastRoundDeclared;
    Session.PendingTheoryTriggerChanged += OnPendingTheoryTriggerChanged;
    OnPhaseChanged(Session.Phase);
  }

  public override void _ExitTree()
  {
    foreach (var player in Turns.Players)
      player.MoneyChanged -= OnMoneyChanged;
    Turns.TurnChanged -= OnTurnChanged;
    Turns.Passed -= OnMoneyChanged;
    Turns.FirstPlayerChanged -= OnFirstPlayerChanged;
    Session.PhaseChanged -= OnPhaseChanged;
    Session.ActionsLeftChanged -= OnActionsLeftChanged;
    Session.LastRoundDeclared -= OnLastRoundDeclared;
    Session.PendingTheoryTriggerChanged -= OnPendingTheoryTriggerChanged;
  }

  private void OnMoneyChanged(Player player) =>
    _balances[player].Text = (player == Turns.FirstPlayer ? "(1) " : string.Empty)
      + $"{player.Name}: {player.Money}$" + (Turns.HasPassed(player) ? " — пас" : string.Empty);

  private void OnFirstPlayerChanged(Player _)
  {
    foreach (var player in Turns.Players)
      OnMoneyChanged(player);
  }

  private void OnActionsLeftChanged(int actionsLeft)
  {
    _actions.Visible = Session.Phase == GamePhase.PlayerTurns;
    _actions.Text = $"Действий: {actionsLeft} из {GameSession.ActionsPerTurn}";
  }

  private void OnPendingTheoryTriggerChanged(TheoryTrigger? trigger) => RefreshTurn();

  private void OnPhaseChanged(GamePhase phase)
  {
    UpdateRound();
    OnActionsLeftChanged(Session.ActionsLeft);
    foreach (var player in Turns.Players)
      OnMoneyChanged(player);
    RefreshTurn();
  }

  /// <summary>
  /// Номер раунда и фаза; в режиме гонки — пометка, что раунд последний.
  /// </summary>
  private void UpdateRound()
  {
    var last = Session.IsLastRound && Session.Phase != GamePhase.GameOver ? " (последний)" : "";
    _round.Text = $"Раунд {Session.Round}{last}: {NameOf(Session.Phase)}";
  }

  private void OnLastRoundDeclared(TheoryColumn column) => UpdateRound();

  private static string NameOf(GamePhase phase) => phase switch
  {
    GamePhase.Setup => "подготовка",
    GamePhase.PlayerTurns => "ходы игроков",
    GamePhase.HeadquartersCheck => "проверка штабов теорий",
    GamePhase.AdeptIncome => "адепты приносят деньги",
    GamePhase.CardsRefresh => "обновление карточек",
    GamePhase.MarketRefresh => "обновление рынка",
    GamePhase.FinalLeadersBonus => "итоговый подсчёт: деньги владельцам за лидеров",
    GamePhase.FinalAdeptIncome => "итоговый подсчёт: адепты приносят деньги",
    GamePhase.GameOver => "партия завершена",
    _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
  };

  private void OnTurnChanged(Player _) => RefreshTurn();

  /// <summary>
  /// Показать, чьё действие сейчас ожидается: обычно это игрок, который ходит, но пока владелец основной теории
  /// выбирает действие после дочерней карточки (даже если ход у другого игрока), ожидается его выбор.
  /// </summary>
  private void RefreshTurn()
  {
    var actor = Session.PendingTheoryTrigger?.Owner ?? Turns.CurrentPlayer;
    _turn.Text = Session.Phase switch
    {
      GamePhase.PlayerTurns => $"Ходит: {actor.Name}",
      GamePhase.FinalLeadersBonus or GamePhase.FinalAdeptIncome or GamePhase.GameOver => "Итоговый подсчёт",
      _ => "Конец раунда",
    };
    _turn.AddThemeColorOverride("font_color", PlayerPalette.MainOf(actor.Color));
    foreach (var (player, balance) in _balances)
      balance.Modulate = player == actor ? Colors.White : Colors.White with { A = WaitingPlayerAlpha };
  }
}
