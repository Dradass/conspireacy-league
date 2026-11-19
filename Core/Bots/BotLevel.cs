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

  /// <summary>
  /// Ультра: веса подобраны партиями против тяжёлого бота.
  /// </summary>
  Ultra,
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
  /// К скольким своим теориям бот стремится в развитии теорий: очки за теории растут всё быстрее, поэтому каждую
  /// теорию до этой цели он ценит не меньше средней цены теории при таком их числе. 0 — только ближайший шаг.
  /// </summary>
  public int TheoryCountTarget { get; init; }

  /// <summary>
  /// Прибавка к цене адепта в теории, где все лидеры — бота: адептов выгодно сосредоточить в одной своей теории,
  /// там их не делят с соперниками. 0 — без прибавки.
  /// </summary>
  public double FocusBonus { get; init; }

  /// <summary>
  /// Срочность покупки: карточку на рынке могут забрать соперники, а карточка в руке никуда не денется. Когда
  /// соперников, которые ещё ходят и могут её купить, не меньше, чем карточек на рынке, покупка получает эту долю
  /// выгоды выкладывания сверху. В развитии теорий — любая карточка, в гонке — карточка теории, которую бот положит лидером
  /// в её штаб; 0 — бот не торопится.
  /// </summary>
  public double MarketUrgency { get; init; }

  /// <summary>
  /// Какую долю выгоды соперника бот засчитывает себе, если купит с рынка карточку теории, которая уже лежит на поле, и
  /// соперник не сможет положить её лидером в эту теорию. 0 — бот не отбирает карточки.
  /// </summary>
  public double MarketDenial { get; init; }

  /// <summary>
  /// Сколько стоит держать на руке одну карточку без теории про запас, в долях выгоды от входа ею в теорию — лучшего
  /// сейчас, но не меньше <see cref="ReserveFloor"/>: такой карточкой потом можно войти в любую теорию. Последнюю такую
  /// карточку бот не выкладывает в личный штаб или в теорию, если выгода меньше этой, и покупает её про запас, если
  /// на руке нет ни одной. Больше 1 — бот не потратит запас даже на лучшую теорию. 0 — резерв не держит.
  /// </summary>
  public double HandReserve { get; init; }

  /// <summary>
  /// Нижняя граница выгоды от входа в теорию, от которой считается <see cref="HandReserve"/>: даже если сейчас входить
  /// некуда, удобная теория может появиться позже. Запас бот тратит на вход в теорию не дешевле этой выгоды.
  /// </summary>
  public double ReserveFloor { get; init; }

  /// <summary>
  /// Какую долю выгоды владельца чужой теории бот засчитывает себе в минус, когда входит в неё лидером (владелец
  /// получает очки за влияние каждого лидера). 1 — полностью; меньше — бот охотнее входит в чужие теории ради адептов.
  /// </summary>
  public double JoinGiftFactor { get; init; } = 1;

  /// <summary>
  /// План перехвата на два хода: если после этой карты боту не хватает влияния, чтобы стать владельцем чужой теории,
  /// но недостающее он может добрать ещё одной картой (с руки или с рынка на оставшиеся деньги), выгода от будущего
  /// перехвата засчитывается с этой долей. Только в развитии теорий; 0 — бот смотрит на один ход.
  /// </summary>
  public double TakeoverPlan { get; init; }

  /// <summary>
  /// Гонка: насколько бот ценит лидеров в своей главной теории — той, что он доводит до финиша. Каждый лидер там стоит
  /// эту долю от очков на финише, делённых на число лидеров для финиша. 0 — только прогноз гонки на нынешнем поле.
  /// </summary>
  public double RaceFocus { get; init; }

  /// <summary>
  /// Насколько бот боится закрыть свою теорию бездомным: точная проверка состоятельности штаба и потеря влияния
  /// и доли адептов этой теории. 0 — грубая проверка и потеря только нынешних адептов.
  /// </summary>
  public double InsolvencyCare { get; init; }

  /// <summary>
  /// Игра на состоятельности штабов: бот открывает свой закрытый (или вот-вот закрытый) бездомными штаб, добавляя туда
  /// не бездомных, и закрывает бездомными чужие сильные штабы, чтобы их владельцы и лидеры не получили за них очки.
  /// Множитель этой выгоды; 0 — бот видит только, не закроет ли он бездомным свой штаб (<see cref="InsolvencyCare"/>).
  /// </summary>
  public double SolvencyPlay { get; init; }

  /// <summary>
  /// Какую долю выгоды от закрытия бездомными чужого штаба бот засчитывает (<see cref="SolvencyPlay"/>): бездомные
  /// и доплата за вход тратят действия и деньги, а соперник может успеть открыть штаб. Спасение своего штаба и
  /// осторожность со своим — полностью.
  /// </summary>
  public double SabotageFactor { get; init; } = 1;

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
  /// кладёт денежных персонажей к учёным, ценных — к полицейскому и подальше от бездомных. Спешит купить карточки,
  /// которые вот-вот разберут соперники, и охотнее входит в чужие теории с адептами. Планирует перехват чужой теории
  /// на два хода, если одной карты не хватает. В гонке доводит до финиша свою главную теорию, входит в чужие теории,
  /// которые вот-вот финишируют, не копит деньги впустую и не вкладывается в личный штаб в ущерб лидерам. Покупает
  /// карточки теорий, лежащих на поле, если иначе их положит лидером соперник. Держит на руке карточку без теории
  /// про запас, чтобы войти ею в чужую теорию, когда будет выгодно (кроме игры на деньги). В развитии теорий
  /// не переоценивает личный штаб и входит в теории, куда владелец будет приводить адептов.
  /// </summary>
  public static BotProfile Easy { get; } = ByModes(new()
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
    MarketUrgency = 0.6,
    JoinGiftFactor = 0.75,
    TakeoverPlan = 0.3,
    RaceFocus = 2,
    MarketDenial = 0.5,
    HandReserve = 1,
    ReserveFloor = 8,
  });

  /// <summary>
  /// Средний бот: веса подобраны партиями против лёгкого бота. Ещё охотнее покупает карточки, выше ценит
  /// персонажей в личном штабе (кроме развития теорий) и дольше копит на сильных.
  /// </summary>
  public static BotProfile Medium { get; } = ByModes(Easy with
  {
    BuyFactor = 1.2,
    UsesFactor = 1.8,
    SavingsShare = 1.6,
    ByMode = null,
  });

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
      [GameMode.TheoryDevelopment] = Medium.ForMode(GameMode.TheoryDevelopment) with { MoneyFactor = 1.6, NewTheoryFactor = 1.3 },
      [GameMode.Money] = Medium.ForMode(GameMode.Money) with { UsesFactor = 1.4, ScientistExact = 1, HomelessHarm = 1 },
      [GameMode.Race] = Medium.ForMode(GameMode.Race) with { MoneyFactor = 4, InfluenceFactor = 0.6, NewTheoryFactor = 0.5 },
    },
  };

  /// <summary>
  /// Ультра бот: веса подобраны партиями против тяжёлого бота. Сильнее учитывает выгоду соперников, делает и мелкие
  /// выгодные ходы, охотнее покупает карточки своих теорий и меньше копит. В развитии теорий не гонится за основанием
  /// новых теорий, не закрывает свои штабы
  /// бездомными и не дарит очки владельцам чужих теорий; в гонке упорнее доводит до финиша свою главную теорию.
  /// </summary>
  public static BotProfile Ultra { get; } = WithMode(WithMode(WithAll(Hard, profile => profile with
  {
    RivalShare = 1.6,
    MinValue = 0.05,
    UsesCap = 5,
    ThemedBuyBonus = 0.25,
  }), GameMode.TheoryDevelopment, profile => profile with
  {
    NewTheoryFactor = 1,
    // Боты закрывали свои штабы бездомными: проверяем состоятельность точно и считаем потерю всего штаба.
    InsolvencyCare = 1,
    // Каждый лидер в чужой теории — очки её владельцу; в партиях с людьми боты так дарили очки победителю.
    JoinGiftFactor = 1,
    // Открывает свои закрытые штабы и закрывает бездомными сильные штабы соперников — прежде всего того, кто впереди.
    SolvencyPlay = 1,
    // В партиях ботов полная выгода от закрытия чужих штабов стоила ~2 очков: бот тратил на это слишком много ходов.
    SabotageFactor = 0.5,
  }), GameMode.Race, profile => profile with { RaceFocus = 4 });

  /// <summary>
  /// В гонке очки дают только лидеры теорий на финише, а личный штаб лишь помогает до него дойти: розыгрыши персонажей
  /// бот ценит меньше (<see cref="RaceUsesFactor"/>).
  /// </summary>
  /// <remarks>
  /// <para>В игре на деньги каждый персонаж в личном штабе приносит деньги, а войти в чужую теорию почти никогда не
  /// выгоднее: карточку про запас бот там не держит (<see cref="HandReserve"/>).</para>
  /// <para>В развитии теорий — по разбору партий против людей: боты выкладывали все карточки в личный штаб и весь матч
  /// играли «получить $», не входя в теории, куда владелец приводит адептов. Персонаж в личном штабе играет не больше
  /// раза за раунд (<see cref="TheoryUsesFactor"/>), а будущих адептов чужой теории бот учитывает
  /// (<see cref="TheoryAdeptGrowth"/>). В партиях ботов между собой это чуть слабее: там все копят деньги.</para>
  /// </remarks>
  private static BotProfile ByModes(BotProfile profile) =>
    WithMode(WithMode(WithMode(profile, GameMode.Race, race => race with { UsesFactor = RaceUsesFactor }),
      GameMode.Money, money => money with { HandReserve = 0 }),
      GameMode.TheoryDevelopment, theory => theory with { UsesFactor = TheoryUsesFactor, AdeptGrowth = TheoryAdeptGrowth });

  /// <summary>
  /// Сколько розыгрышей персонажа из личного штаба бот ожидает в развитии теорий на каждый оставшийся раунд.
  /// </summary>
  private const double TheoryUsesFactor = 1.0;

  /// <summary>
  /// Какую долю будущих адептов теорий бот засчитывает в развитии теорий (<see cref="AdeptGrowth"/>).
  /// </summary>
  private const double TheoryAdeptGrowth = 1.0;

  /// <summary>
  /// Сколько розыгрышей персонажа из личного штаба бот ожидает в гонке на каждый оставшийся раунд.
  /// </summary>
  private const double RaceUsesFactor = 1.0;

  /// <summary>
  /// Изменить веса одного режима (<see cref="ByMode"/>).
  /// </summary>
  private static BotProfile WithMode(BotProfile profile, GameMode mode, Func<BotProfile, BotProfile> change)
  {
    var byMode = profile.ByMode?.ToDictionary(entry => entry.Key, entry => entry.Value) ?? new Dictionary<GameMode, BotProfile>();
    byMode[mode] = change(profile.ForMode(mode));
    return profile with { ByMode = byMode };
  }

  /// <summary>
  /// Изменить общие веса и веса каждого режима (<see cref="ByMode"/>) одинаково.
  /// </summary>
  private static BotProfile WithAll(BotProfile profile, Func<BotProfile, BotProfile> change) =>
    change(profile) with
    {
      ByMode = profile.ByMode?.ToDictionary(entry => entry.Key, entry => change(entry.Value)),
    };

  /// <summary>
  /// Настройки для уровня.
  /// </summary>
  public static BotProfile For(BotLevel level) => level switch
  {
    BotLevel.Medium => Medium,
    BotLevel.Hard => Hard,
    BotLevel.Ultra => Ultra,
    _ => Easy,
  };
}
