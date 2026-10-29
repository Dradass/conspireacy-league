namespace ConspiracyLeague.Core.Field;

/// <summary>
/// Размеры поля и расположение зон.
/// </summary>
public static class FieldLayout
{
  /// <summary>
  /// Количество столбцов поля.
  /// </summary>
  public const int Columns = 50;

  /// <summary>
  /// Количество строк поля.
  /// </summary>
  public const int Rows = 50;

  private const int PlayerZoneWidth = 7;
  private const int PlayerZoneHeight = 3;
  private const int PlayerZoneGap = 3;
  private const int MarketZoneWidth = 8 + 5;
  private const int MarketZoneHeight = 3;
  private const int MarketZoneGap = 1;

  /// <summary>
  /// Верхняя строка рабочих зон игроков: над рынком, через пустой ряд от него.
  /// </summary>
  private const int PlayerZoneTop = Rows - MarketZoneHeight - MarketZoneGap - PlayerZoneHeight;

  /// <summary>
  /// Сколько рядов общей зоны остаётся свободными над самой высокой карточкой; столько же рядов у пустой общей зоны.
  /// </summary>
  public const int CommonZoneFreeRows = 2;

  /// <summary>
  /// На сколько столбцов общая зона расширяется с каждой стороны, когда во всех её столбцах есть карточки.
  /// </summary>
  public const int CommonZoneSideGrowth = 2;

  /// <summary>
  /// Сколько рабочих зон игроков помещается в ряд по ширине поля.
  /// </summary>
  public const int MaxPlayers = (Columns + PlayerZoneGap) / (PlayerZoneWidth + PlayerZoneGap);

  /// <summary>
  /// Начальная общая зона всех игроков: над рабочими зонами игроков, от левого края зоны первого игрока
  /// до правого края зоны последнего, высотой <see cref="CommonZoneFreeRows"/> ряда. Растёт по мере
  /// выкладывания карточек (<see cref="PlayingField.CommonZone"/>). Между ней и зонами игроков остаётся
  /// один ряд — там подписаны итоги теорий.
  /// </summary>
  /// <param name="playerCount">Количество игроков.</param>
  public static Zone CommonZone(int playerCount)
  {
    var first = PlayerZone(0, playerCount);
    var last = PlayerZone(playerCount - 1, playerCount);
    var bottomRow = PlayerZoneTop - 2;
    return new Zone(
      first.Left,
      bottomRow - CommonZoneFreeRows + 1,
      last.Left + last.Width - first.Left,
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
