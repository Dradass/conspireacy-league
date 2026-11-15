using System.Net;
using System.Security.Cryptography.X509Certificates;
using ConspiracyLeague.Core.Logging;
using ConspiracyLeague.Core.Net;
using ConspiracyLeague.Core.Protocol;
using ConspiracyLeague.Core.Server;

namespace ConspiracyLeague.Server;

/// <summary>
/// Отдельный сервер стола для игры через интернет.
/// </summary>
/// <remarks>
/// Запуск:
/// <code>
/// ConspiracyLeagueServer --port 7777 --cert server.pfx --turn-seconds 90
/// </code>
/// Пароль сертификата берётся из переменной окружения CL_CERT_PASSWORD (не из командной строки: её видят другие
/// процессы). С сертификатом соединения шифруются (wss://); если сертификат самоподписанный, сервер печатает его
/// отпечаток — игроки вводят его при подключении. Без сертификата сервер работает только по ws:// и предупреждает,
/// что это годится лишь для локальной сети.
/// </remarks>
public static class Program
{
  public static async Task<int> Main(string[] args)
  {
    // Иначе в консоли Windows кириллица превращается в мусор.
    Console.OutputEncoding = System.Text.Encoding.UTF8;
    Options options;
    try
    {
      options = Options.Parse(args);
    }
    catch (ArgumentException error)
    {
      Console.Error.WriteLine(error.Message);
      Console.Error.WriteLine(Options.Usage);
      return 2;
    }

    if (options.Help)
    {
      Console.WriteLine(Options.Usage);
      return 0;
    }

    X509Certificate2? certificate = null;
    if (options.CertificatePath is { } path)
    {
      try
      {
        certificate = new X509Certificate2(path, Environment.GetEnvironmentVariable("CL_CERT_PASSWORD"), X509KeyStorageFlags.EphemeralKeySet);
      }
      catch (Exception error) when (error is System.Security.Cryptography.CryptographicException or IOException)
      {
        Console.Error.WriteLine($"Не удалось прочитать сертификат «{path}»: {error.Message}");
        return 1;
      }
    }

    // Журнал партии на сервере полный — со скрытым: к серверу у игроков доступа нет.
    var logDirectory = Path.GetFullPath(options.LogDirectory);
    var serverOptions = new WebSocketServerOptions
    {
      Certificate = certificate,
      RecycleWhenEmptyAfter = TimeSpan.FromMinutes(1),
      Table = new TableServerOptions
      {
        TurnTimeout = TimeSpan.FromSeconds(options.TurnSeconds),
        ActionLog = () =>
        {
          var (log, path) = GameActionLog.OpenFile(logDirectory, "game", revealHidden: true);
          Console.WriteLine($"{DateTime.Now:HH:mm:ss} Журнал партии: {path}");
          return log;
        },
      },
    };

    WebSocketTableServer server;
    try
    {
      server = WebSocketTableServer.Start(new IPEndPoint(options.Bind, options.Port), serverOptions);
    }
    catch (System.Net.Sockets.SocketException error)
    {
      Console.Error.WriteLine($"Не удалось открыть порт {options.Port}: {error.Message}");
      return 1;
    }

    await using (server)
    {
      server.Log += text => Console.WriteLine($"{DateTime.Now:HH:mm:ss} {text}");
      var scheme = certificate is null ? "ws" : "wss";
      Console.WriteLine($"Сервер стола слушает {scheme}://{options.Bind}:{server.Port}/");
      if (certificate is null)
        Console.WriteLine("ВНИМАНИЕ: сертификат не задан — соединения не шифруются. Для игры через интернет запустите с --cert.");
      else
        Console.WriteLine($"Отпечаток сертификата (SHA-256): {WebSocketLink.Fingerprint(certificate)}");
      Console.WriteLine("Остановить — Ctrl+C.");

      using var stop = new CancellationTokenSource();
      Console.CancelKeyPress += (_, e) =>
      {
        e.Cancel = true;
        stop.Cancel();
      };
      try
      {
        await Task.Delay(Timeout.Infinite, stop.Token);
      }
      catch (OperationCanceledException)
      {
      }

      Console.WriteLine("Сервер останавливается.");
    }

    certificate?.Dispose();
    return 0;
  }

  private sealed record Options(int Port, IPAddress Bind, string? CertificatePath, int TurnSeconds, string LogDirectory, bool Help)
  {
    public const string Usage =
      "Параметры: [--port 7777] [--bind 0.0.0.0] [--cert файл.pfx] [--turn-seconds 90] [--logs папка]\n" +
      "--turn-seconds — время хода по умолчанию (0 — без ограничения); создатель стола может выбрать своё в лобби.\n" +
      "Пароль сертификата — в переменной окружения CL_CERT_PASSWORD.";

    public static Options Parse(string[] args)
    {
      var options = new Options(7777, IPAddress.Any, null, 90, "logs", false);
      for (var i = 0; i < args.Length; i++)
      {
        string Value() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Нет значения для {args[i]}.");
        options = args[i] switch
        {
          "--port" => options with { Port = int.TryParse(Value(), out var port) && port is > 0 and < 65536 ? port : throw new ArgumentException("Неверный порт.") },
          "--bind" => options with { Bind = IPAddress.TryParse(Value(), out var bind) ? bind : throw new ArgumentException("Неверный адрес.") },
          "--cert" => options with { CertificatePath = Value() },
          "--logs" => options with { LogDirectory = Value() },
          "--turn-seconds" => options with { TurnSeconds = int.TryParse(Value(), out var seconds) && seconds is >= 0 and <= LobbyStart.MaxTurnSeconds ? seconds : throw new ArgumentException("Время хода — от 0 (без ограничения) до 3600 секунд.") },
          "--help" or "-h" => options with { Help = true },
          _ => throw new ArgumentException($"Неизвестный параметр {args[i]}."),
        };
      }

      return options;
    }
  }
}
