using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Bots;

/// <summary>
/// Уровень бота.
/// </summary>
public enum BotLevel
{
  /// <summary>
  /// Лёгкий: вкладывается в карточки и личный штаб, копит на сильных персонажей и прежде всего мешает тому, кто впереди.
  /// </summary>
  Easy,

  /// <summary>
  /// Средний: веса подобраны партиями против лёгкого бота.
  /// </summary>
  Medium,

  /// <summary>
  /// Тяжёлый: веса подобраны партиями против среднего бота, отдельно для каждого режима игры.
  /// </summary>
  Hard,
}

/// <summary>
/// Настройки оценки ходов бота (<see cref="Bot"/>): чем отличаются уровни.
/// </summary>
public sealed record BotProfile
{
  /// <summary>
  /// Меньше этой выгоды ход не делается — бот пасует.
  /// </summary>
  public double MinValue { get; init; } = 0.15;

  /// <summary>
  /// Порог для розыгрыша карточки из личного штаба: он не тратит ни денег, ни карточек.
  /// </summary>
  public double MinUseValue { get; init; } = 0.01;

  /// <summary>
  /// Какая доля выгоды выкладывания засчитывается покупке: купленную карточку ещё нужно выложить.
  /// </summary>
  public double BuyFactor { get; init; } = 0.7;

  /// <summary>
  /// Прибавка к быстрому действию: оно не тратит действие хода.
  /// </summary>
  public double QuickBonus { get; init; } = 0.2;

  /// <summary>
  /// С каким весом в сумме учитывается выгода всех соперников.
  /// </summary>
  public double RivalShare { get; init; } = 0.6;

  /// <summary>
  /// Насколько вес соперника зависит от его места: 0 — все соперники равны, больше — сильнее мешаем лидеру.
  /// </summary>
  public double ThreatFocus { get; init; }

  /// <summary>
  /// Какая доля выгоды на $ от карточки, на которую бот копит, становится ценой $.
  /// </summary>
  public double SavingsShare { get; init; } = 0.6;

  /// <summary>
  /// Сколько будущих раундов бот учитывает, оценивая персонажа в личном штабе.
  /// </summary>
  public int UsesCap { get; init; } = 5;

  /// <summary>
  /// Какая доля будущих раундов, по мнению бота, действительно даст розыгрыш персонажа.
  /// </summary>
  public double UsesFactor { get; init; } = 0.8;

  /// <summary>
  /// Прибавка к покупке карточки с теорией за каждого адепта в штабе этой теории, куда бот её подложит.
  /// </summary>
  public double ThemedBuyBonus { get; init; } = 0.12;

  /// <summary>
  /// Больше скольких адептов прибавка <see cref="ThemedBuyBonus"/> не растёт.
  /// </summary>
  public int ThemedBuyAdeptsCap { get; init; } = 20;

  /// <summary>
  /// Множитель цены $ (сверх обычной для режима).
  /// </summary>
  public double MoneyFactor { get; init; } = 1;

  /// <summary>
  /// Множитель цены влияния в своей теории.
  /// </summary>
  public double InfluenceFactor { get; init; } = 1;

  /// <summary>
  /// Множитель цены лидера в своей теории.
  /// </summary>
  public double LeaderFactor { get; init; } = 1;

  /// <summary>
  /// Множитель выгоды от основания новой теории.
  /// </summary>
  public double NewTheoryFactor { get; init; } = 1;

  /// <summary>
  /// Множитель цены адептов.
  /// </summary>
  public double AdeptFactor { get; init; } = 1;

  /// <summary>
  /// Какую долю ценности владения теорией бот не засчитывает, если соперник может перехватить её одной картой
  /// (своей на руке или купленной с рынка). Только в развитии теорий; 0 — угрозу перехвата бот не видит.
  /// </summary>
  public double TakeoverRisk { get; init; }

  /// <summary>
  /// Насколько бот ценит чужую карточку теории по таблице очков за теории (0 — всегда одинаково, 1 — ровно на столько,
  /// сколько очков теряет или получает соперник). У соперника с 5–6 теориями каждая стоит 15–30 очков.
  /// </summary>
  public double TheoryTableDenial { get; init; }

  /// <summary>
  /// Какую долю потерь соседей бот засчитывает, кладя бездомного в личный штаб: каждый сосед теряет 5% успеха
  /// каждый раунд. 0 — только небольшой штраф за каждого соседа.
  /// </summary>
  public double HomelessHarm { get; init; }

