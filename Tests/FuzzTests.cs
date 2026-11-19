using ConspiracyLeague.Core.Client;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Protocol;
using ConspiracyLeague.Core.Server;
using ConspiracyLeague.Core.Views;

namespace ConspiracyLeague.Core.Tests;

/// <summary>
/// Подделанные сообщения: клиент не должен падать от подменённого сервера, сервер — от подменённого клиента.
/// </summary>
public sealed class FuzzTests
{
  /// <summary>
  /// Настоящие сообщения сервера игроку на месте 0 по ходу партии ботов.
  /// </summary>
  private static List<string> ServerMessagesFor0(GameMode mode, int players, int seed)
  {
    var game = ClientMirrorTests.NewGame(players, mode, seed);
    var messages = new List<string> { ProtocolSerializer.Serialize(new UpdateMessage(game.UpdateFor(0, []))) };
    for (var step = 0; game.RunBotStep(); step++)
    {
      var events = game.DrainEvents();
      if (step % 15 == 0)
        messages.Add(ProtocolSerializer.Serialize(new UpdateMessage(game.UpdateFor(0, events))));
    }

    return messages;
  }

  private static string MirrorJson(GameClient client) =>
    client.Mirror is { } mirror ? ViewSerializer.Serialize(PlayerViews.For(mirror.Session, mirror.Seat, mirror.View.Version)) : "нет";

  /// <summary>
  /// Испорченное сообщение сервера: клиент не падает. Если он сообщил об ошибке, его партия осталась прежней.
  /// Если принял — партия собрана и по ней можно снова собрать вид (интерфейс её нарисует).
  /// </summary>
  [TestCase(GameMode.Money, 3, 401)]
  [TestCase(GameMode.TheoryDevelopment, 4, 402)]
  [TestCase(GameMode.Race, 5, 403)]
  public void Client_SurvivesMutatedServerMessages(GameMode mode, int players, int seed)
  {
    var random = new Random(seed);
    var mutator = new JsonMutator(random);
    var messages = ServerMessagesFor0(mode, players, seed);
    var crashes = new List<string>();
    var rejected = 0;
    var accepted = 0;

    for (var i = 1; i < messages.Count; i++)
    {
      for (var attempt = 0; attempt < 40; attempt++)
      {
        var client = new GameClient(_ => { }, othersAsBots: true);
        client.Receive(messages[i - 1]);
        var before = MirrorJson(client);
        var errors = 0;
        var lastError = string.Empty;
        client.ProtocolError += error =>
        {
          errors++;
          lastError = error;
        };
        var mutated = mutator.Mutate(messages[i], 1 + random.Next(3));
        try
        {
          client.Receive(mutated);
          if (errors > 0)
          {
            rejected++;
            var after = MirrorJson(client);
            if (after != before)
            {
              var at = Enumerable.Range(0, Math.Min(after.Length, before.Length)).FirstOrDefault(k => after[k] != before[k]);
              Assert.Fail($"Партия изменилась, хотя сообщение отклонено ({lastError}):\nбыло …{before[Math.Max(0, at - 150)..Math.Min(before.Length, at + 150)]}\nстало …{after[Math.Max(0, at - 150)..Math.Min(after.Length, at + 150)]}");
            }
          }
          else
          {
            accepted++;
            // Принятая партия годится для показа: по ней собирается вид.
            _ = MirrorJson(client);
          }
        }
        catch (AssertionException)
        {
          throw;
        }
        catch (Exception error)
        {
          crashes.Add($"{error.GetType().Name}: {error.Message}\n{string.Join("\n", error.StackTrace?.Split('\n').Take(4) ?? [])}");
        }
      }
    }

    TestContext.WriteLine($"Отклонено: {rejected}, принято: {accepted}.");
    Assert.That(crashes, Is.Empty, string.Join("\n", crashes.GroupBy(crash => crash).Select(group => $"{group.Count()}× {group.Key}").Take(15)));
  }

