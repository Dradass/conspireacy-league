using System.IO;
using ConspiracyLeague.Core.Game;
using Godot;

namespace ConspiracyLeague.Game.Table;

/// <summary>
/// Отладка: клавиша F9 сохраняет состояние партии в JSON (<see cref="GameStateDump"/>).
/// Файл лежит в папке данных проекта Godot (<see cref="FilePath"/>); путь показывается в подсказке.
/// </summary>
public partial class DebugStateDumper : Node
{
  /// <summary>
  /// Куда сохраняется снимок (папка user:// Godot).
  /// </summary>
  public const string FilePath = "user://debug_state.json";

  /// <summary>
  /// Партия, которую сохраняем.
  /// </summary>
  public GameSession Session { get; init; } = null!;

  /// <summary>
  /// Стол: виды карточек на нём попадают в снимок (раздел BoardViews).
  /// </summary>
  public Board Board { get; init; } = null!;

  /// <summary>
  /// Снимок сохранён; аргумент — сообщение с путём к файлу.
  /// </summary>
  public event Action<string>? Saved;

  public override void _UnhandledKeyInput(InputEvent @event)
  {
    if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.F9 })
      return;

    var path = ProjectSettings.GlobalizePath(FilePath);
    var views = Board.AllViews.Select(pair => new GameStateDump.DumpView(
      pair.Cell.X,
      pair.Cell.Y,
      pair.View.Card.Name,
      $"({pair.View.Position.X:0},{pair.View.Position.Y:0})"));
    File.WriteAllText(path, GameStateDump.ToJson(Session, views));
    GetViewport().SetInputAsHandled();
    GD.Print($"Состояние партии сохранено: {path}");
    Saved?.Invoke($"Состояние партии сохранено: {path}");
  }
}
