using ConspiracyLeague.Core.Bots;
using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Tests;

/// <summary>
/// Бот играет на состоятельности штабов (<see cref="BotProfile.SolvencyPlay"/>): открывает свой закрытый бездомными
/// штаб и закрывает бездомными сильный штаб соперника.
/// </summary>
public sealed class BotSolvencyTests
{
  private static readonly Theory Theory = TheoryCatalog.SecretGovernment;

  /// <summary>
  /// Партия развития теорий: ходит бот (первый игрок), у всех пустые руки.
  /// </summary>
  private static (GameSession Session, Player Bot, Player Rival) NewSession()
  {
    var players = new[] { "Бот", "Соперник", "Третий" }
      .Select((name, index) => new Player(name, (PlayerColor)index, 30))
      .ToList();
    var session = new GameSession(players, new Random(5));
    session.Start();
    while (session.CurrentPlayer != players[0])
      session.Pass();
    foreach (var player in players)
    {
      foreach (var card in player.Hand.Cards.ToList())
        player.Hand.Remove(card);
    }
    return (session, players[0], players[1]);
  }

  /// <summary>
  /// Основать теорию первой карточкой и положить в неё остальные.
  /// </summary>
  private static TheoryColumn BuildTheory(PlayingField field, params (PersonCard Card, Player Owner)[] members)
  {
    var (first, founder) = members[0];
    var cell = field.AvailableCells(first, founder).First(target => field.CommonZone.Contains(target.Cell) && target.Check.Column is null).Cell;
    Assert.That(field.Place(first, cell, founder).IsAllowed, Is.True);
    var column = field.ColumnOf(Theory)!;
    foreach (var (card, owner) in members.Skip(1))
      Assert.That(field.Place(card, column.Top.Above, owner).IsAllowed, Is.True);
    return column;
  }

  /// <summary>
  /// Бот делает свой ход целиком (оба действия и быстрые розыгрыши).
  /// </summary>
  private static void PlayTurn(GameSession session, Player player)
  {
    var bot = new Bot(player, BotProfile.Ultra);
    for (var step = 0; step < 20 && session.CurrentPlayer == player && session.Phase == GamePhase.PlayerTurns; step++)
    {
      if (bot.RespondToPending(session))
        continue;
      var move = bot.ChooseMove(session);
      bot.Execute(session, move);
      if (move is PassMove)
        break;
    }
  }

  [Test]
  public void ReopensOwnClosedHeadquarters()
  {
    var (session, bot, rival) = NewSession();
    var field = session.Field;
    var column = BuildTheory(field,
      (new CitizenCard(Theory), bot), (new CitizenCard(Theory), bot),
      (new HomelessCard(Theory), rival), (new HomelessCard(Theory), rival), (new HomelessCard(Theory), rival));
    field.AddAdepts(column, 10);
    field.CloseInsolventTheories();
    Assume.That(column.IsClosed, "Бездомных больше — штаб закрыт.");
    Assume.That(column.Owner, Is.EqualTo(bot));

    var citizen = new CitizenCard(Theory);
    bot.Hand.Add(citizen);

    PlayTurn(session, bot);
    Assert.That(column.IsInsolvent, Is.False, "Гражданин пошёл в закрытый штаб: в конце раунда он откроется.");
  }

  [Test]
  public void ClosesStrongRivalHeadquarters()
  {
    var (session, bot, rival) = NewSession();
    var field = session.Field;
    var column = BuildTheory(field,
      (new CitizenCard(Theory), rival), (new CitizenCard(Theory), rival),
      (new HomelessCard(Theory), rival), (new HomelessCard(Theory), rival));
    field.AddAdepts(column, 15);
    Assume.That(column.IsActive && column.Owner == rival);

    bot.Hand.Add(new HomelessCard(Theory));

    PlayTurn(session, bot);
    Assert.That(column.IsInsolvent, Is.True, "Бездомный пошёл в штаб соперника: в конце раунда он закроется.");
  }

  [Test]
  public void DoesNotCloseOwnHeadquarters()
  {
    var (session, bot, _) = NewSession();
    var field = session.Field;
    var column = BuildTheory(field,
      (new CitizenCard(Theory), bot), (new CitizenCard(Theory), bot),
      (new HomelessCard(Theory), bot), (new HomelessCard(Theory), bot));
    field.AddAdepts(column, 15);

    bot.Hand.Add(new HomelessCard(Theory));

    PlayTurn(session, bot);
    Assert.That(column.IsInsolvent, Is.False, "Свой штаб бездомным бот не закрывает.");
  }
}
