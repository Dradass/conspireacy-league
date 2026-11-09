using ConspiracyLeague.Core.Field;

namespace ConspiracyLeague.Core.Game;

/// <summary>
/// Победные очки игрока в режиме гонки.
/// </summary>
/// <param name="Player">Игрок.</param>
/// <param name="FinishedTheories">Сколько теорий на финише, где игрок владелец.</param>
/// <param name="FinishPoints">Бонус владельцам теорий на финише: по <see cref="RaceScoring.FinishBonus"/> за каждую.</param>
/// <param name="AdeptPoints">Очки от адептов теорий на финише: каждый лидер получает свою долю, как при делении денег.</param>
public sealed record RaceScore(Player Player, int FinishedTheories, int FinishPoints, int AdeptPoints)
{
  /// <summary>
  /// Все победные очки игрока.
  /// </summary>
  public int Total => FinishPoints + AdeptPoints;
}

/// <summary>
/// Правила режима гонки: где финиш и как считаются очки.
/// </summary>
public static class RaceScoring
{
  /// <summary>
  /// Бонус владельцу каждой теории, которая к концу партии стоит на финише.
  /// </summary>
  public const int FinishBonus = 5;

  /// <summary>
  /// Сколько лидеров нужно штабу теории, чтобы дойти до финиша: 4 + количество игроков.
  /// </summary>
  public static int FinishLeaders(int playerCount) => 4 + playerCount;

  /// <summary>
  /// Сколько лидеров нужно теории, чтобы дойти до финиша. Финишная линия на поле одна: дочерняя теория лежит
  /// на уровне <see cref="PlayingField.SideTheoryLevel"/>-й карточки родительской, поэтому ей нужно на столько же меньше лидеров.
  /// </summary>
  public static int FinishLeaders(TheoryColumn column, int playerCount) =>
    column.IsHeadquarters ? FinishLeaders(playerCount) : FinishLeaders(playerCount) - (PlayingField.SideTheoryLevel - 1);

  /// <summary>
  /// Дошла ли теория до финиша (лидеры — карточки стопки и миплы-лидеры).
  /// </summary>
  public static bool IsFinished(TheoryColumn column, int playerCount) => column.Leaders >= FinishLeaders(column, playerCount);

  /// <summary>
  /// Теории, которые приносят очки: стоят на финише и действующие (открытые, с убеждением не меньше
  /// <see cref="TheoryColumn.ActiveInfluence"/>).
  /// </summary>
  public static IEnumerable<TheoryColumn> ScoringTheories(PlayingField field, int playerCount) =>
    field.TheoryColumns.Where(column =>
      !column.IsClosed && column.Influence >= TheoryColumn.ActiveInfluence && IsFinished(column, playerCount));

  /// <summary>
  /// Очки игрока по состоянию поля.
  /// </summary>
  public static RaceScore Of(Player player, PlayingField field, int playerCount)
  {
    var theories = ScoringTheories(field, playerCount).ToList();
    var finished = theories.Count(column => column.Owner == player);
    var adepts = theories
      .Where(column => column.Adepts > 0)
      .Sum(column => TheoryIncome.Of(column).ByPlayer.Where(income => income.Player == player).Sum(income => income.Amount));
    return new RaceScore(player, finished, finished * FinishBonus, adepts);
  }
}
