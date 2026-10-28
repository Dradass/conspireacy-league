using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Passives;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards;

/// <summary>
/// Базовая карточка.
/// </summary>
public abstract class Card
{
  /// <summary>
  /// Название карточки.
  /// </summary>
  public string Name { get; }

  /// <summary>
  /// Теория, к которой относится карточка. У персон может отсутствовать.
  /// </summary>
  public Theory? Theory { get; }

  /// <summary>
  /// Действие при розыгрыше с руки.
  /// </summary>
  public Effect OnPlayFromHand { get; }

  /// <summary>
  /// Пассивное действие.
  /// </summary>
  public PassiveAbility Passive { get; }

  protected Card(string name, Theory? theory, Effect onPlayFromHand, PassiveAbility? passive = null)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    Name = name;
    Theory = theory;
    OnPlayFromHand = onPlayFromHand;
    Passive = passive ?? PassiveAbility.None;
  }

  /// <summary>
  /// Разыграть карточку с руки.
  /// </summary>
  /// <param name="context">Контекст партии.</param>
  public virtual void PlayFromHand(GameContext context) => OnPlayFromHand.Execute(context);

  /// <summary>
  /// Передать карточке игровое событие для пассивного действия.
  /// </summary>
  /// <param name="gameEvent">Событие.</param>
  /// <param name="context">Контекст партии.</param>
  public void HandleEvent(GameEvent gameEvent, GameContext context) => Passive.Handle(gameEvent, context);

  public override string ToString() => Name;
}
