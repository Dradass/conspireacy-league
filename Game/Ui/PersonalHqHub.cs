using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Общее состояние всех окон личного штаба (<see cref="PersonalHqView"/>): подсветка ячеек, куда можно положить
/// перетаскиваемую карточку, и поиск ячейки под курсором. Карточку из руки принимают только окна «Личный штаб»
/// (штаб того, кто ходит), окна «Штаб другого игрока» — нет.
/// </summary>
public sealed class PersonalHqHub
{
  private readonly List<PersonalHqView> _views = new();
  private readonly HashSet<Cell> _available = new();

  /// <summary>
  /// Ячейки, куда можно положить взятую карточку.
  /// </summary>
  public IReadOnlySet<Cell> Available => _available;

  /// <summary>
  /// Ячейка под перетаскиваемой карточкой и можно ли её туда положить.
  /// </summary>
  public (Cell Cell, bool Allowed)? Hover { get; private set; }

  /// <summary>
  /// Сменилась подсветка.
  /// </summary>
  public event Action? Changed;

  /// <summary>
  /// Двойной щелчок по штабу: перевести камеру на штаб этого игрока на столе.
  /// </summary>
  public event Action<Player>? FocusRequested;

  /// <summary>
  /// Ячейка штаба под точкой экрана в окне, куда можно положить карточку, или <c>null</c>.
  /// </summary>
  public Cell? CellAtScreenPoint(Vector2 point) =>
    _views.FirstOrDefault(view => view.AcceptsDrops && view.ContainsScreenPoint(point))?.CellAtScreenPoint(point);

  /// <summary>
  /// Находится ли точка экрана над каким-нибудь видимым окном штаба (своего или чужого).
  /// </summary>
  public bool ContainsScreenPoint(Vector2 point) => _views.Any(view => view.ContainsScreenPoint(point));

  /// <summary>
  /// Подсветить ячейки, куда можно положить взятую карточку; пустой список — снять подсветку.
  /// </summary>
  public void ShowAvailable(IEnumerable<Cell> cells)
  {
    _available.Clear();
    _available.UnionWith(cells);
    Changed?.Invoke();
  }

  /// <summary>
  /// Подсветить ячейку под перетаскиваемой карточкой: зелёным — можно положить, красным — нельзя.
  /// </summary>
  public void Highlight(Cell? cell, bool allowed)
  {
    (Cell, bool)? hover = cell is { } c ? (c, allowed) : null;
    if (hover == Hover)
      return;
    Hover = hover;
    Changed?.Invoke();
  }

  internal void Register(PersonalHqView view) => _views.Add(view);

  internal void Unregister(PersonalHqView view) => _views.Remove(view);

  internal void RaiseFocus(Player player) => FocusRequested?.Invoke(player);
}
