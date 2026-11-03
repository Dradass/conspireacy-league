using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Field;

/// <summary>
/// Действие основной теории, которое сработало после применения дочерней карточки и требует выбора
/// (карта с руки, теория или карточка на поле). Выбирает владелец основной теории.
/// </summary>
public sealed class TheoryTrigger
{
  private readonly GameContext _context;

  /// <param name="parent">Карточка основной теории.</param>
  /// <param name="context">Контекст для владельца основной теории.</param>
  public TheoryTrigger(OwnedTheoryCard parent, GameContext context)
  {
    Parent = parent;
    _context = context;
  }

  /// <summary>
  /// Карточка основной теории, чьё действие срабатывает.
  /// </summary>
  public OwnedTheoryCard Parent { get; }

  /// <summary>
  /// Владелец основной теории — тот, кто выбирает.
  /// </summary>
  public Player Owner => Parent.Owner;

  /// <summary>
  /// Действие основной теории.
  /// </summary>
  public Effect Effect => Parent.Card.OnPlayFromHand;

  /// <summary>
  /// Выбранная карточка в руке владельца.
  /// </summary>
  public Card? HandCard { get; internal set; }

  /// <summary>
  /// Выбранная теория.
  /// </summary>
  public TheoryColumn? Theory { get; internal set; }

  /// <summary>
  /// Выбранная карточка на поле.
  /// </summary>
  public Cell? Cell { get; internal set; }

  /// <summary>
  /// Все нужные выборы сделаны.
  /// </summary>
  public bool IsComplete =>
    (!Effect.RequiresHandCard || HandCard is not null)
    && (!Effect.RequiresTheory || Theory is not null)
    && (!Effect.RequiresTargetCell || Cell is not null);

  /// <summary>
  /// Контекст действия для владельца с уже выбранными целями.
  /// </summary>
  public GameContext Context
  {
    get
    {
      var context = _context;
      if (HandCard is { } card)
        context = context.WithHandCard(card);
      if (Theory is { } theory)
        context = context.WithTargetTheory(theory);
      if (Cell is { } cell)
        context = context.WithTargetCell(cell);
      return context;
    }
  }
}