  /// <summary>
  /// Подделанные команды от игрока за столом посреди партии: сервер не падает, сбоев (откатов) нет, отклонённая команда
  /// партию не меняет.
  /// </summary>
  [TestCase(GameMode.Money, 3, 411)]
  [TestCase(GameMode.Race, 4, 412)]
  [TestCase(GameMode.TheoryDevelopment, 5, 413)]
  [TestCase(GameMode.Money, 2, 414)]
  public void Server_SurvivesMutatedClientMessages(GameMode mode, int players, int seed)
  {
    var random = new Random(seed);
    var mutator = new JsonMutator(random);
    var game = ClientMirrorTests.NewGame(players, mode, seed, humans: players);
    var faults = 0;
    game.Faulted += (_, _, _) => faults++;
    var host = new GameHost(game);
    var replies = 0;
    host.Outgoing += (_, json) =>
    {
      if (ProtocolSerializer.DeserializeServer(json) is CommandReply)
        replies++;
    };

    // Образцы — настоящие команды людей (ботов по зеркалу), которые сервер принял.
    var link = new LocalLink(host);
    var bots = Enumerable.Range(0, players).Select(seat => new MirrorBot(link.Connect(seat))).ToList();
    var samples = new List<string>();
    var attacks = 0;
    var parsed = 0;
    var accepted = 0;
    for (var step = 0; step < 20_000 && game.Session.Phase != GamePhase.GameOver; step++)
    {
      foreach (var bot in bots)
        bot.Act();
      if (game.Log.Count > samples.Count)
        samples.Add(ProtocolSerializer.Serialize(new CommandRequest(1, game.Log[^1].Command)));

      // Атака: игроки шлют испорченные команды. Они не должны ни ронять сервер, ни менять партию, если отклонены.
      // Команда идёт мимо проверки номеров (иначе сдвинула бы номера честного клиента), но через тот же разбор и проверки.
      if (samples.Count == 0 || step % 2 != 0)
        continue;
      for (var attack = 0; attack < 15; attack++)
      {
        attacks++;
        var seat = random.Next(2) == 0 && game.ActingSeat is { } acting ? acting : random.Next(players);
        // Чаще — свежие образцы: они ссылаются на карточки, которые ещё в игре.
        var sample = samples[random.Next(2) == 0 ? samples.Count - 1 - random.Next(Math.Min(10, samples.Count)) : random.Next(samples.Count)];
        var mutated = mutator.Mutate(sample, 1 + random.Next(2));
        ClientMessage message;
        try
        {
          message = ProtocolSerializer.DeserializeClient(mutated);
        }
        catch (FormatException)
        {
          continue;
        }

        if (message is not CommandRequest { Command: var command })
          continue;
        parsed++;
        var before = GameStateDump.ToJson(game.Session);
        var version = game.Version;
        Commands.CommandResult result = null!;
        Assert.DoesNotThrow(() => result = host.ExecuteForPlayer(seat, command), mutated);
        if (result.Accepted)
          accepted++;
        else
        {
          Assert.That(game.Version, Is.EqualTo(version), "Отклонённая команда изменила версию партии.");
          Assert.That(GameStateDump.ToJson(game.Session), Is.EqualTo(before), $"Отклонённая команда изменила партию: {mutated}");
        }
      }
    }

    Assert.That(faults, Is.Zero, "Подделанная команда прошла проверку и упала при выполнении.");
    Assert.That(game.Session.Phase, Is.EqualTo(GamePhase.GameOver), "Честные игроки не доиграли партию.");
    Assert.That(replies, Is.GreaterThan(0));
    TestContext.WriteLine($"Атак: {attacks}, разобрано команд: {parsed}, принято: {accepted}, записей в журнале: {game.Log.Count}.");
  }

  /// <summary>
  /// Тексты отказов не выдают закрытых карт: игрок нацеливает свои действия на каждую закрытую карту, которой не знает,
  /// и ни в одном отказе нет названия её лицевой стороны (если только это название и так не видно ему на столе).
  /// </summary>
  [TestCase(GameMode.TheoryDevelopment, 4, 431)]
  [TestCase(GameMode.TheoryDevelopment, 6, 432)]
  [TestCase(GameMode.Race, 6, 436)]
  public void Rejections_DoNotRevealHiddenCards(GameMode mode, int players, int seed)
  {
    var game = ClientMirrorTests.NewGame(players, mode, seed);
    var probes = 0;
    var hiddenSeen = 0;
    for (var step = 0; game.RunBotStep(); step++)
    {
      if (step % 5 != 0)
        continue;
      var session = game.Session;
      for (var seat = 0; seat < players; seat++)
      {
        var viewer = session.Players[seat];
        var hidden = session.Field.TheoryColumns.SelectMany(column => column.Members)
          .Where(member => member.Card is Cards.HiddenCard { IsRevealed: false } card && member.Owner != viewer
            && !card.IsKnownBy(viewer) && !session.Field.CanSeeAllHidden(viewer))
          .ToList();
        if (hidden.Count == 0)
          continue;
        hiddenSeen += hidden.Count;
        // JSON экранирует кириллицу — раскодируем, чтобы искать названия как текст.
        var visible = System.Text.RegularExpressions.Regex.Unescape(ViewSerializer.Serialize(game.ViewFor(seat)));

        foreach (var member in hidden)
        {
          var face = ((Cards.HiddenCard)member.Card).Face.Name;
          foreach (var command in Probes(session, viewer, member.Cell))
          {
            probes++;
            try
            {
              game.Processor.Prepare(seat, command);
            }
            catch (Commands.CommandRejectedException rejected)
            {
              if (rejected.Message.Contains(face, StringComparison.OrdinalIgnoreCase))
                Assert.That(visible, Does.Contain(face), $"Отказ выдал закрытую карту «{face}»: {rejected.Message} (команда {command})");
            }
          }
        }
      }
    }

    Assert.That(hiddenSeen, Is.GreaterThan(0), "В партии не было закрытых карт — проверять нечего.");
    TestContext.WriteLine($"Проб: {probes}, закрытых карт: {hiddenSeen}.");
  }

