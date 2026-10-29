using ConspiracyLeague.Core.Game;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Цвета игроков на экране.
/// </summary>
public static class PlayerPalette
{
  private const float ZoneDarkening = 0.6f;
  private const float SlotDarkening = 0.5f;

  /// <summary>
  /// Основной цвет игрока: рамка его зоны, имя и баланс.
  /// </summary>
  public static Color MainOf(PlayerColor color) => color switch
  {
    PlayerColor.Red => new Color("e0654f"),
    PlayerColor.Blue => new Color("5f9be0"),
    PlayerColor.Green => new Color("6fcf7a"),
    PlayerColor.Yellow => new Color("e0b84f"),
    PlayerColor.Purple => new Color("a98fe0"),
    _ => throw new ArgumentOutOfRangeException(nameof(color), color, null),
  };

  /// <summary>
  /// Фон зоны игрока.
  /// </summary>
  public static Color ZoneOf(PlayerColor color) => MainOf(color).Darkened(ZoneDarkening);

  /// <summary>
  /// Ячейки зоны игрока.
  /// </summary>
  public static Color SlotOf(PlayerColor color) => MainOf(color).Darkened(SlotDarkening);
}
