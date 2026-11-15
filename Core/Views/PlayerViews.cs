using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Views;

/// <summary>
/// Собирает вид партии для игрока (<see cref="PlayerView"/>) — единственное, что сервер отправляет клиентам о партии.
/// </summary>
/// <remarks>
/// Всё скрытое здесь отсекается по правилам: чужие руки (только число карточек), лицевые стороны закрытых карт
/// (кроме своих, подсмотренных и раскрытых), колода и резерв рынка (только число), источники чужого дохода.
/// Сюда никогда не попадают ключ случайности и журнал команд. Ничего не меняет в партии, кроме выдачи
/// идентификаторов показанным карточкам.
/// </remarks>
public static class PlayerViews
{
  /// <summary>
  /// Вид партии для игрока на месте <paramref name="viewer"/> или для зрителя (<c>null</c>).
  /// </summary>
  /// <param name="session">Партия.</param>
  /// <param name="viewer">Место игрока или <c>null</c> — зритель.</param>
  /// <param name="version">Версия партии на сервере.</param>
  public static PlayerView For(GameSession session, int? viewer, long version = 0)
  {
    ArgumentNullException.ThrowIfNull(session);
    var players = session.Players;
    if (viewer is { } index && (index < 0 || index >= players.Count))
      throw new ArgumentOutOfRangeException(nameof(viewer), "Такого игрока нет за столом.");

    return new Builder(session, viewer is { } seat ? players[seat] : null).Build(viewer, version);
  }

  private sealed class Builder(GameSession session, Player? viewer)
  {
    private readonly Dictionary<Player, int> _seats = session.Players.Select((player, index) => (player, index)).ToDictionary();

    private PlayingField Field => session.Field;

    public PlayerView Build(int? seat, long version) => new(
      seat,
      version,
      session.Mode,
      session.Phase,
      session.Round,
      session.IsLastRound,
      Seat(session.CurrentPlayer),
      Seat(session.Turns.FirstPlayer),
      session.ActionsLeft,
      new ZoneView(Field.CommonZone.Left, Field.CommonZone.Top, Field.CommonZone.Width, Field.CommonZone.Height),
      Field.FailureConsequences,
      session.Players.Select(PlayerInfo).ToList(),
      viewer is null ? [] : viewer.Hand.Cards.Select(Open).ToList(),
      FieldCards().ToList(),
      Field.TheoryColumns.Select(Theory).ToList(),
      Field.TheoryCards.Select(owned => new TheoryCardView(Open(owned.Card), Seat(owned.Owner), owned.Cell, owned.IsUsed, owned.AttachedTo)).ToList(),
      new MarketView(
        session.Market.OpenCards.OrderBy(pair => pair.Key.Row).ThenBy(pair => pair.Key.Column)
          .Select(pair => new MarketCardView(pair.Key, Open(pair.Value))).ToList(),
        session.Market.DeckCount,
        session.Market.ReserveCount),
      session.PendingCheck is { } check ? new SuccessCheckView(check.Cell, check.Chance, check.Die, check.Succeeded) : null,
      Placement(),
      Trigger());

    private int Seat(Player player) => _seats[player];

    private PlayerInfoView PlayerInfo(Player player) => new(
      Seat(player),
      player.Name,
      player.Color,
      player.IsBot,
      player.BotLevel,
      player.Money,
      player.Hand.Cards.Count,
      session.Turns.HasPassed(player),
      player == viewer ? new Dictionary<string, int>(player.EarnedFrom) : null);

    /// <summary>
    /// Открытая карточка — её видят все.
    /// </summary>
    private CardView Open(Card card) => new(session.CardIds.IdOf(card), false, CardFaces.Of(card));

    /// <summary>
    /// Карточка на поле. Лицевую сторону закрытой карты видят владелец, те, кто в неё посмотрел, владелец карточки
    /// теории, которая показывает все закрытые карты (<see cref="PlayingField.CanSeeAllHidden"/>), и все — после раскрытия.
    /// </summary>
    private CardView OnField(Card card, Player owner)
    {
      if (card is not HiddenCard hidden)
        return Open(card);

      var known = hidden.IsRevealed || viewer is not null && (viewer == owner || hidden.IsKnownBy(viewer) || Field.CanSeeAllHidden(viewer));
      return new CardView(session.CardIds.IdOf(hidden), true, known ? CardFaces.Of(hidden.Face) : null);
    }

