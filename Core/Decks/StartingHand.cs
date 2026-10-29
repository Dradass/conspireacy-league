using System.Diagnostics;
using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Decks;

/// <summary>
/// Стартовая рука игрока: новые бездомные и граждане, не из колоды рынка.
/// </summary>
public static class StartingHand
{
  /// <summary>
  /// Сколько бездомных получает каждый игрок при подготовке.
  /// </summary>
  public const int HomelessCount = 3;

  /// <summary>
  /// Сколько граждан получает каждый игрок при подготовке.
  /// </summary>
  public const int CitizensCount = 3;

  /// <summary>
  /// Собрать стартовую руку: <see cref="HomelessCount"/> бездомных и <see cref="CitizensCount"/> граждан.
  /// Теория у каждой карточки случайная — с той же вероятностью, с какой она встречается у таких карточек
  /// в составе игры (<see cref="GameCardSet"/>); теории может и не быть.
  /// </summary>
  /// <param name="random">Источник случайности.</param>
  public static IEnumerable<PersonCard> Create(Random random)
  {
    ArgumentNullException.ThrowIfNull(random);

    var sets = GameCardSet.All.ToList();
    for (var i = 0; i < HomelessCount; i++)
      yield return new HomelessCard(RandomTheory(sets, set => set.Homeless, random));
    for (var i = 0; i < CitizensCount; i++)
      yield return new CitizenCard(RandomTheory(sets, set => set.Citizens, random));
  }

  /// <summary>
  /// Случайная теория: чем больше карточек этого вида с теорией в составе игры, тем она вероятнее.
  /// </summary>
  /// <param name="sets">Состав игры.</param>
  /// <param name="countOf">Сколько карточек нужного вида в наборе.</param>
  /// <param name="random">Источник случайности.</param>
  private static Theory? RandomTheory(IReadOnlyList<TheoryCardSet> sets, Func<TheoryCardSet, int> countOf, Random random)
  {
    var index = random.Next(sets.Sum(countOf));
    foreach (var set in sets)
    {
      if (index < countOf(set))
        return set.Theory;
      index -= countOf(set);
    }

    throw new UnreachableException();
  }
}
