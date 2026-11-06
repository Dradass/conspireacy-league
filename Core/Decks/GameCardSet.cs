using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Cards.Professionals;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Decks;

/// <summary>
/// Состав карточек персон в игре: сколько бездомных, граждан и каких профессионалов с каждой теорией.
/// </summary>
public static class GameCardSet
{
  /// <summary>
  /// Количество профессионалов каждой профессии без теории.
  /// </summary>
  public const int ProfessionalsWithoutTheoryPerProfession = 4;

  /// <summary>
  /// Карточки без теории.
  /// </summary>
  public static TheoryCardSet WithoutTheory { get; } = new(
    Theory: null,
    Homeless: 18,
    Citizens: 39,
    Professionals: Enum.GetValues<Profession>()
      .SelectMany(profession => Enumerable.Repeat(new ProfessionalEntry(profession), ProfessionalsWithoutTheoryPerProfession))
      .ToList());

  /// <summary>
  /// Карточки с теориями.
  /// </summary>
  public static IReadOnlyList<TheoryCardSet> WithTheories { get; } =
  [
    Set(TheoryCatalog.LeninMushroom, 4, 2,
      Pro(Profession.Journalist, 1), Pro(Profession.Actor, 1), Pro(Profession.SpecialAgent, 3)),

    Set(TheoryCatalog.SecretGovernment, 4, 2,
      Pro(Profession.Policeman, 3), Pro(Profession.Scientist, 2), Pro(Profession.TheoryMaster)),
    Set(TheoryCatalog.Freemasons, 2, 1,
      Pro(Profession.Actor, 1), Pro(Profession.SpecialAgent, 1), Pro(Profession.TheoryMaster)),
    Set(TheoryCatalog.Reptilians, 2, 1,
      Pro(Profession.Psychic, 1), Pro(Profession.Coach, 1)),
    Set(TheoryCatalog.Illuminati, 2, 1,
      Pro(Profession.Journalist, 2), Pro(Profession.Bandit, 1)),

    Set(TheoryCatalog.AliensAmongUs, 4, 2,
      Pro(Profession.Psychic, 4), Pro(Profession.Psychic, 2), Pro(Profession.Coach, 2)),
    Set(TheoryCatalog.AlienTechnology, 2, 1,
      Pro(Profession.SpecialAgent, 1), Pro(Profession.Scientist, 5), Pro(Profession.TheoryMaster)),
    Set(TheoryCatalog.AlienExperiments, 2, 1,
      Pro(Profession.Politician, 3), Pro(Profession.Driver, 3)),
    Set(TheoryCatalog.AlienEnslavement, 2, 1,
      Pro(Profession.Bandit, 4), Pro(Profession.Scientist, 3)),

    Set(TheoryCatalog.ScienceConspiracy, 4, 2,
      Pro(Profession.Politician, 2), Pro(Profession.Actor, 2), Pro(Profession.TheoryMaster)),
    Set(TheoryCatalog.FlatEarth, 2, 1,
      Pro(Profession.Actor, 3), Pro(Profession.Bandit, 3), Pro(Profession.TheoryMaster)),
    Set(TheoryCatalog.AntiVaccination, 2, 1,
      Pro(Profession.Actor, 4), Pro(Profession.Coach, 3)),
    Set(TheoryCatalog.Microchipping, 2, 1,
      Pro(Profession.Psychic, 3), Pro(Profession.Bandit, 2)),
    Set(TheoryCatalog.FiveGTowers, 2, 1,
      Pro(Profession.Policeman, 1), Pro(Profession.Bandit, 4)),

    Set(TheoryCatalog.TinFoilHat, 4, 2,
      Pro(Profession.Journalist, 4), Pro(Profession.Psychic, 4), Pro(Profession.Coach, 4)),

    Set(TheoryCatalog.CommunistConspiracy, 4, 2,
      Pro(Profession.Policeman, 4), Pro(Profession.Scientist, 4), Pro(Profession.Driver, 5)),
    Set(TheoryCatalog.CommunistsOnMoon, 2, 1,
      Pro(Profession.Politician, 1), Pro(Profession.SpecialAgent, 4)),
    Set(TheoryCatalog.CommunistsInAntarctica, 2, 1,
      Pro(Profession.SpecialAgent, 2), Pro(Profession.Scientist, 4)),

    Set(TheoryCatalog.CiaKillsPeople, 4, 2,
      Pro(Profession.Policeman, 2), Pro(Profession.Politician, 5), Pro(Profession.Bandit, 5)),
    Set(TheoryCatalog.DullesPlan, 2, 1,
      Pro(Profession.Journalist, 5), Pro(Profession.Coach, 5), Pro(Profession.TheoryMaster)),
    Set(TheoryCatalog.CiaEbola, 2, 1,
      Pro(Profession.Psychic, 5), Pro(Profession.Coach, 6)),

    Set(TheoryCatalog.GoldenBillion, 4, 2,
      Pro(Profession.Politician, 4), Pro(Profession.Journalist, 3), Pro(Profession.Driver, 1)),
    Set(TheoryCatalog.CovidHoax, 2, 1,
      Pro(Profession.Policeman, 3), Pro(Profession.Bandit, 2)),
    Set(TheoryCatalog.Chemtrails, 2, 1,
      Pro(Profession.Actor, 2), Pro(Profession.Driver, 2)),

    Set(TheoryCatalog.SatanistsKidnapChildren, 4, 2,
      Pro(Profession.Actor, 5), Pro(Profession.Policeman, 4), Pro(Profession.Driver, 5)),

    Set(TheoryCatalog.BirdsAreDrones, 4, 2,
      Pro(Profession.Politician, 2), Pro(Profession.SpecialAgent, 5), Pro(Profession.Scientist, 1)),

    Set(TheoryCatalog.PresidentDoubles, 4, 2,
      Pro(Profession.Policeman, 5), Pro(Profession.Journalist, 2), Pro(Profession.Driver, 4)),
  ];

