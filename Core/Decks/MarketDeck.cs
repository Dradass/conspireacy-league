using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Cards.Professionals;

namespace ConspiracyLeague.Core.Decks;

/// <summary>
/// Стартовая колода рынка.
/// </summary>
public static class MarketDeck
{
  /// <summary>
  /// Сколько бездомных, граждан и профессионалов попадает в колоду на каждого игрока.
  /// </summary>
  public const int CardsOfEachKindPerPlayer = 7;

  /// <summary>
  /// Собрать стартовую колоду рынка: случайные бездомные, граждане и профессионалы из состава игры
  /// (<see cref="GameCardSet"/>), каждых — по <see cref="CardsOfEachKindPerPlayer"/> на игрока. Колода перемешана.
  /// </summary>
  /// <param name="playerCount">Количество игроков.</param>
  /// <param name="random">Источник случайности.</param>
  /// <returns>Карточки колоды; первая — верхняя.</returns>
  public static List<PersonCard> Create(int playerCount, Random random)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(playerCount);
    ArgumentNullException.ThrowIfNull(random);

    var count = playerCount * CardsOfEachKindPerPlayer;
    var sets = GameCardSet.All.ToList();
    var homeless = sets.SelectMany(set => Enumerable.Repeat(set.Theory, set.Homeless)).ToArray();
    var citizens = sets.SelectMany(set => Enumerable.Repeat(set.Theory, set.Citizens)).ToArray();
    var professionals = sets.SelectMany(set => set.Professionals.Select(entry => (set.Theory, entry.Profession))).ToArray();

    PersonCard[] deck =
    [
      .. Pick(homeless, count, random).Select(theory => new HomelessCard(theory)),
      .. Pick(citizens, count, random).Select(theory => new CitizenCard(theory)),
      .. Pick(professionals, count, random).Select(pro => new GenericProfessionalCard(pro.Profession, pro.Theory)),
    ];
    random.Shuffle(deck);
    return deck.ToList();
  }

  private static IEnumerable<T> Pick<T>(T[] pool, int count, Random random)
  {
    if (pool.Length < count)
      throw new InvalidOperationException($"В составе игры только {pool.Length} подходящих карточек, а нужно {count}.");

    random.Shuffle(pool);
    return pool.Take(count);
  }
}