    private IEnumerable<FieldCardView> FieldCards()
    {
      foreach (var (player, zone) in Field.PlayerZones)
      {
        foreach (var cell in zone.Cells)
        {
          if (Field.CardAt(cell) is { } card)
            yield return FieldCard(cell, card, player, null);
        }
      }

      foreach (var column in Field.TheoryColumns)
      {
        foreach (var member in column.Members)
          yield return FieldCard(member.Cell, member.Card, member.Owner, column.Theory.Name);
      }
    }

    private FieldCardView FieldCard(Cell cell, Card card, Player owner, string? theory) => new(
      cell,
      OnField(card, owner),
      Seat(owner),
      theory,
      card is PersonCard person ? person.Influence : 0,
      Field.SuccessChanceAt(cell),
      Field.TrainedBonusAt(cell),
      Field.IsUsed(cell),
      Field.IsRotated(cell),
      Field.AdeptsAt(cell),
      Field.Chips.TryGetValue(cell, out var chipper) ? Seat(chipper) : null,
      Field.Wheels.TryGetValue(cell, out var wheel) ? Seat(wheel) : null,
      Field.IsFoiled(cell),
      card is HiddenCard { IsRevealed: true });

    private TheoryView Theory(TheoryColumn column) => new(
      column.Theory.Name,
      column.Theory.Parent?.Name,
      Seat(column.Owner),
      Seat(column.Founder),
      column.IsClosed,
      column.IsHeadquarters,
      column.IsProtected(),
      column.AdeptsShielded,
      column.Adepts,
      column.Influence,
      column.Leaders,
      column.IsInsolvent,
      column.Cells,
      column.LeaderMeeples.Select(Seat).ToList());

    private PlacementView? Placement()
    {
      if (session.PendingPlacement is not { } choice)
        return null;

      var effect = choice.Effect;
      var chooser = viewer == session.CurrentPlayer;
      return new PlacementView(
        choice.Cell,
        choice.Theory.Theory.Name,
        effect.Description,
        effect.TargetIsOptional,
        effect.RequiresSecondTargetCell,
        chooser
          ? effect.TargetCells(session.PlacementContext(choice)).Where(target => target.Check.IsAllowed).Select(target => target.Cell).ToList()
          : null);
    }

    private TriggerView? Trigger()
    {
      if (session.PendingTheoryTrigger is not { } trigger)
        return null;

      var effect = trigger.Effect;
      var owner = trigger.Owner;
      var chooser = viewer == owner;
      var handTarget = effect.RequiresHandCard || effect.AcceptsHandCardTarget;
      return new TriggerView(
        session.CardIds.IdOf(trigger.Parent.Card),
        Seat(owner),
        effect.Description,
        effect.RequiresHandCard,
        effect.RequiresTheory,
        effect.RequiresTargetCell,
        trigger.HandCard is not null,
        chooser && trigger.HandCard is { } chosen ? session.CardIds.IdOf(chosen) : null,
        trigger.Theory?.Theory.Name,
        trigger.Cell,
        chooser && handTarget && trigger.HandCard is null
          ? owner.Hand.Cards
            .Where(card => effect.RequiresHandCard || effect.CheckHandCard(card, trigger.Context).IsAllowed)
            .Select(card => session.CardIds.IdOf(card)).ToList()
          : null,
        chooser && effect.RequiresTheory && trigger.Theory is null
          ? Field.TheoryColumns.Where(column => effect.CanTargetTheory(column, owner)).Select(column => column.Theory.Name).ToList()
          : null,
        chooser && effect.RequiresTargetCell && trigger.Cell is null
          ? effect.TargetCells(trigger.Context).Where(target => target.Check.IsAllowed).Select(target => target.Cell).ToList()
          : null);
    }
  }
}
