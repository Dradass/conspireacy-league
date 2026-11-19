using System.Text.RegularExpressions;
using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Decks;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Server;
using ConspiracyLeague.Core.Theories;
using ConspiracyLeague.Core.Views;

namespace ConspiracyLeague.Core.Tests;

public sealed class CardFaceTests
{
  [Test]
  public void EveryMarketCard_CanBeRecreatedFromItsFace()
  {
    var (deck, reserve) = MarketDeck.CreateWithReserve(4, new Random(1));
    foreach (var card in deck.Concat(reserve))
    {
      var face = CardFaces.Of(card);
      var copy = CardFaces.Create(face);
      Assert.That(copy.GetType(), Is.EqualTo(card.GetType()), card.Name);
      Assert.That(CardFaces.Of(copy), Is.EqualTo(face), card.Name);
    }
  }

  [Test]
  public void EveryTheoryCard_CanBeRecreatedFromItsFace()
  {
    var created = 0;
    foreach (var theory in TheoryCatalog.All)
    {
      if (Cards.Theories.TheoryCardCatalog.Create(theory) is not { } card)
        continue;
      Assert.That(CardFaces.Of(CardFaces.Create(CardFaces.Of(card))), Is.EqualTo(CardFaces.Of(card)), card.Name);
      created++;
    }

    Assert.That(created, Is.GreaterThan(10));
  }

  [Test]
  public void HiddenCard_HasNoFace() =>
    Assert.Throws<ArgumentException>(() => CardFaces.Of(new HiddenCard(new CitizenCard())));

  [Test]
  public void UnknownTheory_IsRejected() =>
    Assert.Throws<ArgumentException>(() => CardFaces.Create(new CardFace(CardKind.Citizen, "Гражданин", "Нет такой", null, 1, 1, 50)));
}

public sealed class PlayerViewTests
{
  /// <summary>
  /// Закрытую карту видят только её владелец, тот, кто в неё посмотрел, и все — после раскрытия.
  /// </summary>
  [Test]
  public void HiddenCardFace_IsVisibleOnlyToThoseWhoKnowIt()
  {
    var session = new GameSession(
      [new Player("А", PlayerColor.Red, 100), new Player("Б", PlayerColor.Blue, 100), new Player("В", PlayerColor.Green, 100)],
      new Random(3));
    session.Start();
    var (a, b, c) = (session.Players[0], session.Players[1], session.Players[2]);

    // А основывает теорию, Б кладёт в неё карточку взакрытую.
    var (founder, cell) = a.Hand.Cards
      .Where(card => card.Theory is { IsChild: false })
      .SelectMany(card => session.Field.CommonZone.Cells.Select(cell => (Card: card, Cell: cell)))
      .First(pair => session.Field.Check(pair.Card, pair.Cell, a).IsAllowed);
    session.Field.Place(founder, cell, a);
    var column = session.Field.ColumnAt(cell)!;
    var secret = b.Hand.Cards[0];
    session.Field.PlaceHidden(secret, column, b);
    var hidden = (HiddenCard)column.Members[^1].Card;
    var hiddenCell = column.Members[^1].Cell;
    var secretId = session.CardIds.IdOf(secret);

    CardFace? FaceFor(int? seat) => PlayerViews.For(session, seat).Cards.Single(card => card.Cell == hiddenCell).Card.Face;
    bool LeaksId(int? seat) => ContainsId(ViewSerializer.Serialize(PlayerViews.For(session, seat)), secretId);

    Assert.Multiple(() =>
    {
      Assert.That(FaceFor(1), Is.EqualTo(CardFaces.Of(secret)), "Владелец видит свою закрытую карту.");
      Assert.That(FaceFor(0), Is.Null);
      Assert.That(FaceFor(2), Is.Null);
      Assert.That(FaceFor(null), Is.Null);
      Assert.That(LeaksId(0) || LeaksId(2) || LeaksId(null), Is.False, "Идентификатор настоящей карточки утёк.");
    });

    hidden.ShowTo(c);
    Assert.That(FaceFor(2), Is.EqualTo(CardFaces.Of(secret)), "Подсмотревший видит карту.");
    Assert.That(FaceFor(0), Is.Null, "Остальные — нет.");

    hidden.Reveal();
    Assert.That(FaceFor(0), Is.EqualTo(CardFaces.Of(secret)), "Раскрытую карту видят все.");
    Assert.That(FaceFor(null), Is.EqualTo(CardFaces.Of(secret)));
  }

  [Test]
  public void View_SurvivesJsonRoundTrip()
  {
    var game = new NetworkBotGame(4, GameMode.Money, 7, checkRejectedAreClean: false);
    var json = ViewSerializer.Serialize(game.Server.ViewFor(0));
    Assert.That(ViewSerializer.Serialize(ViewSerializer.Deserialize(json)), Is.EqualTo(json));
  }

