namespace ConspiracyLeague.Core.Field;

/// <summary>
/// Размеры поля и расположение зон.
/// </summary>
public static class FieldLayout
{
  /// <summary>
  /// Количество столбцов поля.
  /// </summary>
  public const int Columns = 80;

  /// <summary>
  /// Количество строк поля.
  /// </summary>
  public const int Rows = 50;

  private const int PlayerZoneWidth = 7;
  private const int PlayerZoneHeight = 4;
  private const int PlayerZoneGap = 3;
  // 4 — пустые столбцы слева от открытых карточек (см. Market), по две открытые карточки на игрока в строке.
  private const int MarketZoneWidth = 4 + 2 * MaxPlayers;
  private const int MarketZoneHeight = 3;
  private const int MarketZoneGap = 1;

  /// <summary>
  /// Ряд карточек теорий игрока: сразу под его рабочей зоной (<see cref="PlayingField.TheoryCards"/>).
  /// </summary>
  private const int TheoryCardRowHeight = 1;

  /// <summary>
  /// Верхняя строка рабочих зон игроков: над рынком, через пустой ряд от него и через ряд карточек теорий.
  /// </summary>
  private const int PlayerZoneTop = Rows - MarketZoneHeight - MarketZoneGap - TheoryCardRowHeight - PlayerZoneHeight;

  /// <summary>
  /// Сколько рядов общей зоны остаётся свободными над самой высокой карточкой; столько же рядов у пустой общей зоны.
  /// </summary>
  public const int CommonZoneFreeRows = 2;

  /// <summary>
  /// На сколько столбцов общая зона расширяется с той стороны, где новая теория легла в крайний столбец.
  /// </summary>
  public const int CommonZoneSideGrowth = 2;

  /// <summary>
  /// Сколько рабочих зон игроков помещается в ряд по ширине поля.
  /// </summary>
  public const int MaxPlayers = (Columns + PlayerZoneGap) / (PlayerZoneWidth + PlayerZoneGap);

  /// <summary>
  /// Ширина начальной общей зоны, столбцов.
  /// </summary>
  public const int CommonZoneStartWidth = 5;

  /// <summary>
  /// Начальная общая зона всех игроков: над рабочими зонами игроков, по центру их ряда, шириной
  /// <see cref="CommonZoneStartWidth"/> столбцов, высотой <see cref="CommonZoneFreeRows"/> ряда. Растёт по мере
  /// выкладывания карточек (<see cref="PlayingField.CommonZone"/>). Между ней и зонами игроков остаётся
  /// один ряд — там подписаны итоги теорий.
  /// </summary>
  /// <param name="playerCount">Количество игроков.</param>
  public static Zone CommonZone(int playerCount)
  {
    var first = PlayerZone(0, playerCount);
    var last = PlayerZone(playerCount - 1, playerCount);
    var bottomRow = PlayerZoneTop - 2;
    var span = last.Left + last.Width - first.Left;
    return new Zone(
      first.Left + (span - CommonZoneStartWidth) / 2,
      bottomRow - CommonZoneFreeRows + 1,
      CommonZoneStartWidth,
      CommonZoneFreeRows);
  }

  /// <summary>
  /// Зона рынка: по центру в самом низу поля, под рабочими зонами игроков, через один пустой ряд от них.
  /// </summary>
  public static Zone MarketZone { get; } =
    new((Columns - MarketZoneWidth) / 2, Rows - MarketZoneHeight, MarketZoneWidth, MarketZoneHeight);

  /// <summary>
  /// Рабочая зона (личный штаб) игрока: между общей зоной и рынком. Зоны всех игроков стоят в ряд по центру,
  /// между соседними зонами — пустые столбцы (<see cref="PlayerZoneGap"/>).
  /// </summary>
  /// <param name="index">Номер игрока, с нуля, слева направо.</param>
  /// <param name="playerCount">Количество игроков.</param>
  public static Zone PlayerZone(int index, int playerCount)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(playerCount);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(playerCount, MaxPlayers);
    ArgumentOutOfRangeException.ThrowIfNegative(index);
    ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, playerCount);

    var rowWidth = playerCount * PlayerZoneWidth + (playerCount - 1) * PlayerZoneGap;
    var left = (Columns - rowWidth) / 2 + index * (PlayerZoneWidth + PlayerZoneGap);
    return new Zone(left, PlayerZoneTop, PlayerZoneWidth, PlayerZoneHeight);
  }
}
