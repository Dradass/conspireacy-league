using ConspiracyLeague.Core.Cards;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Связь окон «Рынок» (<see cref="MarketView"/>) с контроллерами: из окна можно потащить карточку рынка в руку,
/// посмотреть её описание и перейти к рынку на столе.
/// </summary>
public sealed class MarketHub
{
  /// <summary>
  /// Ячейка рынка, карточку из которой сейчас тащат: в окнах рынка она пуста. <c>null</c> — ничего не тащат.
  /// </summary>
  public Vector2I? DraggedCell { get; set; }

  /// <summary>
  /// Игрок начал тянуть карточку рынка из окна; аргумент — ячейка рынка.
  /// </summary>
  public event Action<Vector2I>? CardDragStarted;

  /// <summary>
  /// Правой кнопкой нажата карточка рынка в окне.
  /// </summary>
  public event Action<Card>? CardInfoPressed;

  /// <summary>
  /// Правую кнопку отпустили.
  /// </summary>
  public event Action? CardInfoReleased;

  /// <summary>
  /// Двойной щелчок по окну рынка: перевести камеру на рынок на столе.
  /// </summary>
  public event Action? FocusRequested;

  internal void RaiseDragStarted(Vector2I cell) => CardDragStarted?.Invoke(cell);

  internal void RaiseInfoPressed(Card card) => CardInfoPressed?.Invoke(card);

  internal void RaiseInfoReleased() => CardInfoReleased?.Invoke();

  internal void RaiseFocus() => FocusRequested?.Invoke();
}
