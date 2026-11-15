using ConspiracyLeague.Core.Commands;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Game.Net;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Кнопки хода под кнопкой меню (три полоски) в левом верхнем углу: «Пас» и, пока владелец основной теории
/// выбирает её действие после побочной карточки, «Не играть действие теории». Не зависят от окон интерфейса —
/// их нельзя случайно закрыть вместе с окном.
/// </summary>
public partial class TurnButtons : VBoxContainer
{
  private const int FontSize = 16;

  private Button _pass = null!;
  private Button _skipTheory = null!;

  /// <summary>
  /// Сессия игры.
  /// </summary>
  public GameSession Session { get; init; } = null!;

  /// <summary>
  /// Игра через сервер: кнопки отправляют команды. <c>null</c> — игра за одним компьютером.
  /// </summary>
  public NetworkGame? Net { get; init; }

  public override void _Ready()
  {
    MouseFilter = MouseFilterEnum.Ignore;
    AddThemeConstantOverride("separation", 4);

    _pass = new Button { Name = "Pass", Text = "Пас", FocusMode = FocusModeEnum.None };
    _pass.AddThemeFontSizeOverride("font_size", FontSize);
    _pass.Pressed += () =>
    {
      if (Net is { } net)
        net.Send(new PassCommand());
      else
        Session.Pass();
    };
    AddChild(_pass);

    _skipTheory = new Button { Name = "SkipTheory", Text = "Не играть действие теории", FocusMode = FocusModeEnum.None, Visible = false };
    _skipTheory.AddThemeFontSizeOverride("font_size", FontSize);
    _skipTheory.Pressed += () =>
    {
      if (Net is { } net)
        net.Send(new SkipTheoryTriggerCommand());
      else
        Session.SkipTheoryTrigger();
    };
    AddChild(_skipTheory);

    Session.PhaseChanged += OnPhaseChanged;
    Session.PendingCheckChanged += OnPendingCheckChanged;
    Session.PendingPlacementChanged += OnPendingPlacementChanged;
    Session.PendingTheoryTriggerChanged += OnPendingTheoryTriggerChanged;
    Session.Turns.TurnChanged += OnTurnChanged;
    Session.Restored += UpdateButtons;
    UpdateButtons();
  }

  public override void _ExitTree()
  {
    Session.Turns.TurnChanged -= OnTurnChanged;
    Session.PhaseChanged -= OnPhaseChanged;
    Session.PendingCheckChanged -= OnPendingCheckChanged;
    Session.PendingPlacementChanged -= OnPendingPlacementChanged;
    Session.PendingTheoryTriggerChanged -= OnPendingTheoryTriggerChanged;
    Session.Restored -= UpdateButtons;
  }

  private void OnPhaseChanged(GamePhase _) => UpdateButtons();

  private void OnTurnChanged(Player _) => UpdateButtons();

  private void OnPendingCheckChanged(SuccessCheck? _) => UpdateButtons();

  private void OnPendingPlacementChanged(PlacementChoice? _) => UpdateButtons();

  private void OnPendingTheoryTriggerChanged(TheoryTrigger? _) => UpdateButtons();

  /// <summary>
  /// Пасовать можно только во время ходов игроков и не посреди проверки успеха или выбора.
  /// Пока ждём решения владельца основной теории, вместо этого доступна кнопка «Не играть действие теории».
  /// После конца партии кнопки не нужны. Пока ходит или выбирает бот, люди за него не нажимают.
  /// </summary>
  private void UpdateButtons()
  {
    var canAct = Session.Phase == GamePhase.PlayerTurns && Session.PendingCheck is null
      && Session.PendingPlacement is null && Session.PendingTheoryTrigger is null
      && !Session.CurrentPlayer.IsBot;
    _pass.Disabled = !canAct;
    _pass.Visible = Session.Phase != GamePhase.GameOver;
    _skipTheory.Visible = Session.PendingTheoryTrigger is { Owner.IsBot: false };
  }
}