  [TestCase("")]
  [TestCase("[]")]
  [TestCase("""{"viewer":0,"lol":1}""")]
  public void Deserialize_RejectsMalformedMessages(string json) =>
    Assert.Throws<FormatException>(() => ViewSerializer.Deserialize(json));

  /// <summary>
  /// Партии ботов: на каждом шаге вид для каждого игрока и для зрителя не содержит того, что этому игроку знать нельзя.
  /// </summary>
  [TestCase(GameMode.Money, 4, 71)]
  [TestCase(GameMode.TheoryDevelopment, 3, 72)]
  [TestCase(GameMode.Race, 4, 73)]
  [TestCase(GameMode.TheoryDevelopment, 4, 74)]
  public void Views_NeverLeakHiddenInformation(GameMode mode, int players, int seed)
  {
    var checks = 0;
    var hiddenSeen = 0;
    var game = new NetworkBotGame(players, mode, seed, checkRejectedAreClean: false)
    {
      BeforeStep = current =>
      {
        foreach (var seat in Enumerable.Range(0, players).Cast<int?>().Append(null))
        {
          CheckView(current.Server, seat, ref hiddenSeen);
          checks++;
        }
      },
    };

    game.Play();

    TestContext.WriteLine($"Проверено видов: {checks}, закрытых карт на поле встречено: {hiddenSeen}.");
    Assert.That(checks, Is.GreaterThan(100));
  }

  private static void CheckView(ServerGame server, int? seat, ref int hiddenSeen)
  {
    var session = server.Session;
    var viewer = seat is { } index ? session.Players[index] : null;
    var view = server.ViewFor(seat);
    var json = ViewSerializer.Serialize(view);
    Assert.That(ViewSerializer.Serialize(ViewSerializer.Deserialize(json)), Is.EqualTo(json), "Вид не пережил JSON.");

    // Чужие руки: ни одного идентификатора чужой карточки в руке.
    foreach (var player in session.Players.Where(player => player != viewer))
    {
      foreach (var card in player.Hand.Cards)
        Assert.That(ContainsId(json, session.CardIds.IdOf(card)), Is.False, $"Утекла карточка из руки {player}.");
    }

    // Своя рука — целиком, у зрителя — пустая; чужой доход не виден.
    Assert.That(view.Hand.Select(card => card.Id), Is.EqualTo(viewer?.Hand.Cards.Select(session.CardIds.IdOf) ?? []));
    foreach (var info in view.Players)
      Assert.That(info.EarnedFrom is not null, Is.EqualTo(info.Seat == seat), "Источники дохода видны не тому.");

    // Закрытые карты: лицевую сторону знают только владелец, подсмотревшие, владелец «Птиц-дронов» и все после раскрытия.
    foreach (var column in session.Field.TheoryColumns)
    {
      foreach (var member in column.Members)
      {
        if (member.Card is not HiddenCard hidden)
          continue;
        hiddenSeen++;
        var known = hidden.IsRevealed
          || viewer is not null && (viewer == member.Owner || hidden.IsKnownBy(viewer) || session.Field.CanSeeAllHidden(viewer));
        var shown = view.Cards.Single(card => card.Cell == member.Cell).Card;
        Assert.That(shown.Hidden, Is.True);
        Assert.That(shown.Face is not null, Is.EqualTo(known), "Лицевая сторона закрытой карты видна не тому.");
        if (!known)
          Assert.That(ContainsId(json, session.CardIds.IdOf(hidden.Face)), Is.False, "Утёк идентификатор закрытой карты.");
      }
    }

    // Подходящие цели и выбранная карточка в руке — только тому, кто выбирает.
    if (view.PendingPlacement is { } placement)
      Assert.That(placement.AllowedTargets is not null, Is.EqualTo(viewer == session.CurrentPlayer));
    if (view.PendingTrigger is { } trigger && viewer != session.PendingTheoryTrigger!.Owner)
    {
      Assert.That(trigger.HandCardId, Is.Null);
      Assert.That(trigger.AllowedHandCards, Is.Null);
      Assert.That(trigger.AllowedTheories, Is.Null);
      Assert.That(trigger.AllowedCells, Is.Null);
    }

    Assert.That(view.Market.DeckCount, Is.EqualTo(session.Market.DeckCount));
    Assert.That(json.Length, Is.LessThan(ViewSerializer.MaxMessageBytes / 4));
  }

  /// <summary>
  /// Есть ли в JSON число <paramref name="id"/> как отдельное значение.
  /// </summary>
  private static bool ContainsId(string json, int id) => Regex.IsMatch(json, $@"(?<![\d-]){id}(?!\d)");
}
