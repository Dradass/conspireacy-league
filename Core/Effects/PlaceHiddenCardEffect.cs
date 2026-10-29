using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Выложить персонажа с руки взакрытую: игрок кладёт выбранную карточку (<see cref="GameContext.HandCard"/>)
/// сверху стопки выбранной теории (<see cref="GameContext.TargetTheory"/>) рубашкой вверх.
/// Закрытая карточка (<see cref="HiddenCard"/>) считается гражданином без теории, поэтому за неё платят,
/// как за присоединение гражданина без теории, — суммарное влияние стопки. После этого игрок получает
/// <see cref="BonusPerHiddenCard"/> за каждого своего закрытого персонажа на поле, включая только что выложенного.
/// </summary>
public sealed class PlaceHiddenCardEffect : Effect
{
  /// <summary>
  /// Сколько $ игрок получает за каждого своего закрытого персонажа на поле.
  /// </summary>
  public const int BonusPerHiddenCard = 2;

  public override string Description =>
    $"Выложить персонажа взакрытую и получить {BonusPerHiddenCard}$ за каждого своего закрытого персонажа на поле";

  public override bool RequiresTheory => true;

  public override bool RequiresHandCard => true;

  public override void Execute(GameContext context)
  {
    var card = context.HandCard ?? throw new InvalidOperationException("Не выбрана карточка в руке.");
    var theory = context.TargetTheory ?? throw new InvalidOperationException("Не выбрана теория, куда выложить карточку.");
    var player = context.CurrentPlayer;
    if (!player.Hand.Contains(card))
      throw new InvalidOperationException($"Карточки «{card}» нет в руке игрока {player}.");

    context.Field.PlaceHidden(card, theory, player);
    player.Hand.Remove(card);
    player.Earn(BonusPerHiddenCard * context.Field.HiddenCardsOf(player).Count());
  }
}
