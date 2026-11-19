using ConspiracyLeague.Game.Ui;
using Godot;

namespace ConspiracyLeague.Game.Net;

/// <summary>
/// Строка состояния сетевой партии вверху экрана: чьего действия ждёт сервер и сколько осталось секунд,
/// а если связи нет — что идёт переподключение.
/// </summary>
public partial class NetworkStatus : CanvasLayer
{
  private const int FontSize = 16;
  private const float Top = 4f;
  private const double UrgentSeconds = 10;
  private static readonly Color UrgentColor = new("ff7a5c");

  private Label _label = null!;
  private int? _seat;
  private double _secondsLeft;

  /// <summary>
  /// Время хода не ограничено.
  /// </summary>
  private bool _unlimited;

  /// <summary>
  /// Сетевая партия.
  /// </summary>
  public NetworkGame Net { get; init; } = null!;

  public override void _Ready()
  {
    Layer = 1;
    _label = new Label
    {
      Name = "Status",
      HorizontalAlignment = HorizontalAlignment.Center,
      AnchorLeft = 0.5f,
      AnchorRight = 0.5f,
      OffsetTop = Top,
      GrowHorizontal = Control.GrowDirection.Both,
      MouseFilter = Control.MouseFilterEnum.Ignore,
    };
    _label.AddThemeFontSizeOverride("font_size", FontSize);
    UiStyle.MakeNumber(_label);
    AddChild(_label);
    Net.TurnTimerStarted += OnTurnTimer;
    Net.Session.Restored += OnRestored;
    if (Net.LastTimer is { } timer)
      OnTurnTimer(timer.Seat, timer.Seconds);
    OnRestored();
  }

  public override void _ExitTree()
  {
    Net.TurnTimerStarted -= OnTurnTimer;
    Net.Session.Restored -= OnRestored;
  }

  private void OnTurnTimer(int seat, int? seconds)
  {
    _seat = seat;
    _unlimited = seconds is null;
    _secondsLeft = seconds ?? 0;
  }

  /// <summary>
  /// Партия закончилась — таймер не нужен.
  /// </summary>
  private void OnRestored()
  {
    if (Net.Session.Phase == Core.Game.GamePhase.GameOver)
      _seat = null;
  }

  public override void _Process(double delta)
  {
    if (Net.IsClosed)
    {
      Show("Вы больше не за столом.", UrgentColor);
      return;
    }
    if (!Net.HasConnection)
    {
      Show("Нет связи с сервером — переподключение…", UrgentColor);
      return;
    }
    if (_seat is not { } seat || seat >= Net.Session.Players.Count)
    {
      _label.Visible = false;
      return;
    }

    var who = Net.Session.Players[seat] == Net.LocalPlayer ? "Ваш ход" : $"Ходит {Net.Session.Players[seat].Name}";
    if (_unlimited)
    {
      Show($"{who} (время не ограничено)", Colors.White);
      return;
    }

    _secondsLeft = Math.Max(0, _secondsLeft - delta);
    var seconds = (int)Math.Ceiling(_secondsLeft);
    Show($"{who}: {seconds / 60}:{seconds % 60:00}", seconds <= UrgentSeconds ? UrgentColor : Colors.White);
  }

  private void Show(string text, Color color)
  {
    _label.Visible = true;
    _label.Text = text;
    _label.AddThemeColorOverride("font_color", color);
  }
}