  /// <summary>
  /// Насколько точно бот считает выгоду учёного по соседству (0 — грубо по числу соседей, 1 — по +5% успеха
  /// и +1$ каждому соседу, который приносит деньги).
  /// </summary>
  public double ScientistExact { get; init; }

  /// <summary>
  /// Множитель ценности защиты полицейским соседей в личном штабе: чем ценнее соседи и чем больше у соперников
  /// карт, бьющих по персонажам, тем выгоднее класть полицейского рядом с ними. 0 — защиту бот не учитывает.
  /// </summary>
  public double ProtectionValue { get; init; }

  /// <summary>
  /// Какую долю будущих адептов теории бот засчитывает, оценивая свою долю в ней: каждый игрок за раунд приводит
  /// в свою главную теорию столько адептов, сколько в среднем дают карты его личного штаба. Кроме гонки;
  /// 0 — только нынешние адепты.
  /// </summary>
  public double AdeptGrowth { get; init; }

  /// <summary>
  /// Свои настройки для отдельных режимов игры; для режима, которого здесь нет, — эти.
  /// </summary>
  public IReadOnlyDictionary<GameMode, BotProfile>? ByMode { get; init; }

  /// <summary>
  /// Настройки для режима игры.
  /// </summary>
  public BotProfile ForMode(GameMode mode) => ByMode is not null && ByMode.TryGetValue(mode, out var profile) ? profile : this;

  /// <summary>
  /// Сколько карточек в руке не мешает покупать новые.
  /// </summary>
  public int HandLimit { get; init; } = 3;

  /// <summary>
  /// Лёгкий бот: веса подобраны партиями ботов между собой. Вкладывается в карточки и личный штаб, копит
  /// на сильных персонажей, больше учитывает соперников и прежде всего мешает тому, кто впереди. Укрепляет свои теории,
  /// которые соперник может перехватить, и охотнее отбирает теории у того, у кого их много. В личном штабе
  /// кладёт денежных персонажей к учёным, ценных — к полицейскому и подальше от бездомных.
  /// </summary>
  public static BotProfile Easy { get; } = new()
  {
    BuyFactor = 0.85,
    UsesFactor = 1.3,
    UsesCap = 6,
    RivalShare = 1.0,
    SavingsShare = 1.0,
    ThreatFocus = 2,
    TakeoverRisk = 0.5,
    TheoryTableDenial = 1,
    ScientistExact = 0.6,
    HomelessHarm = 0.6,
    ProtectionValue = 1,
  };

  /// <summary>
  /// Средний бот: веса подобраны партиями против лёгкого бота. Ещё охотнее покупает карточки, выше ценит
  /// персонажей в личном штабе и дольше копит на сильных. Охотнее входит в теории, куда владелец каждый раунд приводит
  /// много адептов.
  /// </summary>
  public static BotProfile Medium { get; } = Easy with
  {
    BuyFactor = 1.2,
    UsesFactor = 1.8,
    SavingsShare = 1.6,
    AdeptGrowth = 1.5,
  };

  /// <summary>
  /// Тяжёлый бот: веса подобраны партиями против среднего бота, отдельно для каждого режима игры.
  /// В развитии теорий бережёт деньги и охотнее основывает теории, в гонке деньги ценит намного выше
  /// (они решают ничьи и покупают карточки), в игре на деньги меньше вкладывается в личный штаб, но точнее
  /// расставляет в нём персонажей.
  /// </summary>
  public static BotProfile Hard { get; } = Medium with
  {
    ByMode = new Dictionary<GameMode, BotProfile>
    {
      [GameMode.TheoryDevelopment] = Medium with { MoneyFactor = 1.6, NewTheoryFactor = 1.3 },
      [GameMode.Money] = Medium with { UsesFactor = 1.4, ScientistExact = 1, HomelessHarm = 1 },
      [GameMode.Race] = Medium with { MoneyFactor = 4, InfluenceFactor = 0.6, NewTheoryFactor = 0.5 },
    },
  };

  /// <summary>
  /// Настройки для уровня.
  /// </summary>
  public static BotProfile For(BotLevel level) => level switch
  {
    BotLevel.Medium => Medium,
    BotLevel.Hard => Hard,
    _ => Easy,
  };
}
