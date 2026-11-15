using ConspiracyLeague.Core.Bots;
using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Logging;
using ConspiracyLeague.Core.Server;

namespace ConspiracyLeague.Core.Tests;

public sealed class GameActionLogTests
{
  private static readonly DateTime Clock = new(2026, 11, 13, 12, 0, 0);

  /// <summary>
  /// Партия ботов на сервере: каждое выполненное действие — ровно одна строка журнала, плюс начало раундов и итог.
  /// </summary>
  [TestCase(GameMode.Money, 3, 701)]
  [TestCase(GameMode.TheoryDevelopment, 4, 702)]
  [TestCase(GameMode.Race, 5, 703)]
  public void EveryActionIsLoggedOnce(GameMode mode, int players, int seed)
  {
    var game = ClientMirrorTests.NewGame(players, mode, seed);
    var writer = new StringWriter();
    using var log = new GameActionLog(writer, revealHidden: true, () => Clock);
    log.Attach(game.Session);
    var actions = 0;
    game.Session.ActionTaken += _ => actions++;

    game.RunBots(100_000);
    var lines = writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    Assert.Multiple(() =>
    {
      Assert.That(game.Session.Phase, Is.EqualTo(GamePhase.GameOver));
      Assert.That(actions, Is.EqualTo(game.Log.Count), "Не каждая команда дала ровно одно действие в журнале.");
      Assert.That(lines.Count(line => game.Session.Players.Any(player => line.Contains($" {player.Name}: "))),
        Is.EqualTo(actions + lines.Count(line => line.Contains("автоматический пас"))));
      Assert.That(lines[0], Does.StartWith("2026-11-13 12:00:00 ").And.Contains("Партия: режим"));
      Assert.That(lines, Has.Some.Contains("— Раунд 1. Первым ходит"));
      Assert.That(lines, Has.Some.Contains("Партия завершена. Итог:"));
      Assert.That(lines, Has.Some.Contains("покупает «"));
      Assert.That(lines, Has.Some.Contains("разыгрывает").And.Contains("выпало"));
      Assert.That(lines, Has.Some.Contains(": пасует."));
    });
    TestContext.WriteLine(string.Join(Environment.NewLine, lines.Take(25)));
  }

  /// <summary>
  /// Журнал на компьютере игрока не выдаёт карты, выложенные взакрытую; журнал сервера — пишет их.
  /// </summary>
  [Test]
  public void HiddenCards_OnlyInServerLog()
  {
    // Ищем партию ботов, где карту с руки выкладывают взакрытую или выбирают в руке для основной теории.
    for (var seed = 710; seed < 760; seed++)
    {
      var game = ClientMirrorTests.NewGame(4, (GameMode)(seed % 3), seed);
      var player = new StringWriter();
      var server = new StringWriter();
      using var playerLog = new GameActionLog(player, revealHidden: false);
      using var serverLog = new GameActionLog(server, revealHidden: true);
      playerLog.Attach(game.Session);
      serverLog.Attach(game.Session);
      var secret = new List<string>();
      game.Session.ActionTaken += action =>
      {
        if (action.Kind is PlayerActionKind.PlaceHidden or PlayerActionKind.TriggerHandCard)
          secret.Add(action.Card!.Name);
      };

      game.RunBots(100_000);
      if (secret.Count == 0)
        continue;

      static List<string> Secret(StringWriter writer) =>
        writer.ToString().Split(Environment.NewLine).Where(line => line.Contains("взакрытую") && line.Contains("выкладывает")
          || line.Contains("выбрана карточка в руке")).ToList();
      var playerLines = Secret(player);
      var serverLines = Secret(server);
      TestContext.WriteLine($"Партия {seed}: скрытых действий {secret.Count}.\n{string.Join("\n", serverLines.Take(5))}");
      Assert.Multiple(() =>
      {
        Assert.That(playerLines, Has.Count.EqualTo(secret.Count));
        // У игрока — без названия: «выкладывает карточку взакрытую», «выбрана карточка в руке.».
        Assert.That(playerLines.Select((line, index) => line.Contains($"«{secret[index]}»")), Has.All.False);
        Assert.That(serverLines.Select((line, index) => line.Contains($"«{secret[index]}»")), Has.All.True);
      });
      return;
    }

    Assert.Inconclusive("Ни в одной партии ботов не было скрытых действий с руки.");
  }

