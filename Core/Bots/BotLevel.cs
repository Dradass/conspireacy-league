namespace ConspiracyLeague.Core.Bots;

/// <summary>
/// Уровень бота.
/// </summary>
public enum BotLevel
{
  /// <summary>
  /// Лёгкий: жадно берёт самый выгодный сейчас ход, всем соперникам вредит одинаково.
  /// </summary>
  Easy,

  /// <summary>
  /// Средний: охотнее вкладывается в карточки и личный штаб.
  /// </summary>
  Medium,

  /// <summary>
  /// Сложный: веса подобраны на партиях ботов между собой; вкладывается в развитие и мешает лидеру партии.
  /// </summary>
  Hard,

  /// <summary>
  /// Очень сложный: веса подобраны партиями против сложного бота.
  /// </summary>
  VeryHard,
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
  /// Сколько карточек в руке не мешает покупать новые.
  /// </summary>
  public int HandLimit { get; init; } = 3;

  /// <summary>
  /// Лёгкий бот.
  /// </summary>
  public static BotProfile Easy { get; } = new();

  /// <summary>
  /// Средний бот: охотнее покупает карточки и выше ценит персонажей в личном штабе.
  /// </summary>
  public static BotProfile Medium { get; } = new() { BuyFactor = 0.85, UsesFactor = 1.0 };

  /// <summary>
  /// Сложный бот: веса подобраны партиями ботов между собой. Вкладывается в карточки и личный штаб, копит
  /// на сильных персонажей, больше учитывает соперников и прежде всего мешает тому, кто впереди.
  /// </summary>
  public static BotProfile Hard { get; } = new()
  {
    BuyFactor = 0.85,
    UsesFactor = 1.3,
    UsesCap = 6,
    RivalShare = 1.0,
    SavingsShare = 1.0,
    ThreatFocus = 2,
  };

  /// <summary>
  /// Очень сложный бот: веса подобраны партиями против сложного бота. Ещё охотнее покупает карточки, выше ценит
  /// персонажей в личном штабе и дольше копит на сильных.
  /// </summary>
  public static BotProfile VeryHard { get; } = Hard with
  {
    BuyFactor = 1.2,
    UsesFactor = 1.8,
    SavingsShare = 1.6,
  };

  /// <summary>
  /// Настройки для уровня.
  /// </summary>
  public static BotProfile For(BotLevel level) => level switch
  {
    BotLevel.Medium => Medium,
    BotLevel.Hard => Hard,
    BotLevel.VeryHard => VeryHard,
    _ => Easy,
  };
}
