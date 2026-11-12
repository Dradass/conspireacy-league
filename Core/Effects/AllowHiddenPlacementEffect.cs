using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Действие Спецагента при выкладывании в штаб теории: пока он лежит в этом штабе открыто, его владелец может
/// выкладывать в эту теорию персонажей с руки взакрытую (<see cref="Field.PlayingField.CheckHiddenFromHand"/>).
/// Само выкладывание ничего не делает — право проверяет поле по картам, лежащим в штабе. Если Спецагента
/// закроют, уберут или заберут, право пропадает.
/// </summary>
public sealed class AllowHiddenPlacementEffect : Effect
{
  public override string Description =>
    "Пока Спецагент лежит в этом штабе открыто, вы можете выкладывать сюда персонажей с руки взакрытую";

  public override void Execute(GameContext context)
  {
  }
}
