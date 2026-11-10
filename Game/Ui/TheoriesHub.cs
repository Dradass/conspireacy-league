using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Связь окон «Теории» (<see cref="TheoriesView"/>) с контроллерами: описание карточки под курсором и переход
/// к штабам теорий на столе.
/// </summary>
public sealed class TheoriesHub
{
  /// <summary>
  /// Правой кнопкой нажали в окне; аргумент — точка стола (в мировых координатах) под курсором.
  /// </summary>
  public event Action<Vector2>? InfoPressed;

  /// <summary>
  /// Правую кнопку отпустили.
  /// </summary>
  public event Action? InfoReleased;

  /// <summary>
  /// Двойной щелчок по окну: перевести камеру на точку стола (в мировых координатах).
  /// </summary>
  public event Action<Vector2>? FocusRequested;

  internal void RaiseInfoPressed(Vector2 tablePoint) => InfoPressed?.Invoke(tablePoint);

  internal void RaiseInfoReleased() => InfoReleased?.Invoke();

  internal void RaiseFocus(Vector2 tablePoint) => FocusRequested?.Invoke(tablePoint);
}