  /// <summary>
  /// Сервер стола пишет журнал партии сам: события стола и действия; после сбоя журнал продолжается на пересобранной партии.
  /// </summary>
  [Test]
  public void TableServer_WritesLogAndSurvivesRollback()
  {
    var writer = new StringWriter();
    var now = TimeSpan.Zero;
    using var table = new TableServer(
      new TableServerOptions { BotDelay = TimeSpan.Zero, ActionLog = () => new GameActionLog(writer, revealHidden: true) },
      () => now);
    var connection = new SilentConnection();
    table.Connect(connection);
    table.Receive(connection, Protocol.ProtocolSerializer.Serialize(new Protocol.Hello(Protocol.ProtocolInfo.Version, "Аня")));
    table.Receive(connection, Protocol.ProtocolSerializer.Serialize(new Protocol.LobbyAddBot(BotLevel.Easy)));
    table.Receive(connection, Protocol.ProtocolSerializer.Serialize(new Protocol.LobbyStart(GameMode.Money, true)));
    var game = table.Game!;

    // Человек пасует сразу, бот ходит сам; один ход бота падает — партия откатывается и идёт дальше.
    var faultOnce = true;
    game.FaultInjector = _ =>
    {
      if (faultOnce && game.ActingSeat == 1)
      {
        faultOnce = false;
        throw new InvalidOperationException("Сбой для теста.");
      }
    };
    var seq = 1;
    for (var i = 0; i < 20_000 && game.Session.Phase != GamePhase.GameOver; i++)
    {
      if (game.ActingSeat == 0)
        table.Receive(connection, Protocol.ProtocolSerializer.Serialize(new Protocol.CommandRequest(seq++, new Commands.PassCommand())));
      now += TimeSpan.FromSeconds(1);
      table.Tick();
    }

    var text = writer.ToString();
    Assert.Multiple(() =>
    {
      Assert.That(game.Session.Phase, Is.EqualTo(GamePhase.GameOver));
      Assert.That(faultOnce, Is.False, "Сбой не случился.");
      Assert.That(text, Does.Contain("Сбой: последняя команда отменена"));
      Assert.That(text, Does.Contain("Аня: пасует."));
      Assert.That(text, Does.Contain("Партия началась"));
      Assert.That(text[text.IndexOf("Сбой: последняя", StringComparison.Ordinal)..], Does.Contain("Партия завершена. Итог:"),
        "После сбоя журнал не продолжился.");
    });
  }

  /// <summary>
  /// В тексте о действии у каждой затронутой карточки сказано, чья она и где лежит, у каждой теории — кто её владелец.
  /// В общих текстах (история, события для клиентов) нет скрытого. Сетевая история получает ровно те же тексты.
  /// </summary>
  [TestCase(GameMode.Money, 4, 721)]
  [TestCase(GameMode.TheoryDevelopment, 5, 722)]
  [TestCase(GameMode.Race, 6, 723)]
  public void ActionTexts_NameOwnersAndHideSecrets(GameMode mode, int players, int seed)
  {
    var game = ClientMirrorTests.NewGame(players, mode, seed);
    var texts = new List<(PlayerAction Action, string Text)>();
    game.Session.ActionTaken += action => texts.Add((action, ActionText.Describe(action, revealHidden: false)));
    var events = new List<Views.PlayerActed>();
    for (var step = 0; game.RunBotStep(); step++)
      events.AddRange(game.DrainEvents().OfType<Views.PlayerActed>());

    Assert.Multiple(() =>
    {
      Assert.That(events.Select(acted => acted.Text), Is.EqualTo(texts.Select(item => item.Text)), "Сетевая история отличается от локальной.");
      foreach (var (action, text) in texts)
      {
        var targets = action.Targets ?? Game.ActionTargets.None;
        foreach (var target in new[] { targets.Target, targets.SecondTarget, targets.Borrowed, targets.Copied, targets.AdeptCard })
        {
          if (target?.Owner is { } owner && owner != action.Player)
            Assert.That(text, Does.Contain($"игрока {owner.Name}"), $"Не сказано, чья карточка: {text}");
          if (target?.Theory is { } theory)
            Assert.That(text, Does.Contain($"штаб теории «{theory}»"), $"Не сказано, где карточка: {text}");
        }

        foreach (var theory in (targets.Theories ?? []).Append(targets.SourceTheory).Append(action.Theory).OfType<TheoryRef>())
        {
          if (theory.Owner != action.Player)
            Assert.That(text, Does.Contain($"«{theory.Name}» (владелец — {theory.Owner.Name})"), $"Не сказано, чья теория: {text}");
        }

        if (action.Kind == PlayerActionKind.PlaceHidden)
          Assert.That(text, Does.StartWith("выкладывает карточку взакрытую"));
        if (action.Kind == PlayerActionKind.TriggerHandCard || targets.HandCard is not null)
          Assert.That(text, Does.Not.Contain($"«{(targets.HandCard ?? action.Card)!.Name}»").Or.Contain("разыгрывает"), text);
      }
    });

    foreach (var (action, text) in texts.Where(item => item.Action.Targets?.Borrowed is not null || item.Action.Targets?.Target?.Owner is { } owner && owner != item.Action.Player).Take(6))
      TestContext.WriteLine($"{action.Player.Name}: {text}");
  }

  private sealed class SilentConnection : IConnection
  {
    public void Send(string json)
    {
    }

    public void Close()
    {
    }
  }
}
