using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Decks;

/// <summary>
/// Карточки персон с одной теорией (или без теории), которые есть в игре.
/// </summary>
/// <param name="Theory">Теория; отсутствует у карточек без теории.</param>
/// <param name="Homeless">Количество бездомных.</param>
/// <param name="Citizens">Количество граждан.</param>
/// <param name="Professionals">Профессионалы, по одной записи на карточку.</param>
public sealed record TheoryCardSet(Theory? Theory, int Homeless, int Citizens, IReadOnlyList<ProfessionalEntry> Professionals);
