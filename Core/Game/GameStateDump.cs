using System.Text.Encodings.Web;
using System.Text.Json;
using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Cards.Professionals;
using ConspiracyLeague.Core.Field;

namespace ConspiracyLeague.Core.Game;

/// <summary>
/// Снимок состояния партии для отладки: в JSON попадают игроки, их деньги и руки, личные штабы, теории с лидерами
/// и адептами, карточки теорий, рынок и незавершённые выборы. Нужен, чтобы разбирать правила по живой партии.
/// </summary>
public static class GameStateDump
{
  /// <summary>
  /// Снимок партии в виде JSON.
  /// </summary>
  /// <param name="session">Партия.</param>
  /// <param name="boardViews">Виды карточек на столе (<see cref="DumpView"/>); пусто — раздел не выводится.</param>
  public static string ToJson(GameSession session, IEnumerable<DumpView>? boardViews = null) =>
    JsonSerializer.Serialize(
      Create(session, boardViews),
      new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

  /// <summary>
  /// Снимок партии как объект.
  /// </summary>
  public static DumpState Create(GameSession session, IEnumerable<DumpView>? boardViews = null)
  {
    var field = session.Field;
    var players = session.Players.Select(player => new DumpPlayer(
      player.Name,
      player.Money,
      player.Hand.Cards.Select(card => card.Theory is { } theory ? $"{card.Name} [{theory.Name}]" : card.Name).ToList())).ToList();

    var zones = field.PlayerZones.SelectMany(pair => pair.Value.Cells
        .Where(cell => field.CardAt(cell) is not null)
        .Select(cell => DescribeCard(field, cell, pair.Key.Name)))
      .ToList();

    var theories = field.TheoryColumns.Select(column => new DumpTheory(
      column.Theory.Name,
      column.Theory.Parent?.Name,
      column.Owner.Name,
      column.IsClosed,
      column.IsHeadquarters,
      column.Adepts,
      column.Influence,
      column.Leaders,
      column.IsInsolvent,
      column.Members.Select(member => new DumpMember(
        CellText(member.Cell),
        member.Card.Name,
        member.Card.GetType().Name,
        member.Owner.Name,
        member.Card is HiddenCard hidden ? hidden.Face.Name : null,
        member.Card.Influence)).ToList(),
      column.LeaderMeeples.Select(player => player.Name).ToList())).ToList();

    var theoryCards = field.TheoryCards.Select(owned => new DumpTheoryCard(
      owned.Card.Name,
      owned.Owner.Name,
      CellText(owned.Cell),
      owned.IsUsed,
      owned.AttachedTo is { } attached ? CellText(attached) : null)).ToList();

    var market = session.Market.OpenCards.Select(pair => new DumpMarketCard(
      CellText(pair.Key),
      pair.Value.Name,
      pair.Value.Theory?.Name,
      pair.Value.Cost,
      pair.Value.Influence)).ToList();

    var views = (boardViews ?? []).Select(view => new DumpBoardView(
      CellText(new Cell(view.Column, view.Row)),
      view.Name,
      view.Position,
      field.CardAt(new Cell(view.Column, view.Row)) is { } fieldCard ? fieldCard.Name : null)).ToList();

    return new DumpState(
      session.Round,
      session.Phase.ToString(),
      session.CurrentPlayer.Name,
      session.ActionsLeft,
      session.PendingCheck is { } check ? $"{check.Cell}: шанс {check.Chance}%" : null,
      session.PendingPlacement is { } placement ? CellText(placement.Cell) : null,
      session.PendingTheoryTrigger is { } trigger ? $"{trigger.Parent.Card.Name} ({trigger.Owner.Name})" : null,
      players,
      zones,
      theories,
      theoryCards,
      market,
      views);
  }

  private static DumpCard DescribeCard(PlayingField field, Cell cell, string zoneOwner)
  {
    var card = field.CardAt(cell)!;
    var chipper = field.Chips.TryGetValue(cell, out var chip) ? chip.Name : null;
    return new DumpCard(
      CellText(cell),
      zoneOwner,
      card.Name,
      card.Theory?.Name,
      card.GetType().Name,
      card is PersonCard person ? person.Influence : 0,
      card is PersonCard costed ? costed.Cost : 0,
      field.SuccessChanceAt(cell),
      field.IsUsed(cell),
      field.IsRotated(cell),
      field.AdeptsAt(cell),
      chipper);
  }

  private static string CellText(Cell cell) => $"({cell.Column},{cell.Row})";

  /// <summary>
  /// Партия целиком.
  /// </summary>
  public sealed record DumpState(
    int Round,
    string Phase,
    string CurrentPlayer,
    int ActionsLeft,
    string? PendingCheck,
    string? PendingPlacement,
    string? PendingTheoryTrigger,
    IReadOnlyList<DumpPlayer> Players,
    IReadOnlyList<DumpCard> PersonalZones,
    IReadOnlyList<DumpTheory> Theories,
    IReadOnlyList<DumpTheoryCard> TheoryCards,
    IReadOnlyList<DumpMarketCard> Market,
    IReadOnlyList<DumpBoardView> BoardViews);

  /// <summary>
  /// Вид карточки на столе (то, что нарисовано), берётся из доски.
  /// </summary>
  /// <param name="Column">Колонка ячейки, где вид должен стоять.</param>
  /// <param name="Row">Ряд ячейки.</param>
  /// <param name="Name">Название карточки, которую рисует вид.</param>
  /// <param name="Position">Где вид нарисован сейчас, в координатах стола.</param>
  public sealed record DumpView(int Column, int Row, string Name, string Position);

  /// <summary>
  /// Вид на столе и карточка в данных поля в его ячейке: если они расходятся, вид лишний или стоит не там.
  /// </summary>
  public sealed record DumpBoardView(string Cell, string Name, string Position, string? FieldCard);

  /// <summary>
  /// Игрок: деньги и карточки в руке (у карточки с теорией — теория в скобках).
  /// </summary>
  public sealed record DumpPlayer(string Name, int Money, IReadOnlyList<string> Hand);

  /// <summary>
  /// Карточка в личном штабе.
  /// </summary>
  public sealed record DumpCard(
    string Cell,
    string Zone,
    string Name,
    string? Theory,
    string Kind,
    int Influence,
    int Cost,
    int SuccessChance,
    bool Used,
    bool Rotated,
    int Adepts,
    string? ChippedBy);

  /// <summary>
  /// Карточка в теории: её лидер.
  /// </summary>
  public sealed record DumpMember(string Cell, string Name, string Kind, string Owner, string? HiddenFace, int Influence);

  /// <summary>
  /// Теория (штаб или дочерняя).
  /// </summary>
  public sealed record DumpTheory(
    string Name,
    string? Parent,
    string Owner,
    bool Closed,
    bool Headquarters,
    int Adepts,
    int Influence,
    int Leaders,
    bool Insolvent,
    IReadOnlyList<DumpMember> Members,
    IReadOnlyList<string> LeaderMeeples);

  /// <summary>
  /// Карточка теории у владельца.
  /// </summary>
  public sealed record DumpTheoryCard(string Name, string Owner, string Cell, bool Used, string? AttachedTo);

  /// <summary>
  /// Открытая карточка рынка.
  /// </summary>
  public sealed record DumpMarketCard(string Cell, string Name, string? Theory, int Cost, int Influence);
}
