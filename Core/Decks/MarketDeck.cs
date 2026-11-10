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
  public static List<PersonCard> Create(int playerCount, Random random) => CreateWithReserve(playerCount, random).Deck;

  /// <summary>
  /// Собрать стартовую колоду рынка (<see cref="Create"/>) и резерв — карточки состава игры, не попавшие в колоду.
  /// </summary>
  /// <param name="playerCount">Количество игроков.</param>
  /// <param name="random">Источник случайности.</param>
  /// <returns>Колода (первая карточка — верхняя) и резерв.</returns>
  public static (List<PersonCard> Deck, List<PersonCard> Reserve) CreateWithReserve(int playerCount, Random random)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(playerCount);
    ArgumentNullException.ThrowIfNull(random);

    var count = playerCount * CardsOfEachKindPerPlayer;
    var sets = GameCardSet.All.ToList();
    var homeless = sets.SelectMany(set => Enumerable.Repeat(set.Theory, set.Homeless)).ToArray();
    var citizens = sets.SelectMany(set => Enumerable.Repeat(set.Theory, set.Citizens)).ToArray();
    var professionals = sets.SelectMany(set => set.Professionals.Select(entry => (set.Theory, entry.Profession))).ToArray();

    Check(homeless, count);
    Check(citizens, count);
    Check(professionals, count);
    random.Shuffle(homeless);
    random.Shuffle(citizens);
    random.Shuffle(professionals);

    PersonCard[] deck =
    [
      .. homeless.Take(count).Select(theory => new HomelessCard(theory)),
      .. citizens.Take(count).Select(theory => new CitizenCard(theory)),
      .. professionals.Take(count).Select(pro => new GenericProfessionalCard(pro.Profession, pro.Theory)),
    ];
    random.Shuffle(deck);

    List<PersonCard> reserve =
    [
      .. homeless.Skip(count).Select(theory => new HomelessCard(theory)),
      .. citizens.Skip(count).Select(theory => new CitizenCard(theory)),
      .. professionals.Skip(count).Select(pro => new GenericProfessionalCard(pro.Profession, pro.Theory)),
    ];
    return (deck.ToList(), reserve);
  }

  private static void Check<T>(T[] pool, int count)
  {
    if (pool.Length < count)
      throw new InvalidOperationException($"В составе игры только {pool.Length} подходящих карточек, а нужно {count}.");
  }
}
