using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Выложить персонажа с руки взакрытую: игрок кладёт выбранную карточку (<see cref="GameContext.HandCard"/>)
/// сверху стопки выбранной теории (<see cref="GameContext.TargetTheory"/>) рубашкой вверх.
/// Закрытая карточка (<see cref="HiddenCard"/>) считается гражданином без теории, поэтому за неё платят,
/// как за присоединение гражданина без теории, — суммарное влияние стопки минус <see cref="JoiningDiscount"/>.
/// После этого игрок получает <see cref="BonusPerHiddenCard"/> за каждого своего закрытого персонажа на поле,
/// включая только что выложенного.
/// </summary>
public sealed class PlaceHiddenCardEffect : Effect
{
  /// <summary>
  /// Сколько $ по умолчанию игрок получает за каждого своего закрытого персонажа на поле.
  /// </summary>
  public const int DefaultBonusPerHiddenCard = 1;

  /// <summary>
  /// Скидка, которая делает выкладывание бесплатным.
  /// </summary>
  public const int FreeDiscount = int.MaxValue;

  /// <param name="bonusPerHiddenCard">Сколько $ за каждого своего закрытого персонажа на поле.</param>
  /// <param name="joiningDiscount">Скидка на выкладывание, $.</param>
  public PlaceHiddenCardEffect(int bonusPerHiddenCard = DefaultBonusPerHiddenCard, int joiningDiscount = 0)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(bonusPerHiddenCard);
    ArgumentOutOfRangeException.ThrowIfNegative(joiningDiscount);
    BonusPerHiddenCard = bonusPerHiddenCard;
    JoiningDiscount = joiningDiscount;
  }

  /// <summary>
  /// Сколько $ игрок получает за каждого своего закрытого персонажа на поле.
  /// </summary>
  public int BonusPerHiddenCard { get; }

  /// <summary>
  /// Скидка на выкладывание, $ (<see cref="FreeDiscount"/> — бесплатно).
  /// </summary>
  public int JoiningDiscount { get; }

  public override string Description
  {
    get
    {
      var text = "Выложить персонажа взакрытую";
      if (JoiningDiscount == FreeDiscount)
        text += " бесплатно";
      else if (JoiningDiscount > 0)
        text += $" со скидкой {JoiningDiscount}$";
      if (BonusPerHiddenCard > 0)
        text += $" и получить {BonusPerHiddenCard}$ за каждого своего закрытого персонажа на поле";
      return text;
    }
  }

  public override bool RequiresTheory => true;

  /// <summary>
  /// Персонажа можно положить и в закрытый штаб теории.
  /// </summary>
  public override bool CanTargetTheory(TheoryColumn column, Player player) => true;

  public override bool RequiresHandCard => true;

  public override void Execute(GameContext context)
  {
    var card = context.HandCard ?? throw new InvalidOperationException("Не выбрана карточка в руке.");
    var theory = context.TargetTheory ?? throw new InvalidOperationException("Не выбрана теория, куда выложить карточку.");
    var player = context.CurrentPlayer;
    if (!player.Hand.Contains(card))
      throw new InvalidOperationException($"Карточки «{card}» нет в руке игрока {player}.");

    context.Field.PlaceHidden(card, theory, player, JoiningDiscount);
    player.Hand.Remove(card);
    player.Earn(BonusPerHiddenCard * context.Field.HiddenCardsOf(player).Count());
  }
}
