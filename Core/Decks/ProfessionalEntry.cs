using ConspiracyLeague.Core.Cards.Professionals;

namespace ConspiracyLeague.Core.Decks;

/// <summary>
/// Карточка профессионала в составе игры.
/// </summary>
/// <param name="Profession">Профессия.</param>
/// <param name="Variant">Номер варианта профессионала (число после профессии в таблице); отсутствует, если вариант не задан.</param>
public sealed record ProfessionalEntry(Profession Profession, int? Variant = null);