  /// <summary>
  /// Все карточки: без теории и с теориями.
  /// </summary>
  public static IEnumerable<TheoryCardSet> All => WithTheories.Prepend(WithoutTheory);

  /// <summary>
  /// Карточки с указанной теорией.
  /// </summary>
  /// <param name="theory">Теория.</param>
  public static TheoryCardSet? Of(Theory theory) => WithTheories.FirstOrDefault(set => set.Theory == theory);

  /// <summary>
  /// Сколько карточек вида есть в игре: бездомных, граждан или профессионалов указанной профессии (с любой теорией и без).
  /// </summary>
  /// <param name="kind">Вид персонажа.</param>
  /// <param name="profession">Профессия, если вид — профессионал.</param>
  public static int CountOf(PersonKind kind, Profession? profession = null) =>
    All.Sum(set => CountIn(set, kind, profession));

  /// <summary>
  /// Теории (<c>null</c> — без теории), в составах которых есть карточки вида.
  /// </summary>
  /// <param name="kind">Вид персонажа.</param>
  /// <param name="profession">Профессия, если вид — профессионал.</param>
  public static IReadOnlyList<Theory?> TheoriesOf(PersonKind kind, Profession? profession = null) =>
    All.Where(set => CountIn(set, kind, profession) > 0).Select(set => set.Theory).ToList();

  /// <summary>
  /// Создать карточку вида с теорией (<c>null</c> — без теории).
  /// </summary>
  /// <param name="kind">Вид персонажа.</param>
  /// <param name="theory">Теория или <c>null</c>.</param>
  /// <param name="profession">Профессия, если вид — профессионал.</param>
  /// <exception cref="ArgumentException">Для профессионала не указана профессия.</exception>
  public static PersonCard Create(PersonKind kind, Theory? theory, Profession? profession = null) => kind switch
  {
    PersonKind.Homeless => new HomelessCard(theory),
    PersonKind.Citizen => new CitizenCard(theory),
    _ => new GenericProfessionalCard(profession ?? throw new ArgumentException("Нужна профессия.", nameof(profession)), theory),
  };

  private static int CountIn(TheoryCardSet set, PersonKind kind, Profession? profession) => kind switch
  {
    PersonKind.Homeless => set.Homeless,
    PersonKind.Citizen => set.Citizens,
    _ => set.Professionals.Count(entry => entry.Profession == profession),
  };

  private static TheoryCardSet Set(Theory theory, int homeless, int citizens, params ProfessionalEntry[] professionals) =>
    new(theory, homeless, citizens, professionals);

  private static ProfessionalEntry Pro(Profession profession, int? variant = null) => new(profession, variant);
}