  /// <summary>
  /// Команды игрока, нацеленные на ячейку закрытой карты: действия своих карточек, карточек теорий, выкладывание.
  /// </summary>
  private static IEnumerable<Commands.GameCommand> Probes(GameSession session, Player viewer, Field.Cell target)
  {
    var targets = new Commands.CommandTargets { TargetCell = target };
    var second = new Commands.CommandTargets { TargetCell = target, SecondTargetCell = target };
    foreach (var cell in session.Field.PlayerZones[viewer].Cells.Where(cell => session.Field.CardAt(cell) is not null))
    {
      for (var option = 0; option < 4; option++)
      {
        yield return new Commands.UseCardCommand(cell, option, targets);
        yield return new Commands.UseCardCommand(cell, option, second);
        yield return new Commands.UseCardCommand(cell, option, new Commands.CommandTargets { BorrowedCell = target, TargetCell = target });
      }
    }

    foreach (var owned in session.Field.TheoryCards.Where(owned => owned.Owner == viewer))
      yield return new Commands.UseTheoryCardCommand(session.CardIds.IdOf(owned.Card), targets);
    foreach (var card in viewer.Hand.Cards)
      yield return new Commands.PlaceCommand(session.CardIds.IdOf(card), target);
    yield return new Commands.ResolvePlacementCommand(target);
    yield return new Commands.SelectTriggerCellCommand(target);
  }

  /// <summary>
  /// Сервер стола: любые байты от неизвестного или сидящего за столом клиента не роняют сервер.
  /// </summary>
  [Test]
  public void TableServer_SurvivesGarbage()
  {
    var random = new Random(421);
    var mutator = new JsonMutator(random);
    var now = TimeSpan.Zero;
    var server = new TableServer(new TableServerOptions { MaxViolations = int.MaxValue, RateBurst = int.MaxValue }, () => now);
    var connections = Enumerable.Range(0, 4).Select(_ => new NullConnection()).ToList();
    string[] samples =
    [
      ProtocolSerializer.Serialize(new Hello(ProtocolInfo.Version, "Аня")),
      ProtocolSerializer.Serialize(new Hello(ProtocolInfo.Version, "Боря", "токен")),
      ProtocolSerializer.Serialize(new LobbyAddBot(Bots.BotLevel.Hard)),
      ProtocolSerializer.Serialize(new LobbyRemoveSeat(1)),
      ProtocolSerializer.Serialize(new LobbyStart(GameMode.Money, true)),
      ProtocolSerializer.Serialize(new Pong(5)),
      ProtocolSerializer.Serialize(new SyncRequest()),
      ProtocolSerializer.Serialize(new CommandRequest(3, new Commands.PassCommand())),
    ];

    foreach (var connection in connections)
      server.Connect(connection);
    for (var i = 0; i < 20_000; i++)
    {
      var connection = connections[random.Next(connections.Count)];
      var sample = samples[random.Next(samples.Length)];
      var json = random.Next(3) == 0 ? sample : mutator.Mutate(sample, 1 + random.Next(2));
      Assert.DoesNotThrow(() => server.Receive(connection, json), json);
      if (random.Next(200) == 0)
      {
        server.Disconnect(connection);
        server.Connect(connection);
      }

      now += TimeSpan.FromMilliseconds(5);
      Assert.DoesNotThrow(server.Tick);
    }
  }

  private sealed class NullConnection : IConnection
  {
    public void Send(string json)
    {
    }

    public void Close()
    {
    }
  }
}
